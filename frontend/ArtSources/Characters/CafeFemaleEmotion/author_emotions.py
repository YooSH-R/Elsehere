"""카페 여자 표정 복사본. 기존 얼굴/UV/골격/발음 키를 보존하고 국소 변형만 추가한다.

Blender --background --factory-startup --python author_emotions.py -- --output <새 폴더>
다른 얼굴에는 눈·눈썹 기준점 재측정과 외형 검수가 필요하다.
"""

import argparse
import copy
import json
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

parser = argparse.ArgumentParser()
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--input", type=Path)
parser.add_argument("--eyes", type=Path)
parser.add_argument("--keep-eye-textures", action="store_true")
parser.add_argument("--settings", type=Path)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :])
settings = json.loads(args.settings.read_text()) if args.settings else {}
source = args.input or Path(__file__).resolve().parent.parent / "CafeFemaleFace"
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=True)
blend_path = output / "LittleMung_Cafe_Female_Emotions.blend"
json_path = output / "CafeFemaleEmotions.json"
if blend_path.exists() or json_path.exists():
    raise FileExistsError("기존 수정을 보호하기 위해 새로운 출력 폴더를 사용하세요.")
bpy.ops.wm.open_mainfile(filepath=str(source / "CafeFemaleFacePreview.blend"))
data = json.loads(
    (source / "CafeFemaleFaceShapes.json").read_text(encoding="utf-8-sig")
)
surface = json.loads(
    (source / "CafeFemaleSourceSurface.json").read_text(encoding="utf-8-sig")
)
families = (
    "browInnerUp",
    "browOuterUp",
    "browDown",
    "eyeSquint",
    "eyeWide",
    "cheekRaise",
    "mouthFrown",
    "mouthSmile",
)


def array(values):
    return np.array([[p["x"], -p["z"], p["y"]] for p in values], dtype=float)


def vectors(values):
    return [{"x": float(x), "y": float(z), "z": float(-y)} for x, y, z in values]


def fade(value, inner, outer):
    t = np.clip((value - inner) / (outer - inner), 0, 1)
    return 1 - t * t * (3 - 2 * t)


face_points = array(data["vertices"])
mouth_u, mouth_v = face_points[:, 1] + 0.043, face_points[:, 2] - 0.477
skin_samples = (
    (face_points[:, 0] < -0.15)
    & (abs(mouth_u) < 0.11)
    & (abs(mouth_v) < 0.07)
    & ((mouth_u / 0.045) ** 2 + (mouth_v / 0.025) ** 2 > 1)
)
mouth_fit = np.column_stack(
    [
        np.ones(len(face_points)),
        mouth_u,
        mouth_v,
        mouth_u**2,
        mouth_u * mouth_v,
        mouth_v**2,
    ]
)
mouth_surface = np.linalg.lstsq(
    mouth_fit[skin_samples], face_points[skin_samples, 0], rcond=None
)[0]


def mouth_depth(u, v):
    c = mouth_surface
    return c[0] + c[1] * u + c[2] * v + c[3] * u * u + c[4] * u * v + c[5] * v * v


