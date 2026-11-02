"""Run with Blender --background --factory-startup --python-exit-code 1 --python <this file>."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest

import bpy

sys.dont_write_bytecode = True
PROJECT = Path(os.environ.get('TRIPO_TEST_PROJECT', Path(__file__).resolve().parents[3]))
SCRIPT = PROJECT / 'frontend/UnityClient/Assets/Scripts/Editor/Tripo/Blender/tripo_materials.py'
spec = importlib.util.spec_from_file_location('tripo_materials', SCRIPT)
materials = importlib.util.module_from_spec(spec)
spec.loader.exec_module(materials)


class MaterialExtractionTests(unittest.TestCase):
    def setUp(self):
        self.output_root = Path(os.environ.get('TRIPO_TEST_OUTPUT', tempfile.gettempdir())).resolve()
        self.output_root.mkdir(parents=True, exist_ok=True)
        self.scratch = tempfile.TemporaryDirectory(prefix='tripo-material-test-', dir=self.output_root)
        self.folder = Path(self.scratch.name)

    def tearDown(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if self.folder.resolve().parent != self.output_root:
            raise RuntimeError('Unexpected test cleanup path')
        self.scratch.cleanup()

    def make_fbx(self, missing=None):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.mesh.primitive_cube_add()
        mesh = bpy.context.object
        mat = bpy.data.materials.new('TestSuit')
        mat.use_nodes = True
        mesh.data.materials.append(mat)
        shader = mat.node_tree.nodes.get('Principled BSDF')
        files = {}
        for channel, color in [('color', (.1,.2,.6,1)), ('normal', (.5,.5,1,1))]:
            path = self.folder / (channel + '.png')
            image = bpy.data.images.new(channel, width=16, height=16)
            image.pixels.foreach_set(list(color) * 256)
            image.filepath_raw = str(path)
            image.file_format = 'PNG'
            image.save()
            bpy.data.images.remove(image)
            image = bpy.data.images.load(str(path))
            if channel == 'normal': image.colorspace_settings.name = 'Non-Color'
            node = mat.node_tree.nodes.new('ShaderNodeTexImage')
            node.image = image
            if channel == 'color':
                mat.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
            else:
                normal = mat.node_tree.nodes.new('ShaderNodeNormalMap')
                mat.node_tree.links.new(node.outputs['Color'], normal.inputs['Color'])
                mat.node_tree.links.new(normal.outputs['Normal'], shader.inputs['Normal'])
            files[channel] = path
        source = self.folder / 'Fixture.fbx'
        bpy.ops.export_scene.fbx(filepath=str(source), use_selection=True, bake_anim=False,
                                 path_mode='ABSOLUTE' if missing else 'COPY', embed_textures=not missing)
        if missing: files[missing].unlink()
        return source

    def test_embedded_normal_is_loaded_and_exported_without_modifying_source(self):
        source = self.make_fbx()
        before = hashlib.sha256(source.read_bytes()).hexdigest()
        output = self.folder/'output'
        materials.extract(source, output)
        result = json.loads((output/'materials.json').read_text())
        entry = result['materials'][0]
        self.assertTrue(entry['color'])
        self.assertTrue(entry['normal'])
        self.assertEqual([], result['warnings'])
        image = bpy.data.images.load(str(output/entry['normal']), check_existing=False)
        self.assertEqual([16,16], list(image.size))
        self.assertAlmostEqual(.5, image.pixels[0], delta=.01)
        self.assertAlmostEqual(1, image.pixels[2], delta=.01)
        self.assertEqual(before, hashlib.sha256(source.read_bytes()).hexdigest())

    def test_missing_color_is_still_rejected(self):
        source = self.make_fbx(missing='color')
        with self.assertRaisesRegex(ValueError, 'Missing color image'):
            materials.extract(source, self.folder/'output')

    def test_missing_normal_is_still_rejected(self):
        source = self.make_fbx(missing='normal')
        with self.assertRaisesRegex(ValueError, 'Missing normal image'):
            materials.extract(source, self.folder/'output')


if __name__ == '__main__':
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(MaterialExtractionTests))
    if not result.wasSuccessful(): raise RuntimeError('Material extraction tests failed')
