"""존박 복사본에 공통 입·눈 모듈을 맞춘다. 원본 FBX는 읽기만 한다.

Unity JohnParkFaceBuilder.ExportSource 실행 뒤 Blender background에서 실행한다.
"""

import json
import runpy
import sys
from collections import defaultdict
from pathlib import Path


HERE = Path(__file__).resolve().parent
NAME = "LittleMung_JohnPark_Male"
FOLDER = HERE / NAME


def run_script(name, *args):
    script = HERE / name
    sys.argv = [str(script), "--", *map(str, args)]
    runpy.run_path(str(script), run_name="__main__")


def prepare_eyes():
    source = json.loads((FOLDER / "Surface.json").read_text())
    mouth = json.loads((FOLDER / "Face/FaceShapes.json").read_text())
    source.update(
        vertices=mouth["restVertices"],
        normals=mouth["restNormals"],
        triangles=mouth["triangles"],
    )
    folder = FOLDER / "Eyes"
    folder.mkdir(exist_ok=True)
    (folder / "Surface.json").write_text(json.dumps(source), encoding="utf-8")
    run_script("measure_base_eye_fit.py", folder)
    settings = json.loads((folder / "EyeFit.json").read_text())

    # 독립된 좌우 안구만 특정하고 얼굴·모자·눈썹은 보존한다.
    points = [(p["x"], -p["z"], p["y"]) for p in source["vertices"]]
    parent = list(range(len(points)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    # UV·노멀 경계에서 나뉜 동위치 정점도 연결성분 판정에서만 묶는다.
    positions = {}
    for i, point in enumerate(points):
        key = tuple(round(value, 6) for value in point)
        if key in positions:
            parent[find(i)] = find(positions[key])
        positions[key] = i
    faces = [source["triangles"][i : i + 3] for i in range(0, len(source["triangles"]), 3)]
    for face in faces:
        for i in face[1:]:
            parent[find(i)] = find(face[0])
    components = defaultdict(set)
    for i in set(source["triangles"]):
        components[find(i)].add(i)
    removed = set()
    for eye in settings["eyes"]:
        y, z = eye["center"]
        matches = []
        for indices in components.values():
            if not 100 < len(indices) < 2000:
                continue
            center = [
                (min(points[i][a] for i in indices) + max(points[i][a] for i in indices)) / 2
                for a in range(3)
            ]
            if abs(center[1] - y) < .025 and abs(center[2] - z) < .025 and center[0] < -.15:
                matches.append(indices)
        if len(matches) != 1:
            raise ValueError("원본 안구를 하나로 특정하지 못했습니다: " + eye["side"])
        removed.update(matches[0])
    source["triangles"] = [i for face in faces if not removed.intersection(face) for i in face]
    (folder / "Surface.json").write_text(json.dumps(source), encoding="utf-8")
    (folder / "RemovedEyeVertices.json").write_text(json.dumps(sorted(removed)))
    # 입의 절단면과 겹치지 않는 범위. 교체 영역 내부의 원본 눈꺼풀 틈도 제거한다.
    settings.update(name=NAME, patchScale=1.3, replaceEyeOpenings=True)
    (folder / "EyeFit.json").write_text(json.dumps(settings, indent=2), encoding="utf-8")
    run_script("create_business_eyelids.py", folder)


if __name__ == "__main__":
    run_script("fit_npc_mouth.py", NAME)
    prepare_eyes()
