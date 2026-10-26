"""S15P31A305-128/135: 합성 변경 후보로 사실별 기억의 세션 간 갱신을 검증한다."""

import copy
import socket

import pytest

from feedback.memory_policy import MemoryPolicy, MemoryPolicyError, plan_memory_update

DRINK = "preference.favorite_drink"
NAME = "profile.display_name"
POLICY = MemoryPolicy(allowed_keys={DRINK: "in_game_preference", NAME: "in_game_profile"})


@pytest.fixture(autouse=True)
def no_network(monkeypatch):
    def blocked(*args, **kwargs):
        raise AssertionError("기억 정책 테스트는 네트워크를 호출할 수 없습니다")

    monkeypatch.setattr(socket.socket, "connect", blocked)
    monkeypatch.setattr(socket, "create_connection", blocked)


def fact(content="Prefers coffee.", *, key=DRINK, memory_id="m1", kind="in_game_preference"):
    return {"memory_id": memory_id, "fact_key": key, "kind": kind, "content": content}


def request(facts=(), *, text="My favorite drink is tea.", version=0, conversation="c1"):
    return {
        "schema_version": "0.1.0",
        "request_id": "request_" + conversation,
        "scope": {"user_id": "user1", "npc_id": "npc1", "conversation_id": conversation},
        "conversation_revision": 1,
        "analysis_config_version": "memory_fixture_v1",
        "context": {
            "scene_id": "cafe",
            "mode": "free_chat",
            "scene_description": "Synthetic cafe conversation.",
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
                "final_text": text,
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
        "memory_snapshot": {"version": version, "facts": list(facts)},
    }


def change(req, action="add", *, key=DRINK, content="Prefers tea.", memory_id="m1", kind="in_game_preference"):
    result = {"action": action, "fact_key": key, "evidence": [{"turn_id": "t1", "quote": req["turns"][0]["final_text"]}]}
    if action != "add":
        result["memory_id"] = memory_id
    if action == "retract":
        result["reason_ko"] = "사용자가 기억 철회를 요청했습니다."
    else:
        result.update(kind=kind, content=content)
    return result


def analysis(req, changes=()):
    return {
        **{
            key: copy.deepcopy(req[key])
            for key in ("schema_version", "request_id", "scope", "conversation_revision", "analysis_config_version")
        },
        "status": "completed",
        "base_memory_version": req["memory_snapshot"]["version"],
        "result": {"summary": None, "changes": list(changes)},
        "problem": None,
    }


def plan(req, response, **overrides):
    args = {
        "current_scope": copy.deepcopy(req["scope"]),
        "current_snapshot": copy.deepcopy(req["memory_snapshot"]),
        "current_revision": req["conversation_revision"],
        "policy": POLICY,
    }
    return plan_memory_update(req, response, **(args | overrides))


def test_M01_add_creates_one_active_value_without_mutating_inputs():
    req = request()
    response = analysis(req, [change(req)])
    originals = copy.deepcopy((req, response))
    result = plan(req, response)
    assert result["status"] == "ready"
    assert result["active_values"] == {DRINK: {"kind": "in_game_preference", "content": "Prefers tea."}}
    assert result["base_memory_version"] == 0 and result["next_memory_version"] == 1
    assert result["changes"] == response["result"]["changes"]
    assert (req, response) == originals


def test_M02_replace_keeps_only_new_value_and_preserves_unrelated_memory():
    old_name = fact("Call them Alex.", key=NAME, memory_id="m2", kind="in_game_profile")
    req = request([fact(), old_name], text="I used to prefer coffee, but now I prefer tea.", version=3)
    result = plan(req, analysis(req, [change(req, "replace")]))
    assert result["status"] == "ready" and result["next_memory_version"] == 4
    assert result["active_values"][DRINK]["content"] == "Prefers tea."
    assert result["active_values"][NAME]["content"] == old_name["content"]
    assert "Prefers coffee." not in str(result["active_values"])


def test_M03_retract_removes_active_value_and_keeps_change_evidence():
    req = request([fact()], text="Please forget my favorite drink.", version=4)
    result = plan(req, analysis(req, [change(req, "retract")]))
    assert result["status"] == "ready" and result["active_values"] == {}
    assert result["next_memory_version"] == 5
    assert result["changes"][0]["memory_id"] == "m1"
    assert result["changes"][0]["evidence"][0]["quote"] == req["turns"][0]["final_text"]


def test_M04_repeated_same_value_is_no_change_without_version_increment():
    req = request([fact("Prefers tea.")], version=2)
    result = plan(req, analysis(req, [change(req, "replace", content="  Prefers   tea.  ")]))
    assert result["status"] == "no_change"
    assert result["changes"] == [] and result["next_memory_version"] == 2
    assert result["active_values"][DRINK]["content"] == "Prefers tea."


@pytest.mark.parametrize("field", ["user_id", "npc_id", "conversation_id"])
def test_M05_different_server_scope_is_rejected(field):
    req = request()
    current_scope = req["scope"] | {field: "other"}
    with pytest.raises(MemoryPolicyError, match="SCOPE_MISMATCH"):
        plan(req, analysis(req, [change(req)]), current_scope=current_scope)


@pytest.mark.parametrize("field", ["memory_version", "conversation_revision"])
def test_M06_stale_analysis_does_not_restore_old_memory(field):
    req = request([fact()], text="I now prefer tea.", version=3)
    current = {"version": 4, "facts": []} if field == "memory_version" else copy.deepcopy(req["memory_snapshot"])
    revision = 2 if field == "conversation_revision" else 1
    result = plan(req, analysis(req, [change(req, "replace")]), current_snapshot=current, current_revision=revision)
    assert result["status"] == "stale" and result["changes"] == []
    assert result["next_memory_version"] == current["version"]
    if field == "memory_version":
        assert result["active_values"] == {}


def test_M07_same_version_with_different_snapshot_is_rejected():
    req = request([fact()], version=3)
    current = {"version": 3, "facts": [fact("Prefers juice.")]}
    with pytest.raises(MemoryPolicyError, match="SNAPSHOT_MISMATCH"):
        plan(req, analysis(req, [change(req, "replace")]), current_snapshot=current)


@pytest.mark.parametrize("status", ["failed", "blocked_by_quota", "insufficient_evidence", "processing"])
def test_M08_noncompleted_analysis_preserves_existing_memory(status):
    req = request([fact()], version=3)
    response = analysis(req)
    response.pop("base_memory_version")
    response.update(status=status, result=None)
    problems = {
        "failed": {"code": "AI_PROVIDER_ERROR", "message_ko": "합성 실패", "retryable_now": False},
        "blocked_by_quota": {"code": "AI_QUOTA_EXHAUSTED", "message_ko": "합성 한도 부족", "retryable_now": False},
        "insufficient_evidence": {"code": "AMBIGUOUS_INPUT", "message_ko": "합성 근거 부족"},
        "processing": None,
    }
    response["problem"] = problems[status]
    result = plan(req, response)
    assert result["status"] == "skipped" and result["source_status"] == status
    assert result["changes"] == [] and result["next_memory_version"] == 3
    assert result["active_values"][DRINK]["content"] == "Prefers coffee."


@pytest.mark.parametrize(("key", "kind"), [("mission.completed", "past_interaction"), (NAME, "in_game_preference")])
def test_M09_unapproved_key_or_wrong_kind_is_rejected(key, kind):
    req = request()
    with pytest.raises(MemoryPolicyError, match="KEY_OR_KIND_NOT_ALLOWED"):
        plan(req, analysis(req, [change(req, key=key, kind=kind)]))


@pytest.mark.parametrize("limit", ["changes", "content", "active"])
def test_M10_limits_reject_the_whole_batch_without_mutating_input(limit):
    req = request()
    changes = [change(req), change(req, key=NAME, kind="in_game_profile", content="Call them Alex.")]
    response = analysis(req, changes)
    original = copy.deepcopy((req, response))
    limits = {"max_changes": 1} if limit == "changes" else {"max_content_chars": 3} if limit == "content" else {"max_active_facts": 1}
    policy = MemoryPolicy(allowed_keys=POLICY.allowed_keys, **limits)
    with pytest.raises(MemoryPolicyError, match="LIMIT_EXCEEDED"):
        plan(req, response, policy=policy)
    assert (req, response) == original


@pytest.mark.parametrize("limit", [0, -1, True])
def test_M11_invalid_policy_limit_is_rejected(limit):
    with pytest.raises(MemoryPolicyError, match="INVALID_POLICY"):
        MemoryPolicy(allowed_keys=POLICY.allowed_keys, max_changes=limit)


@pytest.mark.parametrize(
    "error",
    ["npc_evidence", "blocked", "needs_review", "invented_quote", "duplicate_key", "wrong_target", "wrong_response_scope", "old_stt_quote"],
)
def test_M12_invalid_evidence_and_conflicting_batch_are_rejected_atomically(error):
    req = request([fact()], text="I now prefer tea.", version=2)
    response = analysis(req, [change(req, "replace")])
    candidate = response["result"]["changes"][0]
    if error == "npc_evidence":
        req["turns"][0].update(speaker="npc", input_type="generated")
        req["turns"].append(
            req["turns"][0] | {"turn_id": "t2", "sequence": 2, "speaker": "user", "input_type": "text", "final_text": "Hello."}
        )
    elif error in ("blocked", "needs_review"):
        req["turns"][0]["text_status"] = error
    elif error == "invented_quote":
        candidate["evidence"][0]["quote"] = "I never said this."
    elif error == "duplicate_key":
        response["result"]["changes"].append(copy.deepcopy(candidate))
    elif error == "wrong_target":
        candidate["memory_id"] = "not_in_snapshot"
    elif error == "wrong_response_scope":
        response["scope"]["npc_id"] = "other_npc"
    else:
        req["turns"][0].update(input_type="voice", original_transcript="I prefer coffee.")
        candidate["evidence"][0]["quote"] = "I prefer coffee."
    originals = copy.deepcopy((req, response))
    with pytest.raises(MemoryPolicyError, match="INVALID_CONTRACT"):
        plan(req, response)
    assert (req, response) == originals


def persisted_snapshot(result, *, old_ids=None):
    """테스트의 저장소 대역. ID 발급·DB 저장은 실제 BE 구현을 의미하지 않는다."""
    old_ids = old_ids or {}
    return {
        "version": result["next_memory_version"],
        "facts": [
            {"memory_id": old_ids.get(key, f"fixture_{index}"), "fact_key": key, **value}
            for index, (key, value) in enumerate(result["active_values"].items(), 1)
        ],
    }


def test_M13_sessions_add_replace_repeat_retract_and_late_result():
    first = request(text="My favorite drink is coffee.", conversation="c1")
    first_analysis = analysis(first, [change(first, content="Prefers coffee.")])
    added = plan(first, first_analysis)
    snapshot = persisted_snapshot(added)

    second = request(snapshot["facts"], text="I used to prefer coffee, but now I prefer tea.", version=1, conversation="c2")
    updated = plan(second, analysis(second, [change(second, "replace", memory_id=snapshot["facts"][0]["memory_id"])]))
    snapshot = persisted_snapshot(updated)
    assert snapshot["version"] == 2 and len(snapshot["facts"]) == 1
    assert snapshot["facts"][0]["content"] == "Prefers tea."

    third = request(snapshot["facts"], version=2, conversation="c3")
    unchanged = plan(third, analysis(third, [change(third, "replace", memory_id=snapshot["facts"][0]["memory_id"])]))
    assert unchanged["status"] == "no_change" and unchanged["next_memory_version"] == 2

    fourth = request(snapshot["facts"], text="Please forget my favorite drink.", version=2, conversation="c4")
    retracted = plan(fourth, analysis(fourth, [change(fourth, "retract", memory_id=snapshot["facts"][0]["memory_id"])]))
    snapshot = persisted_snapshot(retracted)
    assert snapshot == {"version": 3, "facts": []}

    late = plan(first, first_analysis, current_snapshot=snapshot)
    assert late["status"] == "stale" and late["active_values"] == {}


def test_M14_replaying_an_already_applied_change_is_stale():
    req = request()
    response = analysis(req, [change(req)])
    applied = plan(req, response)
    repeated = plan(req, response, current_snapshot=persisted_snapshot(applied))
    assert repeated["status"] == "stale" and repeated["changes"] == []
    assert repeated["next_memory_version"] == 1


def test_M15_no_candidate_changes_keeps_all_memory_across_sessions():
    req = request([fact()], text="I'd like a latte today.", version=3, conversation="another_visit")
    result = plan(req, analysis(req))
    assert result["status"] == "no_change" and result["next_memory_version"] == 3
    assert result["active_values"][DRINK]["content"] == "Prefers coffee."


def test_M16_case_difference_is_not_silently_merged():
    req = request([fact("Call them US.", key=NAME, kind="in_game_profile")], text="Call me us.", version=2)
    candidate = change(req, "replace", key=NAME, kind="in_game_profile", content="Call them us.")
    result = plan(req, analysis(req, [candidate]))
    assert result["status"] == "ready" and result["active_values"][NAME]["content"] == "Call them us."


def test_M17_policy_can_enable_a_new_situation_without_cafe_specific_code():
    key = "travel.room_preference"
    policy = MemoryPolicy(allowed_keys={key: "in_game_preference"})
    req = request(text="I prefer a quiet room.")
    result = plan(req, analysis(req, [change(req, key=key, content="Prefers a quiet room.")]), policy=policy)
    assert result["active_values"] == {key: {"kind": "in_game_preference", "content": "Prefers a quiet room."}}


def test_M18_later_invalid_change_does_not_partially_apply_earlier_change():
    req = request()
    response = analysis(req, [change(req), change(req, key="mission.completed", kind="past_interaction")])
    original = copy.deepcopy((req, response))
    with pytest.raises(MemoryPolicyError, match="KEY_OR_KIND_NOT_ALLOWED"):
        plan(req, response)
    assert (req, response) == original


def test_M19_add_on_existing_key_cannot_create_duplicate_memory():
    req = request([fact()], version=3)
    with pytest.raises(MemoryPolicyError, match="INVALID_CONTRACT"):
        plan(req, analysis(req, [change(req)]))


def test_M20_stale_plan_distinguishes_analysis_base_from_current_version():
    req = request([fact()], version=3)
    result = plan(req, analysis(req, [change(req, "replace")]), current_snapshot={"version": 8, "facts": []})
    assert result["status"] == "stale"
    assert result["base_memory_version"] == 3
    assert result["current_memory_version"] == result["next_memory_version"] == 8
    assert result["conversation_revision"] == req["conversation_revision"]
    assert result["analysis_config_version"] == req["analysis_config_version"]
