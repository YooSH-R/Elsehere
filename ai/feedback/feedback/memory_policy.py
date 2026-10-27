"""근거를 검증한 사실별 기억 변경 계획. LLM·HTTP·DB 호출을 수행하지 않는다."""

import json
import re
import unicodedata
from collections.abc import Mapping
from dataclasses import dataclass
from types import MappingProxyType

from .contract_v01 import MemorySnapshot, Scope, validate_pair


class MemoryPolicyError(ValueError):
    def __init__(self, code):
        super().__init__(code)
        self.code = code


@dataclass(frozen=True)
class MemoryPolicy:
    allowed_keys: Mapping[str, str]
    max_changes: int = 5
    max_active_facts: int = 20
    max_content_chars: int = 180

    def __post_init__(self):
        limits = (self.max_changes, self.max_active_facts, self.max_content_chars)
        if any(type(limit) is not int or limit < 1 for limit in limits):
            raise MemoryPolicyError("INVALID_POLICY")
        if not isinstance(self.allowed_keys, Mapping) or any(
            not isinstance(key, str)
            or re.fullmatch(r"[a-z][a-z0-9_.]{0,127}", key) is None
            or kind not in ("in_game_profile", "in_game_preference", "past_interaction")
            for key, kind in self.allowed_keys.items()
        ):
            raise MemoryPolicyError("INVALID_POLICY")
        object.__setattr__(self, "allowed_keys", MappingProxyType(dict(self.allowed_keys)))


def plan_memory_update(payload, analysis, *, current_scope, current_snapshot, current_revision, policy):
    """0.1.0 사실별 분석 응답을 검사해 저장 전 변경 계획을 반환한다.

    current_* 값과 policy는 신뢰하는 서버가 제공한다. 원본 인자는 바꾸지 않으며,
    DB 저장·ID 발급·트랜잭션·의미 판단은 수행하지 않는다. 저장 직전 버전 비교는
    호출 측 트랜잭션에서 다시 해야 한다. 계약·정책 위반은 MemoryPolicyError다.
    """
    try:
        req, response = validate_pair("memory", json.dumps(payload), json.dumps(analysis))
        scope = Scope.model_validate(current_scope)
        snapshot = MemorySnapshot.model_validate(current_snapshot)
    except (ValueError, TypeError) as error:
        raise MemoryPolicyError("INVALID_CONTRACT") from error
    if type(current_revision) is not int or current_revision < 0:
        raise MemoryPolicyError("INVALID_REVISION")
    if req.scope != scope:
        raise MemoryPolicyError("SCOPE_MISMATCH")
    active = {fact.fact_key: {"kind": fact.kind, "content": fact.content} for fact in snapshot.facts}
    result = {
        "status": "no_change",
        "source_status": response.status,
        "request_id": req.request_id,
        "scope": req.scope.model_dump(),
        "conversation_revision": req.conversation_revision,
        "analysis_config_version": req.analysis_config_version,
        "base_memory_version": req.memory_snapshot.version,
        "current_memory_version": snapshot.version,
        "next_memory_version": snapshot.version,
        "active_values": active,
        "changes": [],
        "summary": None,
    }
    if req.conversation_revision != current_revision or req.memory_snapshot.version != snapshot.version:
        return result | {"status": "stale"}
    requested_facts = {fact.memory_id: fact for fact in req.memory_snapshot.facts}
    if requested_facts != {fact.memory_id: fact for fact in snapshot.facts}:
        raise MemoryPolicyError("SNAPSHOT_MISMATCH")
    if response.status != "completed":
        return result | {"status": "skipped"}
    if len(response.result.changes) > policy.max_changes:
        raise MemoryPolicyError("LIMIT_EXCEEDED")
    changes = []
    for candidate in response.result.changes:
        kind = requested_facts[candidate.memory_id].kind if candidate.action == "retract" else candidate.kind
        if policy.allowed_keys.get(candidate.fact_key) != kind:
            raise MemoryPolicyError("KEY_OR_KIND_NOT_ALLOWED")
        if candidate.action != "retract" and len(candidate.content) > policy.max_content_chars:
            raise MemoryPolicyError("LIMIT_EXCEEDED")
        if candidate.action == "retract":
            del active[candidate.fact_key]
        else:
            value = {"kind": candidate.kind, "content": candidate.content}
            if candidate.action == "replace":
                old = active[candidate.fact_key]
                if old["kind"] == value["kind"] and normalize_content(old["content"]) == normalize_content(value["content"]):
                    continue
            active[candidate.fact_key] = value
        changes.append(candidate.model_dump(mode="json"))
    if changes and len(active) > policy.max_active_facts:
        raise MemoryPolicyError("LIMIT_EXCEEDED")
    return result | {
        "status": "ready" if changes else "no_change",
        "next_memory_version": snapshot.version + int(bool(changes)),
        "active_values": active,
        "changes": changes,
        "summary": response.result.summary.model_dump() if response.result.summary is not None else None,
    }


def normalize_content(content):
    """공백·유니코드 표기만 비교한다. 대소문자·동의어·의미를 임의로 합치지 않는다."""
    return " ".join(unicodedata.normalize("NFC", content).split())
