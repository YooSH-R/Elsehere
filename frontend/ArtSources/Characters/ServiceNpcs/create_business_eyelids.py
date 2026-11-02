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


removed = set()
parts = []
audit = []
for side, sign in [("Left", 1), ("Right", -1)]:
    # Measured businessman eye centers. Preserve the reviewed eye size, not head size.
    center, cz = sign * 0.1455197133, 0.6378430941
    old_center, old_cz = sign * 0.130, 0.595
    sx, sz = 1.15, 1.12
    patch_rx, patch_rz = 0.084, 0.073
    hole_rx, hole_rz = 0.093, 0.081
    selected = set()

    def key(i):
        return tuple(round(c, 6) for c in vertices[i])

    for index, ids in enumerate(triangles):
        p = sum((vertices[i] for i in ids), Vector()) / 3
        if (
            p.x < -0.15
            and ((p.y - center) / hole_rx) ** 2 + ((p.z - cz) / hole_rz) ** 2 < 1
        ):
            selected.add(index)
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
            if index in selected or not any(key(i) in bad for i in ids):
                continue
            p = sum((vertices[i] for i in ids), Vector()) / 3
            if (
                p.x < -0.15
                and ((p.y - center) / 0.112) ** 2 + ((p.z - cz) / 0.100) ** 2 < 1
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
    # Face curvature and skin are fitted outside the old painted eye rim.
    samples = []
    depth = []
    colors = []
    for a in np.linspace(0, 2 * math.pi, 160, endpoint=False):
        y = center + 0.082 * math.cos(a)
        z = cz + 0.072 * math.sin(a)
        p, n, color = surface(y, z)
        if color[0] > 0.4 and 0.4 < color[1] / color[0] < 0.9:
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
            target_y = center + patch_rx * math.cos(theta)
            target_z = cz + patch_rz * math.sin(theta)
            y += (target_y - (center + (edge.y - old_center) * sx)) * blend
            z += (target_z - (cz + (edge.z - old_cz) * sz)) * blend
        residual = p.x - float(base_fit @ [1, a, b, a * a, a * b, b * b])
        x = face_x(y, z) + residual * (1 - blend)
        return Vector((x, y, z))

    size = 256
    rgba = []
    extent = 0.130
    for row in range(size):
        z = cz - extent + 2 * extent * row / (size - 1)
        for column in range(size):
            y = center - extent + 2 * extent * column / (size - 1)
            clean = np.append(
                np.clip(np.array([1, y - center, z - cz]) @ skin_fit, 0, 1), 1
            )
            radius = math.sqrt(((y - center) / 0.061) ** 2 + ((z - cz) / 0.054) ** 2)
            blend = np.clip((radius - 1.0) / 0.30, 0, 1)
            blend = blend * blend * (3 - 2 * blend)
            actual = surface(y, z)[2]
            rgba.append(clean * (1 - blend) + actual * blend)
    save_image(side + "LidSkin.png", rgba, size)
    save_image(side + "LidLine.png", [[0.055, 0.025, 0.012, 1]] * 4, 2)
    shutil.copy2(
        base_folder / (side + "EyeColor.png"), output / (side + "EyeColor.png")
    )

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
            mapped = [points[:260] for points in mapped]
            faces = [face for face in faces if max(face) < 260]
            uv = [(0.5, 0.5)] * 260
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
surface_edges = edge_counts(source["vertices"], kept)
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
    filepath=str(output / "LittleMung_Business_Male_Blink.blend")
)
