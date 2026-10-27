"""Export an edited player rig for Unity without rewriting the Blender source."""
import argparse
import sys
from pathlib import Path

import bpy

parser = argparse.ArgumentParser()
parser.add_argument("--source", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :])
source = args.source.resolve()
output = args.output.resolve()
if source == output or source.suffix.lower() != ".blend" or output.suffix.lower() != ".fbx":
    raise ValueError("Use a .blend source and a separate .fbx output.")
if output.exists():
    raise FileExistsError("Preserve existing exports; choose a new output path.")
bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
if bpy.context.object and bpy.context.object.mode != "OBJECT":
    bpy.ops.object.mode_set(mode="OBJECT")
objects = [obj for obj in bpy.context.scene.objects if obj.type in {"ARMATURE", "MESH"}]
armatures = [obj for obj in objects if obj.type == "ARMATURE"]
if len(armatures) != 1:
    raise ValueError("This player export requires exactly one armature.")
armature = armatures[0]
bpy.ops.object.select_all(action="DESELECT")
for obj in objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = armature
armature.animation_data_clear()
armature.data.pose_position = "POSE"
for bone in armature.pose.bones:
    bone.matrix_basis.identity()
output.parent.mkdir(parents=True, exist_ok=True)
# Store the scene unit conversion at FBX level, not as a 100x armature transform.
bpy.ops.export_scene.fbx(
    filepath=str(output),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=False,
    axis_forward="-Z",
    axis_up="Y",
    apply_scale_options="FBX_SCALE_ALL",
    path_mode="AUTO",
)
print(f"Exported Unity rig: {output}")
