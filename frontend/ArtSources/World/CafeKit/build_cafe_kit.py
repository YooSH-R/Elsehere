"""Editable cafe parts and FBX exports. Coordinates use Unity X/Y-up/+Z-front.

Blender --background --factory-startup --python this_file -- --output PATH
Existing artist files are protected unless --replace-generated is explicit.
"""

import argparse
import json
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector


PALETTE = {
    "Brick": "C97557", "Trim": "F4E3C4", "Fabric": "23675E",
    "FabricEdge": "398579", "Wood": "9E6C43", "DarkWood": "594634",
    "Metal": "253A43", "Glass": "66A5B7", "Reflection": "ABD8DC",
    "WarmGlass": "D4A05B", "Glow": "FFE3A0", "Foliage": "639B35",
    "Flower": "FFF0CF", "FlowerGold": "EBC456", "Soil": "5D5136",
    "Paver": "DDCCB1",
}
MATERIALS = {}
MODULES = []
ACTIVE = None
RANDOM = random.Random(249)


def position(point):
    return Vector((point[0], -point[2], point[1]))


def linear(channel):
    return channel / 12.92 if channel < 0.04045 else ((channel + 0.055) / 1.055) ** 2.4


def setup_materials():
    for name, value in PALETTE.items():
        rgb = tuple(linear(int(value[index:index + 2], 16) / 255) for index in (0, 2, 4))
        material = bpy.data.materials.new(name)
        material.diffuse_color = (*rgb, 1)
        material.use_nodes = True
        shader = material.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = (*rgb, 1)
        shader.inputs["Roughness"].default_value = 0.72
        if name == "Glow":
            shader.inputs["Emission Color"].default_value = (*rgb, 1)
            shader.inputs["Emission Strength"].default_value = 1.5
        MATERIALS[name] = material


def module(name):
    global ACTIVE
    ACTIVE = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(ACTIVE)
    MODULES.append(ACTIVE)


def register(obj, name, material):
    obj.name = name
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    ACTIVE.objects.link(obj)
    obj.data.materials.clear()
    obj.data.materials.append(MATERIALS[material])
    return obj


def bevel(obj, radius, segments=2):
    modifier = obj.modifiers.new("Soft manufactured edges", "BEVEL")
    modifier.width = radius
    modifier.segments = segments
    modifier.affect = "EDGES"
    modifier = obj.modifiers.new("Weighted surface normals", "WEIGHTED_NORMAL")
    modifier.keep_sharp = True


def box(name, center, size, material, radius=0.035):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position(center))
    obj = register(bpy.context.object, name, material)
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    if radius:
        bevel(obj, min(radius, min(size) * 0.45))
    return obj


def ellipsoid(name, center, size, material, segments=16, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=0.5,
                                       location=position(center))
    obj = register(bpy.context.object, name, material)
    obj.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def tube(name, points, radius, material, resolution=4):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = resolution
    curve.bevel_depth = radius
    curve.bevel_resolution = 1
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, coordinate in zip(spline.bezier_points, points):
        point.co = position(coordinate)
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    ACTIVE.objects.link(obj)
    obj.data.materials.append(MATERIALS[material])
    return obj


