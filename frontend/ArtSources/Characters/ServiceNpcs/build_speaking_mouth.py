"""Replace the copy's painted smile surface with a continuous lip and oral mesh."""

import json
import math
from collections import Counter, defaultdict

import bpy
import numpy as np
from mathutils import Vector, geometry
from mathutils.bvhtree import BVHTree

CENTER_Y = 0.0
CENTER_Z = 0.501


def unity_vector(value):
    return dict(x=float(value.x), y=float(value.z), z=float(-value.y))


def build_speaking_mouth(body, source, vertices, normals, triangles, texture, output):
    removed = set()
    edge_count = Counter()
    source_index = {}

    def position_key(index):
        return tuple(round(component, 5) for component in vertices[index])

    for index, triangle in enumerate(triangles):
        center = sum((vertices[i] for i in triangle), Vector()) / 3
        radius = ((center.y - CENTER_Y) / 0.065) ** 2 + (
            (center.z - CENTER_Z) / 0.034
        ) ** 2
        if center.x >= -0.15 or radius >= 1 or center.z >= 0.526:
            continue
        removed.add(index)
        for first, second in zip(triangle, triangle[1:] + triangle[:1]):
            a, b = position_key(first), position_key(second)
            edge_count[tuple(sorted((a, b)))] += 1
            source_index[a], source_index[b] = first, second

    neighbors = defaultdict(list)
    for (first, second), count in edge_count.items():
        if count == 1:
            neighbors[first].append(second)
            neighbors[second].append(first)
    if not neighbors or any(len(items) != 2 for items in neighbors.values()):
        raise ValueError("The mouth cut must have one closed boundary.")
    start = next(iter(neighbors))
    boundary = []
    previous, current = None, start
    while True:
        boundary.append(source_index[current])
        following = next(item for item in neighbors[current] if item != previous)
        previous, current = current, following
        if current == start:
            break
    if len(boundary) != len(neighbors):
        raise ValueError("The mouth cut contains multiple boundary loops.")
    signed_area = sum(
        vertices[a].y * vertices[b].z - vertices[b].y * vertices[a].z
        for a, b in zip(boundary, boundary[1:] + boundary[:1])
    )
    if signed_area < 0:
        boundary.reverse()

    # Subdivision on the existing edge keeps the new surface joined to the face.
    outer_points, outer_normals = [], []
    posed_outer = {key.name: [] for key in body.data.shape_keys.key_blocks}
    for first, second in zip(boundary, boundary[1:] + boundary[:1]):
        for step in range(4):
            fraction = step / 4
            outer_points.append(vertices[first].lerp(vertices[second], fraction))
            for key in body.data.shape_keys.key_blocks:
                posed_outer[key.name].append(
                    key.data[first].co.lerp(key.data[second].co, fraction)
                )
            outer_normals.append(
                normals[first].lerp(normals[second], fraction).normalized()
            )
    count = len(outer_points)
    first_angle = math.atan2(outer_points[0].z - CENTER_Z, outer_points[0].y - CENTER_Y)
    angles = [first_angle + 2 * math.pi * index / count for index in range(count)]

    fit = np.array(
        [
            [
                1,
                v.y - CENTER_Y,
                v.z - CENTER_Z,
                (v.y - CENTER_Y) ** 2,
                (v.y - CENTER_Y) * (v.z - CENTER_Z),
                (v.z - CENTER_Z) ** 2,
            ]
            for v in outer_points
        ]
    )
    coefficients = np.linalg.lstsq(
        fit, np.array([v.x for v in outer_points]), rcond=None
    )[0]

    def fitted_x(y, z):
        local_y, local_z = y - CENTER_Y, z - CENTER_Z
        return float(
            np.dot(
                coefficients,
                [1, local_y, local_z, local_y**2, local_y * local_z, local_z**2],
            )
        )

    # Bake the local skin color into one continuous UV chart, removing the painted
    # crescent only inside the lip region. The source texture itself is untouched.
    surface = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
    pixels = list(texture.pixels)
    width, height = texture.size
    uv_min_y, uv_max_y = -0.067, 0.073
    uv_min_z, uv_max_z = 0.464, 0.539

    def source_color(y, z):
        point, _, face_index, _ = surface.ray_cast(
            Vector((-1, y, z)), Vector((1, 0, 0))
        )
        if point is None:
            return (1.0, 0.80, 0.64)
        ids = triangles[face_index]
        uv = geometry.barycentric_transform(
            point,
            *[vertices[i] for i in ids],
            *[Vector((source["uv"][i]["x"], source["uv"][i]["y"], 0)) for i in ids],
        )
        px = min(width - 1, max(0, int(uv.x * width)))
        py = min(height - 1, max(0, int(uv.y * height)))
        return pixels[(py * width + px) * 4 : (py * width + px) * 4 + 3]

    # Fit surrounding skin in two dimensions. Sampling a single cheek row can
    # stretch a wrinkle or shadow across the entire mouth patch.
    samples = [
        (dy, dz, source_color(CENTER_Y + dy, CENTER_Z + dz))
        for dy in [-0.05, -0.04, 0.04, 0.05]
        for dz in [-0.024, -0.018, 0.018, 0.024]
    ]
    brightnesses = [sum(color) for dy, dz, color in samples]
    skin_samples = [
        s for s, b in zip(samples, brightnesses) if b > np.median(brightnesses) * 0.8
    ]
    skin_fit = np.linalg.lstsq(
        np.array([[1, dy, dz] for dy, dz, color in skin_samples]),
        np.array([color for dy, dz, color in skin_samples]),
        rcond=None,
    )[0]
    texture_size = 256
    baked_pixels = []
    for row in range(texture_size):
        z = uv_min_z + (uv_max_z - uv_min_z) * row / (texture_size - 1)
        for column in range(texture_size):
            y = uv_min_y + (uv_max_y - uv_min_y) * column / (texture_size - 1)
            color = source_color(y, z)
            radius = math.sqrt(
                ((y - CENTER_Y) / 0.033) ** 2 + ((z - CENTER_Z) / 0.020) ** 2
            )
            if radius < 1:
                # Bilateral cheek samples retain the local skin tone without
                # dragging dark mouth pixels into the new lip surface.
                clean = np.array([1, y - CENTER_Y, z - CENTER_Z]) @ skin_fit
                blend = min(1, max(0, (1 - radius) / 0.2))
                color = [color[i] * (1 - blend) + clean[i] * blend for i in range(3)]
            # Broad tonal modelling follows the skin UVs through every viseme.
            # Fade towards the corners; no narrow contour or colored lip band.
            lateral = (y - CENTER_Y) / 0.022
            centerline = CENTER_Z + 0.004 * min(1, lateral * lateral)
            upper = math.exp(-(((z - centerline - 0.0038) / 0.0045) ** 2))
            lower = math.exp(-(((z - centerline + 0.0042) / 0.0055) ** 2))
            corner_fade = math.exp(-((abs(lateral) / 0.82) ** 4))
            brightness = 1 - corner_fade * (0.06 * upper + 0.03 * lower)
            color = [component * brightness for component in color]
            baked_pixels.extend((*color, 1))
    image = bpy.data.images.new(
        "MannequinMouthColor", width=texture_size, height=texture_size
    )
    image.colorspace_settings.name = texture.colorspace_settings.name
    image.pixels = baked_pixels
    image.filepath_raw = str(output / "MannequinMouthColor.png")
    image.file_format = "PNG"
    image.save()
    image.pack()
    skin_material = bpy.data.materials.new("SpeakingLips")
    skin_material.use_nodes = True
    color_node = skin_material.node_tree.nodes.new("ShaderNodeTexImage")
    color_node.image = image
    skin_shader = skin_material.node_tree.nodes.get("Principled BSDF")
    skin_shader.inputs["Roughness"].default_value = 0.8
    skin_material.node_tree.links.new(
        color_node.outputs["Color"], skin_shader.inputs["Base Color"]
    )

    apertures = {
        "Basis": (0.0175, 0.0015),
        "viseme_PP": (0.0175, 0.00025),
        "viseme_AA": (0.024, 0.012),
        "viseme_E": (0.026, 0.0055),
        "viseme_OH": (0.012, 0.014),
        "viseme_OU": (0.008, 0.014),
        "viseme_FF": (0.021, 0.003),
        "viseme_TH": (0.022, 0.007),
        "viseme_I": (0.024, 0.005),
        "viseme_EU": (0.023, 0.0035),
        "mouthSmileLeft": (0.0175, 0.0015),
        "mouthSmileRight": (0.0175, 0.0015),
    }

    def aperture_point(channel, half_width, half_height, angle):
        cosine, sine = math.cos(angle), math.sin(angle)
        exponent = (
            0.75
            if channel
            in ("viseme_AA", "viseme_E", "viseme_I", "viseme_EU", "viseme_PP")
            else 1
        )
        y = CENTER_Y + half_width * math.copysign(abs(cosine) ** exponent, cosine)
        z = CENTER_Z + half_height * math.copysign(abs(sine) ** exponent, sine)
        if channel == "viseme_FF":
            z += 0.005 * (1 - cosine * cosine)
        if channel in ("Basis", "mouthSmileLeft", "mouthSmileRight"):
            z += 0.006 * cosine * cosine
        if channel.startswith("mouthSmile"):
            side = 1 if channel.endswith("Left") else -1
            z += 0.003 * max(0, side * cosine)
        return y, z

    def cross_2d(first, second):
        return first.x * second.y - first.y * second.x

    def ray_boundary(start, outward, contour):
        intersections = []
        for first, second in zip(contour, contour[1:] + contour[:1]):
            edge_start = Vector((first.y, first.z))
            edge = Vector((second.y - first.y, second.z - first.z))
            denominator = cross_2d(outward, edge)
            if abs(denominator) < 1e-10:
                continue
            offset = edge_start - start
            distance = cross_2d(offset, edge) / denominator
            along = cross_2d(offset, outward) / denominator
            if distance > 0 and 0 <= along <= 1:
                intersections.append((distance, first.lerp(second, along)))
        if not intersections:
            raise ValueError(
                "Lip surface must stay inside the preserved face boundary."
            )
        return min(intersections, key=lambda item: item[0])

    # Keep the outside patch stationary during speech; a fixed support contour
    # separates the irregular source boundary from the animated lip surface.
    support_scales = []
    for angle in angles:
        direction = Vector((0.034 * math.cos(angle), 0.021 * math.sin(angle)))
        distance, _ = ray_boundary(
            Vector((CENTER_Y, CENTER_Z)), direction.normalized(), outer_points
        )
        support_scales.append(min(1, 0.9 * distance / direction.length))
    for _ in range(4):
        support_scales = [
            min(
                scale,
                (
                    support_scales[(i - 1) % count]
                    + 2 * scale
                    + support_scales[(i + 1) % count]
                )
                / 4,
            )
            for i, scale in enumerate(support_scales)
        ]
    support = []
    for angle, scale in zip(angles, support_scales):
        y, z = (
            CENTER_Y + 0.034 * math.cos(angle) * scale,
            CENTER_Z + 0.021 * math.sin(angle) * scale,
        )
        support.append(Vector((fitted_x(y, z), y, z)))

    parts = []
    for part_name, rings in [
        ("SpeakingLips", [1, 0.6, 0.25, 0.1, 0.04, 0]),
        ("MouthInterior", [1, 0.8, 0.5, 0.2, 0]),
    ]:
        targets, target_normals = {}, {}
        for channel, (half_width, half_height) in apertures.items():
            aperture = [
                aperture_point(channel, half_width, half_height, angle)
                for angle in angles
            ]
            purse = {"viseme_OH": 0.002, "viseme_OU": 0.006}.get(channel, 0)
            # Parameterize a convex opening first, then bend its centerline into
            # the smile. Radial strips around an already bent thin crescent fold.
            exponent = (
                0.75
                if channel
                in ("viseme_AA", "viseme_E", "viseme_I", "viseme_EU", "viseme_PP")
                else 1
            )
            unbent = [
                (
                    point[0],
                    CENTER_Z
                    + half_height
                    * math.copysign(abs(math.sin(angle)) ** exponent, math.sin(angle)),
                )
                for point, angle in zip(aperture, angles)
            ]
            directions, limits, hits = [], [], []
            for index, angle in enumerate(angles):
                start = Vector(unbent[index])
                hit = support[index]
                offset = Vector((hit.y, hit.z)) - start
                limit = offset.length
                if limit < 0.0001:
                    raise ValueError(
                        "The mouth aperture must fit inside its support contour."
                    )
                directions.append(offset.normalized())
                limits.append(limit)
                hits.append(hit)
            widths = [min(0.008, limit * 0.55) for limit in limits]
            if channel.startswith("mouthSmile"):
                side = 1 if channel.endswith("Left") else -1
                widths = [
                    width * (1 - 0.5 * max(0, side * math.cos(angle)) ** 2)
                    for width, angle in zip(widths, angles)
                ]
            # Conservatively soften width changes where the source boundary indents.
            for _ in range(3):
                widths = [
                    min(
                        width,
                        (widths[(i - 1) % count] + 2 * width + widths[(i + 1) % count])
                        / 4,
                    )
                    for i, width in enumerate(widths)
                ]

            def lip_surface(index, distance):
                inner_y, inner_z = unbent[index]
                width = widths[index]
                direction = directions[index]
                y, z = (
                    inner_y + direction.x * distance,
                    inner_z + direction.y * distance,
                )
                outer = hits[index]
                fraction = distance / limits[index]
                lateral = max(-1, min(1, (y - CENTER_Y) / half_width))
                center_offset = 0.0
                if channel in ("Basis", "mouthSmileLeft", "mouthSmileRight"):
                    center_offset = 0.006 * lateral * lateral
                if channel.startswith("mouthSmile"):
                    side = 1 if channel.endswith("Left") else -1
                    center_offset += 0.003 * max(0, side * lateral)
                if channel == "viseme_FF":
                    center_offset = 0.005 * (1 - lateral * lateral)
                # A shared vertical shear preserves the narrow lip loops. Fade
                # the smile only beyond the fold, in the stationary skin margin.
                fade = min(1, max(0, (distance - width) / (limits[index] - width)))
                fade = fade * fade * (3 - 2 * fade)
                z += center_offset * (1 - fade)
                x = (
                    fitted_x(y, z)
                    + (outer.x - fitted_x(outer.y, outer.z)) * fraction**2
                    - purse * (1 - fraction) ** 2
                )
                if 0 < distance < width:
                    sine = math.sin(angles[index])
                    # Smooth across the corners while retaining upper/lower peaks.
                    height = (0.0008 - 0.0001 * sine) * (0.25 + 0.75 * sine * sine)
                    x -= height * math.sin(math.pi * distance / width) ** 2
                return Vector((x, y, z))

            points, shading = [], []
            for ring_index, ring in enumerate(rings):
                for index, angle in enumerate(angles):
                    inner_y, inner_z = aperture[index]
                    if part_name == "SpeakingLips":
                        # Align the crest and shoulders by physical distance from
                        # the aperture, independent of the jagged source boundary.
                        width = widths[index]
                        distance = [0, width * 1.25, width, width / 2, width * 0.2, 0][
                            ring_index
                        ]
                        point = lip_surface(index, distance)
                        previous = lip_surface((index - 1) % count, distance)
                        following = lip_surface((index + 1) % count, distance)
                        radial = lip_surface(index, distance + 0.00001) - lip_surface(
                            index, max(0, distance - 0.00001)
                        )
                        normal = (following - previous).cross(radial).normalized()
                        if ring_index == 0:
                            point = posed_outer[channel][index]
                            normal = outer_normals[index]
                        elif ring_index == 1:
                            point = support[index]
                            local_y, local_z = point.y - CENTER_Y, point.z - CENTER_Z
                            slope_y = (
                                coefficients[1]
                                + 2 * coefficients[3] * local_y
                                + coefficients[4] * local_z
                            )
                            slope_z = (
                                coefficients[2]
                                + coefficients[4] * local_y
                                + 2 * coefficients[5] * local_z
                            )
                            normal = Vector((-1, slope_y, slope_z)).normalized()
                        x, y, z = point
                    else:
                        y = CENTER_Y + (inner_y - CENTER_Y) * ring
                        z = CENTER_Z + (inner_z - CENTER_Z) * ring
                        purse = {"viseme_OH": 0.002, "viseme_OU": 0.006}.get(channel, 0)
                        x = fitted_x(y, z) + 0.012 * (1 - ring) - purse * ring
                        normal = Vector((-1, 0, 0))
                    points.append(Vector((x, y, z)))
                    shading.append(normal)
            targets[channel], target_normals[channel] = points, shading
        faces = []
        if part_name == "SpeakingLips":
            # The source cut is concave. Constrained triangulation joins it to
            # the smooth lip loops without the old crossed radial strips.
            coordinates = [
                Vector((point.y, point.z)) for point in targets["Basis"][: count * 2]
            ]
            edges = [
                (r * count + i, r * count + (i + 1) % count)
                for r in range(2)
                for i in range(count)
            ]
            result = geometry.delaunay_2d_cdt(
                coordinates, edges, [list(range(count))], 1, 1e-8
            )
            if any(len(ids) != 1 for ids in result[3]):
                raise ValueError(
                    "Lip contours intersect or merge: "
                    + str(
                        [
                            (i, ids, tuple(result[0][i]))
                            for i, ids in enumerate(result[3])
                            if len(ids) != 1
                        ]
                    )
                )
            for triangle in result[2]:
                ids = [result[3][i][0] for i in triangle]
                if all(i >= count for i in ids):
                    continue
                # CDT is counterclockwise in yz; the visible face points towards -X.
                faces.append(tuple(reversed(ids)))
            for ring_index in range(1, len(rings) - 1):
                for index in range(count):
                    next_index = (index + 1) % count
                    first, second = ring_index * count, (ring_index + 1) * count
                    faces.append(
                        (
                            first + index,
                            second + index,
                            second + next_index,
                            first + next_index,
                        )
                    )
        else:
            for ring_index in range(len(rings) - 1):
                for index in range(count):
                    next_index = (index + 1) % count
                    first, second = ring_index * count, (ring_index + 1) * count
                    faces.append(
                        (
                            first + index,
                            second + index,
                            second + next_index,
                            first + next_index,
                        )
                    )
        mesh = bpy.data.meshes.new(part_name)
        mesh.from_pydata(targets["Basis"], [], faces)
        mesh.update()
        item = bpy.data.objects.new(part_name, mesh)
        bpy.context.collection.objects.link(item)
        for polygon in mesh.polygons:
            polygon.use_smooth = True
        mesh.normals_split_custom_set_from_vertices(target_normals["Basis"])
        item["base_normals"] = json.dumps(
            [unity_vector(n) for n in target_normals["Basis"]]
        )
        if part_name == "SpeakingLips":
            mesh.materials.append(skin_material)
            layer = mesh.uv_layers.new(name="UVMap")
            vertex_uv = [
                dict(
                    x=(v.y - uv_min_y) / (uv_max_y - uv_min_y),
                    y=(v.z - uv_min_z) / (uv_max_z - uv_min_z),
                )
                for v in targets["Basis"]
            ]
            for loop in mesh.loops:
                uv = vertex_uv[loop.vertex_index]
                layer.data[loop.index].uv = (uv["x"], uv["y"])
            item["vertex_uv"] = json.dumps(vertex_uv)
            item["texture_file"] = "MannequinMouthColor.png"
            color = (1, 1, 1)
        else:
            # Warm oral tissue; near-black reads as a painted ring around the teeth.
            color = (0.055, 0.012, 0.008)
            material = bpy.data.materials.new(part_name)
            material.use_nodes = True
            shader = material.node_tree.nodes.get("Principled BSDF")
            shader.inputs["Base Color"].default_value = (0, 0, 0, 1)
            shader.inputs["Emission Color"].default_value = (*color, 1)
            shader.inputs["Emission Strength"].default_value = 1
            material.node_tree.nodes.get("Principled BSDF").inputs[
                "Roughness"
            ].default_value = 1
            material.node_tree.nodes.get("Principled BSDF").inputs[
                "Specular IOR Level"
            ].default_value = 0
            mesh.materials.append(material)
        item.shape_key_add(name="Basis")
        for channel, points in targets.items():
            if channel == "Basis":
                continue
            key = item.shape_key_add(name=channel)
            for vertex, target in zip(key.data, points):
                vertex.co = target
            item["normals_" + channel] = json.dumps(
                [
                    unity_vector(target - base)
                    for target, base in zip(
                        target_normals[channel], target_normals["Basis"]
                    )
                ]
            )
        parts.append((item, color))

    # Keep vertex order, UVs, weights and rig metadata. Only these copy triangles
    # are replaced; the author's original prefab and mesh are not changed.
    for polygon in body.data.polygons:
        polygon.use_smooth = True
    import bmesh

    editable = bmesh.new()
    editable.from_mesh(body.data)
    editable.faces.ensure_lookup_table()
    bmesh.ops.delete(
        editable,
        geom=[editable.faces[i] for i in sorted(removed)],
        context="FACES_ONLY",
    )
    editable.to_mesh(body.data)
    editable.free()
    body.data.normals_split_custom_set_from_vertices(normals)
    kept = [
        value
        for index in range(len(triangles))
        if index not in removed
        for value in source["triangles"][index * 3 : index * 3 + 3]
    ]
    return parts, kept, fitted_x
