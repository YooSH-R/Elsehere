"""S15P31A305-139: 저장 응답 점검기를 합성 응답으로 검증한다."""

import copy
import hashlib
import json
import socket
from pathlib import Path

import pytest
from review_memory_results import main

from feedback.memory_dataset import DatasetError
from feedback.memory_review import review_directory, review_response

CASES = Path(__file__).resolve().parents[1] / "samples" / "memory_regression" / "cases"


@pytest.fixture(autouse=True)
def no_network(monkeypatch):
    def blocked(*args, **kwargs):
        raise AssertionError("저장 응답 점검은 네트워크를 사용할 수 없습니다")

    monkeypatch.setattr(socket.socket, "connect", blocked)
    monkeypatch.setattr(socket, "create_connection", blocked)


def case(case_id="G01"):
    return json.loads((CASES / f"{case_id}.json").read_text(encoding="utf-8"))


def test_fixed_reference_is_structurally_valid_but_semantics_remains_pending():
    sample = case()
    original = copy.deepcopy(sample)
    text = json.dumps(sample["fixture_response"], ensure_ascii=False)
    result = review_response(sample, text)
    assert result["case_id"] == "G01"
    assert result["automatic_status"] == "checks_passed" and result["issues"] == []
    assert result["semantic_status"] == "pending"
    assert result["response_sha256"] == hashlib.sha256(text.encode("utf-8")).hexdigest()
    assert len(result["request_sha256"]) == 64
    assert sample == original


@pytest.mark.parametrize("problem", ["invalid_json", "card_shape", "invented_quote", "wrong_scope", "duplicate_key"])
def test_bad_saved_response_is_rejected_without_echoing_raw_text(problem):
    sample = case()
    response = copy.deepcopy(sample["fixture_response"])
    expected_code = "invalid_reference_or_policy"
    if problem == "card_shape":
        response = {"summary": "legacy card", "facts": []}
        expected_code = "invalid_structure"
    elif problem == "invented_quote":
        response["result"]["changes"][0]["evidence"][0]["quote"] = "NEVER_ECHO_THIS_RAW_TEXT"
    elif problem == "wrong_scope":
        response["scope"]["user_id"] = "other"
    elif problem == "duplicate_key":
        response["result"]["changes"].append(copy.deepcopy(response["result"]["changes"][0]))
    text = "{NEVER_ECHO_THIS_RAW_TEXT" if problem == "invalid_json" else json.dumps(response)
    if problem == "invalid_json":
        expected_code = "invalid_json"
    result = review_response(sample, text)
    assert result["automatic_status"] == "rejected"
    assert result["semantic_status"] == "not_reviewed"
    assert expected_code in {issue["code"] for issue in result["issues"]}
    if problem == "duplicate_key":
        assert "duplicate_fact_key" in {issue["code"] for issue in result["issues"]}
    assert "NEVER_ECHO_THIS_RAW_TEXT" not in json.dumps(result)


def test_missing_expected_update_is_flagged_not_scored_as_model_accuracy():
    sample = case("G03")
    response = copy.deepcopy(sample["fixture_response"])
    response["result"]["changes"] = []
    result = review_response(sample, json.dumps(response))
    codes = {issue["code"] for issue in result["issues"]}
    assert result["automatic_status"] == "flagged"
    assert "missing_expected_change" in codes and "value_diff_review" in codes
    assert result["semantic_status"] == "pending"


def test_unexpected_but_well_formed_fact_is_flagged_for_review():
    sample = case("G02")
    response = copy.deepcopy(sample["fixture_response"])
    user = sample["request"]["turns"][-1]
    response["result"]["changes"] = [
        {
            "action": "add",
            "fact_key": "profile.display_name",
            "kind": "in_game_profile",
            "content": "Call them Alex.",
            "evidence": [{"turn_id": user["turn_id"], "quote": user["final_text"]}],
        }
    ]
    result = review_response(sample, json.dumps(response))
    assert result["automatic_status"] == "flagged"
    assert {"unexpected_change", "unexpected_active_fact"} <= {issue["code"] for issue in result["issues"]}


def test_paraphrase_is_a_review_candidate_not_automatically_wrong():
    sample = case()
    response = copy.deepcopy(sample["fixture_response"])
    response["result"]["changes"][0]["content"] = "Their favorite drink is tea."
    result = review_response(sample, json.dumps(response))
    assert result["issues"] == [{"code": "value_diff_review", "fact_key": "preference.favorite_drink"}]
    assert result["automatic_status"] == "flagged" and result["semantic_status"] == "pending"


def test_policy_fault_fixture_is_not_a_model_quality_case():
    with pytest.raises(DatasetError, match="NON_SEMANTIC_CASE"):
        review_response(case("P01"), "{}")