def mesh(name, vertices, faces, material, smooth=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    ACTIVE.objects.link(obj)
    data.materials.append(MATERIALS[material])
    for polygon in data.polygons:
        polygon.use_smooth = smooth
    return obj


def lathe(name, center, profile, material, sides=20):
    vertices = []
    for height, radius in profile:
        for index in range(sides):
            angle = index * math.tau / sides
            vertices.append(position((center[0] + math.cos(angle) * radius,
                                      center[1] + height,
                                      center[2] + math.sin(angle) * radius)))
    faces = []
    for row in range(len(profile) - 1):
        for index in range(sides):
            a = row * sides + index
            b = row * sides + (index + 1) % sides
            faces.append((a, a + sides, b + sides, b))
    faces.extend([tuple(range(sides)),
                  tuple((len(profile) - 1) * sides + index for index in reversed(range(sides)))])
    return mesh(name, vertices, faces, material, smooth=True)


def flower(center, radius=0.1):
    for index in range(5):
        angle = index * math.tau / 5
        petal = ellipsoid("DaisyPetal", (center[0] + math.cos(angle) * radius * 0.57,
                                          center[1] + 0.006,
                                          center[2] + math.sin(angle) * radius * 0.57),
                          (radius * 0.95, radius * 0.42, radius * 0.6), "Flower", 6, 3)
        petal.rotation_euler.z = -angle
    ellipsoid("DaisyCentre", (center[0], center[1] + 0.035, center[2]),
              (radius * 0.62, radius * 0.4, radius * 0.62), "FlowerGold", 6, 3)


def shrub(name, center, width, depth, height):
    # Voxel union removes intersections between the initial canopy lobes.
    lobes = []
    for x, y, z, size in [(-0.27, 0, 0, .85), (.23, .05, .05, .9),
                          (0, .22, -.1, .95), (.03, .02, .28, .75)]:
        lobes.append(ellipsoid(name, (center[0] + x * width, center[1] + y * height,
                                     center[2] + z * depth),
                               (width * size, height * size, depth * size), "Foliage"))
    bpy.ops.object.select_all(action="DESELECT")
    for obj in lobes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = lobes[0]
    bpy.ops.object.join()
    crown = bpy.context.object
    modifier = crown.modifiers.new("Unified organic silhouette", "REMESH")
    modifier.mode = "VOXEL"
    modifier.voxel_size = max(width / 19, 0.055)
    modifier.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    modifier = crown.modifiers.new("Relax lobe transitions", "SMOOTH")
    modifier.factor = 0.8
    modifier.iterations = 4
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    modifier = crown.modifiers.new("Game mesh density", "DECIMATE")
    modifier.ratio = 0.35
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    for polygon in crown.data.polygons:
        polygon.use_smooth = True
    return crown


def build_walls():
    for name, height in [("WallGround2m", 4), ("WallUpper2m", 3)]:
        module(name)
        box("BrickWall", (0, height / 2, -.11), (2, height, .22), "Brick", .015)
    module("Entrance4m")
    for side in (-1, 1):
        box("SideWall", (side * 1.5, 2, -.11), (1, 4, .22), "Brick", .015)
        box("Jamb", (side * 1.08, 1.1, .07), (.16, 2.2, .28), "Trim", .035)
        box("WoodReveal", (side * 1.035, 1.1, -.055), (.07, 2.2, .1), "DarkWood", .012)
    box("LintelWall", (0, 3.1, -.11), (2, 1.8, .22), "Brick", .015)
    box("Lintel", (0, 2.29, .07), (2.32, .18, .3), "Trim", .045)
    module("Cornice2m")
    box("LowerFillet", (0, .035, .05), (2, .07, .24), "Trim", .018)
    box("Moulding", (0, .14, .1), (2, .14, .32), "Trim", .055)
    box("UpperLip", (0, .255, .14), (2, .09, .4), "Trim", .025)
    module("RoofEdge2m")
    box("Parapet", (0, .15, -.03), (2, .3, .3), "Brick", .02)
    box("TopCap", (0, .335, .015), (2, .13, .48), "Trim", .04)


def build_windows():
    module("WindowUpper2m")
    box("Recess", (0, 1.5, .018), (1.42, 2.13, .06), "DarkWood", .09)
    box("BluePane", (0, 1.55, .065), (1.2, 1.87, .035), "Glass", .05)
    box("WarmLowerPane", (0, .91, .09), (1.15, .58, .025), "WarmGlass", .04)
    for side in (-1, 1):
        box("RoundJamb", (side * .71, 1.5, .13), (.16, 2.2, .24), "Trim", .055)
        for fold in range(3):
            box("CurtainFold", (side * (.49 - fold * .035), 1.56, .11),
                (.07, 1.73, .065), "Trim", .028)
    box("Header", (0, 2.59, .13), (1.67, .23, .34), "Trim", .06)
    box("Sill", (0, .4, .2), (1.72, .16, .48), "Trim", .045)
    box("LampStem", (.06, .8, .17), (.035, .42, .03), "DarkWood", .012)
    lathe("LampShade", (.06, .96, .19), [(0, .19), (.035, .2), (.3, .105)], "Glow", 16)
    box("WindowGlint", (-.34, 1.9, .103), (.07, .8, .012), "Reflection", .02)
    module("WindowShop2m")
    box("Recess", (0, 1.48, .03), (1.8, 2.67, .08), "DarkWood", .075)
    box("WarmInterior", (0, 1.48, .077), (1.57, 2.44, .02), "WarmGlass", .04)
    box("TopReflection", (0, 2.41, .094), (1.55, .55, .013), "Glass", .025)
    for x in (-.86, .86):
        box("TimberFrame", (x, 1.48, .16), (.12, 2.7, .22), "DarkWood", .04)
    for y in (.16, 2.8):
        box("TimberRail", (0, y, .16), (1.84, .13, .24), "DarkWood", .04)
    box("Transom", (0, 2.1, .16), (1.65, .075, .13), "DarkWood", .025)
    box("Mullion", (.32, 1.16, .16), (.06, 1.86, .12), "DarkWood", .02)
    box("DisplayShelf", (0, .7, .23), (1.65, .09, .35), "Wood", .03)
    tube("PendantCable", [(-.3, 2.08, .17), (-.3, 1.72, .17)], .013, "Metal")
    lathe("PendantShade", (-.3, 1.45, .19), [(0, .21), (.06, .23), (.28, .08)], "Glow")
    for index in range(4):
        lathe("CoffeeCanister", (-.56 + index * .31, .75, .25),
              [(0, .09), (.2 + index % 2 * .05, .09), (.23 + index % 2 * .05, .085)],
              "Trim" if index % 2 else "Fabric", 12)


def build_awning():
    module("Awning2m")
    vertices, faces = [], []
    nx, nz = 16, 6
    for z_index in range(nz + 1):
        t = z_index / nz
        for x_index in range(nx + 1):
            x = x_index / nx * 2 - 1
            bulge = .025 * math.cos(x * math.pi * 4) * math.sin(t * math.pi)
            y = .88 - .65 * t - .1 * math.sin(t * math.pi) + bulge
            vertices.append(position((x, y, .04 + t * 1.4)))
    for row in range(nz):
        for column in range(nx):
            a = row * (nx + 1) + column
            faces.append((a, a + nx + 1, a + nx + 2, a + 1))
    cloth = mesh("SoftCloth", vertices, faces, "Fabric", True)
    cloth.modifiers.new("Fabric thickness", "SOLIDIFY").thickness = .025
    for panel in range(4):
        left = -1 + panel * .5
        outline = [(left, .24, 1.44), (left + .5, .24, 1.44)]
        for index in range(13):
            angle = index * math.pi / 12
            outline.append((left + .25 + math.cos(angle) * .25,
                            .1 - math.sin(angle) * .1, 1.44))
        hem = mesh("ScallopedHem", [position(point) for point in outline],
                   [tuple(reversed(range(len(outline))))], "Fabric")
        hem.modifiers.new("Hem thickness", "SOLIDIFY").thickness = .035
        bevel(hem, .012, 2)
        tube("HemPiping", outline[2:], .014, "FabricEdge", resolution=1)
    for x in (-.99, .99):
        tube("EdgePiping", [(x, .88 - .65 * t - .1 * math.sin(t * math.pi), .04 + 1.4 * t)
                            for t in (0, .25, .5, .75, 1)], .014, "FabricEdge")


def build_planter(name, width, height, depth, flower_count):
    module(name)
    box("PlanterBody", (0, height / 2, 0), (width, height, depth), "Wood", .065)
    box("DarkSoil", (0, height + .005, 0), (width - .12, .035, depth - .1), "Soil", .02)
    for side in (-1, 1):
        box("RimFrontBack", (0, height - .015, side * depth / 2),
            (width + .1, .1, .09), "Wood", .03)
        box("RimEnd", (side * width / 2, height - .015, 0),
            (.09, .1, depth), "Wood", .03)
    shrub("RoundedPlant", (0, height + .1, 0), width * .72, depth * .85, .43)
    for index in range(flower_count):
        x = (index / max(1, flower_count - 1) - .5) * (width - .2)
        z = RANDOM.uniform(-depth * .25, depth * .25)
        flower((x, height + .26 + RANDOM.uniform(-.02, .07), z), .085)


def build_tree():
    module("GardenTree4m")
    lathe("TaperedTrunk", (0, 0, 0), [(0, .2), (.12, .19), (1.5, .13), (2.7, .07)], "Wood", 12)
    tube("BranchLeft", [(0, 1.2, 0), (-.35, 1.9, .02), (-.78, 2.35, .04)], .09, "Wood")
    tube("BranchRight", [(0, 1.7, 0), (.4, 2.3, -.05), (.76, 2.65, -.1)], .08, "Wood")
    shrub("UnifiedCrown", (0, 3.0, 0), 2.45, 1.9, 2.15)


def build_furniture():
    module("CafeTable")
    lathe("TableTop", (0, .57, 0), [(0, .4), (.025, .43), (.055, .42)], "Wood", 24)
    lathe("Pedestal", (0, 0, 0), [(0, .23), (.055, .23), (.075, .06), (.58, .04)], "Metal", 16)
    module("CafeChair")
    box("Seat", (0, .28, 0), (.38, .065, .38), "Wood", .045)
    for side in (-1, 1):
        tube("ChairSide", [(side * .15, .02, .14), (side * .15, .3, .11),
                           (side * .15, .32, -.13), (side * .15, .02, -.16)], .022, "Metal")
        tube("BackUpright", [(side * .15, .29, -.13), (side * .15, .63, -.18)], .025, "Metal")
    for y in (.47, .58):
        box("BackSlat", (0, y, -.17), (.37, .08, .045), "Wood", .02)
    module("WallLamp")
    box("WallPlate", (0, 0, .02), (.14, .26, .08), "Metal", .04)
    tube("Gooseneck", [(0, .03, .06), (0, .28, .18), (0, .27, .42), (0, .12, .48)], .028, "Metal")
    lathe("BellShade", (0, -.03, .48), [(0, .2), (.03, .22), (.13, .1), (.16, .055)], "Metal")
    ellipsoid("LampBulb", (0, -.055, .48), (.18, .14, .18), "Glow")
    module("Paving2m")
    for x in (-.5, .5):
        for z in (-.5, .5):
            box("SandstonePaver", (x, -.035, z), (.986, .07, .986), "Paver", .018)


def export_module(collection, directory):
    bpy.ops.object.select_all(action="DESELECT")
    exports = []
    graph = bpy.context.evaluated_depsgraph_get()
    for source in collection.objects:
        data = bpy.data.meshes.new_from_object(source.evaluated_get(graph), depsgraph=graph)
        obj = bpy.data.objects.new(source.name + "_Export", data)
        bpy.context.scene.collection.objects.link(obj)
        obj.matrix_world = source.matrix_world.copy()
        obj.matrix_world.translation -= Vector(collection.get("unity_export_offset", (0, 0, 0)))
        exports.append(obj)
    # Source keeps named editable pieces; FBX meshes are grouped by material.
    merged = []
    material_groups = {
        name: [obj for obj in exports if obj.data.materials and obj.data.materials[0].name == name]
        for name in PALETTE
    }
    for material_name, objects in material_groups.items():
        if not objects:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = collection.name + "_" + material_name
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.02)
        bpy.ops.object.mode_set(mode="OBJECT")
        merged.append(obj)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in merged:
        obj.select_set(True)
    triangles = 0
    for obj in merged:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    bpy.context.view_layer.objects.active = merged[0]
    bpy.ops.export_scene.fbx(filepath=str(directory / (collection.name + ".fbx")),
                             use_selection=True, object_types={"MESH"}, add_leaf_bones=False,
                             bake_anim=False, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                             mesh_smooth_type="OFF", use_triangles=True)
    bounds = [obj.matrix_world @ vertex.co for obj in merged for vertex in obj.data.vertices]
    minimum = [min(point[axis] for point in bounds) for axis in range(3)]
    maximum = [max(point[axis] for point in bounds) for axis in range(3)]
    report = {"name": collection.name, "triangles": triangles, "material_slots": len(merged),
              "width_m": round(maximum[0] - minimum[0], 4),
              "height_m": round(maximum[2] - minimum[2], 4),
              "depth_m": round(maximum[1] - minimum[1], 4)}
    for obj in merged:
        bpy.data.objects.remove(obj, do_unlink=True)
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--replace-generated", action="store_true")
    parser.add_argument("--export-edited", action="store_true",
                        help="Export saved Blender edits without rebuilding the source.")
    parser.add_argument("--source", type=Path, default=Path(__file__).with_name("CafeKit.blend"))
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    args.output.mkdir(parents=True, exist_ok=True)
    source_path = args.source
    if not args.replace_generated and (any(args.output.glob("*.fbx")) or source_path.exists()):
        raise RuntimeError("Files exist. Use a new version or explicitly --replace-generated.")
    if args.export_edited:
        bpy.ops.wm.open_mainfile(filepath=str(source_path))
        collections = [collection for collection in bpy.data.collections
                       if "unity_export_offset" in collection]
        if not collections:
            raise RuntimeError("No exportable CafeKit collections in this source.")
        reports = [export_module(collection, args.output) for collection in collections]
        write_manifest(args.output, reports)
        print("ELSEHERE_EDITED_EXPORT", json.dumps(reports))
        return
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    setup_materials()
    build_walls()
    build_windows()
    build_awning()
    build_planter("FlowerPlanter1m", 1.15, .55, .64, 9)
    build_planter("WindowBox1p6m", 1.6, .27, .4, 11)
    build_tree()
    build_furniture()
    reports = [export_module(collection, args.output) for collection in MODULES]
    write_manifest(args.output, reports)
    # Source is an editable parts tray; exported pivots remain at the module origin.
    for index, collection in enumerate(MODULES):
        offset = Vector(((index % 5) * 5, -(index // 5) * 7, 0))
        collection["unity_export_offset"] = list(offset)
        for obj in collection.objects:
            obj.location += offset
    bpy.context.scene.world.color = (.25, .25, .25)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source_path))
    print("ELSEHERE_MODULES", json.dumps(reports))


def write_manifest(directory, reports):
    palette = [{"name": name, "hex": value} for name, value in PALETTE.items()]
    (directory / "manifest.json").write_text(json.dumps({"palette": palette, "modules": reports},
                                                       indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
