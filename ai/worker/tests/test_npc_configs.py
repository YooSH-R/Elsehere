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
    assert [config["npcId"] for config in configs] == [
        "CAFE_STAFF",
        "HOTEL_STAFF",
        "POLICE_OFFICER",
        "SHOP_STAFF",
        "TAXI_DRIVER",
        "TOWN_PASSERBY",
    ]


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


CAFE_SCENARIO = {"scenarioId": "cafe_order", "locationId": "cafe", "situation": "At the counter.", "facts": ["Latte is $5."]}
HOTEL_SCENARIO = {"scenarioId": "check_in", "locationId": "hotel", "situation": "At the front desk.", "facts": []}


def scenario_folder(tmp_path: Path, links: dict[str, str], scenarios: dict[str, object]) -> Path:
    """NPC 폴더 아래 scenarios/에 서비스 상황 연결(service_scenarios.json)과 상황 파일들을 둔다."""
    folder = tmp_path / "npcs"
    (folder / "scenarios").mkdir(parents=True)
    write_files(folder / "scenarios", {"service_scenarios.json": links, **{f"{name}.json": s for name, s in scenarios.items()}})
    return folder


def test_service_scenarios_are_collected_by_npc_id(tmp_path):
    cafe, hotel, _ = load_npc_examples()
    folder = scenario_folder(
        tmp_path,
        {"npc_cafe_001": "cafe_order", "npc_hotel_001": "check_in"},
        {"cafe_order": CAFE_SCENARIO, "check_in": HOTEL_SCENARIO},
    )

    assert npc_configs.service_scenarios(folder, [cafe, hotel]) == (
        {"npc_cafe_001": CAFE_SCENARIO, "npc_hotel_001": HOTEL_SCENARIO},
        [],
    )


@pytest.mark.parametrize(
    ("links", "scenarios", "problem"),
    [
        (
            {"npc_cafe_001": "cafe_order"},
            {"cafe_order": CAFE_SCENARIO},
            "npc_hotel_001: 서비스 상황 연결이 없음 (scenarios/service_scenarios.json)",
        ),
        (
            {"npc_cafe_001": "cafe_order", "npc_hotel_001": "check_in", "npc_ghost": "check_in"},
            {"cafe_order": CAFE_SCENARIO, "check_in": HOTEL_SCENARIO},
            "scenarios/service_scenarios.json: 설정 파일에 없는 npcId 'npc_ghost'",
        ),
        (
            {"npc_cafe_001": "cafe_order", "npc_hotel_001": "nowhere"},
            {"cafe_order": CAFE_SCENARIO},
            "npc_hotel_001: 상황 파일이 없음 (scenarios/nowhere.json)",
        ),
        (
            {"npc_cafe_001": "cafe_order", "npc_hotel_001": "check_in"},
            {"cafe_order": CAFE_SCENARIO, "check_in": {**HOTEL_SCENARIO, "mood": "busy"}},
            "scenarios/check_in.json mood: Extra inputs are not permitted",
        ),
        (
            {"npc_cafe_001": "check_in", "npc_hotel_001": "check_in"},
            {"check_in": HOTEL_SCENARIO},
            "npc_cafe_001: 상황 장소 'hotel'이 NPC 기본 장소 'cafe'와 다름",
        ),
    ],
)
def test_service_scenario_problems_are_reported_with_their_cause(tmp_path, links, scenarios, problem):
    """상황은 Worker 추론 API가 받는 형식(app.npc_respond.Scenario)으로 검사한다. 백엔드가 보낸 상황을 Worker가 거절하지 않게."""
    cafe, hotel, _ = load_npc_examples()
    folder = scenario_folder(tmp_path, links, scenarios)

    _, problems = npc_configs.service_scenarios(folder, [cafe, hotel])

    assert problems == [problem]


def test_cli_writes_nothing_when_there_is_a_problem(tmp_path, capsys):
    """문제가 있는 배열이 백엔드로 넘어가지 않도록 파일은 만들지 않고, 문제와 실패 코드만."""
    folder = npc_folder(tmp_path, {"bad.json": cafe_with("npcProfile.fears", None)})
    out = tmp_path / "npc_configurations.json"

    assert npc_configs.main([str(folder), "--out", str(out)]) == 1
    assert not out.exists()
    assert capsys.readouterr().err == "bad.json $.npcProfile.fears: None is not of type 'array'\n"


