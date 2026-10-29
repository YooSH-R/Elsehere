"""사용자 × NPC 관계 계산(app.npc_relationship): 알아봄·호감을 공통 규칙으로 계산한다. 설계는 docs/npc-life-design.md 4절.
LLM 없이 숫자만 본다. 날짜는 모두 게임 속 날짜다."""

import json
import sys
from pathlib import Path

from app import npc_persona
from app import npc_relationship as rel
from app.npc_profile_models import NpcProfile

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_derive

NPCS = Path(__file__).resolve().parents[2] / "npcs"
RULES = rel.load_world_rules()
EVENTS = RULES["conversationEvents"]


def card(npc_id: str) -> dict:
    return json.loads((NPCS / f"{npc_id}.json").read_text(encoding="utf-8"))


def test_recognition_stays_for_a_few_days_and_then_fades_each_day():
    grace, drop = RULES["relationship"]["graceDays"], RULES["relationship"]["recognitionDropPerDay"]

    assert rel.recognition_today(50, grace, RULES) == 50
    assert rel.recognition_today(50, grace + 4, RULES) == 50 - 4 * drop
    assert rel.recognition_today(3, 400, RULES) == 0


def test_each_conversation_adds_less_recognition_as_the_npc_knows_them_better():
    first = rel.grow_recognition(0, 1.0, RULES)

    assert first == RULES["relationship"]["recognitionPerConversation"]
    assert 0 < rel.grow_recognition(80, 1.0, RULES) - 80 < first
    assert rel.grow_recognition(0, 1.2, RULES) > first  # 친화성이 높은 NPC는 빨리 알아본다
    assert rel.grow_recognition(100, 1.4, RULES) == 100


def test_the_impression_counts_each_judged_event_once_and_every_rude_line():
    """판정된 사건은 대화에서 한 번씩, 무례는 Spring이 확정한 줄마다 센다. 목록 밖 사건은 버린다."""
    greeting, task_done, rude = (EVENTS[name].get("impression", 0) for name in ("GREETING", "TASK_DONE", "RUDE"))

    assert rel.impression(["GREETING", "GREETING", "TASK_DONE"], 0, {}, RULES) == greeting + task_done
    assert rel.impression([], 2, {}, RULES) == 2 * rude
    assert rel.impression(["SHOUTING"], 0, {}, RULES) == 0


def test_an_npc_reacts_more_or_less_strongly_by_its_event_scale():
    plain = rel.impression([], 1, {}, RULES)

    assert rel.impression([], 1, {"eventScale": {"RUDE": 1.5}}, RULES) == plain * 1.5


def test_affinity_follows_only_part_of_the_impression_and_slows_near_the_ends():
    follow = RULES["relationship"]["affinityFollowRate"]

    assert rel.follow_affinity(0, 10, RULES) == 10 * follow
    assert 0 < rel.follow_affinity(90, 10, RULES) - 90 < 10 * follow  # 이미 좋아하면 덜 오른다
    assert rel.follow_affinity(90, -15, RULES) - 90 == -15 * follow  # 좋아하던 사람이 무례하면 그대로 깎인다
    assert rel.follow_affinity(-100, -15, RULES) == -100
    assert rel.follow_affinity(100, 50, RULES) == 100


def test_after_a_conversation_the_relationship_to_store_is_whole_numbers():
    """백엔드는 정수 칸에 저장한다. 오래 안 온 만큼 알아봄이 줄어든 뒤 이번 대화만큼 오른다."""
    before = {"recognition": 20, "affinity": 5, "days_since_last_talk": 10}

    after = rel.after_conversation(before, ["GREETING", "COMPLIMENT"], 0, {}, RULES)

    assert after == {
        "recognition": round(rel.grow_recognition(rel.recognition_today(20, 10, RULES), 1.0, RULES)),
        "affinity": round(rel.follow_affinity(5, rel.impression(["GREETING", "COMPLIMENT"], 0, {}, RULES), RULES)),
    }
    assert after["affinity"] > 5


def test_a_first_meeting_starts_from_nothing():
    after = rel.after_conversation(None, [], 1, {}, RULES)

    assert after["recognition"] == RULES["relationship"]["recognitionPerConversation"]
    assert after["affinity"] < 0


def test_rates_come_from_the_card_temperament_and_its_behavior_pieces():
    """대답 요청에는 인격 카드만 온다. 카드의 성격 5축과 행동 규칙에 들어 있는 조각으로 계산한 배수가 생활 파일로 계산한 값과 같다."""
    library = npc_persona.load_behaviors()
    for npc_id in ("CAFE_STAFF", "HOTEL_STAFF", "TOWN_PASSERBY"):
        config = card(npc_id)
        life = json.loads((NPCS / "life" / f"{npc_id}.json").read_text(encoding="utf-8"))
        expected = npc_derive.npc_life_values(life, RULES, library, config["defaultLocationId"])["rates"]

        assert rel.npc_rates(NpcProfile.model_validate(config["npcProfile"]), RULES, library) == expected


def test_an_npc_without_a_card_or_temperament_reacts_at_the_base_rate():
    profile = card("HOTEL_STAFF")["npcProfile"]
    plain = {**profile, "personality": {**profile["personality"], "dimensions": None}, "behaviorRules": ["Be polite."]}

    assert rel.npc_rates(None, RULES, npc_persona.load_behaviors()) == {}
    assert rel.npc_rates(NpcProfile.model_validate(plain), RULES, npc_persona.load_behaviors()) == {}
