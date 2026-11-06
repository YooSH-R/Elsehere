"""Fit the reviewed emotion authoring to exported NPC faces, entirely offline.

Export with NpcEmotionBuilder.Export first. Pass --source <Source.json>
--work <new external directory> --output <authoring directory>.
The neutral vertices, UVs, rig and existing speech targets remain in Unity;
this script outputs local emotion deltas and the optional replacement eyelids.
"""

import argparse
import copy
import gzip
import json
import runpy
import shutil
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).parent))
from landmarks import measure
from lid_visibility import keep_lid_lines_visible
from eye_shading import smooth_eye_shading

parser = argparse.ArgumentParser()
parser.add_argument("--source", type=Path, required=True)
parser.add_argument("--work", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--new-eyes", type=Path)
parser.add_argument("--smooth-eye-shading", action="store_true")
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :])
characters = Path(__file__).resolve().parent.parent
source = json.loads(args.source.read_text(encoding="utf-8-sig"))
work = args.work.resolve()
output = args.output.resolve()
if (
    (work / "Input").exists()
    or (output / "Emotions.json").exists()
    or (output / "Emotions.json.gz").exists()
):
    raise FileExistsError("기존 제작 결과를 보존하도록 새로운 작업 폴더를 사용하세요.")
input_folder, eye_folder = work / "Input", work / "Eyes"
input_folder.mkdir(parents=True)
eye_folder.mkdir()
output.mkdir(parents=True, exist_ok=True)


def points(values):
    return np.array([[v["x"], -v["z"], v["y"]] for v in values], dtype=float)


def vectors(values):
    return [{"x": float(x), "y": float(z), "z": float(-y)} for x, y, z in values]


def unit(values):
    return values / np.maximum(np.linalg.norm(values, axis=1, keepdims=True), 1e-12)


body = next(m for m in source["meshes"] if m["path"] == source["body"])
parts = {m["name"]: m for m in source["meshes"] if m is not body}
new_eyes = None
if args.new_eyes:
    new_eyes = json.loads((args.new_eyes / "Blink.json").read_text())
    for part in new_eyes["parts"]:
        parts[part["name"]] = dict(part, texture=str(args.new_eyes / part["texture"]))
if args.smooth_eye_shading:
    if new_eyes:
        raise ValueError("새 눈의 최초 연결 후 내보낸 메시에서 음영을 보정하세요.")
    smooth_eye_shading(body, parts)
left, right = (points(parts[side + "Eye"]["vertices"]) for side in ("Left", "Right"))
left_center, right_center = ((p.min(0) + p.max(0)) / 2 for p in (left, right))
lips = points(parts["SpeakingLips"]["vertices"])
mouth = lips[-len(lips) // 6 :].mean(0)
# Three semantic landmarks, rather than body size, establish the local face frame.
# Keep the reviewed asymmetry between the cafe mouth and eye midpoints.
landmarks = np.array([left_center[1:], right_center[1:], mouth[1:]])
destination = np.array([[0.082, 0.573], [-0.155, 0.573], [-0.043, 0.477]])
mapping = np.linalg.solve(np.column_stack([landmarks, np.ones(3)]), destination)
matrix = np.zeros((3, 3))
matrix[1:, 1:] = mapping[:2].T
matrix[0, 0] = np.sqrt(abs(np.linalg.det(matrix[1:, 1:])))
offset = np.r_[-0.22 - matrix[0, 0] * mouth[0], mapping[2]]
inverse = np.linalg.inv(matrix)
if not 0.2 < matrix[0, 0] < 4:
    raise ValueError("입과 눈 기준점의 비율을 확인하세요.")


def normalized(values, normal=False, delta=False):
    a = points(values)
    if normal:
        return vectors(unit(a @ inverse))
    return vectors(a @ matrix.T + (0 if delta else offset))


def normalized_mesh(mesh):
    result = copy.deepcopy(mesh)
    result["vertices"] = normalized(mesh["vertices"])
    result["normals"] = normalized(mesh["normals"], normal=True)
    for shape in result.get("shapes", []):
        baseline = points(mesh["normals"])
        changed = baseline + points(shape["deltaNormals"])
        shape["deltaNormals"] = vectors(
            unit(changed @ inverse) - unit(baseline @ inverse)
        )
        shape["deltaVertices"] = normalized(shape["deltaVertices"], delta=True)
    return result


def build_object(name, mesh_data, texture=""):
    vertices = points(mesh_data["vertices"])
    normals = points(mesh_data["normals"])
    faces = np.array(mesh_data["triangles"]).reshape(-1, 3).copy()
    for face in faces:
        a, b, c = vertices[face]
        if np.dot(np.cross(b - a, c - a), normals[face[0]]) < 0:
            face[:] = face[::-1]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices.tolist(), [], faces.tolist())
    mesh.update()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(ob)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    mesh.normals_split_custom_set_from_vertices(normals.tolist())
    layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        uv = (
            mesh_data["uv"][loop.vertex_index]
            if len(mesh_data["uv"]) == len(vertices)
            else {"x": 0, "y": 0}
        )
        layer.data[loop.index].uv = uv["x"], uv["y"]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = 0.8
    if texture:
        image = bpy.data.images.load(texture, check_existing=True)
        image.pack()
        node = mat.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        mat.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
    mesh.materials.append(mat)
    ob.shape_key_add(name="Basis")
    for shape in mesh_data.get("shapes", []):
        label = shape["name"] + (
            "" if shape["weight"] == 100 else "_" + str(shape["weight"])
        )
        key = ob.shape_key_add(name=label)
        for vertex, position in zip(
            key.data, vertices + points(shape["deltaVertices"])
        ):
            vertex.co = position
    return ob


