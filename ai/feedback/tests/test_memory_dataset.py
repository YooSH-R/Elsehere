"""S15P31A305-130: 독립 JSON 회귀 자료와 외부 호출 없는 검사기."""

import copy
import json
import socket
from pathlib import Path

import pytest
from run_memory_regression import main

from feedback.memory_dataset import DatasetError, check_cases, load_cases


@pytest.fixture(autouse=True)
def no_network(monkeypatch):
    def blocked(*args, **kwargs):
        raise AssertionError("데이터셋 검사는 네트워크를 사용할 수 없습니다")

    monkeypatch.setattr(socket.socket, "connect", blocked)
    monkeypatch.setattr(socket, "create_connection", blocked)


def fixture_case():
    scope = {"user_id": "synthetic_user", "npc_id": "cafe_npc", "conversation_id": "c1"}
    header = {
        "schema_version": "0.1.0",
        "request_id": "r1",
        "scope": scope,
        "conversation_revision": 1,
        "analysis_config_version": "memory_dataset_v1",
    }
    request = {
        **header,
        "context": {
            "scene_id": "cafe",
            "mode": "free_chat",
            "scene_description": "Synthetic cafe.",
            "target_language": "en-US",
            "explanation_language": "ko-KR",
            "end_reason": "user_end",
        },
        "turns": [
            {
                "turn_id": "t1",
                "sequence": 1,
                "speaker": "user",
                "input_type": "text",
                "text_status": "final",
                "original_transcript": None,
                "final_text": "My favorite drink is tea.",
                "difficulty": {
                    "settings_version": "s1",
                    "vocabulary_profile": "simple",
                    "speech_rate_multiplier": 1.0,
                    "hint_policy": "on_request",
                },
                "assistance_ids": [],
            }
        ],
        "assistance_events": [],
        "memory_snapshot": {"version": 0, "facts": []},
    }
    response = {
        **header,
        "status": "completed",
        "base_memory_version": 0,
        "result": {
            "summary": None,
            "changes": [
                {
                    "action": "add",
                    "fact_key": "preference.favorite_drink",
                    "kind": "in_game_preference",
                    "content": "Prefers tea.",
                    "evidence": [{"turn_id": "t1", "quote": "My favorite drink is tea."}],
                }
            ],
        },
        "problem": None,
    }
    return copy.deepcopy(
        {
            "dataset_version": "memory-regression-1.0",
            "case_id": "G01",
            "case_type": "semantic_reference",
            "title_ko": "명시적 선호",
            "source": "synthetic",
            "tags": ["preference", "cafe"],
            "rubric": {"must": ["차 선호 추가"], "must_not": ["다른 음료 선호 추측"]},
            "request": request,
            "fixture_response": response,
            "current": {"scope": scope, "snapshot": request["memory_snapshot"], "conversation_revision": 1},
            "policy": {
                "allowed_keys": {"preference.favorite_drink": "in_game_preference"},
                "max_changes": 5,
                "max_active_facts": 20,
                "max_content_chars": 180,
            },
            "expected": {
                "status": "ready",
                "next_memory_version": 1,
                "active_values": {"preference.favorite_drink": {"kind": "in_game_preference", "content": "Prefers tea."}},
                "change_targets": [{"action": "add", "fact_key": "preference.favorite_drink"}],
            },
        }
    )


def save_case(directory, case, name="case.json"):
    path = directory / name
    path.write_text(json.dumps(case, ensure_ascii=False), encoding="utf-8")
    return path


def test_load_and_check_valid_reference_without_mutating_input(tmp_path):
    save_case(tmp_path, fixture_case())
    cases = load_cases(tmp_path)
    original = copy.deepcopy(cases)
    report = check_cases(cases)
    assert report["mode"] == "fixed_fixture_check"
    assert report["actual_model_evaluated"] is False and report["external_calls"] == 0
    assert report["total"] == report["passed"] == 1 and report["failed"] == 0
    assert report["results"][0] == {"case_id": "G01", "case_type": "semantic_reference", "passed": True, "issues": []}
    assert cases == original


def test_expected_policy_error_is_a_pass():
    case = fixture_case()
    case.update(case_id="P01", case_type="policy_regression", expected={"error_code": "KEY_OR_KIND_NOT_ALLOWED"})
    case["fixture_response"]["result"]["changes"][0]["fact_key"] = "mission.completed"
    assert check_cases([case])["passed"] == 1


