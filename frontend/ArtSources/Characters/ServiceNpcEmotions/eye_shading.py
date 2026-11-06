"""Blend the replacement lids and surrounding skin into one smooth normal field.

Sparse original eye-rim normals must not fan out across the large EyeJoin
triangles. Keep positions, UVs, eye size and weights; fit only local shading.
"""

import numpy as np


def array(values):
    return np.array([[v[k] for k in ("x", "y", "z")] for v in values])


def vectors(values):
    return [dict(zip(("x", "y", "z"), row)) for row in values.tolist()]


def unit(values):
    return values / np.maximum(np.linalg.norm(values, axis=1, keepdims=True), 1e-12)


def fade(value, first, last):
    t = np.clip((value - first) / (last - first), 0, 1)
    return 1 - t * t * (3 - 2 * t)


def smooth_eye_shading(body, parts):
    vertices, normals = array(body["vertices"]), array(body["normals"])
    active = np.zeros(len(vertices), bool)
    active[np.unique(body["triangles"])] = True
    joins = [array(parts[side + "EyeJoin"]["vertices"]) for side in ("Left", "Right")]
    midline = np.mean([(v[:, 2].min() + v[:, 2].max()) / 2 for v in joins])
    fits = []
    for join in joins:
        center = (join.min(0) + join.max(0)) / 2
        scale = np.ptp(join[:, 1]) / 0.18
        dy = (vertices[:, 1] - center[1]) / scale
        dz = (vertices[:, 2] - center[2]) / scale
        radius = (dy / 0.11) ** 2 + (dz / 0.12) ** 2
        mask = (
            (radius < 2.3)
            & (vertices[:, 0] < center[0] + 0.07 * scale)
            & (normals[:, 0] < -0.4)
            & active
            & (dy > -0.09)
            & (dy < 0.13)
            & (abs(vertices[:, 2] - midline) > 0.035 * scale)
        )
        if mask.sum() < 12:
            raise ValueError("눈 주변 피부 음영을 맞출 표면 표본이 부족합니다.")
        design = np.c_[np.ones(len(vertices)), dy, dz, dy * dy, dy * dz, dz * dz]
        coefficients = np.linalg.lstsq(
            design[mask], vertices[mask, 0] / scale, rcond=None
        )[0]
        fits.append((center, scale, coefficients))
    for mesh in [body] + list(parts.values()):
        if mesh is not body and not mesh["name"].endswith(
            ("EyeJoin", "UpperLid", "LowerLid")
        ):
            continue
        vertices, baseline = array(mesh["vertices"]), array(mesh["normals"])
        changed = baseline.copy()
        for center, scale, c in fits:
            dy = (vertices[:, 1] - center[1]) / scale
            dz = (vertices[:, 2] - center[2]) / scale
            fitted = unit(
                np.c_[
                    -np.ones(len(vertices)),
                    c[1] + 2 * c[3] * dy + c[4] * dz,
                    c[2] + c[4] * dy + 2 * c[5] * dz,
                ]
            )
            radius = np.sqrt((dy / 0.095) ** 2 + (dz / 0.105) ** 2)
            influence = (
                fade(radius, 0.95, 1.65)
                * fade((vertices[:, 0] - center[0]) / scale, 0.05, 0.10)
                * (1 - fade(dy, -0.12, -0.08))
                * fade(dy, 0.09, 0.13)
                * (1 - fade(abs(vertices[:, 2] - midline) / scale, 0.025, 0.05))
            )
            changed = unit(
                changed * (1 - influence[:, None]) + fitted * influence[:, None]
            )
        # Rotate the existing normal deltas with the basis, including blink frames.
        # Position deltas and speech shapes are not altered by a shading repair.
        cross = np.cross(baseline, changed)
        cosine = (baseline * changed).sum(1)[:, None]
        for shape in mesh.get("shapes", []):
            delta = array(shape["deltaNormals"])
            rotated = (
                delta
                + np.cross(cross, delta)
                + np.cross(cross, np.cross(cross, delta))
                / np.maximum(1 + cosine, 1e-8)
            )
            shape["deltaNormals"] = vectors(rotated)
        mesh["normals"] = vectors(changed)
