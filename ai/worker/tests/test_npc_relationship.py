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


def test_small_talk_and_personal_questions_do_not_move_affinity():
    """잡담은 그 대화의 기분만 좋게 하고 호감은 올리지 않는다. 사적인 질문은 NPC의 선과 친한 정도에 따라 대답이 정하므로 숫자로 두지 않는다
    (2026-10-29 결정, 이정훈 님 협의)."""
    assert rel.impression(["SMALL_TALK"], 0, {"eventScale": {"SMALL_TALK": 1.4}}, RULES) == 0
    assert rel.impression(["PERSONAL_QUESTION"], 0, {}, RULES) == 0
    assert EVENTS["SMALL_TALK"]["mood"]["PLEASED"] > 0


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


def test_a_friendly_learner_is_liked_sooner_than_one_who_only_orders():
    """백엔드가 정수로 저장해도 호감이 쌓인다. 칭찬까지 하면 여덟째 대화에 좋아하는 티, 스물한째에 사적인 이야기(잡담은 호감에 더하지 않음).
    인사하고 주문만 하면 열다섯째, 마흔째다(기본 배수, 대화마다 하루 간격)."""
    relationship = RULES["relationship"]
    friendly = ["GREETING", "SMALL_TALK", "COMPLIMENT", "TASK_DONE", "LEAVE"]
    polite = ["GREETING", "TASK_DONE", "LEAVE"]

    def until(threshold: int, events: list[str]) -> int:
        stored, conversations = {"recognition": 0, "affinity": 0}, 0
        while stored["affinity"] < threshold:
            conversations += 1
            stored = rel.after_conversation({**stored, "days_since_last_talk": 1}, events, 0, {}, RULES)
        return conversations

    assert (until(relationship["liked"], friendly), until(relationship["personalTalk"]["affinity"], friendly)) == (8, 21)
    assert (until(relationship["liked"], polite), until(relationship["personalTalk"]["affinity"], polite)) == (15, 40)


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


RUDE, PLAIN = (True, []), (False, [])  # 학습자 줄: (Spring이 확정한 무례인지, 의도 판정이 고른 사건)


def said(*events: str) -> tuple[bool, list[str]]:
    return False, list(events)


def state(relationship: dict, learner_turns: list[tuple[bool, list[str]]] | None = None, npc_id: str | None = None) -> dict:
    profile = NpcProfile.model_validate(card(npc_id)["npcProfile"]) if npc_id else None
    return rel.person_state(relationship, learner_turns or [], profile, RULES, npc_persona.load_behaviors())


def test_how_well_the_npc_knows_them_follows_recognition_after_the_days_away():
    relationship = RULES["relationship"]

    assert state({"recognition": 0})["known"] == "new"
    assert state({"recognition": relationship["seenBefore"]})["known"] == "familiar"
    assert state({"recognition": relationship["regular"]})["known"] == "regular"
    faded = {"recognition": relationship["seenBefore"], "days_since_last_talk": relationship["graceDays"] + 1}
    assert state(faded)["known"] == "new"  # 오래 안 오면 다시 낯선 얼굴


def test_feelings_show_only_past_the_rule_thresholds():
    relationship = RULES["relationship"]

    assert state({"recognition": 30, "affinity": relationship["liked"]})["feeling"] == "liked"
    assert state({"recognition": 30, "affinity": relationship["disliked"]})["feeling"] == "disliked"
    assert state({"recognition": 30, "affinity": 0})["feeling"] is None


def test_personal_talk_needs_both_knowing_and_liking_them():
    personal = RULES["relationship"]["personalTalk"]

    assert state({"recognition": personal["recognition"], "affinity": personal["affinity"]})["personal"] is True
    assert state({"recognition": personal["recognition"] - 1, "affinity": 100})["personal"] is False
    assert state({"recognition": 100, "affinity": personal["affinity"] - 1})["personal"] is False


def test_rudeness_leaves_the_npc_annoyed_for_a_few_turns():
    """Spring이 확정한 무례 줄(history의 rude)로 계산한다. 무례 뒤 학습자가 말할 때마다 decayPerTurn씩 가라앉는다."""
    assert state({}, [PLAIN, RUDE])["annoyed"] is True  # 무례 바로 다음 말
    assert state({}, [RUDE, PLAIN, PLAIN, PLAIN])["annoyed"] is False
    assert state({}, [PLAIN, PLAIN])["annoyed"] is False


def test_a_touchy_npc_stays_annoyed_longer():
    """사건 반응 크기(eventScale.RUDE)가 클수록 같은 무례에 더 오래 짜증 낸다."""
    calm, touchy = {"eventScale": {"RUDE": 0.6}}, {"eventScale": {"RUDE": 1.4}}
    turns = [RUDE, PLAIN]  # 무례 뒤 두 번째 말

    def annoyed(rates: dict) -> float:
        return rel.moods(turns, rates, RULES).get("ANNOYED", 0)

    assert annoyed(touchy) > annoyed(calm)
    assert annoyed(touchy) >= RULES["mood"]["showsAt"] > annoyed(calm)


def test_an_apology_calms_the_annoyance():
    assert state({}, [RUDE, PLAIN])["annoyed"] is True
    assert state({}, [RUDE, said("APOLOGY")])["annoyed"] is False


def test_a_compliment_puts_the_npc_in_a_good_mood_for_a_few_turns():
    assert state({}, [said("COMPLIMENT")])["pleased"] is True
    assert state({}, [said("COMPLIMENT"), PLAIN, PLAIN, PLAIN])["pleased"] is False


def test_small_talk_cheers_the_npc_up_only_when_it_goes_on():
    """잡담 한 번의 좋은 기분은 말투에 드러날 만큼 크지 않고, 이어지면 드러난다(호감은 올리지 않음)."""
    assert state({}, [said("SMALL_TALK")])["pleased"] is False
    assert state({}, [said("SMALL_TALK"), said("SMALL_TALK")])["pleased"] is True


def test_events_on_a_rude_line_are_ignored():
    """무례로 확정된 줄의 다른 사건(욕하면서 칭찬 등)은 버린다(이정훈 님 협의)."""
    rude_compliment = (True, ["COMPLIMENT"])

    assert state({}, [rude_compliment])["pleased"] is False
    assert state({}, [rude_compliment])["annoyed"] is True


def test_with_mixed_feelings_only_the_stronger_shows():
    """칭찬 뒤 무례면 짜증이 더 커서 짜증만 드러난다. 서로 반대인 말투 지시를 함께 주지 않는다."""
    mixed = state({}, [said("COMPLIMENT"), RUDE])

    assert (mixed["annoyed"], mixed["pleased"]) == (True, False)


def test_an_npc_without_a_card_or_temperament_reacts_at_the_base_rate():
    profile = card("HOTEL_STAFF")["npcProfile"]
    plain = {**profile, "personality": {**profile["personality"], "dimensions": None}, "behaviorRules": ["Be polite."]}

    assert rel.npc_rates(None, RULES, npc_persona.load_behaviors()) == {}
    assert rel.npc_rates(NpcProfile.model_validate(plain), RULES, npc_persona.load_behaviors()) == {}
