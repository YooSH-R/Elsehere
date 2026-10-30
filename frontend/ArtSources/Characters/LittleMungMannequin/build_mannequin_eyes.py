"""Stationary eyes and sliding eyelids for the reviewed mannequin copy only."""
import json
import math
from collections import Counter, defaultdict

import bpy
import bmesh
import numpy as np
from mathutils import Vector, geometry
from mathutils.bvhtree import BVHTree

RX, RZ, CZ = .0445, .0395, .595


def unity(v):
    return dict(x=float(v.x), y=float(v.z), z=float(-v.y))


def make_part(name, poses, faces, material, uv=None, normal_targets=None):
    base = poses[0]
    # All eye surfaces face camera -X. Retain this winding across blink frames.
    faces = [tuple(reversed(f)) if (base[f[1]]-base[f[0]]).cross(base[f[2]]-base[f[0]]).x > 0 else f for f in faces]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(base, [], faces)
    mesh.update()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(ob)
    mesh.materials.append(material)
    for poly in mesh.polygons:
        poly.use_smooth = True
    if uv:
        layer = mesh.uv_layers.new(name='UVMap')
        for loop in mesh.loops:
            layer.data[loop.index].uv = uv[loop.vertex_index]
        ob['vertex_uv'] = json.dumps([dict(x=u, y=v) for u, v in uv])
    ob.shape_key_add(name='Basis')
    normals = []
    for position, points in poses.items():
        probe = bpy.data.meshes.new('Shading')
        probe.from_pydata(points, [], faces)
        probe.update()
        values = normal_targets[position] if normal_targets else [v.normal.copy() for v in probe.vertices]
        normals.append(values)
        bpy.data.meshes.remove(probe)
    ob['base_normals'] = json.dumps([unity(v) for v in normals[0]])
    mesh.normals_split_custom_set_from_vertices(normals[0])
    if len(poses) > 1:
        channel = 'eyeBlinkLeft' if 'Left' in name else 'eyeBlinkRight'
        frames = []
        for index, (position, points) in enumerate(list(poses.items())[1:], 1):
            key_name = channel if position == 1 else channel + '_' + str(round(position*100))
            key = ob.shape_key_add(name=key_name)
            for vertex, point in zip(key.data, points):
                vertex.co = point
            frames.append(dict(weight=position*100,
                deltaVertices=[unity(p-b) for p, b in zip(points, base)],
                deltaNormals=[unity(n-b) for n, b in zip(normals[index], normals[0])]))
        ob['blink_frames'] = json.dumps(dict(name=channel, frames=frames))
    return ob


