"""Author face-only deltas on the exported master-bound mannequin copy.

Landmarks belong to LittleMung_Base_Female, not arbitrary generated faces.
Body vertices, UVs, weights and skeleton are preserved; the mouth aperture
gets continuous lip loops and an oral cavity. Source assets stay untouched.
"""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
import numpy as np
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--surface', type=Path, required=True)
parser.add_argument('--texture', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
args.output = args.output.resolve()
args.output.mkdir(parents=True, exist_ok=True)
data = json.loads(args.surface.read_text(encoding='utf-8-sig'))
bpy.ops.wm.read_factory_settings(use_empty=True)

def to_blender(value):
    return Vector((value['x'], -value['z'], value['y']))

def to_unity(value):
    return dict(x=float(value.x), y=float(value.z), z=float(-value.y))

def fade(value, inner, outer):
    t = max(0, min(1, (value - inner) / (outer - inner)))
    return 1 - t * t * (3 - 2 * t)

vertices = [to_blender(v) for v in data['vertices']]
normals = [to_blender(v) for v in data['normals']]
triangles = [data['triangles'][i:i+3] for i in range(0,len(data['triangles']),3)]
for ids in triangles:
    if (vertices[ids[1]]-vertices[ids[0]]).cross(vertices[ids[2]]-vertices[ids[0]]).dot(normals[ids[0]]) < 0:
        ids.reverse()
mesh = bpy.data.meshes.new('LittleMungMannequinFace')
mesh.from_pydata(vertices, [], triangles)
mesh.update()
body = bpy.data.objects.new('LittleMung_Base_Female', mesh)
bpy.context.collection.objects.link(body)
bpy.context.view_layer.objects.active = body
layer = mesh.uv_layers.new(name='UVMap')
for poly in mesh.polygons:
    poly.use_smooth = True
    for li in poly.loop_indices:
        value = data['uv'][mesh.loops[li].vertex_index]
        layer.data[li].uv = (value['x'], value['y'])
mesh.normals_split_custom_set_from_vertices(normals)

# Bake only the two eye interiors into a texture copy. Their geometry, UV seams,
# eyelids and lash silhouettes remain exactly the imported ones.
source_image = bpy.data.images.load(str(args.texture.resolve()))
width, height = source_image.size
pixels = np.array(source_image.pixels[:], dtype=np.float32).reshape(height, width, 4)
original_pixels = pixels.copy()
for ids in triangles:
    xyz = np.array([vertices[i] for i in ids])
    if xyz[:,0].min() > -.15 or xyz[:,2].max() < .548 or xyz[:,2].min() > .641:
        continue
    if np.abs(xyz[:,1]).min() > .184 or np.abs(xyz[:,1]).max() < .076:
        continue
    uv = np.array([[data['uv'][i]['x'] * (width-1), data['uv'][i]['y'] * (height-1)] for i in ids])
    low = np.maximum(np.floor(uv.min(axis=0)).astype(int), 0)
    high = np.minimum(np.ceil(uv.max(axis=0)).astype(int), [width-1,height-1])
    if np.any(high < low):
        continue
    xx, yy = np.meshgrid(np.arange(low[0], high[0]+1), np.arange(low[1], high[1]+1))
    denominator = (uv[1,1]-uv[2,1])*(uv[0,0]-uv[2,0])+(uv[2,0]-uv[1,0])*(uv[0,1]-uv[2,1])
    if abs(denominator) < 1e-8:
        continue
    a = ((uv[1,1]-uv[2,1])*(xx-uv[2,0])+(uv[2,0]-uv[1,0])*(yy-uv[2,1]))/denominator
    b = ((uv[2,1]-uv[0,1])*(xx-uv[2,0])+(uv[0,0]-uv[2,0])*(yy-uv[2,1]))/denominator
    c = 1-a-b
    points = a[...,None]*xyz[0]+b[...,None]*xyz[1]+c[...,None]*xyz[2]
    ey = points[...,1] - np.where(points[...,1]>0,.130,-.130)
    ez = points[...,2] - .595
    radius = np.sqrt((ey/.0445)**2 + (ez/.0395)**2)
    mask = (a>=-.002)&(b>=-.002)&(c>=-.002)&(radius<.99)&(points[...,0]<-.15)
    if not mask.any():
        continue
    color = np.zeros((*xx.shape,3),dtype=np.float32)
    color[:] = (.88,.865,.82)
    iris_radius = np.sqrt((ey/.024)**2+(ez/.030)**2)
    iris = iris_radius < 1
    radial = np.clip(iris_radius,0,1)
    color[iris] = np.stack((.042+.025*(1-radial), .018+.015*(1-radial), .008+.006*(1-radial)),axis=-1)[iris]
    pupil = (ey/.013)**2+(ez/.021)**2 < 1
    color[pupil] = (.003,.002,.001)
    glint = ((ey-.006)/.0045)**2+((ez-.013)/.0045)**2 < 1
    color[glint] = (.97,.97,.95)
    patch = pixels[low[1]:high[1]+1,low[0]:high[0]+1]
    patch[mask,:3] = color[mask]
image = bpy.data.images.new('MannequinColor',width=width,height=height)
image.colorspace_settings.name=source_image.colorspace_settings.name
image.pixels.foreach_set(pixels.reshape(-1))
image.filepath_raw=str(args.output/'MannequinColor.png');image.file_format='PNG';image.save();image.pack()
material=bpy.data.materials.new('MannequinColor');material.use_nodes=True
tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
shader=material.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.8
material.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color']);mesh.materials.append(material)

channels=['mouthSmileLeft','mouthSmileRight','eyeBlinkLeft','eyeBlinkRight','viseme_AA','viseme_FF','viseme_TH','viseme_PP','viseme_E','viseme_OH','viseme_OU','viseme_I','viseme_EU']
lid_surfaces={}
for center in [.130,-.130]:
    samples=[v for v in vertices if v.x<-.12 and 1.35<math.sqrt(((v.y-center)/.045)**2+((v.z-.595)/.041)**2)<1.85]
    fit=np.array([[1,v.y-center,v.z-.595,(v.y-center)**2,(v.z-.595)**2] for v in samples])
    lid_surfaces[center]=np.linalg.lstsq(fit,np.array([v.x for v in samples]),rcond=None)[0]
def lid_x(center,y,z):
    return float(np.dot(lid_surfaces[center],[1,y-center,z-.595,(y-center)**2,(z-.595)**2]))
body.shape_key_add(name='Basis')
normal_deltas={}
for channel in channels:
    key=body.shape_key_add(name=channel)
    deltas=[]
    for i,v in enumerate(vertices):
        q=v.copy();normal=normals[i].copy()
        if v.x < -.12:
            if channel.startswith('eyeBlink'):
                center=.130 if channel.endswith('Left') else -.130
                radius=math.sqrt(((v.y-center)/.045)**2+((v.z-.595)/.041)**2)
                weight=fade(radius,1.05,1.8)*fade(v.x,-.17,-.12)
                q.z+=(.591-.003*((v.y-center)/.045)**2-v.z)*.997*weight
                q.x+=(lid_x(center,q.y,q.z)-v.x)*weight
                normal=normal.lerp(Vector((-1,2.8*v.y,.8*(v.z-.595))).normalized(),weight).normalized()
            else:
                radius=math.sqrt((v.y/.044)**2+((v.z-.501)/.025)**2)
                weight=fade(radius,.45,1.6)*fade(v.z,.526,.54)
                if channel.startswith('mouthSmile'):
                    side=1 if channel.endswith('Left') else -1
                    factor=max(0,min(1,.5+side*v.y/.025))
                    corner=min(1,abs(v.y)/.024)
                    q.z+=.006*weight*corner*factor
                    q.y+=side*.003*weight*corner*factor
                else:
                    radius=math.sqrt((v.y/.043)**2+((v.z-.476)/.017)**2)
                    amount={'viseme_AA':.002,'viseme_OH':.0016,'viseme_OU':.0012,'viseme_PP':-.0004}.get(channel,.0006)
                    q.z-=amount*fade(radius,.4,1.3)
        key.data[i].co=q
        deltas.append(to_unity(normal-normals[i]))
    normal_deltas[channel]=deltas

sys.path.insert(0,str(Path(__file__).parent))
from build_mannequin_mouth import build_speaking_mouth
parts,kept,mouth_x=build_speaking_mouth(body,data,vertices,normals,triangles,image,args.output)
oral_parts=[
 ('ClosedLipCrease',(.26,.055,.025),(.0003,.022,.0008),{'viseme_PP':((0,0,.501),(1,1,1))}),
 ('UpperTeeth',(.88,.82,.72),(.002,.019,.0028),{'viseme_FF':((0,0,.506),(1,1,1)),'viseme_TH':((0,0,.505),(1,1,1)),'viseme_E':((0,0,.5048),(1,1.1,1)),'viseme_I':((0,0,.5048),(1,1.05,.8))}),
 ('LowerTeeth',(.82,.76,.68),(.002,.018,.0017),{'viseme_I':((0,0,.498),(1,1,1))}),
 ('Tongue',(.48,.12,.14),(.005,.009,.0024),{'viseme_AA':((0,0,.492),(1,1.1,1)),'viseme_TH':((0,0,.498),(1,1,.85)),'viseme_OH':((0,0,.491),(1,.75,.85)),'viseme_OU':((0,0,.490),(1,.45,.7))})]
for name,color,scale,placements in oral_parts:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=10)
    ob=bpy.context.object;ob.name=name;unit=[v.co.copy() for v in ob.data.vertices]
    hidden=Vector((mouth_x(0,.501)+.025,0,.501))
    for v,u in zip(ob.data.vertices,unit):v.co=hidden+Vector(tuple(u[i]*scale[i] for i in range(3)))
    mat=bpy.data.materials.new(name);mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1);ob.data.materials.append(mat)
    for poly in ob.data.polygons:poly.use_smooth=True
    ob.shape_key_add(name='Basis')
    for channel,(destination,factors) in placements.items():
        key=ob.shape_key_add(name=channel)
        for v,u in zip(key.data,unit):
            v.co=Vector(destination)+Vector(tuple(u[i]*scale[i]*factors[i] for i in range(3)))
            depth=-.0004 if name=='ClosedLipCrease' else (-.001 if name=='Tongue' and channel=='viseme_TH' else (.005 if name=='Tongue' else .0035))
            purse={'viseme_OH':.002,'viseme_OU':.006}.get(channel,0)
            thickness=.003 if name=='Tongue' else scale[0]
            v.co.x=mouth_x(v.co.y,v.co.z)-purse+depth+u.x*thickness
    parts.append((ob,color))

