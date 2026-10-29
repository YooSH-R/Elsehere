"""Create a local integration-test fixture. This is not a Little Mung asset."""
from pathlib import Path
import sys
import bpy

output = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_cube_add()
face = bpy.context.object
face.name = "TestFace"
face.shape_key_add(name="Basis")
for name in ("mouthSmileLeft", "eyeBlinkLeft", "viseme_AA", "viseme_FF", "viseme_TH"):
    shape = face.shape_key_add(name=name)
    shape.data[0].co.x += 0.2
bpy.ops.object.armature_add()
rig = bpy.context.object
rig.name = "TestRig"
weights = face.vertex_groups.new(name=rig.data.bones[0].name)
weights.add(list(range(len(face.data.vertices))), 1.0, "REPLACE")
modifier = face.modifiers.new("Armature", "ARMATURE")
modifier.object = rig
face.parent = rig
bpy.ops.export_scene.fbx(
    filepath=str(output), use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
)
