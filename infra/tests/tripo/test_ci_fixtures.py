"""Run with Blender in background mode; no production assets or API calls."""

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path

import bpy

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location(
    "create_ci_fixtures", Path(__file__).with_name("create_ci_fixtures.py")
)
fixtures = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixtures)


class CiFixtureTests(unittest.TestCase):
    def test_humanoid_input_has_required_bones_skin_uv_and_real_expression(self):
        with tempfile.TemporaryDirectory(prefix="a305-ci-model-") as folder:
            directory = Path(folder)
            fixtures.create_fixtures(directory)
            model_path = directory / "Humanoid.fbx"
            self.assertTrue(
                model_path.is_file(), "CI must generate its own model input"
            )
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.fbx(filepath=str(model_path))
            rig = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
            required = {
                "Hips",
                "Spine",
                "Chest",
                "Neck",
                "Head",
                "LeftUpperArm",
                "LeftLowerArm",
                "LeftHand",
                "RightUpperArm",
                "RightLowerArm",
                "RightHand",
                "LeftUpperLeg",
                "LeftLowerLeg",
                "LeftFoot",
                "RightUpperLeg",
                "RightLowerLeg",
                "RightFoot",
            }
            self.assertTrue(required.issubset(rig.data.bones.keys()))
            mesh = next(obj for obj in bpy.data.objects if obj.type == "MESH")
            self.assertTrue(all(vertex.groups for vertex in mesh.data.vertices))
            self.assertIsNotNone(mesh.data.uv_layers.active)
            keys = mesh.data.shape_keys.key_blocks
            basis = keys["Basis"]
            for name in (
                "mouthSmileLeft",
                "eyeBlinkLeft",
                "viseme_AA",
                "viseme_FF",
                "viseme_TH",
            ):
                self.assertIn(name, keys)
                self.assertGreater(
                    (keys[name].data[0].co - basis.data[0].co).length, 0.01
                )

    def test_material_input_retains_embedded_png_with_wrong_extension_and_duplicate_normal(
        self,
    ):
        with tempfile.TemporaryDirectory(prefix="a305-ci-material-") as folder:
            directory = Path(folder)
            fixtures.create_fixtures(directory)
            model_path = directory / "MisnamedColor.fbx"
            self.assertTrue(
                model_path.is_file(), "CI must supply the material regression input"
            )
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.fbx(filepath=str(model_path))
            material = next(
                material for material in bpy.data.materials if material.use_nodes
            )
            shader = next(
                node
                for node in material.node_tree.nodes
                if node.type == "BSDF_PRINCIPLED"
            )
            color = shader.inputs["Base Color"].links[0].from_node.image
            normal_map = shader.inputs["Normal"].links[0].from_node
            normal = normal_map.inputs["Color"].links[0].from_node.image
            self.assertTrue(color.filepath.lower().endswith(".jpg"))
            self.assertIsNotNone(color.packed_file, "FBX must embed its texture")
            self.assertTrue(
                bytes(color.packed_file.data).startswith(b"\x89PNG\r\n\x1a\n")
            )
            self.assertEqual(
                bytes(color.packed_file.data), bytes(normal.packed_file.data)
            )


if __name__ == "__main__":
    result = unittest.TextTestRunner(verbosity=2).run(
        unittest.defaultTestLoader.loadTestsFromTestCase(CiFixtureTests)
    )
    if not result.wasSuccessful():
        raise RuntimeError("CI fixture verification failed")
