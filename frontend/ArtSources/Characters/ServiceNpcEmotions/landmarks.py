"""Measure each imported face's eyebrow height and resting lip curvature."""

import bpy
import numpy as np
from mathutils import Vector, geometry
from mathutils.bvhtree import BVHTree


def brow_regions(mask, ys, zs):
    seen, regions = set(), []
    for row, col in zip(*np.where(mask)):
        if (row, col) in seen:
            continue
        stack, region = [(row, col)], []
        seen.add((row, col))
        while stack:
            a, b = stack.pop()
            region.append((a, b))
            for c, d in ((a - 1, b), (a + 1, b), (a, b - 1), (a, b + 1)):
                if (
                    0 <= c < len(zs)
                    and 0 <= d < len(ys)
                    and mask[c, d]
                    and (c, d) not in seen
                ):
                    stack.append((c, d))
                    seen.add((c, d))
        rr, cc = np.array(region).T
        width, height = np.ptp(ys[cc]), np.ptp(zs[rr])
        if (
            len(rr) >= 8
            and 0.025 < width < 0.145
            and height < 0.035
            and width > height * 1.4
        ):
            regions.append((float(zs[rr].mean()), float(ys[cc].mean())))
    return regions


def measure(face, eye_tops):
    def points(values):
        return np.array([[v["x"], -v["z"], v["y"]] for v in values])

    vertices = points(face["vertices"])
    triangles = np.array(face["triangles"]).reshape(-1, 3)
    tree = BVHTree.FromPolygons(
        vertices.tolist(), triangles.tolist(), all_triangles=True
    )
    image = bpy.data.images.load(face["texture"], check_existing=True)
    pixels = np.array(image.pixels[:]).reshape(image.size[1], image.size[0], 4)
    uv = [Vector((v["x"], v["y"], 0)) for v in face["uv"]]
    vectors = [Vector(v) for v in vertices]
    result = {}
    for side, center in [("Left", 0.082), ("Right", -0.155)]:
        ys = np.linspace(center - 0.075, center + 0.075, 100)
        # Exclude the whole eye cutout, including its skin join. Rays through
        # that opening hit the dark back of the head, not an eyebrow.
        zs = np.linspace(max(0.615, eye_tops[side] + 0.008), 0.760, 100)
        colors = np.ones((len(zs), len(ys)))
        for row, z in enumerate(zs):
            for col, y in enumerate(ys):
                hit, _, index, _ = tree.ray_cast(Vector((-1, y, z)), Vector((1, 0, 0)))
                if hit is None:
                    continue
                ids = triangles[index]
                coord = geometry.barycentric_transform(
                    hit, *[vectors[i] for i in ids], *[uv[i] for i in ids]
                )
                px = int(np.clip(coord.x * image.size[0], 0, image.size[0] - 1))
                py = int(np.clip(coord.y * image.size[1], 0, image.size[1] - 1))
                colors[row, col] = pixels[py, px, :3].mean()
        regions = []
        for contrast in (0.55, 0.70, 0.85):
            regions = brow_regions(
                colors < np.quantile(colors, 0.75) * contrast, ys, zs
            )
            if regions:
                break
        if not regions:
            # A side fringe may touch the brow in the texture, making one tall
            # connected region. Measure its lower edge above the eye opening.
            mask = colors < np.quantile(colors, 0.75) * 0.55
            edges = [
                zs[np.flatnonzero(mask[:, col])[0]]
                for col in np.flatnonzero(abs(ys - center) < 0.045)
                if mask[:, col].any()
            ]
            if len(edges) < 12:
                raise ValueError("눈썹 기준점을 자동 측정하지 못했습니다: " + side)
            lower = float(np.quantile(edges, 0.30))
            if lower <= zs[0] + 0.005 or lower >= zs[-1] - 0.015:
                raise ValueError("눈썹과 머리카락 경계를 확인하세요: " + side)
            regions = [(lower + 0.004, center)]
        result["brow" + side] = min(
            regions, key=lambda p: abs(p[0] - 0.665) + abs(p[1] - center) * 0.3
        )[0]
    lips = points(
        next(p for p in face["parts"] if p["name"] == "SpeakingLips")["vertices"]
    )
    inner = lips[-len(lips) // 6 :]
    fit = np.polyfit(inner[:, 1] + 0.043, inner[:, 2], 2)
    # Amplifying the cheek from curvature alone can fold sparse chin triangles.
    result["restingMouthCurvature"] = float(fit[0])
    return result