def test_unexpected_policy_error_is_a_failed_result_not_a_crash():
    case = fixture_case()
    case["fixture_response"]["result"]["changes"][0]["fact_key"] = "mission.completed"
    result = check_cases([case])
    assert result["failed"] == 1
    assert result["results"][0]["issues"] == ["unexpected_error:KEY_OR_KIND_NOT_ALLOWED"]


def test_expected_error_must_not_pass_when_candidate_is_accepted():
    case = fixture_case()
    case.update(case_type="policy_regression", expected={"error_code": "INVALID_CONTRACT"})
    result = check_cases([case])
    assert result["failed"] == 1 and result["results"][0]["issues"] == ["expected_error_not_raised:INVALID_CONTRACT"]


@pytest.mark.parametrize("field", ["status", "next_memory_version", "active_values", "change_targets"])
def test_wrong_gold_expectation_is_reported(field):
    case = fixture_case()
    wrong = {"status": "no_change", "next_memory_version": 7, "active_values": {}, "change_targets": []}
    case["expected"][field] = wrong[field]
    result = check_cases([case])
    assert result["failed"] == 1 and result["results"][0]["issues"] == [f"mismatch:{field}"]


@pytest.mark.parametrize("problem", ["empty", "duplicate", "wrong_version", "missing_expected", "not_synthetic", "unknown_field"])
def test_invalid_dataset_is_rejected_before_checking(tmp_path, problem):
    case = fixture_case()
    if problem == "empty":
        with pytest.raises(DatasetError, match="EMPTY_DATASET"):
            load_cases(tmp_path)
        return
    if problem == "wrong_version":
        case["dataset_version"] = "unknown"
    elif problem == "missing_expected":
        case.pop("expected")
    elif problem == "not_synthetic":
        case["source"] = "real_play"
    elif problem == "unknown_field":
        case["typo"] = True
    save_case(tmp_path, case)
    if problem == "duplicate":
        save_case(tmp_path, case, "duplicate.json")
    with pytest.raises(DatasetError):
        load_cases(tmp_path)


def test_semantic_reference_cannot_claim_success_by_expecting_an_error():
    case = fixture_case()
    case["expected"] = {"error_code": "INVALID_CONTRACT"}
    with pytest.raises(DatasetError):
        check_cases([case])


def test_malformed_json_is_a_dataset_error(tmp_path):
    (tmp_path / "bad.json").write_text("{broken", encoding="utf-8")
    with pytest.raises(DatasetError, match="INVALID_FILE"):
        load_cases(tmp_path)


def test_cli_checks_one_case_and_creates_report(tmp_path, capsys):
    dataset = tmp_path / "cases"
    dataset.mkdir()
    save_case(dataset, fixture_case())
    output = tmp_path / "report.json"
    assert main(["--dataset", str(dataset), "--case", "G01", "--output", str(output)]) == 0
    report = json.loads(output.read_text(encoding="utf-8"))
    assert report["total"] == report["passed"] == 1 and report["external_calls"] == 0
    assert json.loads(capsys.readouterr().out) == report


@pytest.mark.parametrize("problem", ["mismatch", "unknown_case", "existing_output"])
def test_cli_failure_codes_and_no_overwrite(tmp_path, capsys, problem):
    dataset = tmp_path / "cases"
    dataset.mkdir()
    case = fixture_case()
    args = ["--dataset", str(dataset)]
    expected_code = 2
    if problem == "mismatch":
        case["expected"]["active_values"] = {}
        expected_code = 1
    elif problem == "unknown_case":
        args += ["--case", "MISSING"]
    else:
        output = tmp_path / "report.json"
        output.write_text("keep original", encoding="utf-8")
        args += ["--output", str(output)]
    save_case(dataset, case)
    assert main(args) == expected_code
    if problem == "existing_output":
        assert output.read_text(encoding="utf-8") == "keep original"
    elif problem == "mismatch":
        assert json.loads(capsys.readouterr().out)["failed"] == 1


def test_cli_has_no_live_model_option():
    with pytest.raises(SystemExit) as error:
        main(["--execute-real"])
    assert error.value.code == 2


def test_shipped_cases_cover_memory_semantics_and_policy_failures():
    directory = Path(__file__).resolve().parents[1] / "samples" / "memory_regression" / "cases"
    cases = load_cases(directory)
    tags = {tag for case in cases for tag in case["tags"]}
    assert {"cafe", "hotel", "shopping", "transport", "retract", "stt_correction", "blocked", "korean", "stale", "atomic"} <= tags
    assert {case["case_type"] for case in cases} == {"semantic_reference", "policy_regression"}
    assert len(cases) >= 30
    assert check_cases(cases)["failed"] == 0
