"""Run with Blender --background --factory-startup --python-exit-code 1 --python <file>."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
import sys
sys.dont_write_bytecode = True

import bpy

ROOT = Path(__file__).resolve().parents[3]
BRIDGE = ROOT / "frontend/UnityClient/Assets/Scripts/Editor/Tripo/Blender/tripo_bridge.py"
spec = importlib.util.spec_from_file_location("tripo_bridge", BRIDGE)
bridge = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bridge)


class BlenderBridgeTests(unittest.TestCase):
    def test_editable_source_export_preserves_bones_weights_and_expression(self):
        with tempfile.TemporaryDirectory(prefix="tripo-bridge-") as folder:
            folder = Path(folder)
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.mesh.primitive_cube_add()
            face = bpy.context.object
            face.name = "TestFace"
            face.shape_key_add(name="Basis")
            smile = face.shape_key_add(name="mouthSmileLeft")
            smile.data[0].co.x += 0.2
            bpy.ops.object.armature_add()
            rig = bpy.context.object
            rig.name = "TestRig"
            group = face.vertex_groups.new(name=rig.data.bones[0].name)
            group.add(list(range(len(face.data.vertices))), 1.0, "REPLACE")
            modifier = face.modifiers.new("Armature", "ARMATURE")
            modifier.object = rig
            face.parent = rig
            source_fbx = folder / "Generated.fbx"
            bpy.ops.export_scene.fbx(
                filepath=str(source_fbx), use_mesh_modifiers=False,
                add_leaf_bones=False, bake_anim=False,
            )
            blend = folder / "Editable.blend"
            exported = folder / "Candidate.fbx"

            bridge.prepare_source(source_fbx, blend)
            self.assertTrue(blend.is_file(), "Prepare must create an editable source")
            original_source = blend.read_bytes()
            bridge.prepare_source(source_fbx, blend)
            self.assertEqual(blend.read_bytes(), original_source, "Never overwrite artist source")
            bridge.export_model(blend, exported)
            self.assertTrue(exported.is_file(), "Export must create a new FBX candidate")
            self.assertEqual(blend.read_bytes(), original_source, "Export must not modify source")

            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.fbx(filepath=str(exported))
            meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
            self.assertEqual(len(meshes), 1)
            result = meshes[0]
            self.assertIn("mouthSmileLeft", result.data.shape_keys.key_blocks)
            self.assertEqual(len(result.data.vertices), 8)
            self.assertTrue(any(obj.type == "ARMATURE" for obj in bpy.data.objects))
            self.assertTrue(all(vertex.groups for vertex in result.data.vertices))
            key = result.data.shape_keys.key_blocks["mouthSmileLeft"]
            basis = result.data.shape_keys.key_blocks[0]
            self.assertGreater((key.data[0].co - basis.data[0].co).length, 0.1)


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(BlenderBridgeTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful():
        raise RuntimeError("Blender bridge tests failed")
