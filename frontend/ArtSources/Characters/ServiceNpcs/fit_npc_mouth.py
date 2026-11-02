"""Offline authoring on an exported master-bound COPY. Never edits the source FBX."""

import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).parent
NAME = sys.argv[sys.argv.index("--") + 1]
OUT = ROOT / NAME
sys.path.insert(0, str(ROOT))
import face_authoring_common as helper
from face_authoring_common import PROJECT, aim, make_body, setup
from mouth_landmarks import locate_mouth

helper.TEXTURE = (
    PROJECT / (OUT / "TexturePath.txt").read_text(encoding="utf-8-sig").strip()
)
AUTHOR = PROJECT / "frontend/ArtSources/Characters/LittleMungMannequin"
sys.path.insert(0, str(AUTHOR))
from build_speaking_mouth import build_speaking_mouth

DEST = OUT / "Face"
DEST.mkdir(exist_ok=True)
OFFSET = None


def unity(v):
    return dict(x=float(v.x), y=float(v.z), z=float(-v.y))


def shape_data(ob):
    keys = ob.data.shape_keys.key_blocks
    basis = keys[0]
    return [
        dict(
            name=k.name,
            deltaVertices=[unity(v.co - b.co) for v, b in zip(k.data, basis.data)],
            deltaNormals=json.loads(ob.get("normals_" + k.name, "[]")),
        )
        for k in keys[1:]
    ]


bpy.ops.wm.read_factory_settings(use_empty=True)
data = json.loads((OUT / "Surface.json").read_text(encoding="utf-8-sig"))
body, world_vertices, world_normals, triangles, texture = make_body(data)
center, SCALE, landmarks = locate_mouth(data, world_vertices, triangles, texture)
OFFSET = center - Vector((-0.22, 0, 0.501)) * SCALE
(DEST / "Landmarks.json").write_text(json.dumps(landmarks, indent=2))
print("MOUTH_LANDMARKS", NAME, landmarks)
# Use the Base mouth's authored dimensions in a translated local facial frame.
vertices = [(v - OFFSET) / SCALE for v in world_vertices]
normals = [n.copy() for n in world_normals]
for v, p in zip(body.data.vertices, vertices):
    v.co = p
channels = [
    "mouthSmileLeft",
    "mouthSmileRight",
    "viseme_AA",
    "viseme_FF",
    "viseme_TH",
    "viseme_PP",
    "viseme_E",
    "viseme_OH",
    "viseme_OU",
    "viseme_I",
    "viseme_EU",
]
body.shape_key_add(name="Basis")
for channel in channels:
    body.shape_key_add(name=channel)
parts, kept, mouth_x = build_speaking_mouth(
    body, data, vertices, normals, triangles, texture, DEST
)
# Reuse the actual Base tooth/tongue/closed-lip geometry and authored targets.
with bpy.data.libraries.load(
    str(AUTHOR / "LittleMung_Base_Male_Face.blend"), link=False
) as (available, loaded):
    loaded.objects = [
        "SpeakingLips",
        "ClosedLipCrease",
        "UpperTeeth",
        "LowerTeeth",
        "Tongue",
    ]
donor = loaded.objects[0]
count = len(donor.data.vertices) // 6
outer = [v.co for v in donor.data.vertices[:count]]


def terms(v):
    return [1, v.y, v.z - 0.501, v.y * v.y, v.y * (v.z - 0.501), (v.z - 0.501) ** 2]


coeff = np.linalg.lstsq(
    np.array([terms(v) for v in outer]), np.array([v.x for v in outer]), rcond=None
)[0]
active = {
    "ClosedLipCrease": {"viseme_PP"},
    "UpperTeeth": {"viseme_FF", "viseme_TH", "viseme_E", "viseme_I"},
    "LowerTeeth": {"viseme_I"},
    "Tongue": {"viseme_AA", "viseme_TH", "viseme_OH", "viseme_OU"},
}
for ob in loaded.objects[1:]:
    bpy.context.collection.objects.link(ob)
    for key in ob.data.shape_keys.key_blocks:
        for vertex in key.data:
            v = vertex.co
            v.x += mouth_x(v.y, v.z) - float(np.dot(coeff, terms(v)))
            if key.name not in active[ob.name.split(".")[0]]:
                v.x += 0.025
            if ob.name.startswith("ClosedLipCrease"):
                v.y *= 0.0175 / 0.022
    color = (
        ob.data.materials[0]
        .node_tree.nodes.get("Principled BSDF")
        .inputs["Base Color"]
        .default_value[:3]
    )
    parts.append((ob, color))
bpy.data.objects.remove(donor, do_unlink=True)
# Translate the authored patch back to the unchanged master surface coordinates.
for ob in [body] + [p[0] for p in parts]:
    for key in ob.data.shape_keys.key_blocks:
        for vertex in key.data:
            vertex.co = vertex.co * SCALE + OFFSET
    for key in ob.data.shape_keys.key_blocks[1:]:
        key.value = 0
    ob.data.update_tag()
bpy.context.view_layer.update()
result = dict(
    vertices=data["vertices"],
    restVertices=[unity(v) for v in world_vertices],
    restNormals=[unity(v) for v in world_normals],
    triangles=kept,
    shapes=[],
    parts=[],
    textureFile="",
)
for ob, color in parts:
    ob.data.calc_loop_triangles()
    result["parts"].append(
        dict(
            name=ob.name.split(".")[0],
            vertices=[unity(v.co) for v in ob.data.shape_keys.key_blocks[0].data],
            triangles=[i for t in ob.data.loop_triangles for i in t.vertices],
            color=dict(r=color[0], g=color[1], b=color[2], a=1),
            shapes=shape_data(ob),
            uv=json.loads(ob.get("vertex_uv", "[]")),
            textureFile=ob.get("texture_file", ""),
            normals=json.loads(ob.get("base_normals", "[]")),
        )
    )
(DEST / "FaceShapes.json").write_text(
    json.dumps(result, separators=(",", ":")), encoding="utf-8"
)
scene, camera = setup()
for label, angle in [("Front", 0), ("LeftOblique", 35), ("RightOblique", -35)]:
    a = math.radians(angle)
    head_top = max(v.z for v in world_vertices)
    focus = head_top - landmarks["headHeight"] / 2
    camera.location = (-3 * math.cos(a), 3 * math.sin(a), focus)
    aim(camera, (0, 0, focus))
    camera.data.ortho_scale = landmarks["headHeight"] * 1.35
    scene.render.filepath = str(DEST / ("Face_" + label + ".png"))
    bpy.ops.render.render(write_still=True)
camera.location = (-3, center.y, center.z)
aim(camera, center)
camera.data.ortho_scale = 0.16 * SCALE
for channel in ["Basis", "viseme_PP", "viseme_AA", "viseme_FF", "viseme_TH"]:
    for ob, color in parts:
        for key in ob.data.shape_keys.key_blocks[1:]:
            key.value = 1 if key.name == channel else 0
    scene.render.filepath = str(DEST / ("Mouth_" + channel + ".png"))
    bpy.ops.render.render(write_still=True)
for ob, color in parts:
    for key in ob.data.shape_keys.key_blocks[1:]:
        key.value = 0
bpy.ops.wm.save_as_mainfile(filepath=str(DEST / (NAME + "_Face.blend")))
print(
    "NPC_MOUTH_COMPLETE",
    len(data["triangles"]) // 3 - len(kept) // 3,
    "replaced triangles",
    len(parts),
    "mouth parts",
)
