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
from mathutils.bvhtree import BVHTree

parser = argparse.ArgumentParser()
parser.add_argument('--surface', type=Path, required=True)
parser.add_argument('--texture', type=Path, required=True)
parser.add_argument('--reference', type=Path, required=True)
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
lid_surfaces={}
for center in [.130,-.130]:
    samples=[v for v in vertices if v.x<-.12 and 1.35<math.sqrt(((v.y-center)/.045)**2+((v.z-.595)/.041)**2)<1.85]
    fit=np.array([[1,v.y-center,v.z-.595,(v.y-center)**2,(v.z-.595)**2] for v in samples])
    lid_surfaces[center]=np.linalg.lstsq(fit,np.array([v.x for v in samples]),rcond=None)[0]
def lid_x(center,y,z):
    return float(np.dot(lid_surfaces[center],[1,y-center,z-.595,(y-center)**2,(z-.595)**2]))
eye_surface=BVHTree.FromPolygons(vertices,[data['triangles'][i:i+3] for i in range(0,len(data['triangles']),3)],all_triangles=True)
for i,v in enumerate(vertices):
    center=.130 if v.y>0 else -.130
    radius=math.sqrt(((v.y-center)/.0445)**2+((v.z-.595)/.0395)**2)
    if v.x<-.15 and radius<1:
        hit,_,_,_=eye_surface.ray_cast(Vector((-1,v.y,v.z)),Vector((1,0,0)))
        if hit is None or v.x>hit.x+.001:continue
        weight=fade(radius,.3,.65)
        def cap(y,z):
            squared=((y-center)/.0445)**2+((z-.595)/.0395)**2
            return lid_x(center,y,z)-.014*math.sqrt(max(.001,1-squared))
        # Keep the eye in front of the underlying face. Pulling both layers onto
        # the same surface causes overlapping triangles around the lower lid.
        v.x+=(min(v.x,cap(v.y,v.z))-v.x)*weight
        step=.0001
        smooth_normal=Vector((-1,(cap(v.y+step,v.z)-cap(v.y-step,v.z))/(2*step),(cap(v.y,v.z+step)-cap(v.y,v.z-step))/(2*step))).normalized()
        normals[i]=normals[i].lerp(smooth_normal,weight).normalized()
triangles = [data['triangles'][i:i+3] for i in range(0,len(data['triangles']),3)]
for ids in triangles:
    if (vertices[ids[1]]-vertices[ids[0]]).cross(vertices[ids[2]]-vertices[ids[0]]).dot(normals[ids[0]]) < 0:
        ids.reverse()
mesh = bpy.data.meshes.new('LittleMungMannequinFace')
mesh.from_pydata(vertices, [], triangles)
mesh.update()
body = bpy.data.objects.new('LittleMung_Base_Male', mesh)
bpy.context.collection.objects.link(body)
bpy.context.view_layer.objects.active = body
layer = mesh.uv_layers.new(name='UVMap')
for poly in mesh.polygons:
    poly.use_smooth = True
    for li in poly.loop_indices:
        value = data['uv'][mesh.loops[li].vertex_index]
        layer.data[li].uv = (value['x'], value['y'])
mesh.normals_split_custom_set_from_vertices(normals)

