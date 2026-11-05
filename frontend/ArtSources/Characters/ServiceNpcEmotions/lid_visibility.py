"""Keep the smiling lid line on the front of its own moving eyelids."""

import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def points(values):
    return np.array([[v["x"], -v["z"], v["y"]] for v in values])


def frame(mesh, channel, weight):
    frames = sorted(
        [s for s in mesh["shapes"] if s["name"] == channel],
        key=lambda s: s.get("weight", 100),
    )
    previous = np.zeros((len(mesh["vertices"]), 3))
    previous_weight = 0
    for shape in frames:
        current = points(shape["deltaVertices"])
        current_weight = shape.get("weight", 100)
        if weight <= current_weight:
            t = (weight - previous_weight) / (current_weight - previous_weight)
            return previous * (1 - t) + current * t
        previous, previous_weight = current, current_weight
    return previous


def pose(mesh, side, weight, cheeks):
    vertices = points(mesh["vertices"])
    vertices += frame(mesh, "eyeBlink" + side, weight)
    vertices += frame(mesh, "eyeSquint" + side, weight)
    vertices += frame(mesh, "cheekRaiseLeft", cheeks)
    vertices += frame(mesh, "cheekRaiseRight", cheeks)
    return vertices


def keep_lid_lines_visible(authored):
    parts = {p["name"]: p for p in authored["parts"]}
    for side in ("Left", "Right"):
        line = parts[side + "LidLine"]
        for shape in line["shapes"]:
            if shape["name"] != "eyeSquint" + side:
                continue
            weight = shape.get("weight", 100)
            correction = np.zeros((len(line["vertices"]), 3))
            # Base rim is a four-vertex tube. When the closed U curve is lifted,
            # its middle can become thinner than one screen pixel. Retain a
            # continuous silhouette without changing neutral/blink geometry.
            line_pose = pose(line, side, weight, 0)
            sections = line_pose.reshape(-1, 4, 3)
            middle = sections[:, :, 2].mean(1, keepdims=True)
            thickness = np.ptp(sections[:, :, 2], axis=1, keepdims=True)
            minimum = 0.0024 * min(weight / 90, 1)
            scale = np.maximum(1, minimum / np.maximum(thickness, 1e-8))
            correction[:, 2] = ((sections[:, :, 2] - middle) * (scale - 1)).ravel()
            # Laugh and big laugh also raise the cheeks. Include that combined
            # surface rather than checking a line against the eyeball alone.
            for cheeks in (0, 60):
                line_pose = pose(line, side, weight, cheeks) + correction
                for suffix in ("UpperLid", "LowerLid"):
                    lid = parts[side + suffix]
                    tree = BVHTree.FromPolygons(
                        pose(lid, side, weight, cheeks).tolist(),
                        np.array(lid["triangles"]).reshape(-1, 3).tolist(),
                        all_triangles=True,
                    )
                    for i, point in enumerate(line_pose):
                        hit, _, _, _ = tree.ray_cast(
                            Vector((-1, point[1], point[2])), Vector((1, 0, 0))
                        )
                        if hit is not None:
                            correction[i, 0] = min(
                                correction[i, 0], hit.x - 0.0006 - point[0]
                            )
            delta = points(shape["deltaVertices"]) + correction
            shape["deltaVertices"] = [
                {"x": float(x), "y": float(z), "z": float(-y)} for x, y, z in delta
            ]