def displacement(points, family, side):
    x, y, z = points.T
    direction = 1 if side == "Left" else -1
    eye_y = 0.082 if side == "Left" else -0.155
    dy = y - eye_y
    front = fade(x, -0.12, 0.01)
    result = np.zeros_like(points)
    if family == "mouthLaughOpen":
        return result
    elif family.startswith("brow"):
        brow_z = settings.get("brow" + side, 0.681)
        shift = brow_z - 0.681
        r = np.sqrt((dy / 0.080) ** 2 + ((z - brow_z) / 0.080) ** 2)
        influence = fade(r, 0.75, 1.55) * front
        # Brows may rotate strongly, but the lid rim and eyeball must not be
        # dragged down with the forehead. Keep the shared eye boundary fixed.
        influence *= 1 - fade(z, 0.600 + shift, 0.700 + shift)
        influence *= fade(z, 0.710 + shift, 0.755 + shift)
        outward = np.clip(0.5 + direction * dy / 0.07, 0, 1)
        if family == "browInnerUp":
            result[:, 2] = 0.044 * (1 - outward) * influence
        elif family == "browOuterUp":
            result[:, 2] = 0.045 * (0.15 + 0.85 * outward) * influence
        else:
            result[:, 2] = -0.030 * (0.1 + 0.9 * (1 - outward)) * influence
            result[:, 1] = -direction * 0.009 * influence
        if family != "browDown":
            # Ease raised brows below the fixed hairline instead of pulling
            # the hair and upper forehead into a fold.
            limit = 0.65 * np.maximum(0.735 + shift - z, 0)
            ratio = np.divide(
                result[:, 2], limit, out=np.zeros_like(z), where=limit > 0
            )
            result[:, 2] = limit * (1 - np.exp(-ratio))
        # Follow the rounded forehead while moving vertically.
        result[:, 0] = (z - 0.59) * 0.9 * result[:, 2]
    elif family in ("eyeSquint", "eyeWide"):
        r = np.sqrt((dy / 0.058) ** 2 + ((z - 0.573) / 0.048) ** 2)
        influence = fade(r, 1.05, 1.85) * front
        vertical = z - 0.573
        if family == "eyeSquint":
            # Iris and sclera stay fixed; separate lids cover the eye.
            pass
        else:
            result[:, 2] = 0.16 * vertical * influence
    elif family == "cheekRaise":
        r = np.sqrt((dy / 0.070) ** 2 + ((z - 0.535) / 0.027) ** 2)
        influence = fade(r, 0.35, 1.4) * front
        # Keep the eye itself still while lifting only the lower cheek.
        influence *= fade(z, 0.522, 0.538)
        result[:, 2] = 0.004 * influence
        result[:, 0] = -0.0015 * influence
    else:
        dy_mouth = y + 0.043
        r = np.sqrt((dy_mouth / 0.065) ** 2 + ((z - 0.479) / 0.040) ** 2)
        influence = fade(r, 0.5, 1.5) * front
        half = np.clip(0.5 + direction * dy_mouth / 0.025, 0, 1)
        # A quadratic arc has a horizontal tangent at the centre. An absolute
        # distance produces a sharp V in the closed lip crease.
        corner = np.clip((dy_mouth / 0.026) ** 2, 0, 1)
        if family == "mouthSmile":
            result[:, 2] = 0.015 * corner * influence * half
            result[:, 1] = direction * 0.005 * corner * influence * half
        else:
            result[:, 2] = -0.016 * corner * influence * half
    return result


def normal_offsets(points, normals, family, side):
    # Preserve imported smooth normals at seams; transform them by the local
    # deformation gradient instead of replacing them with triangle normals.
    gradient = np.repeat(np.eye(3)[None], len(points), axis=0)
    epsilon = 1e-5
    for axis in range(3):
        step = np.zeros(3)
        step[axis] = epsilon
        derivative = (
            displacement(points + step, family, side)
            - displacement(points - step, family, side)
        ) / (2 * epsilon)
        gradient[:, :, axis] += derivative
    changed = np.linalg.solve(gradient.transpose(0, 2, 1), normals[..., None])[..., 0]
    changed /= np.maximum(np.linalg.norm(changed, axis=1, keepdims=True), 1e-10)
    return changed - normals


