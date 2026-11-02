import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector, geometry
from mathutils.bvhtree import BVHTree

project = Path(__file__).resolve().parents[4]
author = project / "frontend/ArtSources/Characters/ServiceNpcs"
sys.path.insert(0, str(author))
from face_authoring_common import vec

out = author


def components(mask):
    seen = np.zeros(mask.shape, dtype=bool)
    result = []
    for r, c in zip(*np.where(mask)):
        if seen[r, c]:
            continue
        stack = [(r, c)]
        seen[r, c] = True
        region = []
        while stack:
            a, b = stack.pop()
            region.append((a, b))
            for x, y in [(a - 1, b), (a + 1, b), (a, b - 1), (a, b + 1)]:
                if (
                    0 <= x < mask.shape[0]
                    and 0 <= y < mask.shape[1]
                    and mask[x, y]
                    and not seen[x, y]
                ):
                    seen[x, y] = True
                    stack.append((x, y))
        result.append(region)
    return sorted(result, key=len, reverse=True)


for name in [
    "LittleMung_MiddleAge_Male",
    "LittleMung_MiddleAge_Female",
    "LittleMung_BlackYoung_Male",
]:
    data = json.loads((author / name / "Surface.json").read_text(encoding="utf-8-sig"))
    verts = [vec(v) for v in data["vertices"]]
    tris = [data["triangles"][i : i + 3] for i in range(0, len(data["triangles"]), 3)]
    landmarks = json.loads((author / name / "Face/Landmarks.json").read_text())
    height = landmarks["headHeight"]
    top = max(v.z for v in verts)
    low = top - height
    width = max(v.y for v in verts) - min(v.y for v in verts)
    ys = np.linspace(-width * 0.36, width * 0.36, 360)
    zs = np.linspace(low + height * 0.12, low + height * 0.68, 280)
    tree = BVHTree.FromPolygons(verts, tris, all_triangles=True)
    tex = bpy.data.images.load(
        str(project / (author / name / "TexturePath.txt").read_text().strip()),
        check_existing=False,
    )
    pixels = np.array(tex.pixels[:]).reshape(tex.size[1], tex.size[0], 4)
    colors = np.zeros((len(zs), len(ys), 4))
    maps = np.full((len(zs), len(ys), 2), -1.0)
    for r, z in enumerate(zs):
        for c, y in enumerate(ys):
            hit, _, face, _ = tree.ray_cast(
                Vector((-3, float(y), float(z))), Vector((1, 0, 0))
            )
            if hit is None:
                continue
            ids = tris[face]
            uv = geometry.barycentric_transform(
                hit,
                *[verts[i] for i in ids],
                *[Vector((data["uv"][i]["x"], data["uv"][i]["y"], 0)) for i in ids],
            )
            u, v = np.clip([uv.x, uv.y], 0, 0.99999)
            colors[r, c] = pixels[int(v * tex.size[1]), int(u * tex.size[0])]
            maps[r, c] = [u, v]
    image = bpy.data.images.new(
        name + "EyeProjection", width=len(ys), height=len(zs), alpha=True
    )
    image.pixels.foreach_set(colors.astype(np.float32).ravel())
    image.filepath_raw = str(out / name / "EyeProjection.png")
    image.file_format = "PNG"
    image.save()
    lum = colors[:, :, :3].mean(axis=2)
    sat = colors[:, :, :3].max(axis=2) - colors[:, :, :3].min(axis=2)
    results = []
    for side in [0, 1]:
        mask = (lum > 0.68) & (sat < 0.20)
        mask[:, 180:] &= side == 1
        mask[:, :180] &= side == 0
        cc = components(mask)[0]
        rows, cols = zip(*cc)
        r0, r1 = min(rows), max(rows)
        c0, c1 = min(cols), max(cols)
        rr, co = np.mgrid[: len(zs), : len(ys)]
        ellipse = ((rr - (r0 + r1) / 2) / ((r1 - r0) * 0.48)) ** 2 + (
            (co - (c0 + c1) / 2) / ((c1 - c0) * 0.48)
        ) ** 2 < 1
        candidates = components((lum < 0.52) & ellipse)
        iris = candidates[0]
        ir, ic = zip(*iris)
        result = dict(
            eyeBounds=[r0, r1, c0, c1],
            irisBounds=[min(ir), max(ir), min(ic), max(ic)],
            irisCenter=[float(np.mean(ir)), float(np.mean(ic))],
        )
        results.append(result)
    print(name, results, flush=True)
    (out / name / "EyeMeasurements.json").write_text(
        json.dumps(
            dict(eyes=results, ys=ys.tolist(), zs=zs.tolist()), default=lambda v: int(v)
        )
    )
    np.savez_compressed(
        out / name / "EyeProjection.npz", colors=colors, maps=maps, ys=ys, zs=zs
    )
