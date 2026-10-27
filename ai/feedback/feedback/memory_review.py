"""저장된 기억 응답의 로컬 점검. 외부 호출과 의미 자동 채점은 하지 않는다."""

import hashlib
import json
from collections import Counter
from pathlib import Path

from .contract_v01 import ADAPTERS
from .memory_dataset import DatasetError, validate_cases
from .memory_policy import MemoryPolicy, MemoryPolicyError, plan_memory_update


def review_response(case: dict, response_text: str) -> dict:
    case = validate_cases([case])[0]
    _validate_reference(case)
    result = _result_header(case, response_text)
    try:
        response = json.loads(response_text)
    except ValueError:
        return result | {"automatic_status": "rejected", "semantic_status": "not_reviewed", "issues": [{"code": "invalid_json"}]}
    try:
        parsed = ADAPTERS["memory.output"].validate_json(response_text)
    except ValueError:
        return result | {"automatic_status": "rejected", "semantic_status": "not_reviewed", "issues": [{"code": "invalid_structure"}]}
    result["source_status"] = parsed.status
    if parsed.status == "completed":
        counts = Counter(change.fact_key for change in parsed.result.changes)
        result["issues"] = [{"code": "duplicate_fact_key", "fact_key": key} for key, count in counts.items() if count > 1]
    current = case["current"]
    try:
        plan = plan_memory_update(
            case["request"],
            response,
            current_scope=current["scope"],
            current_snapshot=current["snapshot"],
            current_revision=current["conversation_revision"],
            policy=MemoryPolicy(**case["policy"]),
        )
    except MemoryPolicyError as error:
        result["issues"].append({"code": "invalid_reference_or_policy", "detail_code": error.code})
        return result | {"automatic_status": "rejected", "semantic_status": "not_reviewed"}
    result["issues"].extend(_compare_plan(case["expected"], plan))
    return result | {"automatic_status": "flagged" if result["issues"] else "checks_passed"}


def _validate_reference(case):
    if case["case_type"] != "semantic_reference":
        raise DatasetError("NON_SEMANTIC_CASE")
    try:
        ADAPTERS["memory.input"].validate_json(json.dumps(case["request"]))
    except ValueError as error:
        raise DatasetError("INVALID_REQUEST") from error


def _result_header(case, response_text=None):
    request_text = json.dumps(case["request"], ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return {
        "case_id": case["case_id"],
        "automatic_status": "checks_passed",
        "semantic_status": "pending",
        "issues": [],
        "source_status": None,
        "request_sha256": hashlib.sha256(request_text.encode("utf-8")).hexdigest(),
        "response_sha256": hashlib.sha256(response_text.encode("utf-8")).hexdigest() if response_text is not None else None,
    }


def _compare_plan(expected, plan):
    """기대값과의 차이를 표시한다. 내용 의역의 정오를 판정하지 않는다."""
    issues = []
    if expected["status"] != plan["status"] or expected["next_memory_version"] != plan["next_memory_version"]:
        issues.append({"code": "outcome_mismatch"})
    wanted = {(target["action"], target["fact_key"]) for target in expected["change_targets"]}
    actual = {(change["action"], change["fact_key"]) for change in plan["changes"]}
    for code, targets in (("missing_expected_change", wanted - actual), ("unexpected_change", actual - wanted)):
        issues.extend({"code": code, "action": action, "fact_key": key} for action, key in sorted(targets))
    expected_values = expected["active_values"]
    actual_values = plan["active_values"]
    for key in sorted(expected_values.keys() - actual_values.keys()):
        issues.append({"code": "missing_active_fact", "fact_key": key})
    for key in sorted(actual_values.keys() - expected_values.keys()):
        issues.append({"code": "unexpected_active_fact", "fact_key": key})
    for key in sorted(expected_values.keys() & actual_values.keys()):
        if expected_values[key] != actual_values[key]:
            issues.append({"code": "value_diff_review", "fact_key": key})
    return issues


def review_directory(cases: list[dict], directory: str | Path, *, origin: str, model: str | None = None) -> dict:
    if origin not in ("saved_model_response", "synthetic_demo"):
        raise DatasetError("INVALID_PROVENANCE")
    if (origin == "saved_model_response" and (not isinstance(model, str) or not model.strip())) or (
        origin == "synthetic_demo" and model is not None
    ):
        raise DatasetError("INVALID_PROVENANCE")
    selected = [case for case in validate_cases(cases) if case["case_type"] == "semantic_reference"]
    if not selected:
        raise DatasetError("NO_SEMANTIC_CASES")
    for case in selected:
        _validate_reference(case)
    directory = Path(directory)
    if not directory.is_dir():
        raise DatasetError("RESPONSE_DIRECTORY_NOT_FOUND")
    ids = {case["case_id"] for case in selected}
    if any(path.stem not in ids for path in directory.glob("*.json")):
        raise DatasetError("UNEXPECTED_RESPONSE_FILE")
    results = []
    for case in selected:
        path = directory / (case["case_id"] + ".json")
        result = _result_header(case) | {"semantic_status": "not_reviewed"}
        if not path.exists():
            result.update(automatic_status="not_run", issues=[{"code": "missing_response"}])
        else:
            try:
                response_text = path.read_text(encoding="utf-8")
            except (OSError, UnicodeError):
                result.update(automatic_status="rejected", issues=[{"code": "unreadable_response"}])
            else:
                result = review_response(case, response_text)
        results.append(result)
    counts = {
        status: sum(result["automatic_status"] == status for result in results)
        for status in ("checks_passed", "flagged", "rejected", "not_run")
    }
    issues = Counter(issue["code"] for result in results for issue in result["issues"])
    return {
        "evaluation_version": "memory-offline-review-1.0",
        "dataset_version": selected[0]["dataset_version"],
        "mode": "saved_response_review",
        "origin": origin,
        "declared_model": model,
        "provenance_verified": False,
        "model_calls_made": 0,
        "semantic_quality_evaluated": False,
        "selected_cases": len(selected),
        "available_responses": len(selected) - counts["not_run"],
        "counts": counts,
        "issue_counts": dict(sorted(issues.items())),
        "results": results,
    }