def match_lip_boundary(
    points, delta, normal_delta, family, side, face_delta=None, face_normal_delta=None
):
    """Keep subdivided lip-border points on the deformed original face edges."""
    face = array(data["vertices"])
    face_normals = array(surface["normals"])
    triangles = np.array(data["triangles"]).reshape(-1, 3)
    edges = np.concatenate(
        [triangles[:, [0, 1]], triangles[:, [1, 2]], triangles[:, [2, 0]]]
    )
    first, direction = face[edges[:, 0]], face[edges[:, 1]] - face[edges[:, 0]]
    length2 = np.maximum(np.sum(direction * direction, axis=1), 1e-20)
    moved = displacement(face, family, side) if face_delta is None else face_delta
    normals_moved = (
        normal_offsets(face, face_normals, family, side)
        if face_normal_delta is None
        else face_normal_delta
    )
    count = len(points) // 6
    for i, point in enumerate(points[:count]):
        fraction = np.clip(np.sum((point - first) * direction, axis=1) / length2, 0, 1)
        distance = np.linalg.norm(first + fraction[:, None] * direction - point, axis=1)
        edge = int(np.argmin(distance))
        if distance[edge] > 1e-6:
            raise ValueError("입술 바깥 경계가 원본 면의 변에 없습니다.")
        a, b = edges[edge]
        t = fraction[edge]
        expected = moved[a] * (1 - t) + moved[b] * t
        expected_normal = normals_moved[a] * (1 - t) + normals_moved[b] * t
        correction = expected - delta[i]
        normal_correction = expected_normal - normal_delta[i]
        for ring, amount in enumerate([1, 0.8, 0.55, 0.3, 0.1, 0]):
            delta[ring * count + i] += correction * amount * amount
            normal_delta[ring * count + i] += normal_correction * amount * amount


eye_folder = args.eyes or Path(__file__).parent / "BaseEyes"
eye_data = json.loads((eye_folder / "Blink.json").read_text(encoding="utf-8"))
export = {
    "sourceVertexCount": len(data["vertices"]),
    "meshes": [],
    "parts": [],
    "bodyTriangles": eye_data["bodyTriangles"],
}
body = bpy.data.objects["CafeFemaleFacePreview"]
parts = {part["name"]: part for part in data["parts"]}
for ob in [body] + [bpy.data.objects[name] for name in parts]:
    if ob == body:
        positions = array(data["vertices"])
        normals = array(surface["normals"])
        mesh_name = "CafeFemaleFace"
    else:
        part = parts[ob.name]
        positions = array(part["vertices"])
        normals = (
            array(part["normals"])
            if len(part.get("normals", [])) == len(positions)
            else None
        )
        mesh_name = ob.name
    shapes = []
    for family in families:
        for side in ("Left", "Right"):
            # Closed lid lines hide inside the head until blink. Do not move
            # them independently from their original blink trajectory.
            if ob.name in ("ClosedLeftLid", "ClosedRightLid"):
                continue
            delta = displacement(positions, family, side)
            if np.max(np.linalg.norm(delta, axis=1)) < 1e-7:
                continue
            name = family + side
            normal_delta = (
                normal_offsets(positions, normals, family, side)
                if normals is not None
                else np.zeros_like(positions)
            )
            if ob.name == "SpeakingLips":
                match_lip_boundary(positions, delta, normal_delta, family, side)
            key = ob.data.shape_keys.key_blocks.get(name)
            if key is None:
                key = ob.shape_key_add(name=name)
            for point, value in zip(key.data, positions + delta):
                point.co = value
            shapes.append(
                {
                    "name": name,
                    "deltaVertices": vectors(delta),
                    "deltaNormals": vectors(normal_delta),
                }
            )
    export["meshes"].append(
        {
            "name": mesh_name,
            "vertices": copy.deepcopy(
                data["vertices"] if ob == body else parts[ob.name]["vertices"]
            ),
            "shapes": shapes,
        }
    )
    for key in ob.data.shape_keys.key_blocks:
        key.value = 0

# Reuse the reviewed Base eye construction: stationary eyes, eyelids with
# quarter-blink frames and a join that retains every old skin boundary vertex.
kept = {
    tuple(sorted(eye_data["bodyTriangles"][i : i + 3]))
    for i in range(0, len(eye_data["bodyTriangles"]), 3)
}
editable = bmesh.new()
editable.from_mesh(body.data)
remove = [
    face
    for face in editable.faces
    if tuple(sorted(v.index for v in face.verts)) not in kept
]
bmesh.ops.delete(editable, geom=remove, context="FACES_ONLY")
editable.to_mesh(body.data)
editable.free()
for name in ["eyeBlinkLeft", "eyeBlinkRight", "eyeWideLeft", "eyeWideRight"]:
    key = body.data.shape_keys.key_blocks.get(name)
    if key:
        for point, basis in zip(
            key.data, body.data.shape_keys.key_blocks["Basis"].data
        ):
            point.co = basis.co
