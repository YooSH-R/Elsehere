import numpy as np
from mathutils import Vector, geometry
from mathutils.bvhtree import BVHTree


def locate_mouth(data, vertices, triangles, texture):
    head = data["bones"].index("Head")
    weights = data["weights"]
    ids = [
        i
        for i, w in enumerate(weights)
        if sum(
            w["m_Weight" + str(k)]
            for k in range(4)
            if w["m_BoneIndex" + str(k)] == head
        )
        > 0.8
    ]
    head_points = [vertices[i] for i in ids]
    low = min(v.z for v in head_points)
    high = max(v.z for v in head_points)
    h = high - low
    width = max(v.y for v in head_points) - min(v.y for v in head_points)
    center = (max(v.y for v in head_points) + min(v.y for v in head_points)) / 2
    surface = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
    pixels = list(texture.pixels)
    tw, th = texture.size
    n = 112
    ys = np.linspace(center - width * 0.08, center + width * 0.08, n)
    zs = np.linspace(low + h * 0.07, low + h * 0.33, n)
    values = np.full((n, n), np.nan)
    depths = np.zeros((n, n))
    for row, z in enumerate(zs):
        for col, y in enumerate(ys):
            hit, _, face, _ = surface.ray_cast(
                Vector((-3, float(y), float(z))), Vector((1, 0, 0))
            )
            if hit is None:
                continue
            indices = triangles[face]
            uv = geometry.barycentric_transform(
                hit,
                *[vertices[i] for i in indices],
                *[Vector((data["uv"][i]["x"], data["uv"][i]["y"], 0)) for i in indices],
            )
            x = min(tw - 1, max(0, int(uv.x * tw)))
            yy = min(th - 1, max(0, int(uv.y * th)))
            color = pixels[(yy * tw + x) * 4 : (yy * tw + x) * 4 + 3]
            values[row, col] = sum(
                a * b for a, b in zip(color, (0.2126, 0.7152, 0.0722))
            )
            depths[row, col] = hit.x
    median = float(np.nanmedian(values))
    mask = values < median * 0.48
    seen = set()
    components = []
    for r in range(n):
        for c in range(n):
            if not mask[r, c] or (r, c) in seen:
                continue
            stack = [(r, c)]
            seen.add((r, c))
            component = []
            while stack:
                rr, cc = stack.pop()
                component.append((rr, cc))
                for nr, nc in ((rr - 1, cc), (rr + 1, cc), (rr, cc - 1), (rr, cc + 1)):
                    if (
                        0 <= nr < n
                        and 0 <= nc < n
                        and mask[nr, nc]
                        and (nr, nc) not in seen
                    ):
                        seen.add((nr, nc))
                        stack.append((nr, nc))
            if (
                len(component) > 8
                and max(c for r, c in component) - min(c for r, c in component) > 4
            ):
                components.append(component)
    if not components:
        raise ValueError("No reliable mouth contrast region; manual landmark required.")
    components = [
        c
        for c in components
        if (max(ys[col] for row, col in c) - min(ys[col] for row, col in c))
        > 1.4 * (max(zs[row] for row, col in c) - min(zs[row] for row, col in c))
    ]
    if not components:
        raise ValueError("No horizontal mouth-shaped contrast region.")
    components.sort(key=lambda x: len(x), reverse=True)
    region = components[0]
    y0 = min(ys[c] for r, c in region)
    y1 = max(ys[c] for r, c in region)
    z0 = min(zs[r] for r, c in region)
    z1 = max(zs[r] for r, c in region)
    scale = float((y1 - y0) / 0.035)
    if not 0.45 < scale < 3:
        raise ValueError("Mouth scale outside expected range: " + str(scale))
    if y1 - y0 > width * 0.22:
        raise ValueError("Detected region too broad for a mouth.")
    point = Vector(
        (
            float(np.median([depths[r, c] for r, c in region])),
            float((y0 + y1) / 2),
            float(z0 + 0.0015 * scale),
        )
    )
    return (
        point,
        scale,
        dict(
            center=list(point),
            scale=scale,
            headHeight=h,
            mouthWidth=float(y1 - y0),
            mouthHeight=float(z1 - z0),
            pixels=len(region),
            skinMedian=median,
            components=len(components),
        ),
    )
