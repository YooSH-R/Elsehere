"""Keep smiling eyelids in front of the eye when cheek and closure mix."""

import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

from lid_visibility import frame, points


def blink_surface(part, side):
    if "shapes" in part:
        return part
    # A newly fitted Base eye stores absolute blink frames; an existing
    # runtime face stores blend shape offsets. Accept both authoring inputs.
    basis = points(part["vertices"])
    shapes = []
    for pose in part.get("frames", []):
        delta = points(pose["vertices"]) - basis
        shapes.append(
            {
                "name": "eyeBlink" + side,
                "weight": pose["weight"],
                "deltaVertices": [
                    {"x": float(x), "y": float(z), "z": float(-y)}
                    for x, y, z in delta
                ],
            }
        )
    return dict(part, shapes=shapes)


def keep_smiling_lids_in_front(authored, originals):
    parts = {part["name"]: part for part in authored["parts"]}
    for side in ("Left", "Right"):
        eye = originals[side + "Eye"]
        eye_points = points(eye["vertices"])
        tree = BVHTree.FromPolygons(
            eye_points.tolist(),
            np.array(eye["triangles"]).reshape(-1, 3).tolist(),
            all_triangles=True,
        )
        margin = np.ptp(eye_points[:, 2]) * 0.012
        for suffix in ("UpperLid", "LowerLid"):
            part = parts[side + suffix]
            original = blink_surface(originals[part["name"]], side)
            basis = points(part["vertices"])
            cheeks = frame(part, "cheekRaise" + side, 100)
            # The first ring joins the skin and must remain stationary here.
            first_inner = len(basis) // 25
            for shape in part["shapes"]:
                if shape["name"] != "eyeSquint" + side:
                    continue
                weight = shape.get("weight", 100)
                delta = points(shape["deltaVertices"])
                closures = [weight * ratio for ratio in (0.5, 0.55, 0.6, 0.75, 1)] + [40]
                for closure in closures:
                    closed = basis + frame(original, "eyeBlink" + side, closure)
                    for raised in (0, 0.25, 0.6, 0.8, 1):
                        positions = closed + delta + cheeks * raised
                        for i in range(first_inner, len(positions)):
                            point = positions[i]
                            hit, _, _, _ = tree.ray_cast(
                                Vector((-10, point[1], point[2])), Vector((1, 0, 0))
                            )
                            if hit is not None:
                                delta[i, 0] = min(
                                    delta[i, 0], delta[i, 0] + hit.x - margin - point[0]
                                )
                shape["deltaVertices"] = [
                    {"x": float(x), "y": float(z), "z": float(-y)}
                    for x, y, z in delta
                ]
