import bpy, json, math
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).parent
PROJECT=Path(__file__).resolve().parents[5]
TEXTURE=PROJECT/'frontend/UnityClient/Assets/Art/Characters/Tripo/NPC/LittleMung_Business_Male/Textures/Material_0_color.png'
def vec(v): return Vector((v['x'],-v['z'],v['y']))
def aim(ob,target): ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
def setup():
    scene=bpy.context.scene
    scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
    scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('ReviewWorld');scene.world.use_nodes=True
    scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.55,.55,.55,1)
    scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.6
    scene.view_settings.view_transform='Standard'
    for name,loc,power in [('Key',(-3,-3,4),180),('Fill',(-3,3,2),90)]:
        bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object
        light.name=name;light.data.energy=power;light.data.size=4;aim(light,(0,0,.5))
    bpy.ops.object.camera_add();camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO'
    return scene,camera
def make_body(data):
    vertices=[vec(v) for v in data['vertices']];normals=[vec(n) for n in data['normals']]
    faces=[data['triangles'][i:i+3] for i in range(0,len(data['triangles']),3)]
    for f in faces:
        if (vertices[f[1]]-vertices[f[0]]).cross(vertices[f[2]]-vertices[f[0]]).dot(normals[f[0]])<0: f.reverse()
    mesh=bpy.data.meshes.new('BusinessSurface');mesh.from_pydata(vertices,[],faces);mesh.update()
    ob=bpy.data.objects.new('LittleMung_Business_Male',mesh);bpy.context.collection.objects.link(ob)
    for poly in mesh.polygons:poly.use_smooth=True
    mesh.normals_split_custom_set_from_vertices(normals)
    layer=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:
        uv=data['uv'][loop.vertex_index];layer.data[loop.index].uv=(uv['x'],uv['y'])
    image=bpy.data.images.load(str(TEXTURE));image.pack()
    mat=bpy.data.materials.new('BusinessColor');mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.8
    node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
    mat.node_tree.links.new(node.outputs['Color'],shader.inputs['Base Color']);mesh.materials.append(mat)
    return ob,vertices,normals,faces,image
if __name__=='__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    data=json.loads((OUT/'MasterSurface.json').read_text(encoding='utf-8-sig'))
    ob,vertices,normals,faces,image=make_body(data)
    pix=list(image.pixels);w,h=image.size
    dark=[]
    for i,v in enumerate(vertices):
        uv=data['uv'][i];j=(min(h-1,int(uv['y']*h))*w+min(w-1,int(uv['x']*w)))*4
        c=pix[j:j+3]
        if v.x<-.12 and abs(v.y)<.055 and .43<v.z<.58 and max(c)<.4:dark.append([i,*v,*c])
    (OUT/'MouthSamples.json').write_text(json.dumps(dark))
    print('MOUTH_DARK',dark)
    scene,camera=setup()
    for label,angle in [('Front',0),('LeftOblique',35),('RightOblique',-35)]:
        a=math.radians(angle);camera.location=(-3*math.cos(a),3*math.sin(a),.65);aim(camera,(0,0,.65));camera.data.ortho_scale=.72
        scene.render.filepath=str(OUT/('Business_Master_'+label+'.png'));bpy.ops.render.render(write_still=True)