def material(name, color, image=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Roughness'].default_value = .8
    if image:
        node = mat.node_tree.nodes.new('ShaderNodeTexImage')
        node.image = image
        mat.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
    return mat


def build_eyes(body, source, vertices, normals, triangles, kept, texture, reference, output):
    surface = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
    pixels = np.array(texture.pixels[:]).reshape(texture.size[1], texture.size[0], 4)
    ref = np.array(reference.pixels[:]).reshape(reference.size[1], reference.size[0], 4)
    body_image=bpy.data.images['MannequinColor']
    body_pixels=np.array(body_image.pixels[:],dtype=np.float32).reshape(body_image.size[1],body_image.size[0],4)

    def sample(array, x, y):
        x = min(array.shape[1]-1.001, max(0, x))
        y = min(array.shape[0]-1.001, max(0, y))
        ix, iy = int(x), int(y)
        fx, fy = x-ix, y-iy
        return (array[iy, ix]*(1-fx)+array[iy, ix+1]*fx)*(1-fy)+(array[iy+1, ix]*(1-fx)+array[iy+1, ix+1]*fx)*fy

    def skin_color(y, z):
        point, _, face, _ = surface.ray_cast(Vector((-1, y, z)), Vector((1, 0, 0)))
        if point is None:
            return np.array([1, .7, .45, 1])
        ids = triangles[face]
        uv = geometry.barycentric_transform(point, *[vertices[i] for i in ids],
            *[Vector((source['uv'][i]['x'], source['uv'][i]['y'], 0)) for i in ids])
        return sample(pixels, uv.x*(texture.size[0]-1), uv.y*(texture.size[1]-1))

    parts, removed_ids = [], set()
    for side, center in [('Left', .130), ('Right', -.130)]:
        selected, edges, lookup = set(), Counter(), {}
        def position_key(i):
            return tuple(round(v, 5) for v in vertices[i])
        for index, ids in enumerate(triangles):
            point = sum((vertices[i] for i in ids), Vector())/3
            if point.x >= -.12 or ((point.y-center)/.095)**2+((point.z-CZ)/.085)**2 >= 1:
                continue
            selected.add(index)
            for a, b in zip(ids, ids[1:]+ids[:1]):
                ka, kb = position_key(a), position_key(b)
                edges[tuple(sorted((ka, kb)))] += 1
                lookup[ka], lookup[kb] = a, b
        # Include tiny boundary wedges left by the generated triangulation.
        # This keeps a continuous hole edge instead of bridging overlapping cuts.
        for attempt in range(16):
            edges = Counter()
            for index in selected:
                ids=triangles[index]
                for a,b in zip(ids,ids[1:]+ids[:1]):
                    ka,kb=position_key(a),position_key(b)
                    edges[tuple(sorted((ka,kb)))] += 1
                    lookup[ka],lookup[kb]=a,b
            neighbors = defaultdict(list)
            for (a, b), count in edges.items():
                if count == 1:
                    neighbors[a].append(b)
                    neighbors[b].append(a)
            bad={key for key,items in neighbors.items() if len(items)!=2}
            if not bad:break
            added=set()
            for index,ids in enumerate(triangles):
                if index in selected or not any(position_key(i) in bad for i in ids):continue
                p=sum((vertices[i] for i in ids),Vector())/3
                if p.x<-.12 and ((p.y-center)/.108)**2+((p.z-CZ)/.093)**2<1:
                    added.add(index)
            if not added:break
            selected.update(added)
        if any(len(items) != 2 for items in neighbors.values()):
            raise ValueError(side+' eye cut has a branching boundary')
        loops, remaining = [], set(neighbors)
        while remaining:
            start = next(iter(remaining))
            previous, current, loop = None, start, []
            while True:
                loop.append(lookup[current])
                remaining.discard(current)
                following = next(v for v in neighbors[current] if v != previous)
                previous, current = current, following
                if current == start:
                    break
            loops.append(loop)
        def area(loop):
            return sum(vertices[a].y*vertices[b].z-vertices[b].y*vertices[a].z for a,b in zip(loop,loop[1:]+loop[:1]))
        boundary = max(loops, key=lambda loop: abs(area(loop)))
        if area(boundary) < 0:
            boundary.reverse()
        # Keep every original boundary corner to avoid tiny holes from a
        # uniform resampling chord across the generated triangulation.
        target_count = 168 if side == 'Left' else 152
        edges_in_order = list(zip(boundary, boundary[1:]+boundary[:1]))
        lengths = np.array([(vertices[b]-vertices[a]).length for a,b in edges_in_order])
        allocation = np.maximum(1, np.floor(lengths/lengths.sum()*target_count).astype(int))
        while allocation.sum() < target_count:
            allocation[np.argmax(lengths/allocation)] += 1
        while allocation.sum() > target_count:
            costs = np.where(allocation>1, lengths/allocation, np.inf)
            allocation[np.argmin(costs)] -= 1
        outer, outer_normals = [], []
        for (a,b), divisions in zip(edges_in_order, allocation):
            for step in range(divisions):
                outer.append(vertices[a].lerp(vertices[b], step/divisions))
                outer_normals.append(normals[a].lerp(normals[b], step/divisions).normalized())
        distances=[0.0]
        for a,b in zip(outer,outer[1:]+outer[:1]):
            distances.append(distances[-1]+(b-a).length)
        first_angle=math.atan2((outer[0].z-CZ)/.073,(outer[0].y-center)/.077)
        angles = [first_angle+2*math.pi*d/distances[-1] for d in distances[:-1]]
        count = len(outer)
        fit = np.array([[1, p.y-center, p.z-CZ, (p.y-center)**2, (p.y-center)*(p.z-CZ), (p.z-CZ)**2] for p in outer])
        coefficients = np.linalg.lstsq(fit, np.array([p.x for p in outer]), rcond=None)[0]

        def face_x(y, z):
            a, b = y-center, z-CZ
            return float(np.dot(coefficients, [1, a, b, a*a, a*b, b*b]))

        def cap_x(y, z):
            radius = ((y-center)/RX)**2+((z-CZ)/RZ)**2
            return face_x(y,z)-.007*max(0,1-radius/1.44)**2

        def normal_at(y,z):
            epsilon=.0001
            return Vector((-1,(cap_x(y+epsilon,z)-cap_x(y-epsilon,z))/(2*epsilon),
                (cap_x(y,z+epsilon)-cap_x(y,z-epsilon))/(2*epsilon))).normalized()

        # Continuous clean skin chart: sample the original face outside the eye
        # decal and interpolate into the lid, retaining the surrounding skin tone.
        color_points=[]
        color_values=[]
        for angle in np.linspace(0,2*math.pi,96,endpoint=False):
            y=center+.09*math.cos(angle);z=CZ+.087*math.sin(angle)
            color=skin_color(y,z)
            if color[0]>.4 and .4<color[1]/color[0]<.86:
                color_points.append([1,y-center,z-CZ])
                color_values.append(color[:3])
        skin_fit=np.linalg.lstsq(np.array(color_points),np.array(color_values),rcond=None)[0]
        def clean_skin(y,z):
            return np.append(np.clip(np.array([1,y-center,z-CZ])@skin_fit,0,1),1)
        size = 192
        colors, eye_colors = [], []
        for row in range(size):
            z = CZ-.075+.15*row/(size-1)
            for column in range(size):
                y = center-.075+.15*column/(size-1)
                radius = math.sqrt(((y-center)/.058)**2+((z-CZ)/.056)**2)
                angle = math.atan2((z-CZ)/.056,(y-center)/.058)
                outside = clean_skin(y,z)
                actual = skin_color(y,z)
                strength = min(1,max(0,(radius-1.5)/.2))
                colors.extend(outside*(1-strength)+actual*strength)
                sx = (466 if side=='Left' else 775)-(y-center)/RX*51.5
                sy = reference.size[1]-1-(461-(z-CZ)/RZ*47)
                eye_colors.extend(sample(ref,sx,sy))
        # Remove baked dark eye fragments from the adjacent source-face copy as
        # well, then blend into untouched skin outside the replacement boundary.
        for ids in triangles:
            xyz=np.array([vertices[i] for i in ids])
            if xyz[:,0].min()>-.12 or xyz[:,2].max()<.49 or xyz[:,2].min()>.70:continue
            if xyz[:,1].min()>center+.130 or xyz[:,1].max()<center-.130:continue
            uv=np.array([[source['uv'][i]['x']*(body_image.size[0]-1),source['uv'][i]['y']*(body_image.size[1]-1)] for i in ids])
            low=np.maximum(np.floor(uv.min(axis=0)).astype(int),0)
            high=np.minimum(np.ceil(uv.max(axis=0)).astype(int),np.array(body_image.size)-1)
            if np.any(high<low):continue
            xx,yy=np.meshgrid(np.arange(low[0],high[0]+1),np.arange(low[1],high[1]+1))
            denominator=(uv[1,1]-uv[2,1])*(uv[0,0]-uv[2,0])+(uv[2,0]-uv[1,0])*(uv[0,1]-uv[2,1])
            if abs(denominator)<1e-8:continue
            a=((uv[1,1]-uv[2,1])*(xx-uv[2,0])+(uv[2,0]-uv[1,0])*(yy-uv[2,1]))/denominator
            b=((uv[2,1]-uv[0,1])*(xx-uv[2,0])+(uv[0,0]-uv[2,0])*(yy-uv[2,1]))/denominator
            c=1-a-b
            points=a[...,None]*xyz[0]+b[...,None]*xyz[1]+c[...,None]*xyz[2]
            radius=np.sqrt(((points[...,1]-center)/.110)**2+((points[...,2]-CZ)/.100)**2)
            strength=np.clip((1.15-radius)/.25,0,1)
            mask=(a>=-.002)&(b>=-.002)&(c>=-.002)&(strength>0)&(points[...,0]<-.12)&(np.abs(points[...,1])>.038)
            clean=skin_fit[0]+(points[...,1,None]-center)*skin_fit[1]+(points[...,2,None]-CZ)*skin_fit[2]
            patch=body_pixels[low[1]:high[1]+1,low[0]:high[0]+1]
            blend=strength[...,None]
            patch[mask,:3]=(patch[...,:3]*(1-blend)+clean*blend)[mask]
        for i,v in enumerate(vertices):
            radius=math.sqrt(((v.y-center)/.110)**2+((v.z-CZ)/.100)**2)
            if v.x<-.12 and radius<1.15 and abs(v.y)>.038:
                a,b=v.y-center,v.z-CZ
                fitted_normal=Vector((-1,coefficients[1]+2*coefficients[3]*a+coefficients[4]*b,
                    coefficients[2]+coefficients[4]*a+2*coefficients[5]*b)).normalized()
                strength=min(1,max(0,(1.15-radius)/.25))
                normals[i]=normals[i].lerp(fitted_normal,strength).normalized()
        # Match the updated face normals at the shared outer edge; retaining the
        # generated eye-border normals here leaves radial shading scars.
        outer_normals=[]
        for (a,b), divisions in zip(edges_in_order, allocation):
            for step in range(divisions):
                outer_normals.append(normals[a].lerp(normals[b],step/divisions).normalized())
        images = []
        for suffix, values in [('LidColor',colors),('EyeColor',eye_colors)]:
            image = bpy.data.images.new(side+suffix,width=size,height=size)
            image.colorspace_settings.name = texture.colorspace_settings.name
            image.pixels.foreach_set(np.array(values,dtype=np.float32))
            image.filepath_raw = str(output/(side+suffix+'.png'))
            image.file_format='PNG'
            image.save(); image.pack(); images.append(image)
        skin_material = material(side+'LidSkin',(1,1,1),images[0])
        eye_material = material(side+'Eye',(1,1,1),images[1])
        lash_material = material(side+'Lashes',(.035,.016,.008))
        def uv_for(points):
            return [((v.y-center+.075)/.15,(v.z-CZ+.075)/.15) for v in points]

        # Smooth eye surface never has a blink delta. Lids occlude it geometrically.
        eye_points=[Vector((cap_x(center,CZ)+.0003,center,CZ))]
        for ring in range(1,17):
            radius=1.12*ring/16
            for j in range(96):
                angle=2*math.pi*j/96
                y,z=center+RX*radius*math.cos(angle),CZ+RZ*radius*math.sin(angle)
                eye_points.append(Vector((cap_x(y,z)+.0003,y,z)))
        eye_faces=[(0,1+j,1+(j+1)%96) for j in range(96)]
        for ring in range(15):
            for j in range(96):
                a,b=1+ring*96+j,1+ring*96+(j+1)%96
                eye_faces.append((a,a+96,b+96,b))
        eye=make_part(side+'Eye',{0:eye_points},eye_faces,eye_material,uv_for(eye_points),
            {0:[normal_at(v.y,v.z) for v in eye_points]})
        eye['texture_file']=side+'EyeColor.png'
        parts.append((eye,(1,1,1)))

        def lid_edge(angle,t):
            cosine,sine=math.cos(angle),math.sin(angle)
            y=center+RX*cosine
            z=(CZ+RZ*sine)*(1-t)+(CZ-.014*(1-cosine*cosine))*t
            return Vector((cap_x(y,z)-.0015,y,z))

        for upper in [True,False]:
            poses, normal_targets = {}, {}
            for t in [0,.25,.5,.75,1]:
                points, shading = [], []
                for ring in [1-i/24 for i in range(25)]:
                    for i,angle in enumerate(angles):
                        inner=lid_edge(angle,t)
                        circle=Vector((0,center+.059*math.cos(angle),CZ+.054*math.sin(angle)))
                        if ring>=.5:
                            edge=(ring-.5)*2
                            y=circle.y*(1-edge)+outer[i].y*edge
                            z=circle.z*(1-edge)+outer[i].z*edge
                        else:
                            edge=0
                            y=inner.y*(1-ring*2)+circle.y*ring*2
                            z=inner.z*(1-ring*2)+circle.z*ring*2
                        edge_weight=edge*edge*(3-2*edge)
                        x=cap_x(y,z)-.0015*(1-edge_weight)+(outer[i].x-cap_x(outer[i].y,outer[i].z))*edge_weight
                        points.append(Vector((x,y,z)))
                        shading.append(normal_at(y,z).lerp(outer_normals[i],edge_weight).normalized())
                poses[t]=points; normal_targets[t]=shading
            faces=[]
            for ring in range(24):
                for j,angle in enumerate(angles):
                    next_j=(j+1)%count
                    mid=math.sin(angle)+math.sin(angles[next_j])
                    if (mid>=0) != upper:continue
                    faces.append((ring*count+j,(ring+1)*count+j,(ring+1)*count+next_j,ring*count+next_j))
            ob=make_part(('Upper' if upper else 'Lower')+side+'Lid',poses,faces,skin_material,uv_for(poses[0]),normal_targets)
            ob['texture_file']=side+'LidColor.png'
            parts.append((ob,(1,1,1)))

        # Thin upper lash line and two short curved lashes travel with the upper lid.
        lash_poses={}
        lash_faces=[]
        for t in [0,.25,.5,.75,1]:
            points=[]
            curves=[]
            curves.append([(lid_edge(math.pi*j/64,t),.0008) for j in range(65)])
            sign=1 if side=='Left' else -1
            for angle,length in [(.22,.009),(.42,.007)]:
                anchor=lid_edge(angle if sign>0 else math.pi-angle,t)
                curve=[]
                for j in range(13):
                    u=j/12
                    point=anchor+Vector((-.0005,sign*length*u*(.72+.1*t),length*u*(.7-.12*t)+.002*math.sin(u*math.pi)))
                    curve.append((point,.0013*(1-u)+.00008))
                curves.append(curve)
            for curve in curves:
                start=len(points)
                for point,width in curve:
                    for angle in [0,math.pi/2,math.pi,math.pi*1.5]:
                        points.append(point+Vector((width*math.cos(angle)-.0006,0,width*math.sin(angle))))
                if t==0:
                    for j in range(len(curve)-1):
                        for k in range(4):
                            a=start+j*4+k;b=start+j*4+(k+1)%4
                            lash_faces.append((a,b,b+4,a+4))
            lash_poses[t]=points
        lashes=make_part(side+'Lashes',lash_poses,lash_faces,lash_material)
        parts.append((lashes,(.035,.016,.008)))
        removed_ids.update(selected)
        print('EYE_PATCH',side,len(selected),'triangles, boundary',len(boundary),'loops',len(loops))

    cut_triangles={tuple(sorted(triangles[i])) for i in removed_ids}
    editable=bmesh.new();editable.from_mesh(body.data);editable.faces.ensure_lookup_table()
    to_remove=[f for f in editable.faces if tuple(sorted(v.index for v in f.verts)) in cut_triangles]
    bmesh.ops.delete(editable,geom=to_remove,context='FACES_ONLY')
    editable.to_mesh(body.data);editable.free()
    body.data.normals_split_custom_set_from_vertices(normals)
    body_image.pixels.foreach_set(body_pixels.reshape(-1))
    body_image.save();body_image.pack()
    kept=[value for start in range(0,len(kept),3) if tuple(sorted(kept[start:start+3])) not in cut_triangles for value in kept[start:start+3]]
    return parts,kept
