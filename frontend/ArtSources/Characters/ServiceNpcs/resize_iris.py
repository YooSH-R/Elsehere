"""Shrink iris/pupil appearance inside the original eye outline on a texture COPY."""

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

for name in [
    "LittleMung_MiddleAge_Male",
    "LittleMung_MiddleAge_Female",
    "LittleMung_BlackYoung_Male",
]:
    data = json.loads((author / name / "Surface.json").read_text(encoding="utf-8-sig"))
    verts = np.array([vec(v)[:] for v in data["vertices"]])
    tris = np.array(data["triangles"]).reshape(-1, 3)
    uv = np.array([[v["x"], v["y"]] for v in data["uv"]])
    tree = BVHTree.FromPolygons(
        [Vector(v) for v in verts], tris.tolist(), all_triangles=True
    )
    tex = bpy.data.images.load(
        str(project / (author / name / "TexturePath.txt").read_text().strip()),
        check_existing=False,
    )
    tw, th = tex.size
    original = np.array(tex.pixels[:]).reshape(th, tw, 4)
    edited = original.copy()
    measurements = json.loads((out / name / "EyeMeasurements.json").read_text())
    ys = measurements["ys"]
    zs = measurements["zs"]
    eyes = []
    projection = np.load(out / name / "EyeProjection.npz")["colors"]
    for e in measurements["eyes"]:
        r0, r1, c0, c1 = e["eyeBounds"]
        ir0, ir1, ic0, ic1 = e["irisBounds"]
        center = np.array([(ys[c0] + ys[c1]) / 2, (zs[r0] + zs[r1]) / 2])
        radii = np.array([(ys[c1] - ys[c0]) / 2, (zs[r1] - zs[r0]) / 2])
        # Measure the colored iris at its middle rows; the white bounds exclude its inner edge.
        spans = []
        for row in range(int(r0 + (r1 - r0) * 0.38), int(r0 + (r1 - r0) * 0.62)):
            line = projection[row, :, :3]
            dark = (line.mean(axis=1) < 0.52) & (line[:, 0] < 0.43)
            seed = int((ic0 + ic1) / 2)
            if not dark[seed]:
                continue
            left = right = seed
            while left > max(0, c0 - 12) and dark[left - 1]:
                left -= 1
            while right < min(len(ys) - 1, c1 + 12) and dark[right + 1]:
                right += 1
            spans.append((left, right))
        left, right = np.median(spans, axis=0)
        full_left = min(c0, int(left) - 2)
        full_right = max(c1, int(right) + 2)
        center[0] = (ys[full_left] + ys[full_right]) / 2
        radii[0] = (ys[full_right] - ys[full_left]) / 2
        iris = np.array(
            [
                np.interp((left + right) / 2, np.arange(len(ys)), ys),
                (zs[r0] + zs[r1]) / 2,
            ]
        )
        iris_radius = np.array(
            [(right - left) * (ys[1] - ys[0]) * 0.55, (zs[r1] - zs[r0]) * 0.445]
        )
        rr, cc = np.mgrid[r0 : r1 + 1, c0 : c1 + 1]
        region = projection[r0 : r1 + 1, c0 : c1 + 1, :3]
        white = (region.mean(axis=2) > 0.72) & (
            (region.max(axis=2) - region.min(axis=2)) < 0.16
        )
        terms = np.stack(
            [
                np.ones_like(rr),
                np.asarray(ys)[cc] - iris[0],
                np.asarray(zs)[rr] - iris[1],
            ],
            axis=-1,
        )
        fit = np.linalg.lstsq(terms[white], region[white], rcond=None)[0]
        eyes.append((center, radii, iris, iris_radius, fit))
    changed = set()

    def sample(u, v):
        x = np.clip(u * tw - 0.5, 0, tw - 1.001)
        y = np.clip(v * th - 0.5, 0, th - 1.001)
        x0, y0 = int(x), int(y)
        a, b = x - x0, y - y0
        return (
            original[y0, x0] * (1 - a) * (1 - b)
            + original[y0, x0 + 1] * a * (1 - b)
            + original[y0 + 1, x0] * (1 - a) * b
            + original[y0 + 1, x0 + 1] * a * b
        )

    for ids in tris:
        triangle = verts[ids]
        texcoords = uv[ids]
        if not any(
            np.all(triangle[:, 1:3].max(axis=0) >= center - radii)
            and np.all(triangle[:, 1:3].min(axis=0) <= center + radii)
            for center, radii, iris, iris_radius, fit in eyes
        ):
            continue
        low = np.maximum(0, np.floor(texcoords.min(axis=0) * [tw, th] - 1).astype(int))
        high = np.minimum(
            [tw - 1, th - 1], np.ceil(texcoords.max(axis=0) * [tw, th] + 1).astype(int)
        )
        a, b, c = texcoords
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-12:
            continue
        for y in range(low[1], high[1] + 1):
            for x in range(low[0], high[0] + 1):
                p = np.array([(x + 0.5) / tw, (y + 0.5) / th])
                w0 = (
                    (b[1] - c[1]) * (p[0] - c[0]) + (c[0] - b[0]) * (p[1] - c[1])
                ) / den
                w1 = (
                    (c[1] - a[1]) * (p[0] - c[0]) + (a[0] - c[0]) * (p[1] - c[1])
                ) / den
                w2 = 1 - w0 - w1
                if min(w0, w1, w2) < -0.00001:
                    continue
                world = triangle.T @ np.array([w0, w1, w2])
                point = world[1:3]
                for center, radii, iris, iris_radius, fit in eyes:
                    if np.sum(((point - center) / radii) ** 2) >= 1:
                        continue
                    hit, _, _, _ = tree.ray_cast(
                        Vector((-3, *point)), Vector((1, 0, 0))
                    )
                    if hit is None or abs(hit.x - world[0]) > 0.002:
                        continue
                    delta = point - iris
                    radius = np.linalg.norm(delta / iris_radius)
                    if radius > 1.35:
                        continue
                    white = np.clip(np.array([1, delta[0], delta[1]]) @ fit, 0.65, 1)
                    shade = np.append(white, 1.0)
                    shrink = 0.72
                    if radius < shrink:
                        source = iris + delta / shrink
                        hit, _, face, _ = tree.ray_cast(
                            Vector((-3, *source)), Vector((1, 0, 0))
                        )
                        if hit is not None:
                            sourceids = tris[face]
                            at = geometry.barycentric_transform(
                                hit,
                                *[Vector(verts[i]) for i in sourceids],
                                *[Vector((*uv[i], 0)) for i in sourceids],
                            )
                            color = sample(at.x, at.y)
                            alpha = np.clip((shrink - radius) / 0.025, 0, 1)
                            shade = color * alpha + shade * (1 - alpha)
                    outer = np.clip((1.35 - radius) / 0.1, 0, 1)
                    edited[y, x] = shade * outer + original[y, x] * (1 - outer)
                    changed.add((y, x))
                    break
    # Extend corrected edge texels into the UV gutter to prevent old iris colors bleeding at seams.
    mask = np.zeros((th, tw), bool)
    for y, x in changed:
        mask[y, x] = True
    for _ in range(2):
        next_mask = mask.copy()
        next_pixels = edited.copy()
        for dy, dx in [(0, 1), (0, -1), (1, 0), (-1, 0)]:
            neighbor = np.roll(mask, (dy, dx), (0, 1))
            fill = neighbor & ~next_mask
            next_pixels[fill] = np.roll(edited, (dy, dx), (0, 1))[fill]
            next_mask[fill] = True
        edited = next_pixels
        mask = next_mask
    image = tex.copy()
    image.name = name + "_SmallerIris"
    image.pixels.foreach_set(edited.astype(np.float32).ravel())
    image.filepath_raw = str(out / name / "CharacterColor.png")
    image.file_format = "PNG"
    image.save()
    print(name, "changed texels", len(changed), flush=True)
    # Pack the new texture into a separate Blender review file.
    bpy.ops.wm.open_mainfile(
        filepath=str(author / name / "Face" / (name + "_Face.blend"))
    )
    for ob in bpy.data.objects:
        if ob.type == "MESH" and len(ob.data.vertices) == len(data["vertices"]):
            for material in ob.data.materials:
                if material and material.use_nodes:
                    for node in material.node_tree.nodes:
                        if node.type == "TEX_IMAGE":
                            node.image = bpy.data.images.load(
                                str(out / name / "CharacterColor.png")
                            )
                            node.image.pack()
    bpy.ops.wm.save_as_mainfile(
        filepath=str(out / name / "Face" / (name + "_SmallerIris.blend"))
    )
