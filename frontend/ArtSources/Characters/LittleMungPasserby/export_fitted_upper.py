"""Export the fitted Blender garment copy for PasserbyOutfitBuilder.UpdateUpperFit."""
import argparse
import json
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


def export_upper(blend_path, output_path):
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    garment = bpy.data.objects.get("Suit_Navy_Upper_Fitted")
    if garment is None or garment.type != "MESH":
        raise ValueError("The fitted garment mesh is missing.")
    basis_values = garment["unity_basis"]
    basis = Matrix([basis_values[start : start + 3] for start in range(0, 9, 3)])
    origin = Vector(garment["unity_origin"])
    bone_names = [group.name for group in garment.vertex_groups]
    mesh = garment.data
    mesh.calc_loop_triangles()
    positions, uvs, triangles, weights = [], [], [], []
    for triangle in mesh.loop_triangles:
        start = len(positions)
        for loop_index in triangle.loops:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index]
            point = garment.matrix_world @ vertex.co
            canonical = Vector((point.x, point.z, -point.y))
            world = basis @ canonical + origin
            positions.append(dict(x=world.x, y=world.y, z=world.z))
            uv = mesh.uv_layers.active.data[loop_index].uv
            uvs.append(dict(x=uv.x, y=uv.y))
            influences = sorted(vertex.groups, key=lambda group: group.weight, reverse=True)[:4]
            total = sum(group.weight for group in influences)
            if total <= 0:
                raise ValueError(f"Vertex {vertex.index} has no skin weights.")
            row = {}
            for slot in range(4):
                row[f"boneIndex{slot}"] = influences[slot].group if slot < len(influences) else 0
                row[f"weight{slot}"] = influences[slot].weight / total if slot < len(influences) else 0
            weights.append(row)
        triangles.extend([start, start + 1, start + 2])
    part = dict(
        name="Suit_Navy_Upper",
        vertices=positions,
        uv=uvs,
        triangles=triangles,
        weights=weights,
        color=dict(r=1, g=1, b=1, a=1),
        texture="SuitBaseColor.png",
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        json.dumps(dict(boneNames=bone_names, parts=[part]), separators=(",", ":")),
        encoding="utf-8",
    )
    print(f"Exported {len(triangles) // 3} garment triangles to {output_path}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--blend", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :])
    if args.output.suffix.lower() != ".json":
        parser.error("The output must be a separate JSON file.")
    export_upper(args.blend, args.output)