# Bake only the two eye interiors into a texture copy. UV seams and open-eye
# lash silhouettes stay imported; the iris surface above is smoothed locally.
source_image = bpy.data.images.load(str(args.texture.resolve()))
width, height = source_image.size
pixels = np.array(source_image.pixels[:], dtype=np.float32).reshape(height, width, 4)
original_pixels = pixels.copy()
reference = bpy.data.images.load(str(args.reference.resolve()))
reference_pixels = np.array(reference.pixels[:], dtype=np.float32).reshape(reference.size[1],reference.size[0],4)
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
    # Include the lower rim so the original dark iris cannot survive as small
    # triangular fragments just outside the reference eye footprint.
    rim = np.where(ez < 0, 1.06, .99)
    mask = (a>=-.002)&(b>=-.002)&(c>=-.002)&(radius<rim)&(points[...,0]<-.15)
    if not mask.any():
        continue
    # Project the approved eye reference, retaining its iris shape, brown color,
    # gaze and highlight instead of inventing a new centered black oval.
    source_x = np.where(points[...,1]>0,466.0,775.0) - ey/.0445*51.5
    source_y = reference.size[1]-1-(461.0-ez/.0395*47.0)
    x0=np.clip(source_x.astype(int),0,reference.size[0]-2)
    y0=np.clip(source_y.astype(int),0,reference.size[1]-2)
    fx=np.clip(source_x-x0,0,1)[...,None]
    fy=np.clip(source_y-y0,0,1)[...,None]
    color=((reference_pixels[y0,x0,:3]*(1-fx)+reference_pixels[y0,x0+1,:3]*fx)*(1-fy)
        +(reference_pixels[y0+1,x0,:3]*(1-fx)+reference_pixels[y0+1,x0+1,:3]*fx)*fy)
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
                # Blink belongs entirely to the separate eyelids and lashes.
                # Preserve the eyebrows and the original face surface.
                pass
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
    # Teeth and tongue rest inside the actual mouth, behind closed lips.
    # Moving them from far behind the oral cavity made weak speech weights
    # show a black opening even when the lip/phoneme mapping was correct.
    rest_z = {'UpperTeeth':.506, 'LowerTeeth':.4965, 'Tongue':.490}.get(name,.501)
    rest_depth = .025 if name=='ClosedLipCrease' else (.005 if name=='Tongue' else .0035)
    for v,u in zip(ob.data.vertices,unit):
        v.co=Vector((0,0,rest_z))+Vector(tuple(u[i]*scale[i] for i in range(3)))
        v.co.x=mouth_x(v.co.y,v.co.z)+rest_depth+u.x*scale[0]
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
    if name in ('UpperTeeth', 'LowerTeeth'):
        # Match the barista's per-phoneme tooth visibility while retaining the
        # near-mouth rest positions needed for a soft I. Rounded vowels must
        # not inherit both rows of teeth from that rest pose.
        speech_channels = ('viseme_AA', 'viseme_FF', 'viseme_TH', 'viseme_PP',
                           'viseme_E', 'viseme_OH', 'viseme_OU', 'viseme_I', 'viseme_EU')
        for channel in speech_channels:
            if channel in placements:
                continue
            key = ob.shape_key_add(name=channel)
            for vertex, rest in zip(key.data, ob.data.shape_keys.key_blocks['Basis'].data):
                vertex.co = rest.co + Vector((.045, 0, 0))
    parts.append((ob,color))

from build_mannequin_eyes import build_eyes
eye_parts,kept=build_eyes(body,data,vertices,normals,triangles,kept,source_image,reference,args.output)
parts.extend(eye_parts)

def shape_data(ob):
    base=ob.data.shape_keys.key_blocks[0]
    if 'blink_frames' in ob:
        authored=json.loads(ob['blink_frames'])
        final=authored['frames'][-1]
        return [dict(name=authored['name'],deltaVertices=final['deltaVertices'],deltaNormals=final['deltaNormals'],frames=authored['frames'])]
    return [dict(name=k.name,deltaVertices=[to_unity(v.co-b.co) for v,b in zip(k.data,base.data)],deltaNormals=json.loads(ob.get('normals_'+k.name,'[]'))) for k in ob.data.shape_keys.key_blocks[1:]]

result=dict(vertices=data['vertices'],restVertices=[to_unity(v) for v in vertices],restNormals=[to_unity(n) for n in normals],triangles=kept,shapes=shape_data(body),parts=[],textureFile='MannequinColor.png')
for shape in result['shapes']:shape['deltaNormals']=normal_deltas[shape['name']]
for ob,color in parts:
    ob.data.calc_loop_triangles()
    result['parts'].append(dict(name=ob.name,vertices=[to_unity(v.co) for v in ob.data.vertices],triangles=[i for t in ob.data.loop_triangles for i in t.vertices],color=dict(r=color[0],g=color[1],b=color[2],a=1),shapes=shape_data(ob),uv=json.loads(ob.get('vertex_uv','[]')),textureFile=ob.get('texture_file',''),normals=json.loads(ob.get('base_normals','[]'))))
(args.output/'MannequinFaceShapes.json').write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(args.output/'LittleMung_Base_Male_Face.blend'))
print('FACE_AUTHORING_COMPLETE',len(vertices),len(parts),int(np.any(pixels!=original_pixels,axis=2).sum()))