def save_response(directory, sample, response=None):
    path = directory / (sample["case_id"] + ".json")
    path.write_text(json.dumps(response or sample["fixture_response"], ensure_ascii=False), encoding="utf-8")
    return path


def test_missing_responses_are_not_run_and_never_replaced_with_gold(tmp_path):
    report = review_directory([case()], tmp_path, origin="synthetic_demo")
    assert report["counts"] == {"checks_passed": 0, "flagged": 0, "rejected": 0, "not_run": 1}
    assert report["available_responses"] == 0 and report["model_calls_made"] == 0
    assert report["semantic_quality_evaluated"] is False
    assert report["results"][0]["issues"] == [{"code": "missing_response"}]


def test_partial_saved_results_keep_missing_cases_visible_and_mark_provenance(tmp_path):
    save_response(tmp_path, case())
    report = review_directory([case(), case("G03")], tmp_path, origin="saved_model_response", model="declared-test-model")
    assert report["selected_cases"] == 2 and report["available_responses"] == 1
    assert report["counts"]["checks_passed"] == report["counts"]["not_run"] == 1
    assert report["origin"] == "saved_model_response" and report["declared_model"] == "declared-test-model"
    assert report["provenance_verified"] is False and report["semantic_quality_evaluated"] is False


@pytest.mark.parametrize(
    ("origin", "model"),
    [("unknown", None), ("saved_model_response", None), ("saved_model_response", " "), ("synthetic_demo", "real-model")],
)
def test_invalid_or_misleading_provenance_is_rejected(tmp_path, origin, model):
    with pytest.raises(DatasetError, match="INVALID_PROVENANCE"):
        review_directory([case()], tmp_path, origin=origin, model=model)


def test_unknown_file_is_rejected_instead_of_silently_skipped(tmp_path):
    (tmp_path / "G999.json").write_text("{}", encoding="utf-8")
    with pytest.raises(DatasetError, match="UNEXPECTED_RESPONSE_FILE"):
        review_directory([case()], tmp_path, origin="synthetic_demo")


def test_invalid_encoding_is_reported_without_stopping_other_cases(tmp_path):
    (tmp_path / "G01.json").write_bytes(b"\xff\xfe")
    save_response(tmp_path, case("G03"))
    report = review_directory([case(), case("G03")], tmp_path, origin="synthetic_demo")
    assert report["counts"]["rejected"] == report["counts"]["checks_passed"] == 1
    assert report["issue_counts"]["unreadable_response"] == 1


def test_invalid_request_is_a_dataset_error_not_a_model_failure(tmp_path):
    sample = case()
    sample["request"]["turns"] = "bad input"
    save_response(tmp_path, sample)
    with pytest.raises(DatasetError, match="INVALID_REQUEST"):
        review_directory([sample], tmp_path, origin="synthetic_demo")


def cli_inputs(tmp_path):
    dataset = tmp_path / "cases"
    responses = tmp_path / "responses"
    dataset.mkdir()
    responses.mkdir()
    sample = case()
    (dataset / "G01.json").write_text(json.dumps(sample, ensure_ascii=False), encoding="utf-8")
    return sample, responses, ["--dataset", str(dataset), "--responses-dir", str(responses), "--origin", "synthetic_demo"]


def test_cli_writes_new_report_and_preserves_semantic_pending(tmp_path, capsys):
    sample, responses, args = cli_inputs(tmp_path)
    save_response(responses, sample)
    output = tmp_path / "report.json"
    assert main(args + ["--output", str(output)]) == 0
    report = json.loads(output.read_text(encoding="utf-8"))
    assert report["counts"]["checks_passed"] == 1
    assert report["semantic_quality_evaluated"] is False and report["model_calls_made"] == 0
    assert report == json.loads(capsys.readouterr().out)


def test_cli_empty_response_directory_is_not_a_pass(tmp_path, capsys):
    _, _, args = cli_inputs(tmp_path)
    assert main(args) == 1
    assert json.loads(capsys.readouterr().out)["counts"]["not_run"] == 1


def test_cli_does_not_overwrite_existing_report(tmp_path):
    sample, responses, args = cli_inputs(tmp_path)
    save_response(responses, sample)
    output = tmp_path / "report.json"
    output.write_text("keep", encoding="utf-8")
    assert main(args + ["--output", str(output)]) == 2
    assert output.read_text(encoding="utf-8") == "keep"


def test_cli_rejects_false_provenance_instead_of_guessing(tmp_path):
    _, _, args = cli_inputs(tmp_path)
    args[-1] = "saved_model_response"
    assert main(args) == 2


def test_cli_cannot_execute_real_model():
    with pytest.raises(SystemExit) as error:
        main(["--execute-real"])
    assert error.value.code == 2