for name in ["ClosedLeftLid", "ClosedRightLid"]:
    if name in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
with bpy.data.libraries.load(
    str(eye_folder / "LittleMung_Cafe_Female_Blink.blend"), link=False
) as (source_library, target_library):
    target_library.objects = [p["name"] for p in eye_data["parts"]]
for ob in target_library.objects:
    bpy.context.collection.objects.link(ob)
for part in eye_data["parts"]:
    name = part["name"]
    positions = array(part["vertices"])
    normals = array(part["normals"])
    ob = bpy.data.objects[name]
    shapes = []
    eye_tree = None
    if part["frames"]:
        center = 0.082 if part["channel"].endswith("Left") else -0.155
        eye = next(
            p
            for p in eye_data["parts"]
            if p["name"] == ("LeftEye" if center > 0 else "RightEye")
        )
        eye_tree = BVHTree.FromPolygons(
            array(eye["vertices"]).tolist(),
            np.array(eye["triangles"]).reshape(-1, 3).tolist(),
        )
        boundary_weight = np.ones(len(positions))
        if name.endswith("Lid"):
            rings = np.arange(len(positions)) // (len(positions) // 25)
            boundary_weight = np.clip(rings / 8, 0, 1)
    blink_frames = [
        dict(frame, weight=(90 if frame["weight"] == 100 else frame["weight"]))
        for frame in part["frames"]
    ]
    if part["frames"]:
        blink_frames.append(part["frames"][-1])
    for frame in blink_frames:
        closure = array(frame["vertices"])
        horizontal = np.clip(1 - ((closure[:, 1] - center) / 0.044) ** 2, 0, 1)
        ring = np.sqrt(
            ((positions[:, 1] - center) / 0.066) ** 2
            + ((positions[:, 2] - 0.573) / 0.060) ** 2
        )
        side = "Left" if center > 0 else "Right"
        smile_lift = settings.get("eyeSmileLift" + side, 0.020)
        lift = smile_lift * horizontal * fade(ring, 0.55, 1.35) * boundary_weight
        # Keep the lid in front of the eye over the full possible smile lift,
        # including partial closure, rather than checking only the end pose.
        for fraction in np.linspace(0, 1, 9):
            for i, point in enumerate(closure):
                hit, _, _, _ = eye_tree.ray_cast(
                    Vector((-1, point[1], point[2] + lift[i] * fraction)),
                    Vector((1, 0, 0)),
                )
                if hit is not None:
                    point[0] = min(
                        point[0], hit.x - (0.0016 if "Line" in name else 0.001)
                    )
        shapes.append(
            {
                "name": part["channel"],
                "weight": frame["weight"],
                "deltaVertices": vectors(closure - positions),
                "deltaNormals": vectors(array(frame["normals"]) - normals),
            }
        )
        key_name = part["channel"] + (
            "" if frame["weight"] == 100 else "_" + str(frame["weight"])
        )
        key = ob.data.shape_keys.key_blocks.get(key_name)
        if key is None:
            key = ob.shape_key_add(name=key_name)
        for point, value in zip(key.data, closure):
            point.co = value
    if part["frames"]:
        # Raise the closed lid curve for smiling eyes instead of a sleepy lid.
        # The eyeball itself has no blend shape.
        key_name = part["channel"].replace("eyeBlink", "eyeSquint")
        center = 0.082 if part["channel"].endswith("Left") else -0.155
        eye = next(
            p
            for p in eye_data["parts"]
            if p["name"] == ("LeftEye" if center > 0 else "RightEye")
        )
        eye_points = array(eye["vertices"])
        eye_tree = BVHTree.FromPolygons(
            eye_points.tolist(), np.array(eye["triangles"]).reshape(-1, 3).tolist()
        )
        # A linear chord passes through a convex eyeball. Keep quarter frames
        # on its front surface, including the raised laughing-eye curve.
        squint_frames = [
            dict(frame, weight=(90 if frame["weight"] == 100 else frame["weight"]))
            for frame in part["frames"]
        ]
        squint_frames.append(part["frames"][-1])
        for frame in squint_frames:
            amount = frame["weight"] / 100
            if frame["weight"] >= 90:
                amount = 1
            closed = array(part["frames"][-1]["vertices"])
            target = closed.copy()
            horizontal = np.clip(1 - ((target[:, 1] - center) / 0.044) ** 2, 0, 1)
            ring = np.sqrt(
                ((positions[:, 1] - center) / 0.066) ** 2
                + ((positions[:, 2] - 0.573) / 0.060) ** 2
            )
            boundary_weight = np.ones(len(positions))
            if name.endswith("Lid"):
                # The outer ring is shared with EyeJoin and must remain fixed.
                rings = np.arange(len(positions)) // (len(positions) // 25)
                boundary_weight = np.clip(rings / 8, 0, 1)
            target[:, 2] += (
                smile_lift
                * amount
                * horizontal
                * fade(ring, 0.55, 1.35)
                * boundary_weight
            )
            for point in target:
                hit, _, _, _ = eye_tree.ray_cast(
                    Vector((-1, point[1], point[2])), Vector((1, 0, 0))
                )
                if hit is not None:
                    point[0] = min(
                        point[0], hit.x - (0.0016 if "Line" in name else 0.001)
                    )
            # Squint is the smile-curve correction on top of eyeBlink closure.
            # Closing two independently interpolated lid meshes would reopen
            # or pierce the eye during a partial blink.
            delta = target - closed
            shapes.append(
                {
                    "name": key_name,
                    "weight": frame["weight"],
                    "deltaVertices": vectors(delta),
                    "deltaNormals": vectors(np.zeros_like(normals)),
                }
            )
            key = ob.shape_key_add(
                name=key_name + ("" if frame["weight"] == 100 else str(frame["weight"]))
            )
            for point, value in zip(key.data, positions + delta):
                point.co = value
        frame = part["frames"][0]
        key_name = part["channel"].replace("eyeBlink", "eyeWide")
        delta = (array(frame["vertices"]) - positions) * -0.35
        shapes.append(
            {
                "name": key_name,
                "weight": 100,
                "deltaVertices": vectors(delta),
                "deltaNormals": vectors((array(frame["normals"]) - normals) * -0.35),
            }
        )
        key = ob.shape_key_add(name=key_name)
        for point, value in zip(key.data, positions + delta):
            point.co = value
    # Move shared skin boundary points with the same local brow/cheek field.
    if not name.endswith("Eye"):
        for family in [
            "browInnerUp",
            "browOuterUp",
            "browDown",
            "cheekRaise",
            "mouthSmile",
            "mouthFrown",
        ]:
            for side in ["Left", "Right"]:
                delta = displacement(positions, family, side)
                if np.max(np.linalg.norm(delta, axis=1)) < 1e-7:
                    continue
                key_name = family + side
                key = ob.shape_key_add(name=key_name)
                for point, value in zip(key.data, positions + delta):
                    point.co = value
                shapes.append(
                    {
                        "name": key_name,
                        "weight": 100,
                        "deltaVertices": vectors(delta),
                        "deltaNormals": vectors(
                            normal_offsets(positions, normals, family, side)
                        ),
                    }
                )
    export["parts"].append(
        {
            "name": name,
            "vertices": part["vertices"],
            "triangles": part["triangles"],
            "normals": part["normals"],
            "uv": part["uv"],
            "texture": part["texture"],
            "shapes": shapes,
        }
    )
    import shutil

    if not (output / part["texture"]).exists():
        shutil.copyfile(eye_folder / part["texture"], output / part["texture"])