bpy.ops.wm.read_factory_settings(use_empty=True)
eye_export = {
    "parts": [],
    "bodyTriangles": new_eyes["bodyTriangles"] if new_eyes else body["triangles"],
}
eye_names = {
    side + part
    for side in ("Left", "Right")
    for part in ("Eye", "UpperLid", "LowerLid", "LidLine", "EyeJoin")
}
for name in sorted(eye_names):
    original = parts[name]
    part = normalized_mesh(original)
    if new_eyes:
        frames = [
            {
                "weight": f["weight"],
                "vertices": normalized(f["vertices"]),
                "normals": normalized(f["normals"], normal=True),
            }
            for f in original["frames"]
        ]
        channel = original["channel"]
    else:
        channel = "eyeBlink" + ("Left" if name.startswith("Left") else "Right")
        frames = []
        for shape in original["shapes"]:
            if shape["name"] == channel:
                frames.append(
                    {
                        "weight": shape["weight"],
                        "vertices": normalized(
                            vectors(
                                points(original["vertices"])
                                + points(shape["deltaVertices"])
                            )
                        ),
                        "normals": normalized(
                            vectors(
                                points(original["normals"])
                                + points(shape["deltaNormals"])
                            ),
                            normal=True,
                        ),
                    }
                )
    texture = Path(original["texture"])
    # Preserve every character's reviewed eye and skin texture byte for byte.
    filename = texture.name
    shutil.copyfile(texture, eye_folder / filename)
    part.update(frames=frames, channel=channel if frames else "", texture=filename)
    eye_export["parts"].append(part)
    build_object(name, part, str(texture))
(eye_folder / "Blink.json").write_text(json.dumps(eye_export))
bpy.ops.wm.save_as_mainfile(
    filepath=str(eye_folder / "LittleMung_Cafe_Female_Blink.blend")
)
bpy.ops.wm.read_factory_settings(use_empty=True)
face = normalized_mesh(body)
face["parts"] = [
    normalized_mesh(m)
    for name, m in parts.items()
    if name not in eye_names
    and not name.startswith("Closed")
    or name == "ClosedLipCrease"
]
build_object("CafeFemaleFacePreview", face, body["texture"])
for part in face["parts"]:
    build_object(part["name"], part, part["texture"])
(input_folder / "CafeFemaleFaceShapes.json").write_text(json.dumps(face))
(input_folder / "CafeFemaleSourceSurface.json").write_text(json.dumps(face))
eye_tops = {
    side: float(
        max(
            points(part["vertices"])[:, 2].max()
            for part in eye_export["parts"]
            if part["name"] in (side + "Eye", side + "EyeJoin")
        )
    )
    for side in ("Left", "Right")
}
settings = measure(face, eye_tops)
for side in ("Left", "Right"):
    line = next(p for p in eye_export["parts"] if p["name"] == side + "LidLine")
    closed = points(line["frames"][-1]["vertices"])
    center = (closed[:, 1].min() + closed[:, 1].max()) / 2
    radius = np.ptp(closed[:, 1]) / 2
    distance = abs(closed[:, 1] - center) / radius
    curvature = closed[distance < 0.15, 2].mean() - closed[distance > 0.85, 2].mean()
    settings["eyeSmileLift" + side] = float(np.clip(0.012 - curvature, 0.020, 0.040))
