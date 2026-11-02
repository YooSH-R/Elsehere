"""Offline authoring on an exported master-bound COPY. Never edits the source FBX."""
import sys, json, math
from pathlib import Path
import bpy, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
OUT=Path(__file__).parent
sys.path.insert(0,str(OUT))
from inspect_master_face import make_body, setup, aim, PROJECT, vec
AUTHOR=PROJECT/'frontend/ArtSources/Characters/LittleMungMannequin'
sys.path.insert(0,str(AUTHOR))
from build_business_mouth import build_speaking_mouth
DEST=OUT/'Retouched';DEST.mkdir(exist_ok=True)
OFFSET=None
def unity(v):return dict(x=float(v.x),y=float(v.z),z=float(-v.y))
def shape_data(ob):
    keys=ob.data.shape_keys.key_blocks;basis=keys[0]
    return [dict(name=k.name,deltaVertices=[unity(v.co-b.co) for v,b in zip(k.data,basis.data)],
        deltaNormals=json.loads(ob.get('normals_'+k.name,'[]'))) for k in keys[1:]]
bpy.ops.wm.read_factory_settings(use_empty=True)
data=json.loads((OUT/'MasterSurface.json').read_text(encoding='utf-8-sig'))
body,world_vertices,world_normals,triangles,texture=make_body(data)
# Preserve the original eyes and face. Locate the mouth using the same UV landmarks.
corners=[world_vertices[i] for i in (1558,7789)]
OFFSET=Vector((0,sum(v.y for v in corners)/2,sum(v.z for v in corners)/2-.006-.501))
smoothed=[]
print('MOUTH_OFFSET',tuple(OFFSET))
# Use the Base mouth's authored dimensions in a translated local facial frame.
vertices=[v-OFFSET for v in world_vertices];normals=[n.copy() for n in world_normals]
for v,p in zip(body.data.vertices,vertices):v.co=p
channels=['mouthSmileLeft','mouthSmileRight','viseme_AA','viseme_FF','viseme_TH','viseme_PP','viseme_E','viseme_OH','viseme_OU','viseme_I','viseme_EU']
body.shape_key_add(name='Basis')
for channel in channels:body.shape_key_add(name=channel)
parts,kept,mouth_x=build_speaking_mouth(body,data,vertices,normals,triangles,texture,DEST)
# Reuse the actual Base tooth/tongue/closed-lip geometry and authored targets.
with bpy.data.libraries.load(str(AUTHOR/'LittleMung_Base_Male_Face.blend'),link=False) as (available,loaded):
    loaded.objects=['SpeakingLips','ClosedLipCrease','UpperTeeth','LowerTeeth','Tongue']
donor=loaded.objects[0]
count=len(donor.data.vertices)//6
outer=[v.co for v in donor.data.vertices[:count]]
def terms(v):return [1,v.y,v.z-.501,v.y*v.y,v.y*(v.z-.501),(v.z-.501)**2]
coeff=np.linalg.lstsq(np.array([terms(v) for v in outer]),np.array([v.x for v in outer]),rcond=None)[0]
active={'ClosedLipCrease':{'viseme_PP'},'UpperTeeth':{'viseme_FF','viseme_TH','viseme_E','viseme_I'},'LowerTeeth':{'viseme_I'},'Tongue':{'viseme_AA','viseme_TH','viseme_OH','viseme_OU'}}
for ob in loaded.objects[1:]:
    bpy.context.collection.objects.link(ob)
    for key in ob.data.shape_keys.key_blocks:
        for vertex in key.data:
            v=vertex.co;v.x+=mouth_x(v.y,v.z)-float(np.dot(coeff,terms(v)))
            if key.name not in active[ob.name.split('.')[0]]:v.x+=.025
            if ob.name.startswith('ClosedLipCrease'):v.y*=.0175/.022
    color=ob.data.materials[0].node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value[:3]
    parts.append((ob,color))
bpy.data.objects.remove(donor,do_unlink=True)
# Translate the authored patch back to the unchanged master surface coordinates.
for ob in [body]+[p[0] for p in parts]:
    for key in ob.data.shape_keys.key_blocks:
        for vertex in key.data:vertex.co+=OFFSET
    for key in ob.data.shape_keys.key_blocks[1:]:key.value=0
    ob.data.update_tag()
bpy.context.view_layer.update()
result=dict(vertices=data['vertices'],restVertices=[unity(v) for v in world_vertices],restNormals=[unity(v) for v in world_normals],triangles=kept,
    shapes=[],parts=[],textureFile='')
for ob,color in parts:
    ob.data.calc_loop_triangles()
    result['parts'].append(dict(name=ob.name.split('.')[0],vertices=[unity(v.co) for v in ob.data.shape_keys.key_blocks[0].data],
        triangles=[i for t in ob.data.loop_triangles for i in t.vertices],color=dict(r=color[0],g=color[1],b=color[2],a=1),
        shapes=shape_data(ob),uv=json.loads(ob.get('vertex_uv','[]')),textureFile=ob.get('texture_file',''),
        normals=json.loads(ob.get('base_normals','[]'))))
(DEST/'BusinessFaceShapes.json').write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
scene,camera=setup()
for label,angle in [('Front',0)]:
    a=math.radians(angle);camera.location=(-3*math.cos(a),3*math.sin(a),.65);aim(camera,(0,0,.65));camera.data.ortho_scale=.72
    scene.render.filepath=str(DEST/('Business_Retouched_'+label+'.png'));bpy.ops.render.render(write_still=True)
camera.location=(-3,0,.501+OFFSET.z);aim(camera,(0,0,.501+OFFSET.z));camera.data.ortho_scale=.20
for channel in ['Basis','viseme_PP','viseme_AA']:
    for ob,color in parts:
        for key in ob.data.shape_keys.key_blocks[1:]:key.value=1 if key.name==channel else 0
    scene.render.filepath=str(DEST/('Mouth_'+channel+'.png'));bpy.ops.render.render(write_still=True)
for ob,color in parts:
    for key in ob.data.shape_keys.key_blocks[1:]:key.value=0
bpy.ops.wm.save_as_mainfile(filepath=str(DEST/'LittleMung_Business_Male_Face.blend'))
print('BUSINESS_MOUTH_COMPLETE',len(data['triangles'])//3-len(kept)//3,'replaced triangles',len(parts),'mouth parts')
print('EYE_SURFACE_SMOOTHED',len(smoothed))
