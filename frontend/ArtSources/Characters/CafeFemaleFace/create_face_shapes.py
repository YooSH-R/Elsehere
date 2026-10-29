"""Create local face shape keys from the exact Unity cafe mesh export.

The Unity mesh/rig stays the authority. Source and result paths are separate;
only face vertex offsets are returned, so skin weights and bind poses survive.
Landmarks are authored for this face, not a general automatic facial rig.
"""
import argparse
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

p = argparse.ArgumentParser()
p.add_argument('--surface', type=Path, required=True)
p.add_argument('--texture', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args(sys.argv[sys.argv.index('--')+1:])
a.output.mkdir(parents=True, exist_ok=True)
if (a.output/'CafeFemaleFacePreview.blend').exists():
    raise FileExistsError('Use a fresh output directory to preserve existing artist edits.')
d = json.loads(a.surface.read_text(encoding='utf-8-sig'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
def cv(v): return Vector((v['x'], -v['z'], v['y']))
def uv(v): return dict(x=float(v.x), y=float(v.z), z=float(-v.y))
vertices = [cv(v) for v in d['vertices']]
normals = [cv(v) for v in d['normals']]
triangles = []
for i in range(0,len(d['triangles']),3):
    t = d['triangles'][i:i+3]
    v0,v1,v2 = [vertices[j] for j in t]
    if (v1-v0).cross(v2-v0).dot(normals[t[0]])<0: t.reverse()
    triangles.append(t)
mesh = bpy.data.meshes.new('CafeFemaleFace')
mesh.from_pydata(vertices,[],triangles)
mesh.update()
body = bpy.data.objects.new('CafeFemaleFacePreview', mesh)
bpy.context.collection.objects.link(body)
bpy.context.view_layer.objects.active = body
layer = mesh.uv_layers.new(name='UVMap')
for poly in mesh.polygons:
    poly.use_smooth=True
    for i in poly.loop_indices:
        t=d['uv'][mesh.loops[i].vertex_index]
        layer.data[i].uv=(t['x'],t['y'])
mat=bpy.data.materials.new('CafeFemaleOriginalColor')
mat.use_nodes=True
node=mat.node_tree.nodes.new('ShaderNodeTexImage')
node.image=bpy.data.images.load(str(a.texture.resolve()))
node.image.pack()
shader=mat.node_tree.nodes.get('Principled BSDF')
shader.inputs['Roughness'].default_value=.8
mat.node_tree.links.new(node.outputs['Color'],shader.inputs['Base Color'])
mesh.materials.append(mat)
mesh.normals_split_custom_set_from_vertices(normals)
body.shape_key_add(name='Basis')
channels = ['mouthSmileLeft', 'mouthSmileRight', 'eyeBlinkLeft', 'eyeBlinkRight',
            'viseme_AA', 'viseme_FF', 'viseme_TH', 'viseme_PP', 'viseme_E',
            'viseme_OH', 'viseme_OU', 'viseme_I', 'viseme_EU']
def fade(r,inner,outer):
    t=max(0,min(1,(r-inner)/(outer-inner)))
    return 1-t*t*(3-2*t)
normal_offsets={}
for channel in channels:
    offsets=[]
    key=body.shape_key_add(name=channel)
    for i,v in enumerate(vertices):
        q=v.copy()
        normal=normals[i].copy()
        if v.x<-.11:
            if channel.startswith('eyeBlink'):
                center=.082 if channel.endswith('Left') else -.155
                r=math.sqrt(((v.y-center)/.058)**2+((v.z-.573)/.048)**2)
                w=fade(r,1.15,2.1)*fade(v.x,-.17,-.11)
                q.z+=(.567-.004*((v.y-center)/.058)**2-v.z)*.997*w
                q.x+=.003*w
                surface_normal=Vector((-1,2.8*(v.y+.043),.8*(v.z-.573))).normalized()
                normal=normals[i].lerp(surface_normal,w).normalized()
            else:
                y=v.y+.043
                r=math.sqrt((y/.044)**2+((v.z-.479)/.024)**2)
                w=fade(r,.45,1.6)*fade(v.z,.502,.515)
                if channel.startswith('mouthSmile'):
                    side=1 if channel.endswith('Left') else -1
                    factor=max(0,min(1,.5+side*y/.025))
                    corner=min(1,abs(y)/.024)
                    q.z+=.007*w*corner*factor
                    q.y+=side*.004*w*corner*factor
                elif channel.startswith('viseme_'):
                    # Small chin motion accompanies the separate authored lip loops.
                    # Their boundary follows these same displaced vertices.
                    radius = math.sqrt((y/.043)**2 + ((v.z-.452)/.017)**2)
                    influence = fade(radius, .4, 1.3)
                    jaw_offsets = {'viseme_AA': .002, 'viseme_OH': .0016, 'viseme_OU': .0012, 'viseme_PP': -.0004}
                    q.z -= jaw_offsets.get(channel, .0006) * influence
        key.data[i].co=q
        offsets.append(uv(normal-normals[i]))
    normal_offsets[channel]=offsets

sys.path.insert(0, str(Path(__file__).parent))
from build_speaking_mouth import build_speaking_mouth
parts, kept_triangles, mouth_surface_x = build_speaking_mouth(body, d, vertices, normals, triangles, node.image, a.output)

# Teeth and tongue use the same head binding, with a distinct placement for
# each viseme. Hidden rest positions prevent teeth leaking through closed lips.
oral_parts = [
    # A thin contact crease makes fully closed lips readable without opening
    # the aperture or revealing teeth. It is hidden for every other viseme.
    ('ClosedLipCrease', (.26, .055, .025), (.0003, .022, .0008), {
        'viseme_PP': ((-.256, -.043, .477), (1, 1, 1)),
    }),
    ('UpperTeeth', (.88, .82, .72), (.002, .019, .0028), {
        'viseme_FF': ((-.256, -.043, .482), (1, 1, 1)),
        'viseme_TH': ((-.256, -.043, .481), (1, 1, 1)),
        'viseme_E': ((-.256, -.043, .4808), (1, 1.1, 1)),
        'viseme_I': ((-.256, -.043, .4808), (1, 1.05, .8)),
    }),
    ('LowerTeeth', (.82, .76, .68), (.002, .018, .0017), {
        'viseme_I': ((-.255, -.043, .474), (1, 1, 1)),
    }),
    ('Tongue', (.48, .12, .14), (.005, .009, .0024), {
        'viseme_AA': ((-.245, -.043, .468), (1, 1.1, 1)),
        'viseme_TH': ((-.258, -.043, .474), (1, 1, .85)),
        'viseme_OH': ((-.248, -.043, .467), (1, .75, .85)),
        'viseme_OU': ((-.252, -.043, .466), (1, .45, .7)),
    }),
]
for name, color, scale, placements in oral_parts:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=10, location=(0, 0, 0))
    ob = bpy.context.object
    ob.name = name
    unit_vertices = [vertex.co.copy() for vertex in ob.data.vertices]
    for vertex, unit in zip(ob.data.vertices, unit_vertices):
        vertex.co = Vector((unit.x*scale[0]-.215, unit.y*scale[1]-.043, unit.z*scale[2]+.48))
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = (*color, 1)
    ob.data.materials.append(material)
    for polygon in ob.data.polygons:
        polygon.use_smooth = True
    ob.shape_key_add(name='Basis')
    for channel, (destination, factors) in placements.items():
        key = ob.shape_key_add(name=channel)
        for vertex, unit in zip(key.data, unit_vertices):
            vertex.co = Vector(destination) + Vector(tuple(unit[index]*scale[index]*factors[index] for index in range(3)))
            # Follow the curved lip surface instead of placing a flat tooth or
            # tongue slab in front of the cheeks. Only TH intentionally protrudes.
            purse = {'viseme_OH': .002, 'viseme_OU': .006}.get(channel, 0)
            if name == 'ClosedLipCrease':
                depth = -.0004
            elif name == 'Tongue':
                depth = -.001 if channel == 'viseme_TH' else .005
            else:
                depth = .0035
            thickness = .003 if name == 'Tongue' else scale[0]
            vertex.co.x = mouth_surface_x(vertex.co.y, vertex.co.z)-purse+depth+unit.x*thickness
    parts.append((ob, color))

# Closed lid contours remain hidden at neutral and follow the blink channel.
for side, center in [('Left',.082),('Right',-.155)]:
    curve=bpy.data.curves.new('Closed'+side+'Lid','CURVE')
    curve.dimensions='3D'
    curve.bevel_depth=.0014
    curve.bevel_resolution=2
    curve.use_fill_caps=True
    line=curve.splines.new('POLY')
    line.points.add(24)
    for i,point in enumerate(line.points):
        y=center-.044+.088*i/24
        z=.567-.004*((y-center)/.058)**2
        point.co=(-.14,y,z,1)
    ob=bpy.data.objects.new('Closed'+side+'Lid',curve)
    bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active=ob
    bpy.ops.object.convert(target='MESH')
    ob=bpy.context.object
    color=(.035,.018,.009)
    mat=bpy.data.materials.new(ob.name)
    mat.use_nodes=True
    mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1)
    ob.data.materials.append(mat)
    ob.shape_key_add(name='Basis')
    key=ob.shape_key_add(name='eyeBlink'+side)
    for v in key.data:
        v.co.x+=-.254+(v.co.y+.043)**2+.14
    parts.append((ob,color))

def shape_data(ob):
    basis=ob.data.shape_keys.key_blocks['Basis']
    result=[]
    for key in ob.data.shape_keys.key_blocks[1:]:
        result.append(dict(name=key.name,deltaVertices=[uv(k.co-b.co) for k,b in zip(key.data,basis.data)],deltaNormals=json.loads(ob.get('normals_'+key.name,'[]'))))
    return result
result=dict(vertices=d['vertices'],triangles=kept_triangles,shapes=shape_data(body),parts=[])
for shape in result['shapes']:shape['deltaNormals']=normal_offsets[shape['name']]
for ob,color in parts:
    ob.data.calc_loop_triangles()
    result['parts'].append(dict(name=ob.name,vertices=[uv(v.co) for v in ob.data.vertices],triangles=[i for t in ob.data.loop_triangles for i in t.vertices],color=dict(r=color[0],g=color[1],b=color[2],a=1),shapes=shape_data(ob),useSourceMaterial=bool(ob.get('use_source_material',False)),uv=json.loads(ob.get('vertex_uv','[]')),textureFile=ob.get('texture_file',''),normals=json.loads(ob.get('base_normals','[]'))))
(a.output/'CafeFemaleFaceShapes.json').write_text(json.dumps(result,separators=(',',':')))
for ob in [body,*[p[0] for p in parts]]:
    for k in ob.data.shape_keys.key_blocks:k.value=0
bpy.ops.wm.save_as_mainfile(filepath=str(a.output/'CafeFemaleFacePreview.blend'))

scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.world.color=(.3,.3,.3);scene.view_settings.view_transform='AgX'
def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for pos,power in [((-2,-2,3),180),((-2,2,1),80)]:
    bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.size=3;aim(o,(0,0,.65))
bpy.ops.object.camera_add(location=(-2,0,.65));cam=bpy.context.object;aim(cam,(0,0,.65));cam.data.type='ORTHO';cam.data.ortho_scale=.78;scene.camera=cam
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
poses = {'Neutral': [], 'ClosedLips': ['viseme_PP'], 'OpenMouth': ['viseme_AA'],
         'E': ['viseme_E'], 'OH': ['viseme_OH'], 'OU': ['viseme_OU'], 'FV': ['viseme_FF'],
         'TH': ['viseme_TH'], 'WideI': ['viseme_I'], 'FlatEu': ['viseme_EU'],
         'Smile': channels[:2], 'Blink': channels[2:4]}
for name,active in poses.items():
    for ob in [body,*[p[0] for p in parts]]:
        for k in ob.data.shape_keys.key_blocks:k.value=1 if k.name in active else 0
    posed_normals=[]
    for i,n in enumerate(normals):
        result_normal=n.copy()
        for channel in active:result_normal+=cv(normal_offsets[channel][i])
        posed_normals.append(result_normal.normalized())
    mesh.normals_split_custom_set_from_vertices(posed_normals)
    scene.render.filepath=str(a.output/(name+'.png'));bpy.ops.render.render(write_still=True)
print('Cafe female ten speech mouths plus smile and blink saved.')