# Author a rounded laugh aperture and fair its existing skin boundary.
# Keep this correction separate from every neutral, smile and speech key.
lips = parts["SpeakingLips"]
lip_basis = array(lips["vertices"])
lip_aa = lip_basis + array(
    next(s for s in lips["shapes"] if s["name"] == "viseme_AA")["deltaVertices"]
)
count = len(lip_basis) // 6
sys.path.insert(0, str(Path(__file__).parent))
from laugh_geometry import bind_surface, round_boundary

body_triangles = np.array(export["bodyTriangles"]).reshape(-1, 3)
body_laugh_delta, body_laugh_normals, angle = round_boundary(
    face_points, array(surface["normals"]), body_triangles, lip_aa, mouth_depth
)
follow_body = bind_surface(
    face_points, body_triangles, body_laugh_delta, body_laugh_normals
)
cosine, sine = np.cos(angle), np.sin(angle)
rim = np.zeros((count, 3))
rim[:, 1] = -0.043 + 0.041 * cosine
rim[:, 2] = 0.480 + 0.004 * cosine**2 + np.where(sine >= 0, 0.006, 0.030) * sine
rim[:, 0] = mouth_depth(rim[:, 1] + 0.043, rim[:, 2] - 0.477)


def skin_normals(points):
    u, v = points[:, 1] + 0.043, points[:, 2] - 0.477
    c = mouth_surface
    normals = np.column_stack(
        [
            -np.ones(len(points)),
            c[1] + 2 * c[3] * u + c[4] * v,
            c[2] + c[4] * u + 2 * c[5] * v,
        ]
    )
    return normals / np.linalg.norm(normals, axis=1, keepdims=True)