(input_folder / "Settings.json").write_text(json.dumps(settings, indent=2))
print("표정 기준점", source["name"], settings, flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(input_folder / "CafeFemaleFacePreview.blend"))
sys.argv = [
    "author_emotions.py",
    "--",
    "--input",
    str(input_folder),
    "--eyes",
    str(eye_folder),
    "--output",
    str(work / "Canonical"),
    "--keep-eye-textures",
    "--settings",
    str(input_folder / "Settings.json"),
]
runpy.run_path(
    str(characters / "CafeFemaleEmotion/author_emotions.py"), run_name="__main__"
)
authored = json.loads((work / "Canonical/CafeFemaleEmotions.json").read_text())
keep_lid_lines_visible(authored)
originals = dict(parts, CafeFemaleFace=body)
for mesh in authored["meshes"] + authored["parts"]:
    original = originals[mesh["name"]]
    mesh["path"] = original.get("path", "")
    # Exact original arrays avoid introducing rounding changes to neutral meshes.
    mesh["vertices"] = original["vertices"]
    for shape in mesh["shapes"]:
        shape["deltaVertices"] = vectors(points(shape["deltaVertices"]) @ inverse.T)
        baseline = points(normalized(original["normals"], normal=True))
        target = unit((baseline + points(shape["deltaNormals"])) @ matrix)
        shape["deltaNormals"] = vectors(target - points(original["normals"]))
        if shape["name"].startswith(("browDown", "mouthFrown")):
            # Sparse skin triangles exaggerate the analytic deformation gradient.
            # Keep the stronger brow/lip silhouette with the soft stylized shading.
            shape["deltaNormals"] = vectors(points(shape["deltaNormals"]) * .2)
        if mesh["name"].endswith("EyeJoin") and shape["name"] == "mouthLaughOpen":
            # The bridge has an outer skin boundary and an inner lid boundary.
            # Only the outer boundary follows the laughing cheek; the inner one
            # must stay attached to the stationary outer ring of the eyelids.
            vertices = points(original["vertices"])
            body_vertices = points(body["vertices"])
            distance = ((vertices[:, None] - body_vertices) ** 2).sum(2).min(1)
            inner = distance > 1e-10
            delta = points(shape["deltaVertices"])
            delta[inner] = 0
            shape["deltaVertices"] = vectors(delta)
            normals = points(shape["deltaNormals"])
            normals[inner] = 0
            shape["deltaNormals"] = vectors(normals)
    if "normals" in mesh or args.smooth_eye_shading:
        mesh["normals"] = original["normals"]
    if "texture" in mesh:
        shutil.copyfile(work / "Canonical" / mesh["texture"], output / mesh["texture"])
authored.update(name=source["name"], prefab=source["prefab"], newEyes=bool(new_eyes))
with gzip.GzipFile(
    filename=str(output / "Emotions.json.gz"), mode="wb", mtime=0
) as stream:
    stream.write(json.dumps(authored, separators=(",", ":")).encode("utf-8"))
(output / "Landmarks.json").write_text(
    json.dumps(
        {
            "eyes": [left_center.tolist(), right_center.tolist()],
            "mouth": mouth.tolist(),
            "toCanonical": matrix.tolist(),
            "offset": offset.tolist(),
            "expressions": settings,
        },
        indent=2,
    )
)
for ob in bpy.data.objects:
    if ob.type != "MESH":
        continue
    if ob.data.shape_keys:
        for key in ob.data.shape_keys.key_blocks:
            values = (np.array([tuple(v.co) for v in key.data]) - offset) @ inverse.T
            for vertex, position in zip(key.data, values):
                vertex.co = position
for mesh in authored["parts"]:
    ob = bpy.data.objects[mesh["name"]]
    for shape in mesh["shapes"]:
        if shape["name"] == "mouthLaughOpen" and mesh["name"].endswith("EyeJoin"):
            key_name = shape["name"]
        elif shape["name"].startswith("eyeSquint") and mesh["name"].endswith("LidLine"):
            weight = shape.get("weight", 100)
            key_name = shape["name"] + ("" if weight == 100 else str(weight))
        else:
            continue
        key = ob.data.shape_keys.key_blocks[key_name]
        for vertex, position in zip(
            key.data, points(mesh["vertices"]) + points(shape["deltaVertices"])
        ):
            vertex.co = position
bpy.ops.wm.save_as_mainfile(filepath=str(output / (source["name"] + "_Emotions.blend")))
print(source["name"], "감정 제작 완료", flush=True)
