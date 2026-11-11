"""공통 감정 저작 결과에서 얼굴과 분리된 모자·머리카락·소품을 고정한다."""

import argparse
from collections import defaultdict
import gzip
import json
from pathlib import Path
import sys

import bpy
import numpy as np


def points(values):
    return np.array([[v["x"], v["y"], v["z"]] for v in values])


def vectors(values):
    return [dict(x=float(x), y=float(y), z=float(z)) for x, y, z in values]


def components(vertices, triangles):
    parent = list(range(len(vertices)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    positions = {}
    for index, vertex in enumerate(vertices):
        key = tuple(np.round(vertex, 6))
        if key in positions:
            parent[find(index)] = find(positions[key])
        positions[key] = index
    for a, b, c in np.array(triangles).reshape(-1, 3):
        parent[find(b)] = parent[find(a)] = find(c)
    groups = defaultdict(list)
    for i in set(triangles):
        groups[find(i)].append(i)
    return list(groups.values())


def protect(source, authored):
    body = next(m for m in source["meshes"] if m["path"] == source["body"])
    target = next(m for m in authored["meshes"] if m["path"] == source["body"])
    vertices = points(body["vertices"])
    groups = components(vertices, body["triangles"])
    lips = points(next(m for m in source["meshes"] if m["name"] == "SpeakingLips")["vertices"])
    head = min(groups, key=lambda ids: np.linalg.norm(vertices[ids] - lips.mean(0), axis=1).min())
    if not 3000 < len(head) < 6000:
        raise ValueError("존박의 얼굴 피부 연결성분을 특정하지 못했습니다.")
    brows = []
    for side in ("Left", "Right"):
        eye = points(next(m for m in source["meshes"] if m["name"] == side + "Eye")["vertices"])
        center = (eye.min(0) + eye.max(0)) / 2
        matches = [ids for ids in groups if 100 < len(ids) < 700
                   and abs(vertices[ids].mean(0)[2] - center[2]) < .05
                   and .11 < vertices[ids].mean(0)[1] - center[1] < .18
                   and vertices[ids].mean(0)[0] < -.20]
        if len(matches) != 1:
            raise ValueError("눈썹 연결성분을 하나로 특정하지 못했습니다: " + side)
        brows.extend(matches[0])
    mask = np.zeros(len(vertices))
    mask[head] = 1
    mask[brows] = 1
    # 눈썹은 회전하되, 모자 바로 아래 피부는 고정된 두피로 부드럽게 이어진다.
    mask *= np.clip((.76 - vertices[:, 1]) / .04, 0, 1)
    for shape in target["shapes"]:
        if shape["name"].startswith("viseme_"):
            raise ValueError("기존 발음 모양은 수정하지 않습니다.")
        for field in ("deltaVertices", "deltaNormals"):
            shape[field] = vectors(points(shape[field]) * mask[:, None])
    return [target], dict(faceVertices=len(head), browVertices=len(brows), protectedVertices=int(np.sum(mask == 0)),
                          protectedVertexIndices=np.flatnonzero(mask == 0).tolist())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    source = json.loads(args.source.read_text(encoding="utf-8-sig"))
    with gzip.open(args.input / "Emotions.json.gz", "rt") as stream:
        authored = json.load(stream)
    modified, report = protect(source, authored)
    bpy.ops.wm.open_mainfile(filepath=str(args.input / (source["name"] + "_Emotions.blend")))
    for target in modified:
        if target["name"] == "CafeFemaleFace":
            ob = next(ob for ob in bpy.data.objects if ob.type == "MESH" and len(ob.data.vertices) == len(target["vertices"]))
        else:
            ob = bpy.data.objects[target["name"]]
        base = points(target["vertices"])
        for shape in target["shapes"]:
            key = ob.data.shape_keys.key_blocks.get(shape["name"])
            if key is None:
                raise ValueError("Blender 감정 키가 빠졌습니다: " + shape["name"])
            updated = base + points(shape["deltaVertices"])
            for vertex, (x, y, z) in zip(key.data, updated):
                vertex.co = (x, -z, y)
    args.output.mkdir(parents=True, exist_ok=True)
    with gzip.GzipFile(filename=str(args.output / "Emotions.json.gz"), mode="wb", mtime=0) as stream:
        stream.write(json.dumps(authored, separators=(",", ":")).encode())
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output / (source["name"] + "_Emotions.blend")))
    (args.output / "BoundaryProtection.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print({key: value for key, value in report.items() if key != "protectedVertexIndices"})


if __name__ == "__main__":
    main()
