"""Build disposable Blender inputs for the existing Unity integration tests."""

import sys
from pathlib import Path

import bpy


def create_humanoid():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.9), scale=(0.3, 0.2, 1.6))
    body = bpy.context.object
    body.name = "FixtureBody"
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    body.shape_key_add(name="Basis")
    for name in (
        "mouthSmileLeft",
        "eyeBlinkLeft",
        "viseme_AA",
        "viseme_FF",
        "viseme_TH",
    ):
        shape = body.shape_key_add(name=name)
        shape.data[0].co.x += 0.06

    bones = [
        ("Hips", None, (0, 0, 0.9), (0, 0, 1.05)),
        ("Spine", "Hips", (0, 0, 1.05), (0, 0, 1.25)),
        ("Chest", "Spine", (0, 0, 1.25), (0, 0, 1.45)),
        ("Neck", "Chest", (0, 0, 1.45), (0, 0, 1.58)),
        ("Head", "Neck", (0, 0, 1.58), (0, 0, 1.8)),
    ]
    for side, sign in (("Left", 1), ("Right", -1)):
        bones.extend(
            [
                (side + "Shoulder", "Chest", (0, 0, 1.45), (sign * 0.15, 0, 1.45)),
                (
                    side + "UpperArm",
                    side + "Shoulder",
                    (sign * 0.15, 0, 1.45),
                    (sign * 0.45, 0, 1.45),
                ),
                (
                    side + "LowerArm",
                    side + "UpperArm",
                    (sign * 0.45, 0, 1.45),
                    (sign * 0.7, 0, 1.45),
                ),
                (
                    side + "Hand",
                    side + "LowerArm",
                    (sign * 0.7, 0, 1.45),
                    (sign * 0.8, 0, 1.45),
                ),
                (side + "UpperLeg", "Hips", (sign * 0.1, 0, 0.9), (sign * 0.1, 0, 0.5)),
                (
                    side + "LowerLeg",
                    side + "UpperLeg",
                    (sign * 0.1, 0, 0.5),
                    (sign * 0.1, 0, 0.1),
                ),
                (
                    side + "Foot",
                    side + "LowerLeg",
                    (sign * 0.1, 0, 0.1),
                    (sign * 0.1, -0.18, 0.05),
                ),
            ]
        )
    armature = bpy.data.armatures.new("FixtureSkeleton")
    rig = bpy.data.objects.new("Armature", armature)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    body.select_set(False)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent, head, tail in bones:
        bone = armature.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        if parent:
            bone.parent = armature.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    weights = body.vertex_groups.new(name="Hips")
    weights.add(list(range(len(body.data.vertices))), 1.0, "REPLACE")
    modifier = body.modifiers.new("Armature", "ARMATURE")
    modifier.object = rig
    body.parent = rig
    return body


def add_material_regression(body, directory):
    # Keep PNG bytes under a JPEG name and reuse them as a normal input: this is
    # the malformed source the importer regression test is meant to repair.
    image = bpy.data.images.new("MisnamedColor", width=2, height=2)
    image.generated_color = (0.8, 0.3, 0.1, 1)
    image.filepath_raw = str(directory / "Color.jpg")
    image.file_format = "PNG"
    image.save()
    image.pack()
    material = bpy.data.materials.new("FixtureMaterial")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    shader = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    color = nodes.new("ShaderNodeTexImage")
    color.image = image
    normal = nodes.new("ShaderNodeNormalMap")
    links = material.node_tree.links
    links.new(color.outputs["Color"], shader.inputs["Base Color"])
    links.new(color.outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], shader.inputs["Normal"])
    body.data.materials.append(material)


def export_model(path):
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=False,
        # Match the workbench's meter/centimeter representation so an unedited
        # round trip does not introduce a synthetic 100x armature scale change.
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        path_mode="COPY",
        embed_textures=True,
    )


def create_fixtures(directory):
    directory = Path(directory).resolve()
    directory.mkdir(parents=True, exist_ok=True)
    body = create_humanoid()
    export_model(directory / "Humanoid.fbx")
    add_material_regression(body, directory)
    export_model(directory / "MisnamedColor.fbx")


if __name__ == "__main__":
    create_fixtures(Path(sys.argv[sys.argv.index("--") + 1]))
