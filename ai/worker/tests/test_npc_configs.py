"""ai/tools/npc_configs.py: NPC 설정을 공통 인격 스키마 원본으로 검사 — 생성 모델(Pydantic)이 놓치는 규칙까지 —
하고, NPC마다 하나인 설정 파일을 백엔드 전달용 배열로 합친다."""

import json
import sys
from pathlib import Path

import pytest

from conftest import cafe_with, load_npc_examples

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_configs


def broken_places(config: dict) -> list[str]:
    return [where for where, _ in npc_configs.schema_errors(config)]


def write_files(folder: Path, files: dict[str, object]) -> Path:
    """files[파일 이름] = 내용. 글이면 그대로, 아니면 JSON으로 쓴다."""
    for name, content in files.items():
        text = content if isinstance(content, str) else json.dumps(content, ensure_ascii=False)
        (folder / name).write_text(text, encoding="utf-8")
    return folder


def test_contract_examples_pass_the_schema():
    for config in load_npc_examples():
        assert npc_configs.schema_errors(config) == []


def test_the_service_npc_files_pass_the_schema_and_merge():
    """ai/npcs의 서비스 NPC 프로필(NPC마다 파일 하나)이 원본 스키마를 지키고 백엔드 전달용 배열로 합쳐진다."""
    configs, problems = npc_configs.bundle(npc_configs.ROOT / "ai/npcs")

    assert problems == []
    assert "CAFE_STAFF" in [config["npcId"] for config in configs]


def test_an_active_npc_needs_a_voice():
    """조건 규칙: isActive가 true면 voiceId가 있어야 한다. 계약 예시처럼 비활성 NPC는 null이어도 된다."""
    silent = cafe_with("isActive", True)  # 예시의 voiceId는 null
    voiced = {**silent, "voiceId": "af_heart"}

    assert broken_places(silent) == ["$.voiceId"]
    assert broken_places(voiced) == []


@pytest.mark.parametrize(
    ("path", "value"),
    [
        ("npcProfile.values", ["같은 말", "같은 말"]),  # 목록 중복 금지
        ("npcProfile.fears", None),  # 생략은 되지만 null은 안 됨
        ("npcProfile.speechStyle.directness", None),
        ("npcProfile.profileVersion", "1"),  # 글을 숫자로 바꿔 받지 않음
        ("isActive", "false"),  # 글을 참거짓으로 바꿔 받지 않음
    ],
)
def test_rules_the_generated_models_miss_are_reported_where_they_break(path, value):
    assert set(broken_places(cafe_with(path, value))) == {f"$.{path}"}


def test_every_broken_rule_is_reported_at_once():
    """작성자가 한 번에 고칠 수 있게 첫 오류에서 멈추지 않고, 위치 순으로 모두 알려 준다."""
    config = cafe_with("npcProfile.fears", None)
    config["isActive"] = "false"

    assert broken_places(config) == ["$.isActive", "$.npcProfile.fears"]


def test_npc_files_are_merged_in_file_name_order(tmp_path):
    cafe, hotel, passerby = load_npc_examples()
    write_files(tmp_path, {"b_hotel.json": hotel, "a_cafe.json": cafe, "c_passerby.json": passerby})

    assert npc_configs.bundle(tmp_path) == ([cafe, hotel, passerby], [])


def test_the_same_npc_id_in_two_files_is_a_problem(tmp_path):
    cafe = load_npc_examples()[0]
    copied = cafe_with("npcProfile.name", "Mia")  # 파일을 복사해 고치다 npcId를 그대로 둔 경우
    write_files(tmp_path, {"a_cafe.json": cafe, "b_copied.json": copied})

    _, problems = npc_configs.bundle(tmp_path)

    assert problems == ["b_copied.json: npcId 'npc_cafe_001'를 a_cafe.json에서 이미 씀"]


def test_schema_errors_are_reported_with_the_file_name(tmp_path):
    write_files(tmp_path, {"bad.json": cafe_with("npcProfile.fears", None)})

    _, problems = npc_configs.bundle(tmp_path)

    assert problems == ["bad.json $.npcProfile.fears: None is not of type 'array'"]


def test_a_file_that_is_not_json_is_a_problem(tmp_path):
    write_files(tmp_path, {"broken.json": "{"})

    _, problems = npc_configs.bundle(tmp_path)

    assert problems == ["broken.json: JSON 형식이 아님 (줄 1, 칸 2)"]


def test_an_empty_folder_breaks_the_list_rule_of_the_schema(tmp_path):
    """NPC가 1명 이상이어야 한다는 규칙은 코드에 따로 적지 않고 스키마(NpcConfigurationList)를 그대로 쓴다."""
    assert npc_configs.bundle(tmp_path) == ([], ["합친 배열 $: [] should be non-empty"])


def npc_folder(tmp_path: Path, files: dict[str, object]) -> Path:
    folder = tmp_path / "npcs"
    folder.mkdir()
    return write_files(folder, files)


def test_cli_without_out_only_checks(tmp_path, capsys):
    cafe, hotel, _ = load_npc_examples()
    folder = npc_folder(tmp_path, {"cafe.json": cafe, "hotel.json": hotel})

    assert npc_configs.main([str(folder)]) == 0
    assert capsys.readouterr().out == "NPC 2명 검사 통과: npc_cafe_001, npc_hotel_001\n"
    assert list(tmp_path.iterdir()) == [folder]  # 아무 파일도 만들지 않음


def test_cli_writes_the_merged_list_to_the_out_file(tmp_path):
    """Windows PowerShell 5.1의 > 저장은 BOM을 붙여 JSON 읽기가 깨지므로 도구가 직접 UTF-8(BOM 없음)로 쓴다."""
    cafe, hotel, _ = load_npc_examples()
    folder = npc_folder(tmp_path, {"cafe.json": cafe, "hotel.json": hotel})
    out = tmp_path / "npc_configurations.json"

    assert npc_configs.main([str(folder), "--out", str(out)]) == 0
    assert json.loads(out.read_text(encoding="utf-8")) == [cafe, hotel]
    assert not out.read_bytes().startswith(b"\xef\xbb\xbf")


def test_cli_writes_nothing_when_there_is_a_problem(tmp_path, capsys):
    """문제가 있는 배열이 백엔드로 넘어가지 않도록 파일은 만들지 않고, 문제와 실패 코드만."""
    folder = npc_folder(tmp_path, {"bad.json": cafe_with("npcProfile.fears", None)})
    out = tmp_path / "npc_configurations.json"

    assert npc_configs.main([str(folder), "--out", str(out)]) == 1
    assert not out.exists()
    assert capsys.readouterr().err == "bad.json $.npcProfile.fears: None is not of type 'array'\n"
