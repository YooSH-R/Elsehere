"""Measure existing review eyes and iris color without changing model assets."""

import json, sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector, geometry
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parents[4]
folder = Path(sys.argv[-1])
outputs = [folder] if (folder / "Surface.json").exists() else sorted(folder.iterdir())
for output in outputs:
    if not (output / "Surface.json").exists():
        continue
    d = json.loads((output / "Surface.json").read_text(encoding="utf-8-sig"))
    verts = [Vector((p["x"], -p["z"], p["y"])) for p in d["vertices"]]
    faces = [d["triangles"][i : i + 3] for i in range(0, len(d["triangles"]), 3)]
    tree = BVHTree.FromPolygons(verts, faces, all_triangles=True)
    image = bpy.data.images.load(str(root / "frontend/UnityClient" / d["texture"]))
    pixels = np.array(image.pixels[:]).reshape(image.size[1], image.size[0], 4)
    height = d["headMax"]["y"] - d["headMin"]["y"]
    ys = np.linspace(-0.32, 0.32, 320)
    zs = np.linspace(
        d["headMin"]["y"] + height * 0.15, d["headMin"]["y"] + height * 0.47, 220
    )
    colors = np.zeros((len(zs), len(ys), 3))
    for row, z in enumerate(zs):
        for col, y in enumerate(ys):
            p, n, f, _ = tree.ray_cast(Vector((-2, y, z)), Vector((1, 0, 0)))
            if p is None:
                continue
            uv = geometry.barycentric_transform(
                p,
                *[verts[i] for i in faces[f]],
                *[Vector((d["uv"][i]["x"], d["uv"][i]["y"], 0)) for i in faces[f]],
            )
            colors[row, col] = pixels[
                min(image.size[1] - 1, max(0, int(uv.y * image.size[1]))),
                min(image.size[0] - 1, max(0, int(uv.x * image.size[0]))),
                :3,
            ]
    white = (colors.mean(2) > 0.62) & (np.ptp(colors, axis=2) < 0.23)
    eyes = []
    for side, sign in [("Left", 1), ("Right", -1)]:
        mask = white & ((ys[None, :] * sign) > 0.025)
        seen = set()
        regions = []
        for row, col in zip(*np.where(mask)):
            if (row, col) in seen:
                continue
            stack = [(row, col)]
            seen.add((row, col))
            region = []
            while stack:
                a, b = stack.pop()
                region.append((a, b))
                for c, e in [(a - 1, b), (a + 1, b), (a, b - 1), (a, b + 1)]:
                    if (
                        0 <= c < len(zs)
                        and 0 <= e < len(ys)
                        and mask[c, e]
                        and (c, e) not in seen
                    ):
                        seen.add((c, e))
                        stack.append((c, e))
            regions.append(region)
        region = max(regions, key=len)
        rr, cc = zip(*region)
        cy = (ys[min(cc)] + ys[max(cc)]) / 2
        cz = (zs[min(rr)] + zs[max(rr)]) / 2
        rx = (ys[max(cc)] - ys[min(cc)]) / 2 * 1.04
        rz = (zs[max(rr)] - zs[min(rr)]) / 2 * 1.04
        yy, zz = np.meshgrid(ys, zs)
        inside = ((yy - cy) / rx) ** 2 + ((zz - cz) / rz) ** 2 < 0.60
        lum = colors.mean(2)
        sat = np.ptp(colors, axis=2) / np.maximum(colors.max(2), 0.001)
        candidates = colors[inside & (sat > 0.18) & (lum > 0.035) & (lum < 0.55)]
        if len(candidates) < 8:
            raise ValueError(output.name + " insufficient iris samples")
        iris = np.median(candidates, axis=0)
        eyes.append(
            dict(
                side=side,
                center=[float(cy), float(cz)],
                radii=[float(rx), float(rz)],
                irisColor=iris.tolist(),
                colorSamples=len(candidates),
            )
        )
    (output / "EyeFit.json").write_text(
        json.dumps(dict(name=output.name, eyes=eyes), indent=2), encoding="utf-8"
    )
    print(output.name, eyes, flush=True)
