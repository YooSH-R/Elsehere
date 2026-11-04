from pathlib import Path

import bpy
from mathutils import Vector

OUT = Path(__file__).parent
PROJECT = Path(__file__).resolve().parents[4]
TEXTURE = None  # Caller supplies the texture belonging to the exported source.


def vec(v):
    return Vector((v["x"], -v["z"], v["y"]))


def aim(ob, target):
    ob.rotation_euler = (
        (Vector(target) - ob.location).to_track_quat("-Z", "Y").to_euler()
    )


def setup():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 12
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new("ReviewWorld")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes.get("Background").inputs[0].default_value = (
        0.55,
        0.55,
        0.55,
        1,
    )
    scene.world.node_tree.nodes.get("Background").inputs[1].default_value = 0.6
    scene.view_settings.view_transform = "Standard"
    for name, loc, power in [("Key", (-3, -3, 4), 180), ("Fill", (-3, 3, 2), 90)]:
        bpy.ops.object.light_add(type="AREA", location=loc)
        light = bpy.context.object
        light.name = name
        light.data.energy = power
        light.data.size = 4
        aim(light, (0, 0, 0.5))
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    scene.camera = camera
    camera.data.type = "ORTHO"
    return scene, camera


def make_body(data):
    vertices = [vec(v) for v in data["vertices"]]
    normals = [vec(n) for n in data["normals"]]
    faces = [data["triangles"][i : i + 3] for i in range(0, len(data["triangles"]), 3)]
    for f in faces:
        if (vertices[f[1]] - vertices[f[0]]).cross(vertices[f[2]] - vertices[f[0]]).dot(
            normals[f[0]]
        ) < 0:
            f.reverse()
    mesh = bpy.data.meshes.new("BusinessSurface")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    ob = bpy.data.objects.new("LittleMung_Business_Male", mesh)
    bpy.context.collection.objects.link(ob)
    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.normals_split_custom_set_from_vertices(normals)
    layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        uv = data["uv"][loop.vertex_index]
        layer.data[loop.index].uv = (uv["x"], uv["y"])
    image = bpy.data.images.load(str(TEXTURE))
    image.pack()
    mat = bpy.data.materials.new("CharacterColor")
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = 0.8
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    mat.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
    mesh.materials.append(mat)
    return ob, vertices, normals, faces, image
