"""Offline upper fitting recipe for the measured Male Base; never edits source models."""
import argparse, json
from pathlib import Path
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
parser = argparse.ArgumentParser()
parser.add_argument('--source', required=True, help='Existing fitted upper JSON; geometry is preserved')
parser.add_argument('--measurement', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args(__import__('sys').argv[__import__('sys').argv.index('--') + 1:])
if Path(args.source).resolve() == (Path(args.output) / 'PasserbyParts.json').resolve():
    parser.error('Keep the input model data and output file separate.')
data = json.loads(Path(args.measurement).read_text())
out = Path(args.output)
out.mkdir(parents=True, exist_ok=True)
vec = lambda p: np.array([p['x'], p['y'], p['z']], float)
world = np.array([vec(p) for p in data['vertices']])
land = {b['name']: vec(b['position']) for b in data['landmarks']}
right = land['RightUpperArm'] - land['LeftUpperArm']
right[1] = 0
right /= np.linalg.norm(right)
forward = np.cross(right, [0, 1, 0])
if np.dot(forward, land['LeftToes'] - land['LeftFoot']) < 0:
    forward *= -1
matrix = np.array([right, [0, 1, 0], forward]).T
origin = (land['LeftUpperArm'] + land['RightUpperArm']) / 2
origin[1] = world[:, 1].min()
body = (world - origin) @ matrix
land = {k: (v - origin) @ matrix for k, v in land.items()}
triangles = np.array(data['triangles']).reshape(-1, 3)
names = data['skinBones']
weights = np.zeros((len(body), len(names)))
for i, row in enumerate(data['weights']):
    for k in range(4):
        weights[i, row['m_BoneIndex' + str(k)]] += row['m_Weight' + str(k)]
allowed = {'Torso': ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck', 'Neck_Twist_A', 'Left_Shoulder', 'Right_Shoulder']}
for side in ('Left', 'Right'):
    allowed[side] = [side + '_' + n for n in ('Shoulder', 'UpperArm', 'LowerArm')]
trees = {}
for group, bones in allowed.items():
    mask = np.array([n in bones for n in names])
    strength = weights[:, mask].sum(axis=1)
    subset = triangles[(strength[triangles].mean(axis=1) > 0.35) & (strength[triangles].min(axis=1) > 0.05) & (body[triangles, :][..., 1].mean(axis=1) < 0.77)]
    trees[group] = (BVHTree.FromPolygons([Vector(v) for v in body], subset.tolist(), all_triangles=True), subset, mask)

def transfer(p, group):
    tree, tris, mask = trees[group]
    hit, _, index, _ = tree.find_nearest(Vector(p))
    a, b, c = [Vector(body[j]) for j in tris[index]]
    bary = np.maximum(np.array(barycentric_transform(hit, a, b, c, Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))), 0)
    bary /= bary.sum()
    row = (weights[tris[index]] * bary[:, None]).sum(axis=0) * mask
    if row.sum() < 1e-08:
        raise ValueError('No permitted bone influence')
    return row / row.sum()

def smooth(value):
    value = np.clip(value, 0, 1)
    return value * value * (3 - 2 * value)
existing = json.loads(Path(args.source).read_text())
for part in existing['parts']:
    rows = []
    for vertex in part['vertices']:
        p = (vec(vertex) - origin) @ matrix
        side = 'Right' if p[0] > 0 else 'Left'
        arm = float(smooth((abs(p[0]) - 0.15) / 0.11))
        if p[1] < 0.48:
            arm *= float(smooth((abs(p[0]) - 0.22) / 0.06))
        row = transfer(p, 'Torso') * (1 - arm) + transfer(p, side) * arm
        order = np.argsort(row)[-4:][::-1]
        values = row[order]
        values /= values.sum()
        result = {}
        for k, (bone, weight) in enumerate(zip(order, values)):
            result['boneIndex' + str(k)] = int(bone)
            result['weight' + str(k)] = float(weight)
        rows.append(result)
    part['weights'] = rows
existing['boneNames'] = names
(out / 'PasserbyParts.json').write_text(json.dumps(existing, separators=(',', ':')))
print('Restricted jacket influences; vertices, triangles and UVs preserved.')