def test_cli_writes_the_service_scenarios_next_to_the_merged_list(tmp_path):
    cafe, hotel, _ = load_npc_examples()
    folder = scenario_folder(
        tmp_path,
        {"npc_cafe_001": "cafe_order", "npc_hotel_001": "check_in"},
        {"cafe_order": CAFE_SCENARIO, "check_in": HOTEL_SCENARIO},
    )
    write_files(folder, {"cafe.json": cafe, "hotel.json": hotel})
    out, scenarios_out = tmp_path / "npc-configurations.json", tmp_path / "npc-scenarios.json"

    assert npc_configs.main([str(folder), "--out", str(out), "--scenarios-out", str(scenarios_out)]) == 0
    assert json.loads(out.read_text(encoding="utf-8")) == [cafe, hotel]
    assert json.loads(scenarios_out.read_text(encoding="utf-8")) == {"npc_cafe_001": CAFE_SCENARIO, "npc_hotel_001": HOTEL_SCENARIO}


def test_cli_writes_neither_file_when_a_service_scenario_has_a_problem(tmp_path, capsys):
    """설정은 맞아도 상황이 틀리면 백엔드가 대화를 시작할 수 없으므로 둘 다 쓰지 않는다."""
    cafe, hotel, _ = load_npc_examples()
    folder = scenario_folder(tmp_path, {"npc_cafe_001": "cafe_order"}, {"cafe_order": CAFE_SCENARIO})
    write_files(folder, {"cafe.json": cafe, "hotel.json": hotel})
    out, scenarios_out = tmp_path / "npc-configurations.json", tmp_path / "npc-scenarios.json"

    assert npc_configs.main([str(folder), "--out", str(out), "--scenarios-out", str(scenarios_out)]) == 1
    assert not out.exists() and not scenarios_out.exists()
    assert capsys.readouterr().err == "npc_hotel_001: 서비스 상황 연결이 없음 (scenarios/service_scenarios.json)\n"


BACKEND_CONTENT = npc_configs.ROOT / "backend/src/main/resources/content"
REGENERATE = (
    "ai/worker/.venv/Scripts/python ai/tools/npc_configs.py ai/npcs"
    " --out backend/src/main/resources/content/npc-configurations.json"
    " --scenarios-out backend/src/main/resources/content/npc-scenarios.json"
)


def service_npcs() -> tuple[list[dict], dict[str, dict]]:
    configs, problems = npc_configs.bundle(npc_configs.ROOT / "ai/npcs")
    scenarios, scenario_problems = npc_configs.service_scenarios(npc_configs.ROOT / "ai/npcs", configs)
    assert problems + scenario_problems == []
    return configs, scenarios


def test_the_service_npcs_each_have_a_service_scenario():
    _, scenarios = service_npcs()

    assert {npc_id: s["scenarioId"] for npc_id, s in scenarios.items()} == {
        "CAFE_STAFF": "cafe_counter",
        "HOTEL_STAFF": "hotel_front_desk",
        "POLICE_OFFICER": "police_front_desk",
        "SHOP_STAFF": "drugstore_counter",
        "TAXI_DRIVER": "taxi_ride",
        "TOWN_PASSERBY": "town_street",
    }


def test_the_service_npcs_each_have_their_own_voice():
    """한 목소리를 모두 쓰면 남자 인물도 여자 목소리로 말한다. 인물 성별·성격에 맞춘 Kokoro 목소리를 NPC마다 따로 둔다."""
    configs, _ = service_npcs()

    assert {config["npcId"]: config["voiceId"] for config in configs} == {
        "CAFE_STAFF": "af_heart",  # Mia: 밝은 여자
        "HOTEL_STAFF": "am_onyx",  # Jake: 낮고 무덤덤한 남자
        "POLICE_OFFICER": "am_eric",  # Dan: 차분하고 믿음직한 남자
        "SHOP_STAFF": "am_michael",  # Alex: 차분한 남자
        "TAXI_DRIVER": "am_puck",  # Joe: 밝고 수다스러운 남자
        "TOWN_PASSERBY": "am_fenrir",  # Ben: 분명하고 단호한 남자
    }


def test_the_backend_content_files_match_the_npc_sources():
    """백엔드 시드용 생성 파일(백엔드 연결은 별도 작업). ai/npcs를 고쳤으면 저장소 최상위에서 REGENERATE 명령으로 다시 만든다."""
    configs, scenarios = service_npcs()

    assert json.loads((BACKEND_CONTENT / "npc-configurations.json").read_text(encoding="utf-8")) == configs, REGENERATE
    assert json.loads((BACKEND_CONTENT / "npc-scenarios.json").read_text(encoding="utf-8")) == scenarios, REGENERATE