for side,center in [('Left',.130),('Right',-.130)]:
    curve=bpy.data.curves.new('Closed'+side+'Lid','CURVE');curve.dimensions='3D';curve.bevel_depth=.0015;curve.bevel_resolution=2;curve.use_fill_caps=True
    line=curve.splines.new('POLY');line.points.add(32)
    for i,p in enumerate(line.points):
        y=center-.043+.086*i/32;z=.591-.003*((y-center)/.045)**2;p.co=(-.14,y,z,1)
    ob=bpy.data.objects.new(curve.name,curve);bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.convert(target='MESH');ob=bpy.context.object
    color=(.035,.018,.009);mat=bpy.data.materials.new(ob.name);mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1);ob.data.materials.append(mat)
    ob.shape_key_add(name='Basis');key=ob.shape_key_add(name='eyeBlink'+side)
    for v in key.data:v.co.x=lid_x(center,v.co.y,v.co.z)-.0007
    parts.append((ob,color))

def shape_data(ob):
    base=ob.data.shape_keys.key_blocks[0]
    return [dict(name=k.name,deltaVertices=[to_unity(v.co-b.co) for v,b in zip(k.data,base.data)],deltaNormals=json.loads(ob.get('normals_'+k.name,'[]'))) for k in ob.data.shape_keys.key_blocks[1:]]

result=dict(vertices=data['vertices'],triangles=kept,shapes=shape_data(body),parts=[],textureFile='MannequinColor.png')
for shape in result['shapes']:shape['deltaNormals']=normal_deltas[shape['name']]
for ob,color in parts:
    ob.data.calc_loop_triangles()
    result['parts'].append(dict(name=ob.name,vertices=[to_unity(v.co) for v in ob.data.vertices],triangles=[i for t in ob.data.loop_triangles for i in t.vertices],color=dict(r=color[0],g=color[1],b=color[2],a=1),shapes=shape_data(ob),uv=json.loads(ob.get('vertex_uv','[]')),textureFile=ob.get('texture_file',''),normals=json.loads(ob.get('base_normals','[]'))))
(args.output/'MannequinFaceShapes.json').write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(args.output/'LittleMung_Base_Female_Face.blend'))
print('FACE_AUTHORING_COMPLETE',len(vertices),len(parts),int(np.any(pixels!=original_pixels,axis=2).sum()))
