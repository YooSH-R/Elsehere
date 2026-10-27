"""Blender source preparation and FBX export for the Unity Tripo workbench."""
import argparse
from pathlib import Path
import sys

import bpy


def prepare_source(source_path, blend_path):
    source_path = Path(source_path).resolve()
    blend_path = Path(blend_path).resolve()
    if blend_path.exists():
        return
    if not source_path.is_file():
        raise FileNotFoundError(source_path)
    if source_path.suffix.lower() != ".fbx":
        raise ValueError("Source must be an FBX file")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source_path), use_anim=False)
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)


def export_model(blend_path, output_path):
    blend_path = Path(blend_path).resolve()
    output_path = Path(output_path).resolve()
    if not blend_path.is_file() or blend_path.suffix.lower() != ".blend":
        raise ValueError("A saved .blend source is required")
    if output_path.exists():
        raise FileExistsError("Export must use a new candidate path")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(blend_path), load_ui=False, use_scripts=False)
    # Applying topology-changing modifiers can remove shape keys. Export the editable mesh.
    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        check_existing=False,
        object_types={"ARMATURE", "MESH", "EMPTY"},
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=False,
        # Store meter/centimeter conversion in FBX units, not a 100x armature scale.
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=True,
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("operation", choices=("prepare", "export"))
    parser.add_argument("source")
    parser.add_argument("destination")
    arguments = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if arguments.operation == "prepare":
        prepare_source(arguments.source, arguments.destination)
    else:
        export_model(arguments.source, arguments.destination)


if __name__ == "__main__":
    main()
