# Cafe female face authoring copy

This directory contains a face-only study of the Tripo cafe female. The source NPC and service rig remain unchanged.

- `CafeFemaleFacePreview.blend`: editable Basis plus seven shape keys. Lips, oral interior, teeth, tongue and closed-lid contours are separate; this is not a replacement body rig.
- `CafeFemaleSourceSurface.json`: exact source Unity mesh positions, UVs, normals and rig weight data used to identify the source topology.
- `CafeFemaleFaceShapes.json`: vertex offsets, blink normal offsets and oral mesh data consumed by the Unity builder.
- `build_speaking_mouth.py`: replaces the copy's mouth surface with lip loops and an oral bowl. It bakes a separate local skin texture; the source texture remains untouched.
- `References/`: supplied speech-mouth targets; only AA, FF and TH are implemented in this six-pose stage.
- `create_face_shapes.py`: local Blender authoring recipe for this face. Use a fresh output directory to protect artist edits. Another face requires its own landmarks and visual checks.

Run Blender in background with `--python create_face_shapes.py -- --surface CafeFemaleSourceSurface.json --texture <original BaristaColor.png> --output <new output directory>`.

See `docs/unity/cafe-female-face.md` for the Unity scene, control names and prototype limitations. No service API is involved in this workflow.

Unity applies explicit shape-normal offsets. Blender live Shape Key shading can differ; use the Unity preview scene as the final visual check.
