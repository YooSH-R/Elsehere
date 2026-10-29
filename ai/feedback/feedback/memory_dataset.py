"""합성 기억 회귀 자료를 읽어 고정 응답의 변경 계획을 점검한다. 외부 호출 없음."""

import json
from pathlib import Path
from typing import Annotated, Any, Literal

from pydantic import Field, model_validator

from .contract_v01 import Contract, MemoryKind, MemorySnapshot, Scope, Text
from .memory_policy import MemoryPolicy, MemoryPolicyError, plan_memory_update

DATASET_VERSION = "memory-regression-1.0"


class DatasetError(ValueError):
    pass


class CurrentState(Contract):
    scope: Scope
    snapshot: MemorySnapshot
    conversation_revision: Annotated[int, Field(ge=0)]


class ActiveValue(Contract):
    kind: MemoryKind
    content: Text


class ChangeTarget(Contract):
    action: Literal["add", "replace", "retract"]
    fact_key: Text


class ExpectedPlan(Contract):
    status: Literal["ready", "no_change", "stale", "skipped"]
    next_memory_version: Annotated[int, Field(ge=0)]
    active_values: dict[str, ActiveValue]
    change_targets: list[ChangeTarget]


class ExpectedError(Contract):
    error_code: Literal[
        "INVALID_CONTRACT", "INVALID_REVISION", "SCOPE_MISMATCH", "SNAPSHOT_MISMATCH", "KEY_OR_KIND_NOT_ALLOWED", "LIMIT_EXCEEDED"
    ]


class Rubric(Contract):
    must: Annotated[list[Text], Field(min_length=1)]
    must_not: Annotated[list[Text], Field(min_length=1)]


class MemoryCase(Contract):
    dataset_version: Literal["memory-regression-1.0"]
    case_id: Annotated[str, Field(pattern=r"^[A-Z][A-Z0-9_-]{1,63}$")]
    case_type: Literal["semantic_reference", "policy_regression"]
    title_ko: Text
    source: Literal["synthetic"]
    tags: Annotated[list[Text], Field(min_length=1)]
    rubric: Rubric
    request: dict[str, Any]
    fixture_response: dict[str, Any]
    current: CurrentState
    policy: dict[str, Any]
    expected: ExpectedPlan | ExpectedError

    @model_validator(mode="after")
    def consistent(self):
        MemoryPolicy(**self.policy)
        if self.case_type == "semantic_reference" and isinstance(self.expected, ExpectedError):
            raise ValueError("의미 기대값 사례는 유효한 계획을 기대해야 한다")
        return self


def validate_cases(cases: list[dict]) -> list[dict]:
    if not cases:
        raise DatasetError("EMPTY_DATASET")
    validated, ids = [], set()
    for case in cases:
        try:
            parsed = MemoryCase.model_validate(case)
        except (ValueError, TypeError) as error:
            raise DatasetError("INVALID_CASE") from error
        if parsed.case_id in ids:
            raise DatasetError("DUPLICATE_CASE_ID")
        ids.add(parsed.case_id)
        validated.append(parsed.model_dump(mode="json"))
    return validated


def load_cases(directory: str | Path) -> list[dict]:
    cases = []
    for path in sorted(Path(directory).glob("*.json")):
        try:
            cases.append(json.loads(path.read_text(encoding="utf-8")))
        except (OSError, ValueError) as error:
            raise DatasetError(f"INVALID_FILE:{path.name}") from error
    return validate_cases(cases)


def check_cases(cases: list[dict]) -> dict:
    """고정 응답을 검사한다. 의미 기준 문구나 실제 모델 응답을 자동 채점하지 않는다."""
    results = []
    for case in validate_cases(cases):
        current = case["current"]
        expected = case["expected"]
        try:
            plan = plan_memory_update(
                case["request"],
                case["fixture_response"],
                current_scope=current["scope"],
                current_snapshot=current["snapshot"],
                current_revision=current["conversation_revision"],
                policy=MemoryPolicy(**case["policy"]),
            )
        except MemoryPolicyError as error:
            issues = [] if expected.get("error_code") == error.code else [f"unexpected_error:{error.code}"]
        else:
            if "error_code" in expected:
                issues = [f"expected_error_not_raised:{expected['error_code']}"]
            else:
                actual = {
                    "status": plan["status"],
                    "next_memory_version": plan["next_memory_version"],
                    "active_values": plan["active_values"],
                    "change_targets": [{"action": change["action"], "fact_key": change["fact_key"]} for change in plan["changes"]],
                }
                issues = [f"mismatch:{key}" for key in expected if actual[key] != expected[key]]
        results.append({"case_id": case["case_id"], "case_type": case["case_type"], "passed": not issues, "issues": issues})
    passed = sum(result["passed"] for result in results)
    return {
        "dataset_version": DATASET_VERSION,
        "mode": "fixed_fixture_check",
        "actual_model_evaluated": False,
        "external_calls": 0,
        "total": len(results),
        "passed": passed,
        "failed": len(results) - passed,
        "results": results,
    }
