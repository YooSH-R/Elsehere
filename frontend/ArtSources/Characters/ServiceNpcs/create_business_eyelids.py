"""Fit the existing Base eye module to the businessman review copy, offline."""

import json
import math
import shutil
import sys
from collections import Counter, defaultdict
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector, geometry
from mathutils.bvhtree import BVHTree


def vector(p):
    return Vector((p["x"], -p["z"], p["y"]))


def unity(p):
    return dict(x=float(p[0]), y=float(p[2]), z=float(-p[1]))


def save_image(name, values, size):
    image = bpy.data.images.new(name, width=size, height=size, alpha=True)
    image.pixels.foreach_set(np.asarray(values, dtype=np.float32).reshape(-1))
    image.filepath_raw = str(output / name)
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image


bpy.ops.wm.read_factory_settings(use_empty=True)
output = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
root = Path(__file__).resolve().parents[4]
base_folder = root / "frontend/ArtSources/Characters/LittleMungMannequin"
source = json.loads((output / "Surface.json").read_text())
base = json.loads((base_folder / "MannequinFaceShapes.json").read_text())
vertices = [vector(p) for p in source["vertices"]]
normals = [vector(p) for p in source["normals"]]
triangles = [
    source["triangles"][i : i + 3] for i in range(0, len(source["triangles"]), 3)
]
texture_path = Path(source["texture"])
if not texture_path.is_absolute():
    texture_path = root / "frontend/UnityClient" / texture_path
texture = bpy.data.images.load(str(texture_path))
pixels = np.array(texture.pixels[:]).reshape(texture.size[1], texture.size[0], 4)
tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)


def surface(y, z):
    point, normal, face, _ = tree.ray_cast(Vector((-2, y, z)), Vector((1, 0, 0)))
    if point is None:
        raise ValueError("Eye fitting left the face surface")
    ids = triangles[face]
    uv = geometry.barycentric_transform(
        point,
        *[vertices[i] for i in ids],
        *[Vector((source["uv"][i]["x"], source["uv"][i]["y"], 0)) for i in ids],
    )
    x = np.clip(uv.x * (texture.size[0] - 1), 0, texture.size[0] - 1.001)
    v = np.clip(uv.y * (texture.size[1] - 1), 0, texture.size[1] - 1.001)
    ix, iy = int(x), int(v)
    fx, fy = x - ix, v - iy
    color = (pixels[iy, ix] * (1 - fx) + pixels[iy, ix + 1] * fx) * (1 - fy) + (
        pixels[iy + 1, ix] * (1 - fx) + pixels[iy + 1, ix + 1] * fx
    ) * fy
    return point, normal, color