for mesh_data in export["meshes"] + export["parts"]:
    name = mesh_data["name"]
    if name.endswith("Eye") or name in ("ClosedLeftLid", "ClosedRightLid"):
        continue
    original = data if name == "CafeFemaleFace" else parts.get(name, mesh_data)
    positions = array(mesh_data["vertices"])
    ob = body if name == "CafeFemaleFace" else bpy.data.objects[name]
    normals = array(
        surface["normals"] if name == "CafeFemaleFace" else original["normals"]
    )
    if len(normals) != len(positions):
        normals = np.array([tuple(vertex.normal) for vertex in ob.data.vertices])
    original_shapes = {s["name"]: s for s in original["shapes"]}
    aa = original_shapes.get("viseme_AA")
    aa_positions = positions + (array(aa["deltaVertices"]) if aa else 0)
    aa_normals = normals + (
        array(aa["deltaNormals"])
        if aa and len(aa["deltaNormals"]) == len(positions)
        else 0
    )
    delta, normal_delta = np.zeros_like(positions), np.zeros_like(positions)
    if name == "CafeFemaleFace":
        delta, normal_delta = body_laugh_delta, body_laugh_normals
    elif name.endswith("EyeJoin") or name == "SpeakingLips":
        delta, normal_delta = follow_body(aa_positions)
    target, target_normals = aa_positions + delta, aa_normals + normal_delta
    if name == "SpeakingLips":
        outer = target[:count].copy()
        outer_normals = target_normals[:count].copy()
        for ring_index, weight in enumerate([1, 0.8, 0.55, 0.3, 0.1, 0]):
            region = slice(ring_index * count, (ring_index + 1) * count)
            points = rim * (1 - weight) + outer * weight
            boundary_depth = outer[:, 0] - mouth_depth(
                outer[:, 1] + 0.043, outer[:, 2] - 0.477
            )
            smooth_weight = weight * weight * (3 - 2 * weight)
            points[:, 0] = (
                mouth_depth(points[:, 1] + 0.043, points[:, 2] - 0.477)
                + boundary_depth * smooth_weight
            )
            target[region] = points
            shading = (
                skin_normals(points) * (1 - smooth_weight)
                + outer_normals * smooth_weight
            )
            target_normals[region] = shading / np.linalg.norm(
                shading, axis=1, keepdims=True
            )
        # The shared edge must match exactly, including its custom normals.
        target[:count], target_normals[:count] = outer, outer_normals
    elif name == "MouthInterior":
        for ring_index, weight in enumerate([1, 0.8, 0.5, 0.2, 0]):
            region = slice(ring_index * count, (ring_index + 1) * count)
            points = rim.copy()
            points[:, 1] = -0.043 + (rim[:, 1] + 0.043) * weight
            points[:, 2] = 0.473 + (rim[:, 2] - 0.473) * weight
            points[:, 0] = mouth_depth(
                points[:, 1] + 0.043, points[:, 2] - 0.477
            ) + 0.024 * (1 - weight)
            target[region] = points
            target_normals[region] = skin_normals(points)
    elif name in ("UpperTeeth", "Tongue"):
        channel = "viseme_E" if name == "UpperTeeth" else "viseme_AA"
        visible = positions + array(original_shapes[channel]["deltaVertices"])
        target = visible.copy()
        target[:, 1] = -0.043 + (visible[:, 1] + 0.043) * 1.5
        target[:, 2] += 0.001 if name == "UpperTeeth" else -0.010
        if name == "Tongue":
            # Retargeted tongues vary in height. Keep the whole visible tongue
            # above the laugh opening instead of letting it exit below the lip.
            target[:, 2] += max(0, rim[:, 2].min() + 0.004 - target[:, 2].min())
        target[:, 0] = (
            mouth_depth(target[:, 1] + 0.043, target[:, 2] - 0.477)
            + 0.004
            + (visible[:, 0] - visible[:, 0].mean()) * 0.2
        )
        target_normals = aa_normals
    delta, normal_delta = target - aa_positions, target_normals - aa_normals
    if name == "CafeFemaleFace":
        face_laugh_delta, face_laugh_normal_delta = delta, normal_delta
    if name == "SpeakingLips":
        match_lip_boundary(
            positions,
            delta,
            normal_delta,
            "mouthLaughOpen",
            "",
            face_laugh_delta,
            face_laugh_normal_delta,
        )
    # Keep zero frames too, so Refresh replaces old corrective offsets.
    mesh_data["shapes"].append(
        {
            "name": "mouthLaughOpen",
            "weight": 100,
            "deltaVertices": vectors(delta),
            "deltaNormals": vectors(normal_delta),
        }
    )
    key = ob.shape_key_add(name="mouthLaughOpen")
    for point, value in zip(key.data, positions + delta):
        point.co = value

