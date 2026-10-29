"""Create the reference barista appearance on a copy of the shared rest rig.

The eyes, mouth, hands and shoes retain the base geometry. Hair, clothing and
the forehead boundary are authored locally; Unity binds the appearance to
the inherited player bones. Existing artist-edited outputs are rejected.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

parser = argparse.ArgumentParser()
parser.add_argument("--source", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--preview", type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
args.output.mkdir(parents=True, exist_ok=True)
args.preview.mkdir(parents=True, exist_ok=True)
blend_path = args.output / "LittleMung_Barista.blend"
if blend_path.exists():
    raise FileExistsError("Choose a new output directory; keep existing artist edits.")
bpy.ops.wm.open_mainfile(filepath=str(args.source.resolve()), load_ui=False)
if bpy.context.object and bpy.context.object.mode != "OBJECT":
    bpy.ops.object.mode_set(mode="OBJECT")
armature = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
body = next(o for o in bpy.context.scene.objects if o.type == "MESH")
armature.animation_data_clear()
armature.data.pose_position = "POSE"
for bone in armature.pose.bones:
    bone.matrix_basis.identity()
bpy.context.view_layer.update()
points = [body.matrix_world @ v.co for v in body.data.vertices]
bvh = BVHTree.FromPolygons(points, [list(p.vertices) for p in body.data.polygons])
original_bones = {b.name: list(b.head_local) + list(b.tail_local) for b in armature.data.bones}
original_positions = [tuple(v.co) for v in body.data.vertices]

# Keep a separate packed texture. A material on the physical iris polygons
# changes eye color without editing atlas pixels shared by the eyebrows.
material = body.data.materials[0].copy()
material.name = "BaristaBody"
body.data.materials[0] = material
image_node = next(link.from_node for link in material.node_tree.links
                  if link.to_socket.name == "Base Color" and link.from_node.type == "TEX_IMAGE")
source_image = image_node.image
pixels = np.empty(len(source_image.pixels), dtype=np.float32)
source_image.pixels.foreach_get(pixels)
texture = bpy.data.images.new("BaristaBaseColor", width=source_image.size[0], height=source_image.size[1])
texture.colorspace_settings.name = source_image.colorspace_settings.name
texture.pixels.foreach_set(pixels)
texture.filepath_raw = str(args.output / "BaristaBaseColor.png")
texture.file_format = "PNG"
texture.save()
texture.pack()
image_node.image = texture

def make_material(name, color, metallic=0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.node_tree.nodes.clear()
    principled = mat.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
    output = mat.node_tree.nodes.new("ShaderNodeOutputMaterial")
    mat.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    principled.inputs["Base Color"].default_value = (*color, 1)
    principled.inputs["Roughness"].default_value = .48 if "Hair" in name else .8 if not metallic else .35
    principled.inputs["Metallic"].default_value = metallic
    mat.diffuse_color = (*color, 1)
    return mat

outfit = []

def weights_at(point):
    position, normal, poly_index, distance = bvh.find_nearest(Vector(point))
    polygon_vertices = source_polygons[poly_index]
    # Smooth inverse-distance interpolation on the source triangle.
    samples = [(i, 1/max((points[i]-position).length_squared, 1e-10)) for i in polygon_vertices]
    total = sum(w for _, w in samples)
    weights = {}
    for i, factor in samples:
        for name, source_weight in source_weights[i]:
            if name in armature.data.bones:
                weights[name] = weights.get(name, 0) + source_weight * factor / total
    chosen = sorted(weights.items(), key=lambda pair: pair[1], reverse=True)[:4]
    total = sum(weight for _, weight in chosen)
    return [(name, weight / total) for name, weight in chosen]

def finish(obj, mat):
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    for vertex in obj.data.vertices:
        for name, weight in weights_at(obj.matrix_world @ vertex.co):
            group = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
            group.add([vertex.index], weight, "REPLACE")
    modifier = obj.modifiers.new("SharedSkeleton", "ARMATURE")
    modifier.object = armature
    obj.parent = armature
    obj.matrix_parent_inverse = armature.matrix_world.inverted()
    outfit.append(obj)
    return obj

def mesh_object(name, vertices, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat)

def tube(name, path, radius, mat, closed=False):
    vertices, faces = [], []
    count = len(path)
    for i, point in enumerate(path):
        tangent = Vector(path[(i+1)%count]) - Vector(path[i-1] if i else path[-1] if closed else path[0])
        if i == count-1 and not closed:
            tangent = Vector(path[-1])-Vector(path[-2])
        tangent.normalize()
        perpendicular = tangent.cross(Vector((1,0,0)))
        if perpendicular.length < .01:
            perpendicular = tangent.cross(Vector((0,0,1)))
        perpendicular.normalize()
        second = tangent.cross(perpendicular).normalized()
        for side in range(8):
            angle=side*math.tau/8
            vertices.append(Vector(point)+radius*(math.cos(angle)*perpendicular+math.sin(angle)*second))
    for i in range(count if closed else count-1):
        for side in range(8):
            a=i*8+side; b=i*8+(side+1)%8
            c=((i+1)%count)*8+(side+1)%8;d=((i+1)%count)*8+side
            faces.append((a,b,c,d))
    if not closed:
        faces.append(tuple(range(7,-1,-1)))
        faces.append(tuple((count-1)*8+k for k in range(8)))
    return mesh_object(name, vertices, faces, mat)


# Reference design: blond side bun, terracotta collared shirt, cream waist
# apron and sage trousers. The rig remains the parent's exact rest skeleton.
skin_mat = make_material("ForeheadSkin", (.84,.48,.25))
hair_mat = make_material("GoldenHair", (.60,.28,.055))
hair_light = make_material("HairHighlights", (.72,.36,.08))
shirt_mat = make_material("TerracottaShirt", (.47,.125,.065))
pants_mat = make_material("SageTrousers", (.25,.30,.17))
cream_mat = make_material("CreamApron", (.83,.63,.38))
seam_mat = make_material("ApronSeam", (.57,.37,.19))
button_mat = make_material("ShirtButtons", (.36,.085,.04))
# Remove only obsolete hair and garment faces from the appearance copy.
# UVs, all surviving vertices, facial surface and the source rig are preserved.
import bmesh
source_polygons = [list(p.vertices) for p in body.data.polygons]
source_weights = [[(body.vertex_groups[g.group].name,g.weight) for g in v.groups]
                  for v in body.data.vertices]
tex = np.asarray(source_image.pixels[:],dtype=np.float32).reshape(source_image.size[1],source_image.size[0],4)
uv = body.data.uv_layers.active.data
remove=[]
hair_faces=[]
for polygon in body.data.polygons:
    center=sum((points[i] for i in polygon.vertices),Vector())/len(polygon.vertices)
    texcoord=sum((uv[i].uv for i in polygon.loop_indices),Vector((0,0)))/len(polygon.loop_indices)
    color=tex[min(int(texcoord.y*tex.shape[0]),tex.shape[0]-1),min(int(texcoord.x*tex.shape[1]),tex.shape[1]-1),:3]
    face_region = center.x>.13 and abs(center.y)<.32 and .46<center.z<.745
    ear_region = abs(center.y)>.30 and color[0]>.65 and .50<center.z<.72
    old_hair = center.z>.745 or (center.z>.39 and max(color)<.60 and not face_region and not ear_region)
    cool_cloth = .12<center.z<.425 and max(color[1],color[2])>color[0]*1.08
    hidden_torso = .212<center.z<.396 and abs(center.y)<.099
    if old_hair:
        hair_faces.append(polygon.index)
        remove.append(polygon.index)
    arm_remnant = .235<center.z<.31 and abs(center.y)>.105 and not (color[0]>.55 and color[0]>color[1]*1.1)
    if cool_cloth or hidden_torso or arm_remnant:remove.append(polygon.index)
# Rebuild a smooth forehead/side strip from the exact retained face boundary.
body.data.materials.append(skin_mat)
changed_hair=[]
bm=bmesh.new();bm.from_mesh(body.data);bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[bm.faces[i] for i in set(remove)],context="FACES_ONLY")
boundary=[e for e in bm.edges if e.is_boundary and all((body.matrix_world@v.co).z>.42 for v in e.verts)]
remaining=set(boundary);loops=[]
while remaining:
    seed=remaining.pop();edges=[seed];vertices=set(seed.verts)
    while True:
        found={e for e in remaining if any(v in vertices for v in e.verts)}
        if not found:break
        for e in found:vertices.update(e.verts);edges.append(e)
        remaining-=found
    loops.append(edges)
boundary=max(loops,key=len)
for edges in loops:
    if edges is boundary:continue
    filled=bmesh.ops.holes_fill(bm,edges=edges,sides=0)
    for face in filled["faces"]:face.material_index=1;face.smooth=True
border=list({v for e in boundary for v in e.verts})
origin={v:body.matrix_world@v.co for v in border}
target={}
for v,p in origin.items():
    q=p.copy()
    if p.z>.715:
        q.z=.90+.04*min(1,(p.z-.715)/.12);q.y*=.85;q.x=.20
        center=Vector((-.012,0,.728));d=q-center
        q=center+d/max(1,math.sqrt((d.x/.333)**2+(d.y/.397)**2+(d.z/.284)**2))*.90
    elif p.z<.51:
        q.x=-.10;q.y*=.9;q.z=max(.46,p.z)
    else:
        q.x=-.10;q.y=math.copysign(.355,p.y)
    target[v]=q
deform=bm.verts.layers.deform.verify()
head_group=body.vertex_groups["Head"].index
previous={v:v for v in border}
relax=[]
for row in range(1,9):
    t=row/8
    current={}
    for v in border:
        position=origin[v].lerp(target[v],t**.70 if origin[v].z>.715 else t)
        # Round the forehead outwards while keeping both edge rings exact.
        if origin[v].z>.715:position.x+=.004*math.sin(math.pi*t)
        new=bm.verts.new(body.matrix_world.inverted()@position)
        new[deform][head_group]=1
        current[v]=new
        if row<8:relax.append(new)
    for edge in boundary:
        a,b=edge.verts
        face=bm.faces.new((previous[a],previous[b],current[b],current[a]))
        face.material_index=1;face.smooth=True
    previous=current
forehead_border=[v for v in bm.verts if (body.matrix_world@v.co).z>.728 and (body.matrix_world@v.co).x>.15]
changed_hair=[v.index for v in forehead_border if 0<=v.index<len(original_positions)]
relax=list(set(relax+forehead_border))
for _ in range(40):bmesh.ops.smooth_vert(bm,verts=relax,factor=.40,use_axis_x=True,use_axis_y=True,use_axis_z=True)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(body.data);bm.free();body.data.update()
points=[body.matrix_world@v.co for v in body.data.vertices]
body.name="BaristaBody"
bpy.context.view_layer.objects.active=body
if body.data.has_custom_normals:
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
# A separate iris material avoids changing eyebrows that share atlas pixels.
iris_material=material.copy();iris_material.name="IrisGreen"
principled=next(n for n in iris_material.node_tree.nodes if n.type=="BSDF_PRINCIPLED")
linked=next(link.from_socket for link in iris_material.node_tree.links if link.to_socket==principled.inputs["Base Color"])
tint=iris_material.node_tree.nodes.new("ShaderNodeVectorMath");tint.operation="MULTIPLY"
tint.inputs[1].default_value=(.64,1.6,.55)
iris_material.node_tree.links.new(linked,tint.inputs[0])
iris_material.node_tree.links.new(tint.outputs[0],principled.inputs["Base Color"])
body.data.materials.append(iris_material)
uv=body.data.uv_layers.active.data
for polygon in body.data.polygons:
    center=sum((points[i] for i in polygon.vertices),Vector())/len(polygon.vertices)
    if center.x>.15 and center.z>.730:
        polygon.material_index=1
    if not (center.x>.24 and .585<center.z<.684 and .08<abs(center.y)<.205):continue
    coord=sum((uv[i].uv for i in polygon.loop_indices),Vector((0,0)))/len(polygon.loop_indices)
    color=tex[min(int(coord.y*tex.shape[0]),tex.shape[0]-1),min(int(coord.x*tex.shape[1]),tex.shape[1]-1),:3]
    if color[0]<.5 and color[0]>color[1]*1.1:polygon.material_index=2

def head_weights(obj):
    obj.vertex_groups.clear()
    group=obj.vertex_groups.new(name="Head")
    group.add(list(range(len(obj.data.vertices))),1,"REPLACE")
    return obj

def ellipsoid(name,center,radii,mat,head=False):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=20,location=center)
    obj=bpy.context.object;obj.name=name;obj.scale=radii
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    finish(obj,mat)
    if head:head_weights(obj)
    return obj

def surface(name,rows,mat,head=False,closed=False):
    count=len(rows[0]);verts=[p for row in rows for p in row];faces=[]
    for j in range(len(rows)-1):
        for i in range(count if closed else count-1):
            n=(i+1)%count;a=j*count+i
            faces.append((a,j*count+n,(j+1)*count+n,a+count))
    obj=mesh_object(name,verts,faces,mat)
    if head:head_weights(obj)
    return obj

center=Vector((-.012,0,.728));radii=Vector((.333,.397,.284))
rows=[]
for t in np.linspace(.001,1,36):
    row=[]
    for phi in np.linspace(-math.pi,math.pi,97,endpoint=False):
        side=abs(phi)
        boundary=np.interp(side,[0,.45,1.05,1.57,2.25,math.pi],[.86,.84,.72,.56,.47,.47])
        theta=math.acos(max(-1,min(1,(boundary-center.z)/radii.z)))*t
        p=center+Vector((radii.x*math.sin(theta)*math.cos(phi),
                    radii.y*math.sin(theta)*math.sin(phi),radii.z*math.cos(theta)))
        widen=1+.30*max(0,min(1,(.68-p.z)/.22))
        p.x=center.x+(p.x-center.x)*widen;p.y*=widen
        row.append(p)
    rows.append(row)
surface("HairCap",rows,hair_mat,head=True,closed=True)

def smooth_path(control,steps=10):
    control=[Vector(p) for p in control];result=[]
    for i in range(len(control)-1):
        a=control[max(0,i-1)];b=control[i];c=control[i+1];d=control[min(len(control)-1,i+2)]
        for t in np.linspace(0,1,steps,endpoint=False):
            result.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    result.append(control[-1])
    return result

def hair_lock(name,control,width,mat):
    path=smooth_path(control,10)
    path=[center+(p-center)/math.sqrt(sum(((p[i]-center[i])/radii[i])**2 for i in range(3)))*1.015 for p in path]
    for p in path:
        widen=1+.30*max(0,min(1,(.68-p.z)/.22))
        p.x=center.x+(p.x-center.x)*widen;p.y*=widen
    verts=[];faces=[]
    for i,p in enumerate(path):
        tangent=(path[min(i+1,len(path)-1)]-path[max(0,i-1)]).normalized()
        normal=Vector(((p.x-center.x)/.333**2,p.y/.397**2,(p.z-center.z)/.284**2)).normalized()
        sideways=tangent.cross(normal).normalized();normal=sideways.cross(tangent).normalized()
        taper=max(.002,math.sin(math.pi*i/(len(path)-1))**.45)
        for k in range(12):
            a=math.tau*k/12
            verts.append(p+sideways*math.cos(a)*width*taper+normal*math.sin(a)*.018*taper)
    for i in range(len(path)-1):
        for k in range(12):
            a=i*12+k;faces.append((a,i*12+(k+1)%12,(i+1)*12+(k+1)%12,a+12))
    return head_weights(mesh_object(name,verts,faces,mat))

# Swept locks flow outwards from the center part and gather at the low side bun.
for side in [-1,1]:
    for index in range(5):
        shift=index*.018
        hair_lock("SweptLock"+str(side)+"_"+str(index),[
            (.247-shift,side*(.008+index*.020),.902+index*.014),
            (.251-shift*.9,side*(.145+index*.028),.866+index*.010),
            (.187-shift*.7,side*(.295+index*.015),.775+index*.008),
            (.025-shift*.9,side*(.387-index*.006),.625+index*.012),
            (-.180-shift*.4,side*(.286-index*.018),.510+index*.008)
        ],.036 if index else .045,hair_mat if index%2 else hair_light)
# Back combed sections, broad enough to read as hair rather than narrow cords.
for index in range(7):
    y=-.29+index*.095
    z=.728+.284*math.sqrt(max(.01,1-(y/.41)**2))
    hair_lock("BackSweep"+str(index),[
        (-.03,y,z),(-.25,y*.93,.86),(-.335,y*.65,.67),
        (-.29,-.21,.50),(-.235,-.31,.46)
    ],.034,hair_mat if index%2 else hair_light)
# Soft strands in front of the ears.
for side in [-1,1]:
    path=smooth_path([(.22,side*.285,.775),(.19,side*.335,.65),
                     (.16,side*.34,.53),(.135,side*.312,.455),(.155,side*.302,.432)])
    head_weights(tube("FaceFraming"+str(side),path,.0105,hair_light))
# Braided side bun; the interlocking lobes are distinct meshes for editing.
ellipsoid("BunCore",(-.242,-.282,.465),(.108,.12,.103),hair_mat,True)
for i in range(9):
    a=i*math.tau/9
    ellipsoid("BunBraid"+str(i),(-.25+.068*math.cos(a),-.298+.07*math.sin(a),
              .465+.052*math.sin(a+.8)),(.061,.055,.049),hair_light if i%3==0 else hair_mat,True)

# Shirt body, slightly loose around the existing short torso.
shirt_profile=[(.211,.095,.083),(.24,.100,.085),(.28,.095,.081),
               (.32,.086,.075),(.36,.072,.069),(.385,.052,.052),(.403,.030,.030)]
rows=[]
for z,rx,ry in shirt_profile:
    rows.append([(.025+rx*math.cos(a),ry*math.sin(a),z) for a in np.linspace(0,math.tau,64,endpoint=False)])
surface("Shirt",rows,shirt_mat,closed=True)
# Sleeves and rolled cuffs follow the existing upper-arm axis.
for side,prefix in [(1,"Left"),(-1,"Right")]:
    shoulder=armature.data.bones[prefix+"_UpperArm"].head_local
    elbow=armature.data.bones[prefix+"_LowerArm"].head_local
    axis=(elbow-shoulder).normalized()
    along=Vector((1,0,0));across=axis.cross(along).normalized()
    rows=[]
    for t,radius in [(-.12,.039),(0,.041),(.4,.040),(.8,.039),(1.06,.040)]:
        c=shoulder.lerp(elbow,t)
        rows.append([c+radius*(math.cos(a)*along+math.sin(a)*across) for a in np.linspace(0,math.tau,32,endpoint=False)])
    surface("Sleeve"+prefix,rows,shirt_mat,closed=True)
    cuffcenter=shoulder.lerp(elbow,1.03)
    path=[cuffcenter+.041*(math.cos(a)*along+math.sin(a)*across) for a in np.linspace(0,math.tau,48,endpoint=False)]
    tube("RolledCuff"+prefix,path,.005,shirt_mat)
# Original generated coat had no forearm surface underneath its long sleeves.
# Add a skin sleeve down to the existing wrist, weighted to the same arm bones.
for prefix in ["Left","Right"]:
    elbow=armature.data.bones[prefix+"_LowerArm"].head_local
    wrist=armature.data.bones[prefix+"_Hand"].head_local
    axis=(wrist-elbow).normalized();along=Vector((1,0,0));across=axis.cross(along).normalized()
    rows=[]
    for t in np.linspace(-.05,1.07,16):
        t=float(t)
        c=elbow.lerp(wrist,t);radius=.027-.006*max(0,t)
        rows.append([c+radius*(math.cos(a)*along+math.sin(a)*across) for a in np.linspace(0,math.tau,32,endpoint=False)])
    surface("Forearm"+prefix,rows,skin_mat,closed=True)
    ellipsoid("Wrist"+prefix,wrist,(.025,.026,.025),skin_mat)

ellipsoid("NeckSkin",(.018,0,.404),(.031,.033,.044),skin_mat)
# Folded collar and front placket.
for side in [-1,1]:
    vertices=[(.057,side*.009,.402),(.033,side*.031,.405),
              (.086,side*.056,.378),(.103,side*.028,.365)]
    collar=mesh_object("Collar"+str(side),vertices,[(0,1,2,3)],shirt_mat)
    solid=collar.modifiers.new("CollarThickness","SOLIDIFY");solid.thickness=.002
    bpy.context.view_layer.objects.active=collar
    bpy.ops.object.modifier_move_up(modifier=solid.name);bpy.ops.object.modifier_apply(modifier=solid.name)
tube("ButtonPlacket",[(.100,0,.37),(.113,0,.34),(.120,0,.30),(.126,0,.264)],.002,button_mat)
for z,x in [(.352,.110),(.321,.118),(.288,.124)]:
    ellipsoid("ShirtButton",(x,0,z),(.002,.004,.004),button_mat)

# Trousers cover the legs while retaining each leg's source skin weights.
for side,prefix in [(1,"Left"),(-1,"Right")]:
    hip=armature.data.bones[prefix+"_UpperLeg"].head_local
    knee=armature.data.bones[prefix+"_LowerLeg"].head_local
    ankle=armature.data.bones[prefix+"_Foot"].head_local
    rows=[]
    for z in np.linspace(.068,.218,21):
        t=(z-.068)/.15
        y=float(np.interp(z,[.068,.127,.218],[ankle.y,knee.y,hip.y]))
        x=.033
        rx=.056+.005*t;ry=.040+.005*t
        rows.append([(x+rx*math.cos(a),y+ry*math.sin(a),z) for a in np.linspace(0,math.tau,32,endpoint=False)])
    surface("TrouserLeg"+prefix,rows,pants_mat,closed=True)
    path=[(.033+.058*math.cos(a),ankle.y+.042*math.sin(a),.075) for a in np.linspace(0,math.tau,40,endpoint=False)]
    tube("TrouserCuff"+prefix,path,.005,pants_mat)

# Waist apron, open at the back, with a right-side bow and a stitched pocket.
apron_rows=[]
for z in np.linspace(.137,.263,22):
    t=(z-.137)/.126
    rx=.127-.008*t;ry=.113-.017*t
    apron_rows.append([(.021+rx*math.cos(a),ry*math.sin(a),z) for a in np.linspace(-1.60,1.60,49)])
apron=surface("WaistApron",apron_rows,cream_mat)
solid=apron.modifiers.new("ApronThickness","SOLIDIFY");solid.thickness=.002
bpy.context.view_layer.objects.active=apron;bpy.ops.object.modifier_move_up(modifier=solid.name);bpy.ops.object.modifier_apply(modifier=solid.name)
tube("ApronHem",apron_rows[0],.0015,seam_mat)
# Waistband follows the torso, without crossing the arms.
belt_rows=[]
for z in [.251,.269]:
    belt_rows.append([(.025+.103*math.cos(a),.092*math.sin(a),z) for a in np.linspace(0,math.tau,80,endpoint=False)])
surface("Waistband",belt_rows,cream_mat,closed=True)
pocket=[]
for z in np.linspace(.163,.217,9):
    row=[]
    for y in np.linspace(-.073,-.017,15):
        x=.021+.127*math.sqrt(max(0,1-(y/.11)**2))+.004
        row.append((x,y,z))
    pocket.append(row)
surface("ApronPocket",pocket,cream_mat)
edge=[row[0] for row in pocket[::-1]]+pocket[0][1:]+[row[-1] for row in pocket[1:]]
tube("PocketStitch",edge,.001, seam_mat)
# Bow on the character's left, visible on the right of the front view.
bow=Vector((.130,.045,.250))
ellipsoid("BowKnot",bow,(.007,.010,.008),cream_mat)
for side in [-1,1]:
    path=smooth_path([bow,bow+Vector((.004,side*.020,.010)),
        bow+Vector((.001,side*.029,.004)),bow+Vector((.004,side*.022,-.006)),bow])
    tube("BowLoop"+str(side),path,.0055,cream_mat)
    path=smooth_path([bow,bow+Vector((.010,side*.009,-.022)),bow+Vector((.011,side*.017,-.045))])
    tube("BowTail"+str(side),path,.006,cream_mat)


assert all(original_positions[i] == tuple(body.data.vertices[i].co) for i in range(len(original_positions)) if i not in changed_hair)
assert original_bones == {b.name:list(b.head_local)+list(b.tail_local) for b in armature.data.bones}
for obj in outfit:
    for vertex in obj.data.vertices:
        assert abs(sum(g.weight for g in vertex.groups)-1)<1e-5, (obj.name,vertex.index)
bpy.ops.object.select_all(action="DESELECT")
for obj in outfit: obj.select_set(True)
bpy.context.view_layer.objects.active = apron
bpy.ops.object.join()
apron.name = "BaristaOutfit"
outfit = [apron]
bpy.ops.object.select_all(action="DESELECT")
for obj in [armature,body,*outfit]: obj.select_set(True)
bpy.context.view_layer.objects.active=armature
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
bpy.ops.export_scene.fbx(filepath=str(args.output/"BaristaOutfit.fbx"),use_selection=True,
    object_types={"ARMATURE","MESH"},add_leaf_bones=False,bake_anim=False,
    axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_ALL",path_mode="AUTO")
report={"eyes_mouth_and_soles_unchanged":True,"smoothed_forehead_boundary_vertices":len(changed_hair),"removed_obsolete_faces":len(remove),"skeleton_unchanged":True,"bone_count":len(original_bones),
    "outfit":[{"name":o.name,"vertices":len(o.data.vertices),"polygons":len(o.data.polygons)} for o in outfit]}
(args.preview/"model-check.json").write_text(json.dumps(report,indent=2))

def aim(obj,target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()
scene=bpy.context.scene
bpy.ops.object.camera_add(location=(2,0,.65));camera=bpy.context.object
camera.data.type="ORTHO";camera.data.ortho_scale=1.14;scene.camera=camera
scene.render.engine="CYCLES";scene.cycles.samples=48
scene.world=bpy.data.worlds.new("Studio");scene.world.color=(.5,.5,.5)
for location,power,size in [((2,-2,3),220,3),((1,2,1),120,2),((-1,1,2),180,2)]:
    bpy.ops.object.light_add(type="AREA",location=location);light=bpy.context.object
    light.data.energy=power;light.data.shape="DISK";light.data.size=size;aim(light,(0,0,.5))
scene.view_settings.view_transform="AgX"
scene.render.resolution_x=768;scene.render.resolution_y=880;scene.render.resolution_percentage=100
scene.render.image_settings.file_format="PNG";scene.render.film_transparent=True
for name,location in [("front",(2,0,.65)),("left",(1.6,-1.1,.7)),("back",(-2,0,.65))]:
    camera.location=location;aim(camera,(0,0,.5))
    scene.render.filepath=str(args.preview/("barista-"+name+".png"))
    bpy.ops.render.render(write_still=True)
print("Barista copy exported and verified.")