def make_part(name, poses, faces, uv, texture_name, channel="", shading=None):
    mesh = bpy.data.meshes.new(name)
    # Unity source winding is retained in JSON; Blender coordinate conversion is a rotation.
    mesh.from_pydata(poses[0], [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        layer.data[loop.index].uv = uv[loop.vertex_index]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = 1
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = bpy.data.images.load(str(output / texture_name), check_existing=True)
    node.image.pack()
    mat.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
    mesh.materials.append(mat)
    obj.shape_key_add(name="Basis")
    frames = []
    if shading is None:
        shading = [[Vector((-1, 0, 0)) for _ in poses[0]] for _ in poses]
    mesh.normals_split_custom_set_from_vertices(shading[0])
    for i, points in enumerate(poses[1:], 1):
        key = obj.shape_key_add(name=channel + ("_" + str(i * 25) if i < 4 else ""))
        for vertex, p in zip(key.data, points):
            vertex.co = p
        frames.append(
            dict(
                weight=i * 25,
                vertices=[unity(p) for p in points],
                normals=[unity(n) for n in shading[i]],
            )
        )
    return dict(
        name=name,
        channel=channel,
        texture=texture_name,
        vertices=[unity(p) for p in poses[0]],
        normals=[unity(n) for n in shading[0]],
        uv=[dict(x=float(u), y=float(v)) for u, v in uv],
        triangles=[i for face in faces for i in face],
        frames=frames,
    )


# Preserve existing openings (especially the separate speaking-mouth seam).
source_edges = Counter()
for face in triangles:
    for a, b in zip(face, face[1:] + face[:1]):
        source_edges[
            tuple(
                sorted(
                    (
                        tuple(round(c, 6) for c in vertices[a]),
                        tuple(round(c, 6) for c in vertices[b]),
                    )
                )
            )
        ] += 1
protected_boundary = {
    point for edge, count in source_edges.items() if count == 1 for point in edge
}


def touches_existing_opening(ids):
    return any(
        tuple(round(c, 6) for c in vertices[i]) in protected_boundary for i in ids
    )


removed = set()
parts = []
audit = []
fit_path = output / "EyeFit.json"
eye_settings = (
    json.loads(fit_path.read_text(encoding="utf-8")) if fit_path.exists() else None
)
for side, sign in [("Left", 1), ("Right", -1)]:
    # Measured businessman eye centers. Preserve the reviewed eye size, not head size.
    center, cz = sign * 0.1455197133, 0.6378430941
    old_center, old_cz = sign * 0.130, 0.595
    sx, sz = 1.15, 1.12
    patch_rx, patch_rz = 0.084, 0.073
    hole_rx, hole_rz = 0.093, 0.081
    eye_fit = (
        next(e for e in eye_settings["eyes"] if e["side"] == side)
        if eye_settings
        else None
    )
    if eye_fit:
        center, cz = eye_fit["center"]
        rx, rz = eye_fit["radii"]
        sx, sz = rx / 0.0445, rz / 0.0395
        compact = "Grandmother" in eye_settings["name"]
        patch_scale = eye_settings.get("patchScale", 1.35 if compact else 1.65)
        patch_rx, patch_rz = rx * patch_scale, rz * patch_scale
        hole_rx, hole_rz = patch_rx * 1.12, patch_rz * 1.12
    selected = set()

    def key(i):
        return tuple(round(c, 6) for c in vertices[i])

    for index, ids in enumerate(triangles):
        if touches_existing_opening(ids):
            continue
        p = sum((vertices[i] for i in ids), Vector()) / 3
        if (
            p.x < -0.15
            and ((p.y - center) / hole_rx) ** 2 + ((p.z - cz) / hole_rz) ** 2 < 1
        ):
            selected.add(index)
    if eye_settings and "Grandmother" in eye_settings["name"]:
        # Glasses and lashes can be disconnected shells inside the cut ellipse.
        # Keep these shells; replace the largest connected face patch only.
        vertex_faces = defaultdict(set)
        for index in selected:
            for vertex in triangles[index]:
                vertex_faces[key(vertex)].add(index)
        remaining_faces = set(selected)
        components = []
        while remaining_faces:
            stack = [remaining_faces.pop()]
            component = set(stack)
            while stack:
                current = stack.pop()
                for vertex in triangles[current]:
                    neighbors_in_cut = vertex_faces[key(vertex)] & remaining_faces
                    remaining_faces.difference_update(neighbors_in_cut)
                    component.update(neighbors_in_cut)
                    stack.extend(neighbors_in_cut)
            components.append(component)
        print(side, "face/glasses components", [len(c) for c in components], flush=True)
        selected = max(components, key=len)
    for attempt in range(20):
        edges = Counter()
        lookup = {}
        for index in selected:
            ids = triangles[index]
            for a, b in zip(ids, ids[1:] + ids[:1]):
                edges[tuple(sorted((key(a), key(b))))] += 1
                lookup[key(a)], lookup[key(b)] = a, b
        neighbors = defaultdict(list)
        for (a, b), count in edges.items():
            if count == 1:
                neighbors[a].append(b)
                neighbors[b].append(a)
        bad = {k for k, v in neighbors.items() if len(v) != 2}
        if not bad:
            break
        added = set()
        for index, ids in enumerate(triangles):
            if (
                index in selected
                or touches_existing_opening(ids)
                or not any(key(i) in bad for i in ids)
            ):
                continue
            p = sum((vertices[i] for i in ids), Vector()) / 3
            if (
                p.x < -0.15
                and ((p.y - center) / (hole_rx * 1.20)) ** 2
                + ((p.z - cz) / (hole_rz * 1.20)) ** 2
                < 1
            ):
                added.add(index)
        if not added:
            raise ValueError(side + " original eye has a non-manifold boundary")
        selected.update(added)
    if bad:
        raise ValueError(side + " eye boundary is not a loop")
    loops = []
    remaining = set(neighbors)
    while remaining:
        start = next(iter(remaining))
        previous = None
        current = start
        loop = []
        while True:
            loop.append(lookup[current])
            remaining.discard(current)
            following = next(v for v in neighbors[current] if v != previous)
            previous, current = current, following
            if current == start:
                break
        loops.append(loop)
    if len(loops) != 1:
        raise ValueError(side + " needs a single eye replacement boundary")
    boundary = loops[0]

    def angle(p):
        return math.atan2((p.z - cz) / patch_rz, (p.y - center) / patch_rx) % (
            2 * math.pi
        )

    area = sum(
        vertices[a].y * vertices[b].z - vertices[b].y * vertices[a].z
        for a, b in zip(boundary, boundary[1:] + boundary[:1])
    )
    if area < 0:
        boundary.reverse()
    start = min(range(len(boundary)), key=lambda i: angle(vertices[boundary[i]]))
    boundary = boundary[start:] + boundary[:start]
    outer = [vertices[i] for i in boundary]
    def patch_edge(theta):
        # Keep the new eyelid ring inside the actual cut, including concave corners
        # next to the preserved mouth. A fixed ellipse could cross that boundary.
        direction = Vector((patch_rx * math.cos(theta), patch_rz * math.sin(theta)))
        origin = Vector((center, cz))
        hits = []
        def cross(a, b):
            return a.x * b.y - a.y * b.x
        for first, second in zip(outer, outer[1:] + outer[:1]):
            start = Vector((first.y, first.z)) - origin
            edge = Vector((second.y - first.y, second.z - first.z))
            denominator = cross(direction, edge)
            if abs(denominator) < 1e-10:
                continue
            distance = cross(start, edge) / denominator
            fraction = cross(start, direction) / denominator
            if distance > 0 and 0 <= fraction <= 1:
                hits.append(distance)
        if not hits:
            raise ValueError(side + " eyelid ray has no surrounding skin boundary")
        return origin + direction * min(1, min(hits) * 0.88)
    # Face curvature and skin are fitted outside the old painted eye rim.
    samples = []
    depth = []
    colors = []
    for a in np.linspace(0, 2 * math.pi, 160, endpoint=False):
        y = center + patch_rx * 0.98 * math.cos(a)
        z = cz + patch_rz * 0.98 * math.sin(a)
        p, n, color = surface(y, z)
        if color[0] > 0.06 and 0.4 < color[1] / color[0] < 0.9:
            dy, dz = y - center, z - cz
            samples.append([1, dy, dz, dy * dy, dy * dz, dz * dz])
            depth.append(p.x)
            colors.append(color[:3])
    coefficients = np.linalg.lstsq(np.array(samples), np.array(depth), rcond=None)[0]
    skin_fit = np.linalg.lstsq(np.array(samples)[:, :3], np.array(colors), rcond=None)[
        0
    ]

    def face_x(y, z):
        a, b = y - center, z - cz
        return float(coefficients @ [1, a, b, a * a, a * b, b * b])

    def normal_at(y, z):
        a, b = y - center, z - cz
        return Vector(
            (
                -1,
                coefficients[1] + 2 * coefficients[3] * a + coefficients[4] * b,
                coefficients[2] + coefficients[4] * a + 2 * coefficients[5] * b,
            )
        ).normalized()

    original_lid = next(p for p in base["parts"] if p["name"] == "Upper" + side + "Lid")
    count = len(original_lid["vertices"]) // 25
    old_outer = [vector(p) for p in original_lid["vertices"][:count]]
    old_inner = [vector(p) for p in original_lid["vertices"][24 * count :]]
    fitting = np.array(
        [
            [
                1,
                p.y - old_center,
                p.z - old_cz,
                (p.y - old_center) ** 2,
                (p.y - old_center) * (p.z - old_cz),
                (p.z - old_cz) ** 2,
            ]
            for p in old_outer
        ]
    )
    base_fit = np.linalg.lstsq(fitting, np.array([p.x for p in old_outer]), rcond=None)[
        0
    ]

    def transform(p, index=None):
        a, b = p.y - old_center, p.z - old_cz
        y, z = center + a * sx, cz + b * sz
        blend = 0
        if index is not None:
            ring, j = divmod(index, count)
            blend = max(0, 1 - ring / 12) ** 2
            edge = old_outer[j]
            theta = math.atan2(
                (old_inner[j].z - old_cz) / 0.0395,
                (old_inner[j].y - old_center) / 0.0445,
            )
            target_y, target_z = patch_edge(theta)
            y += (target_y - (center + (edge.y - old_center) * sx)) * blend
            z += (target_z - (cz + (edge.z - old_cz) * sz)) * blend
        residual = p.x - float(base_fit @ [1, a, b, a * a, a * b, b * b])
        x = face_x(y, z) + residual * (1 - blend)
        return Vector((x, y, z))

    size = 256
    rgba = []
    extent = 0.130
    clean_rx, clean_rz = eye_fit["radii"] if eye_fit else (0.061, 0.054)
    fade_end = min(
        math.sqrt(((p.y - center) / clean_rx) ** 2 + ((p.z - cz) / clean_rz) ** 2)
        for p in outer
    ) * 0.98
    # Uneven source topology can place a boundary corner inside the fitted ellipse.
    # Always reach the original texture before that corner instead of leaving a color seam.
    fade_start = min(1.04, fade_end * 0.8)
    for row in range(size):
        z = cz - extent + 2 * extent * row / (size - 1)
        for column in range(size):
            y = center - extent + 2 * extent * column / (size - 1)
            clean = np.append(
                np.clip(np.array([1, y - center, z - cz]) @ skin_fit, 0, 1), 1
            )
            radius = math.sqrt(((y - center) / clean_rx) ** 2 + ((z - cz) / clean_rz) ** 2)
            # 모든 피부 경계에 도달하기 전에 원래 텍스처로 돌아가 색이 다른 다각형을 남기지 않는다.
            blend = np.clip((radius - fade_start) / (fade_end - fade_start), 0, 1)
            blend = blend * blend * (3 - 2 * blend)
            # Texture chart corners can lie outside the face silhouette; no mesh uses them.
            try:
                actual = surface(y, z)[2]
            except ValueError:
                actual = clean
            rgba.append(clean * (1 - blend) + actual * blend)
    save_image(side + "LidSkin.png", rgba, size)
    save_image(side + "LidLine.png", [[0.055, 0.025, 0.012, 1]] * 4, 2)
    shutil.copy2(
        base_folder / (side + "EyeColor.png"), output / (side + "EyeColor.png")
    )

    if eye_fit:
        image = bpy.data.images.load(str(output / (side + "EyeColor.png")))
        rgba_eye = np.array(image.pixels[:]).reshape(image.size[1], image.size[0], 4)
        rgb = rgba_eye[:, :, :3]
        brightness = rgb.max(2)
        saturation = np.ptp(rgb, axis=2) / np.maximum(brightness, 0.001)
        iris_mask = (saturation > 0.22) & (brightness > 0.035) & (brightness < 0.80)
        target_color = np.array(eye_fit["irisColor"])
        iris_luminance = rgb[iris_mask].mean(axis=1)
        shading = np.clip(iris_luminance / np.median(iris_luminance), 0.25, 1.6)
        # Preserve Base shading without amplifying a brown texture's weak blue channel.
        rgb[iris_mask] = np.clip(target_color[None, :] * shading[:, None], 0, 1)
        image.pixels.foreach_set(rgba_eye.astype(np.float32).reshape(-1))
        image.pack()
        save_image(side + "EyeColor.png", rgba_eye, image.size[0])

    def skin_uv(points):
        return [
            ((p.y - center + extent) / (2 * extent), (p.z - cz + extent) / (2 * extent))
            for p in points
        ]

    for source_name, dest_name in [
        (side + "Eye", side + "Eye"),
        ("Upper" + side + "Lid", side + "UpperLid"),
        ("Lower" + side + "Lid", side + "LowerLid"),
        (side + "Lashes", side + "LidLine"),
    ]:
        module = next(p for p in base["parts"] if p["name"] == source_name)
        basis = [vector(p) for p in module["vertices"]]
        poses = [basis]
        if module["shapes"]:
            for frame in module["shapes"][0]["frames"]:
                poses.append(
                    [p + vector(d) for p, d in zip(basis, frame["deltaVertices"])]
                )
        lid = "Lid" in source_name
        mapped = [
            [transform(p, i if lid else None) for i, p in enumerate(points)]
            for points in poses
        ]
        faces = [
            module["triangles"][i : i + 3]
            for i in range(0, len(module["triangles"]), 3)
        ]
        if "Lashes" in source_name:
            # Retain the Base eyelid rim, without adding long eyelashes to the male design.
            lash_count = (
                364
                if eye_settings
                and (
                    "Female" in eye_settings["name"]
                    or "Grandmother" in eye_settings["name"]
                )
                else 260
            )
            mapped = [points[:lash_count] for points in mapped]
            faces = [face for face in faces if max(face) < lash_count]
            uv = [(0.5, 0.5)] * lash_count
            image = side + "LidLine.png"
        elif lid:
            uv = skin_uv(mapped[0])
            image = side + "LidSkin.png"
        else:
            uv = [(p["x"], p["y"]) for p in module["uv"]]
            image = side + "EyeColor.png"
        shading = [[normal_at(p.y, p.z) for p in points] for points in mapped]
        parts.append(
            make_part(
                dest_name,
                mapped,
                faces,
                uv,
                image,
                "eyeBlink" + side if len(mapped) > 1 else "",
                shading,
            )
        )
    # Join the Base outer ring to every original boundary corner. No overlapping eyes or open seam.
    inner = [transform(p, i) for i, p in enumerate(old_outer)]
    inner.sort(key=angle)
    bridge = inner + outer
    ia = [angle(p) for p in inner]
    oa = [angle(p) for p in outer]
    faces = []
    i = j = 0
    ni, no = len(inner), len(outer)
    while i < ni or j < no:
        next_i = (
            ia[(i + 1) % ni] + (2 * math.pi if i + 1 >= ni else 0)
            if i < ni
            else float("inf")
        )
        next_j = (
            oa[(j + 1) % no] + (2 * math.pi if j + 1 >= no else 0)
            if j < no
            else float("inf")
        )
        if next_i < next_j:
            faces.append([i % ni, (i + 1) % ni, ni + j % no])
            i += 1
        else:
            faces.append([i % ni, ni + (j + 1) % no, ni + j % no])
            j += 1
    for face in faces:
        if (bridge[face[1]] - bridge[face[0]]).cross(
            bridge[face[2]] - bridge[face[0]]
        ).x > 0:
            face.reverse()
    shading = [normal_at(p.y, p.z) for p in inner] + [normals[i] for i in boundary]
    parts.append(
        make_part(
            side + "EyeJoin",
            [bridge],
            faces,
            skin_uv(bridge),
            side + "LidSkin.png",
            shading=[shading],
        )
    )
    removed.update(selected)
    audit.append(
        dict(
            side=side,
            source="LittleMungMannequin",
            irisColor=eye_fit["irisColor"] if eye_fit else None,
            cutTriangles=len(selected),
            boundaryVertices=len(boundary),
            blinkFrames=4,
            eyeScale=[sx, sz],
        )
    )
    print(audit[-1], flush=True)


def edge_counts(points, indices):
    counts = Counter()

    def position(index):
        return tuple(round(points[index][axis], 6) for axis in ("x", "y", "z"))

    for offset in range(0, len(indices), 3):
        ids = indices[offset : offset + 3]
        for first, second in zip(ids, ids[1:] + ids[:1]):
            counts[tuple(sorted((position(first), position(second))))] += 1
    return counts


kept = [i for index, face in enumerate(triangles) if index not in removed for i in face]
# Use the same float32 coordinates as Blender/Unity mesh buffers on both sides.
surface_edges = edge_counts([unity(vertex) for vertex in vertices], kept)
for part in parts:
    if part["name"].endswith("Eye") or part["name"].endswith("Line"):
        continue
    surface_edges.update(edge_counts(part["vertices"], part["triangles"]))
for part in parts:
    if not part["name"].endswith("Join"):
        continue
    boundary_edges = edge_counts(part["vertices"], part["triangles"])
    gaps = [
        edge
        for edge, count in boundary_edges.items()
        if count == 1 and surface_edges[edge] != 2
    ]
    if gaps:
        print("GAP_DETAIL", [(edge, surface_edges[edge]) for edge in gaps], flush=True)
        raise ValueError(
            part["name"] + " has disconnected skin edges: " + str(len(gaps))
        )
for entry in audit:
    entry["unmatchedSkinEdges"] = 0

(output / "Blink.json").write_text(
    json.dumps(dict(parts=parts, bodyTriangles=kept)), encoding="utf-8"
)
(output / "BlinkAudit.json").write_text(json.dumps(audit, indent=2), encoding="utf-8")
# Editable context copy. The original files are only read.
body = bpy.data.meshes.new("FaceSurface")
body.from_pydata(
    vertices, [], [triangles[i] for i in range(len(triangles)) if i not in removed]
)
body.update()
obj = bpy.data.objects.new("FaceSurface", body)
bpy.context.collection.objects.link(obj)
for poly in body.polygons:
    poly.use_smooth = True
layer = body.uv_layers.new(name="UVMap")
for loop in body.loops:
    layer.data[loop.index].uv = (
        source["uv"][loop.vertex_index]["x"],
        source["uv"][loop.vertex_index]["y"],
    )
mat = bpy.data.materials.new("FaceSurface")
mat.use_nodes = True
node = mat.node_tree.nodes.new("ShaderNodeTexImage")
node.image = texture
texture.pack()
mat.node_tree.links.new(
    node.outputs["Color"],
    mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"],
)
body.materials.append(mat)
bpy.ops.wm.save_as_mainfile(
    filepath=str(
        output
        / (
            (eye_settings["name"] if eye_settings else "LittleMung_Business_Male")
            + "_Blink.blend"
        )
    )
)