for side in () if args.keep_eye_textures else ("Left", "Right"):
    texture_name = side + "LidSkin.png"
    image = bpy.data.images.load(str(output / texture_name), check_existing=False)
    pixels = np.array(image.pixels[:]).reshape(image.size[1], image.size[0], 4)
    y, z = np.meshgrid(
        np.linspace(-0.13, 0.13, image.size[0]), np.linspace(-0.13, 0.13, image.size[1])
    )
    radius = np.sqrt((y / 0.075) ** 2 + (z / 0.070) ** 2)
    clean = radius < 0.65
    coordinates = np.stack([np.ones_like(y), y, z], axis=-1)
    skin_fit = np.linalg.lstsq(coordinates[clean], pixels[clean, :3], rcond=None)[0]
    expected = np.clip(coordinates @ skin_fit, 0, 1)
    # Tiny old painted eye-rim flecks outside the original bake's clean ellipse
    # become visible when the lid moves. Keep brow/hair pixels outside this patch.
    blend = np.clip((expected[:, :, 0] - pixels[:, :, 0] - 0.035) / 0.07, 0, 1) * fade(
        radius, 0.95, 1.12
    )
    pixels[:, :, :3] = (
        pixels[:, :, :3] * (1 - blend[..., None]) + expected * blend[..., None]
    )
    image.pixels.foreach_set(pixels.astype(np.float32).reshape(-1))
    image.filepath_raw = str(output / texture_name)
    image.file_format = "PNG"
    image.save()
    image.pack()
    for ob in target_library.objects:
        for material in ob.data.materials:
            for node in material.node_tree.nodes:
                if (
                    node.type == "TEX_IMAGE"
                    and Path(node.image.filepath).name == texture_name
                ):
                    node.image = image
json_path.write_text(json.dumps(export, separators=(",", ":")), encoding="utf-8")
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
print(
    "기존 메시·발음·중립 보존. 추가 채널:",
    [(m["name"], len(m["shapes"])) for m in export["meshes"]],
)
