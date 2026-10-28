"""Export the existing Mixamo motions and their actual rest skeleton without changing source files."""
import argparse
import json
import sys
from pathlib import Path

import bpy


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--replace-generated", action="store_true")
    arguments = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :])
    source = arguments.source.resolve()
    output = arguments.output.resolve()
    if source == output:
        raise ValueError("Source and output must be different directories.")
    names = ["Walk", "Run", "Jump"]
    for name in names:
        if not (source / (name + ".fbx")).is_file():
            raise FileNotFoundError(source / (name + ".fbx"))
    destinations = [output / (name + ".fbx") for name in names + ["Reference"]]
    if not arguments.replace_generated and any(path.exists() for path in destinations):
        raise FileExistsError("Generated files already exist; use --replace-generated explicitly.")
    output.mkdir(parents=True, exist_ok=True)
    report = []
    for name in names:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(
            filepath=str(source / (name + ".fbx")), automatic_bone_orientation=False
        )
        armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
        armature.name = "MotionRig"
        bpy.context.view_layer.objects.active = armature
        bpy.ops.object.select_all(action="DESELECT")
        armature.select_set(True)
        first, last = armature.animation_data.action.frame_range
        bpy.context.scene.frame_start = int(first)
        bpy.context.scene.frame_end = int(last)
        report.append(
            {"motion": name, "fps": bpy.context.scene.render.fps, "first": first, "last": last}
        )
        bpy.ops.export_scene.fbx(
            filepath=str(output / (name + ".fbx")),
            use_selection=True,
            object_types={"ARMATURE"},
            add_leaf_bones=False,
            bake_anim=True,
            bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=False,
            bake_anim_simplify_factor=0,
            axis_forward="-Z",
            axis_up="Y",
        )
        if name == "Walk":
            armature.animation_data_clear()
            for bone in armature.pose.bones:
                bone.matrix_basis.identity()
            armature.data.pose_position = "REST"
            bpy.context.view_layer.update()
            bpy.ops.export_scene.fbx(
                filepath=str(output / "Reference.fbx"),
                use_selection=True,
                object_types={"ARMATURE"},
                add_leaf_bones=False,
                bake_anim=False,
                axis_forward="-Z",
                axis_up="Y",
            )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
