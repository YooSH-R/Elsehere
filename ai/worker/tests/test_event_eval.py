"""ai/tools/event_eval.py: 대화 사건 판정 테스트셋과 채점. 실제 모델은 부르지 않는다."""

import re
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import event_eval as E

from app import prompts as P

ROOT = Path(__file__).resolve().parents[3]
GROUPS = {"clear", "none", "boundary", "mission"}
JAVA_STAGES = (ROOT / "backend/src/main/java/com/elsehere/mission/entity/FirstDayStage.java").read_text(encoding="utf-8")
SEED = (ROOT / "backend/src/main/resources/db/seed/world.sql").read_text(encoding="utf-8")


@pytest.mark.parametrize("which", list(E.CASE_SETS))
def test_cases_follow_the_guide_labels(which):
    cases = E.load_cases(which)
    assert len({c["id"] for c in cases}) == len(cases)
    for c in cases:
        assert c["group"] in GROUPS, c["id"]
        assert c["npc"] in E.NPCS, c["id"]
        assert set(c["events"]) <= set(E.EVENTS), c["id"]
        assert len(c["events"]) == len(set(c["events"])), c["id"]
        assert c["text"].strip() and c["last_npc"].strip(), c["id"]


def test_dev_set_does_not_reuse_test_sentences():
    """지시문은 개발셋을 보고 다듬으므로, 테스트셋 문장이 개발셋에 섞이면 테스트셋 결과를 믿을 수 없다."""
    test = {c["text"].lower() for c in E.load_cases("test")}
    assert not [c["id"] for c in E.load_cases("dev") if c["text"].lower() in test]


@pytest.mark.parametrize("which", list(E.CASE_SETS))
def test_mission_cases_expect_only_that_stages_goals(which):
    for c in E.load_cases(which):
        if c["state"] == "free_talk":
            assert c["group"] != "mission" and "intents" not in c, c["id"]
            continue
        assert c["group"] == "mission", c["id"]
        goals = set(E.STAGES[c["state"]][1])
        assert c["intents"] == ["unclear"] or set(c["intents"]) <= goals, c["id"]


def test_every_event_and_group_is_covered():
    cases = E.load_cases()
    for e in E.EVENTS:
        assert any(e in c["events"] for c in cases), e
    assert {c["group"] for c in cases} == GROUPS


def test_split_fact_cases_send_the_prior_learner_statements():
    case = next(case for case in E.load_cases() if case["id"] == "mission-missing-split-facts")
    assert case["history"][0]["text"] in E.messages("before", case)[1]["content"]
    assert E.state_of("MISSING_BAG").accumulate_history is True


@pytest.mark.parametrize("stage", list(E.STAGES))
def test_stages_match_spring_first_day_mission(stage):
    """Spring의 FirstDayStage를 바꾸면 여기도 맞춰야 실제 판정 요청과 같은 조건으로 잰다."""
    instruction, goals = E.STAGES[stage]
    assert f'"{instruction}"' in re.sub(r'"\s*\+\s*"', "", JAVA_STAGES)
    block = re.split(r"\n  [A-Z_]+\(|;\n", JAVA_STAGES.split(f"\n  {stage}(", 1)[1], maxsplit=1)[0]
    java_goals = re.findall(r'goal\(\s*"(\w+)",\s*"[^"]*",\s*"([^"]*)"(?:,\s*"[^"]*")?\s*\)', block)
    assert dict(java_goals) == goals


def test_npcs_match_seed_personas():
    for npc in E.NPCS.values():
        assert f"'{npc.name}'" in SEED
        assert f'"role": "{npc.role}"' in SEED
        assert f'"scene": "{npc.scene}"' in SEED


def test_before_keeps_the_current_intent_prompt_and_schema():
    case = next(c for c in E.load_cases() if c["state"] == "CAFE_BREAK")
    state = E.state_of("CAFE_BREAK")
    assert E.messages("before", case) == P.intent_messages(
        E.NPCS["CAFE_STAFF"], state, [P.Line(who="npc", text=case["last_npc"])], case["text"]
    )
    assert E.schema("before", state) == P.intent_schema(state)


def test_after_adds_events_to_the_same_call():
    case = E.load_cases()[0]
    before, after = E.messages("before", case), E.messages("after", case)
    assert after[0] == before[0]
    assert after[1]["content"] == before[1]["content"] + "\n" + E.EVENTS_PROMPT
    out = E.schema("after", E.state_of("free_talk"))
    assert out["properties"]["events"]["items"]["enum"] == E.EVENTS
    assert "events" in out["required"] and "intents" in out["required"]


def test_service_sends_the_worker_prompt_and_schema_as_they_are():
    """서비스 판정(/v1/judge)과 같은 지시문·출력 형식이어야 지금 서비스의 사건 판정을 잰다."""
    case = E.load_cases()[0]
    state = E.state_of(case["state"])
    assert E.messages("service", case) == P.intent_messages(
        E.NPCS[case["npc"]], state, [P.Line(who="npc", text=case["last_npc"])], case["text"]
    )
    assert E.schema("service", state) == P.intent_schema(state)
    assert E.scores_events("service") and E.scores_events("after") and not E.scores_events("before")


def test_clean_events_drops_unknown_and_duplicates():
    assert E.clean_events({"events": ["GREETING", "RUDE", "GREETING", 3, "LEAVE"]}) == ["GREETING", "LEAVE"]
    assert E.clean_events({"events": "GREETING"}) == []
    assert E.clean_events({}) == []
    assert E.clean_events(["GREETING"]) == []


def test_score_counts_precision_recall_and_false_alarms():
    rows = [
        {"events": ["GREETING"], "got_events": ["GREETING"]},
        {"events": [], "got_events": ["COMPLIMENT"]},
        {"events": [], "got_events": []},
        {"events": ["COMPLIMENT"], "got_events": []},
        {"events": [], "intents": ["order_drink"], "got_events": [], "got_intents": ["order_drink"]},
        {"events": [], "intents": ["dine_in"], "got_events": [], "got_intents": ["unclear"]},
        {"events": ["LEAVE"], "error": "ReadTimeout"},
    ]
    s = E.score(rows)
    assert s["n"] == 7 and s["failed"] == 1
    assert s["events"]["GREETING"] == {"tp": 1, "fp": 0, "fn": 0, "precision": 1.0, "recall": 1.0}
    assert s["events"]["COMPLIMENT"] == {"tp": 0, "fp": 1, "fn": 1, "precision": 0.0, "recall": 0.0}
    assert s["events"]["LEAVE"]["precision"] is None  # 실패한 줄은 세지 않음
    assert s["empty_lines"] == 4 and s["empty_false_alarm"] == 0.25
    assert s["exact"] == 4 / 6
    assert s["intent_n"] == 2 and s["intent_exact"] == 0.5


def test_score_reports_latency_of_answered_calls():
    rows = [{"events": [], "got_events": [], "latency_s": t} for t in (0.2, 0.4, 0.6, 0.8, 1.0)]
    rows.append({"events": [], "error": "ReadTimeout", "latency_s": 30.0})  # 실패한 호출은 지연에서 뺀다
    s = E.score(rows)
    assert s["latency_p50"] == 0.6 and s["latency_p90"] == 1.0 and s["latency_max"] == 1.0


def test_batches_send_cases_by_concurrency():
    assert E.batches([1, 2, 3, 4, 5], 2) == [[1, 2], [3, 4], [5]]
    assert E.batches([1, 2, 3], 1) == [[1], [2], [3]]
