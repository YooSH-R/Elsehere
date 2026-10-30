"""Replace the copy's painted smile surface with a continuous lip and oral mesh."""
import json
import math
from collections import Counter, defaultdict

import bpy
import numpy as np
from mathutils import Vector, geometry
from mathutils.bvhtree import BVHTree

CENTER_Y = 0.0
CENTER_Z = .501


def unity_vector(value):
    return dict(x=float(value.x), y=float(value.z), z=float(-value.y))


def build_speaking_mouth(body, source, vertices, normals, triangles, texture, output):
    removed = set()
    edge_count = Counter()
    source_index = {}

    def position_key(index):
        return tuple(round(component, 5) for component in vertices[index])

    for index, triangle in enumerate(triangles):
        center = sum((vertices[i] for i in triangle), Vector()) / 3
        radius = ((center.y-CENTER_Y)/.052)**2 + ((center.z-CENTER_Z)/.028)**2
        if center.x >= -.15 or radius >= 1 or center.z >= .526:
            continue
        removed.add(index)
        for first, second in zip(triangle, triangle[1:]+triangle[:1]):
            a, b = position_key(first), position_key(second)
            edge_count[tuple(sorted((a, b)))] += 1
            source_index[a], source_index[b] = first, second

    neighbors = defaultdict(list)
    for (first, second), count in edge_count.items():
        if count == 1:
            neighbors[first].append(second)
            neighbors[second].append(first)
    if not neighbors or any(len(items) != 2 for items in neighbors.values()):
        raise ValueError('The mouth cut must have one closed boundary.')
    start = next(iter(neighbors))
    boundary = []
    previous, current = None, start
    while True:
        boundary.append(source_index[current])
        following = next(item for item in neighbors[current] if item != previous)
        previous, current = current, following
        if current == start:
            break
    if len(boundary) != len(neighbors):
        raise ValueError('The mouth cut contains multiple boundary loops.')
    signed_area = sum(
        vertices[a].y*vertices[b].z-vertices[b].y*vertices[a].z
        for a, b in zip(boundary, boundary[1:]+boundary[:1])
    )
    if signed_area < 0:
        boundary.reverse()

    # Subdivision on the existing edge keeps the new surface joined to the face.
    outer_points, outer_normals = [], []
    posed_outer = {key.name: [] for key in body.data.shape_keys.key_blocks}
    for first, second in zip(boundary, boundary[1:]+boundary[:1]):
        for step in range(4):
            fraction = step/4
            outer_points.append(vertices[first].lerp(vertices[second], fraction))
            for key in body.data.shape_keys.key_blocks:
                posed_outer[key.name].append(key.data[first].co.lerp(key.data[second].co, fraction))
            outer_normals.append(normals[first].lerp(normals[second], fraction).normalized())
    count = len(outer_points)
    distances = [0.0]
    for first, second in zip(outer_points, outer_points[1:]+outer_points[:1]):
        distances.append(distances[-1]+(second-first).length)
    first_angle = math.atan2(outer_points[0].z-CENTER_Z, outer_points[0].y-CENTER_Y)
    angles = [first_angle+2*math.pi*distance/distances[-1] for distance in distances[:-1]]

    fit = np.array([[1, v.y-CENTER_Y, v.z-CENTER_Z, (v.y-CENTER_Y)**2,
                     (v.y-CENTER_Y)*(v.z-CENTER_Z), (v.z-CENTER_Z)**2]
                    for v in outer_points])
    coefficients = np.linalg.lstsq(fit, np.array([v.x for v in outer_points]), rcond=None)[0]

    def fitted_x(y, z):
        local_y, local_z = y-CENTER_Y, z-CENTER_Z
        return float(np.dot(coefficients, [1, local_y, local_z, local_y**2, local_y*local_z, local_z**2]))

    # Bake the local skin color into one continuous UV chart, removing the painted
    # crescent only inside the lip region. The source texture itself is untouched.
    surface = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
    pixels = list(texture.pixels)
    width, height = texture.size
    uv_min_y, uv_max_y = -.067, .073
    uv_min_z, uv_max_z = .464, .539

    def source_color(y, z):
        point, _, face_index, _ = surface.ray_cast(Vector((-1, y, z)), Vector((1, 0, 0)))
        if point is None:
            return (1.0, .80, .64)
        ids = triangles[face_index]
        uv = geometry.barycentric_transform(point, *[vertices[i] for i in ids],
                *[Vector((source['uv'][i]['x'], source['uv'][i]['y'], 0)) for i in ids])
        px = min(width-1, max(0, int(uv.x*width)))
        py = min(height-1, max(0, int(uv.y*height)))
        return pixels[(py*width+px)*4:(py*width+px)*4+3]

    texture_size = 256
    baked_pixels = []
    for row in range(texture_size):
        z = uv_min_z+(uv_max_z-uv_min_z)*row/(texture_size-1)
        for column in range(texture_size):
            y = uv_min_y+(uv_max_y-uv_min_y)*column/(texture_size-1)
            color = source_color(y, z)
            radius = math.sqrt(((y-CENTER_Y)/.054)**2+((z-CENTER_Z)/.029)**2)
            if radius < 1:
                # Bilateral cheek samples retain the local skin tone without
                # dragging dark mouth pixels into the new lip surface.
                left, right = source_color(CENTER_Y-.043, z), source_color(CENTER_Y+.043, z)
                fraction = max(0, min(1, (y-CENTER_Y+.043)/.086))
                clean = [left[i]*(1-fraction)+right[i]*fraction for i in range(3)]
                blend = min(1, max(0, (1-radius)/.2))
                color = [color[i]*(1-blend)+clean[i]*blend for i in range(3)]
            baked_pixels.extend((*color, 1))
    image = bpy.data.images.new('MannequinMouthColor', width=texture_size, height=texture_size)
    image.colorspace_settings.name = texture.colorspace_settings.name
    image.pixels = baked_pixels
    image.filepath_raw = str(output/'MannequinMouthColor.png')
    image.file_format = 'PNG'
    image.save()
    image.pack()
    skin_material = bpy.data.materials.new('SpeakingLips')
    skin_material.use_nodes = True
    color_node = skin_material.node_tree.nodes.new('ShaderNodeTexImage')
    color_node.image = image
    skin_shader = skin_material.node_tree.nodes.get('Principled BSDF')
    skin_shader.inputs['Roughness'].default_value = .8
    skin_material.node_tree.links.new(color_node.outputs['Color'], skin_shader.inputs['Base Color'])

    apertures = {
        'Basis': (.022, .0018),
        'viseme_PP': (.022, .00025),
        'viseme_AA': (.024, .012),
        'viseme_E': (.026, .0055),
        'viseme_OH': (.012, .014),
        'viseme_OU': (.008, .014),
        'viseme_FF': (.021, .003),
        'viseme_TH': (.022, .007),
        'viseme_I': (.024, .005),
        'viseme_EU': (.023, .0035),
        'mouthSmileLeft': (.022, .0018),
        'mouthSmileRight': (.022, .0018),
    }
    parts = []
    for part_name, rings in [('SpeakingLips', [1, .8, .55, .3, .1, 0]),
                             ('MouthInterior', [1, .8, .5, .2, 0])]:
        targets, target_normals = {}, {}
        for channel, (half_width, half_height) in apertures.items():
            points, shading = [], []
            for ring in rings:
                for index, angle in enumerate(angles):
                    cosine, sine = math.cos(angle), math.sin(angle)
                    exponent = .75 if channel in ('viseme_AA', 'viseme_E', 'viseme_I', 'viseme_EU', 'viseme_PP') else 1
                    inner_y = CENTER_Y+half_width*math.copysign(abs(cosine)**exponent, cosine)
                    inner_z = CENTER_Z+half_height*math.copysign(abs(sine)**exponent, sine)
                    if channel == 'viseme_FF':
                        inner_z += .005*(1-cosine*cosine)
                    smiling = channel in ('Basis', 'mouthSmileLeft', 'mouthSmileRight')
                    if smiling:
                        inner_z += .004*(math.cos(angle)**2)
                    if channel.startswith('mouthSmile'):
                        side = 1 if channel.endswith('Left') else -1
                        inner_z += .003*max(0, side*math.cos(angle))
                    if part_name == 'SpeakingLips':
                        outer = posed_outer[channel][index]
                        y = inner_y*(1-ring)+outer.y*ring
                        z = inner_z*(1-ring)+outer.z*ring
                        purse = {'viseme_OH': .002, 'viseme_OU': .006}.get(channel, 0)
                        x = fitted_x(y, z)+(outer.x-fitted_x(outer.y, outer.z))*ring-purse*(1-ring)**2
                        local_y, local_z = y-CENTER_Y, z-CENTER_Z
                        slope_y = coefficients[1]+2*coefficients[3]*local_y+coefficients[4]*local_z
                        slope_z = coefficients[2]+coefficients[4]*local_y+2*coefficients[5]*local_z
                        surface_normal = Vector((-1, slope_y+purse*40*cosine*(1-ring), slope_z+purse*40*sine*(1-ring))).normalized()
                        normal = surface_normal.lerp(outer_normals[index], ring).normalized()
                    else:
                        y = CENTER_Y+(inner_y-CENTER_Y)*ring
                        z = CENTER_Z+(inner_z-CENTER_Z)*ring
                        purse = {'viseme_OH': .002, 'viseme_OU': .006}.get(channel, 0)
                        x = fitted_x(y, z)+.012*(1-ring)-purse*ring
                        normal = Vector((-1, -.3*math.cos(angle)*(1-ring), -.3*math.sin(angle)*(1-ring))).normalized()
                    points.append(Vector((x, y, z)))
                    shading.append(normal)
            targets[channel], target_normals[channel] = points, shading
        faces = []
        for ring_index in range(len(rings)-1):
            for index in range(count):
                next_index = (index+1)%count
                first, second = ring_index*count, (ring_index+1)*count
                faces.append((first+index, second+index, second+next_index, first+next_index))
        mesh = bpy.data.meshes.new(part_name)
        mesh.from_pydata(targets['Basis'], [], faces)
        mesh.update()
        item = bpy.data.objects.new(part_name, mesh)
        bpy.context.collection.objects.link(item)
        for polygon in mesh.polygons:
            polygon.use_smooth = True
        mesh.normals_split_custom_set_from_vertices(target_normals['Basis'])
        item['base_normals'] = json.dumps([unity_vector(n) for n in target_normals['Basis']])
        if part_name == 'SpeakingLips':
            mesh.materials.append(skin_material)
            layer = mesh.uv_layers.new(name='UVMap')
            vertex_uv = [dict(x=(v.y-uv_min_y)/(uv_max_y-uv_min_y),
                              y=(v.z-uv_min_z)/(uv_max_z-uv_min_z)) for v in targets['Basis']]
            for loop in mesh.loops:
                uv = vertex_uv[loop.vertex_index]
                layer.data[loop.index].uv = (uv['x'], uv['y'])
            item['vertex_uv'] = json.dumps(vertex_uv)
            item['texture_file'] = 'MannequinMouthColor.png'
            color = (1, 1, 1)
        else:
            # Warm oral tissue; near-black reads as a painted ring around the teeth.
            color = (.30, .10, .075)
            material = bpy.data.materials.new(part_name)
            material.use_nodes = True
            material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = (*color, 1)
            material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value = 1
            material.node_tree.nodes.get('Principled BSDF').inputs['Specular IOR Level'].default_value = 0
            mesh.materials.append(material)
        item.shape_key_add(name='Basis')
        for channel, points in targets.items():
            if channel == 'Basis':
                continue
            key = item.shape_key_add(name=channel)
            for vertex, target in zip(key.data, points):
                vertex.co = target
            item['normals_'+channel] = json.dumps([
                unity_vector(target-base) for target, base in zip(target_normals[channel], target_normals['Basis'])])
        parts.append((item, color))

    # Keep vertex order, UVs, weights and rig metadata. Only these copy triangles
    # are replaced; the author's original prefab and mesh are not changed.
    for polygon in body.data.polygons:
        polygon.use_smooth = True
    import bmesh
    editable = bmesh.new()
    editable.from_mesh(body.data)
    editable.faces.ensure_lookup_table()
    bmesh.ops.delete(editable, geom=[editable.faces[i] for i in sorted(removed)], context='FACES_ONLY')
    editable.to_mesh(body.data)
    editable.free()
    body.data.normals_split_custom_set_from_vertices(normals)
    kept = [value for index in range(len(triangles)) if index not in removed
            for value in source['triangles'][index*3:index*3+3]]
    return parts, kept, fitted_x
