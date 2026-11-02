"""Visual-only proposal: resize whole eyes while retaining original texture and iris shape."""

import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np

root = Path(__file__).resolve().parent
out = root / "WholeEyes"
out.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(root))
from face_authoring_common import aim, vec

names = (
    sys.argv[sys.argv.index("--") + 1 :]
    if "--" in sys.argv
    else [
        "LittleMung_MiddleAge_Male",
        "LittleMung_MiddleAge_Female",
        "LittleMung_BlackYoung_Male",
    ]
)
for name in names:
    dest = out / name
    dest.mkdir(exist_ok=True)
    data = json.loads((root / name / "Surface.json").read_text(encoding="utf-8-sig"))
    landmarks = json.loads((root / name / "EyeMeasurements.json").read_text())
    ys = landmarks["ys"]
    zs = landmarks["zs"]
    eyes = []
    points = np.array([vec(v)[:] for v in data["vertices"]])
    normals = np.array([vec(v)[:] for v in data["normals"]])
    for side, e in enumerate(landmarks["eyes"]):
        r0, r1, c0, c1 = e["eyeBounds"]
        if side == 0:
            c1 += 5
        else:
            c0 -= 5
        cy = (ys[c0] + ys[c1]) / 2
        cz = (zs[r0] + zs[r1]) / 2
        ry = (ys[c1] - ys[c0]) / 2
        rz = (zs[r1] - zs[r0]) / 2
        r = np.sqrt(((points[:, 1] - cy) / ry) ** 2 + ((points[:, 2] - cz) / rz) ** 2)
        ring = points[(r > 1.3) & (r < 1.7) & (points[:, 0] < -0.1)]
        cx = float(np.median(ring[:, 0]))
        eyes.append((np.array([cx, cy, cz]), ry, rz))

    def warp(points):
        result = points.copy()
        for center, ry, rz in eyes:
            radial = np.sqrt(
                ((points[:, 1] - center[1]) / ry) ** 2
                + ((points[:, 2] - center[2]) / rz) ** 2
            )
            t = np.clip((radial - 1.15) / 0.85, 0, 1)
            depth = np.clip((points[:, 0] - center[0] - 0.01) / 0.12, 0, 1)
            weight = (1 - t * t * (3 - 2 * t)) * (1 - depth * depth * (3 - 2 * depth))
            result += (points - center) * (-0.14 * weight[:, None])
        return result

    modified = warp(points)
    eps = 1e-5
    jac = np.stack(
        [
            (warp(points + np.eye(3)[i] * eps) - warp(points - np.eye(3)[i] * eps))
            / (2 * eps)
            for i in range(3)
        ],
        axis=2,
    )
    transformed = np.linalg.solve(np.swapaxes(jac, 1, 2), normals[..., None])[..., 0]
    transformed /= np.linalg.norm(transformed, axis=1)[:, None]
    assert np.linalg.det(jac).min() > 0.1, "Eye deformation folds the surface"

    def unity(v):
        return dict(x=float(v[0]), y=float(v[2]), z=float(-v[1]))

    (dest / "EyeSurface.json").write_text(
        json.dumps(
            dict(
                originalVertices=data["vertices"],
                vertices=[unity(v) for v in modified],
                normals=[unity(v) for v in transformed],
            ),
            separators=(",", ":"),
        )
    )
    (dest / "EyeSurfaceAudit.json").write_text(
        json.dumps(
            dict(
                minimumJacobian=float(np.linalg.det(jac).min()),
                changedVertices=int(
                    np.sum(np.linalg.norm(modified - points, axis=1) > 1e-7)
                ),
                totalVertices=len(points),
                scale=0.86,
            ),
            indent=2,
        )
    )
    face_data = json.loads((root / name / "Face/FaceShapes.json").read_text())
    surface_parts = []
    kept = np.array(face_data["triangles"]).reshape(-1, 3)
    edges = np.unique(
        np.sort(
            np.concatenate([kept[:, [0, 1]], kept[:, [1, 2]], kept[:, [2, 0]]]), axis=1
        ),
        axis=0,
    )
    starts = points[edges[:, 0]]
    segments = points[edges[:, 1]] - starts
    lengths = np.sum(segments * segments, axis=1)
    for part in face_data["parts"]:
        pv = np.array([vec(v)[:] for v in part["vertices"]])
        pn = np.array([vec(v)[:] for v in part["normals"]])
        moved = warp(pv)
        shift = moved - pv
        if not np.any(np.linalg.norm(shift, axis=1) > 1e-7):
            continue
        jacobian = np.stack(
            [
                (warp(pv + np.eye(3)[i] * eps) - warp(pv - np.eye(3)[i] * eps))
                / (2 * eps)
                for i in range(3)
            ],
            axis=2,
        )
        nn = np.linalg.solve(np.swapaxes(jacobian, 1, 2), pn[..., None])[..., 0]
        nn /= np.linalg.norm(nn, axis=1)[:, None]
        if part["name"] == "SpeakingLips":
            for i in range(len(pv) // 6):
                fraction = np.clip(
                    np.sum((pv[i] - starts) * segments, axis=1)
                    / np.maximum(lengths, 1e-20),
                    0,
                    1,
                )
                distance = np.linalg.norm(
                    starts + segments * fraction[:, None] - pv[i], axis=1
                )
                edge = int(np.argmin(distance))
                assert distance[edge] < 1e-5, "Mouth seam does not match a body edge"
                a, b = edges[edge]
                t = fraction[edge]
                moved[i] = modified[a] * (1 - t) + modified[b] * t
                nn[i] = transformed[a] * (1 - t) + transformed[b] * t
                nn[i] /= np.linalg.norm(nn[i])
        active = np.linalg.norm(moved - pv, axis=1) > 1e-7
        delta_max = max(
            [
                max(
                    [
                        np.linalg.norm(vec(v))
                        for v, a in zip(sh["deltaVertices"], active)
                        if a
                    ],
                    default=0,
                )
                for sh in part["shapes"]
            ],
            default=0,
        )
        assert delta_max < 1e-6, "Eye edit reaches an animated mouth vertex"
        print(
            part["name"],
            "moved",
            int(active.sum()),
            "affected shape delta",
            delta_max,
            flush=True,
        )
        surface_parts.append(
            dict(
                name=part["name"],
                originalVertices=part["vertices"],
                vertices=[unity(v) for v in moved],
                normals=[unity(v) for v in nn],
            )
        )
    (dest / "EyeSurface.json").write_text(
        json.dumps(
            dict(
                originalVertices=data["vertices"],
                vertices=[unity(v) for v in modified],
                normals=[unity(v) for v in transformed],
                parts=surface_parts,
            ),
            separators=(",", ":"),
        )
    )
    for stage in ["SmallerWholeEyes"]:
        bpy.ops.wm.open_mainfile(
            filepath=str(root / name / "Face" / (name + "_Face.blend"))
        )
        body = next(
            ob
            for ob in bpy.data.objects
            if ob.type == "MESH" and len(ob.data.vertices) == len(points)
        )
        body.name = name
        if stage == "SmallerWholeEyes":
            for key in body.data.shape_keys.key_blocks:
                values = np.array([v.co[:] for v in key.data])
                moved = warp(values)
                for vertex, co in zip(key.data, moved):
                    vertex.co = co
            body.data.normals_split_custom_set_from_vertices(transformed.tolist())
            for part in surface_parts:
                ob = next(
                    o
                    for o in bpy.data.objects
                    if o.type == "MESH" and o.name.split(".")[0] == part["name"]
                )
                offsets = [
                    vec(b) - vec(a)
                    for a, b in zip(part["originalVertices"], part["vertices"])
                ]
                for key in ob.data.shape_keys.key_blocks:
                    for vertex, delta in zip(key.data, offsets):
                        vertex.co += delta
                ob.data.normals_split_custom_set_from_vertices(
                    [vec(v) for v in part["normals"]]
                )
        scene = bpy.context.scene
        camera = scene.camera
        scene.cycles.samples = 16
        h = max(points[:, 2]) - min(points[:, 2])
        head_height = json.loads((root / name / "Face/Landmarks.json").read_text())[
            "headHeight"
        ]
        focus = max(points[:, 2]) - head_height / 2
        camera.data.ortho_scale = head_height * 1.35
        for label, angle in [("Front", 0), ("LeftOblique", 30), ("RightOblique", -30)]:
            a = math.radians(angle)
            camera.location = (-3 * math.cos(a), 3 * math.sin(a), focus)
            aim(camera, (0, 0, focus))
            scene.render.filepath = str(dest / (stage + "_" + label + ".png"))
            bpy.ops.render.render(write_still=True)
        if stage == "SmallerWholeEyes":
            bpy.ops.wm.save_as_mainfile(
                filepath=str(dest / (name + "_WholeEyes.blend"))
            )
    print(
        name,
        "Whole-eye authoring complete; import through the review builder",
        flush=True,
    )
