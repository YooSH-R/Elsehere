"""대답 경로(/v1/npc/reply)의 관계·기분. 백엔드가 저장한 관계(알아봄·호감)와 마지막 대화 뒤 지난 게임 날 수를 relationship에
실어 보내면, 숫자 대신 말로 풀어 지시문에 넣는다(app.npc_relationship). 얼굴을 알아봐야 평범한 기억을 떠올린다.
가짜 공급자라 진짜 호출 없음."""

import pytest
from test_npc_reply_profile import NPC, profile_of, reply_request, system_of

from app import npc_relationship as rel

RULES = rel.load_world_rules()["relationship"]
CARD = {
    "name": "Minsu",
    "claims": ["says they love soy milk"],
    "events": ["chatted about the rain"],
    "attitude": "neutral",
    "struggled": [],
}
MEMORY = {"visits": 3, "last_order": "a soy latte", "card": CARD}
WITH_PROFILE = {**NPC, "profile": profile_of("CAFE_STAFF")}


def system(client, provider, npc: dict = WITH_PROFILE, **body) -> str:
    return system_of(reply_request(client, provider, npc, **body))


@pytest.mark.parametrize("npc", [NPC, WITH_PROFILE], ids=["role-line", "profile"])
def test_a_first_meeting_is_a_new_face_on_both_reply_prompts(client, provider, npc):
    prompt = system(client, provider, npc, relationship={"recognition": 0, "affinity": 0, "days_since_last_talk": 0})

    assert "You do not recognize this person" in prompt


def test_an_npc_who_does_not_recognize_them_does_not_recall_everyday_details(client, provider):
    """기억 카드가 있어도 알아봄이 낮으면 이름·했던 말·지난 주문을 떠올리지 않는다."""
    prompt = system(client, provider, relationship={"recognition": RULES["seenBefore"] - 1}, memory=MEMORY)

    assert "Minsu" not in prompt and "soy" not in prompt and "talked 3 time(s)" not in prompt


def test_a_familiar_face_brings_back_what_they_remember(client, provider):
    prompt = system(client, provider, relationship={"recognition": RULES["seenBefore"]}, memory=MEMORY)

    assert "looks familiar" in prompt
    assert "Their name: Minsu" in prompt and "says they love soy milk" in prompt and "Last time they ordered a soy latte" in prompt


def test_a_regular_is_known_well(client, provider):
    assert "is a regular" in system(client, provider, relationship={"recognition": RULES["regular"]}, memory=MEMORY)


def test_after_a_long_time_away_even_a_regular_becomes_a_new_face(client, provider):
    away = {"recognition": RULES["regular"], "days_since_last_talk": 400}

    assert "You do not recognize this person" in system(client, provider, relationship=away, memory=MEMORY)


def test_a_strong_feeling_from_before_is_remembered_even_without_recognizing_them(client, provider):
    """무례 같은 강한 일은 얼굴을 몰라도 기억한다. 기억 카드의 태도가 friendly·wary일 때만 지난 일을 넣는다."""
    wary = {**MEMORY, "card": {**CARD, "events": ["was rude to me about the wait"], "attitude": "wary"}}

    prompt = system(client, provider, relationship={"recognition": 0}, memory=wary)

    assert "was rude to me about the wait" in prompt and "wary" in prompt
    assert "Minsu" not in prompt


@pytest.mark.parametrize(
    ("affinity", "line"),
    [(RULES["liked"], "You like this person."), (RULES["disliked"], "You do not like this person much")],
)
def test_liking_or_disliking_them_reaches_the_prompt(client, provider, affinity, line):
    assert line in system(client, provider, relationship={"recognition": RULES["seenBefore"], "affinity": affinity})


def test_the_feeling_from_affinity_replaces_the_card_attitude(client, provider):
    """호감 문장과 기억 카드의 태도(말투 참고값)를 겹쳐 넣지 않는다. 호감이 기준 안이면 카드의 태도를 쓴다."""
    friendly = {**MEMORY, "card": {**CARD, "attitude": "friendly"}}

    liked = system(client, provider, relationship={"recognition": RULES["seenBefore"], "affinity": RULES["liked"]}, memory=friendly)
    neutral = system(client, provider, relationship={"recognition": RULES["seenBefore"]}, memory=friendly)

    assert "You like this person." in liked and "You like this customer." not in liked
    assert "You like this customer." in neutral


def test_a_strong_feeling_is_remembered_without_recognizing_them_even_when_disliked(client, provider):
    wary = {**MEMORY, "card": {**CARD, "events": ["was rude to me about the wait"], "attitude": "wary"}}

    prompt = system(client, provider, relationship={"recognition": 0, "affinity": RULES["disliked"]}, memory=wary)

    assert "was rude to me about the wait" in prompt and "You do not like this person much" in prompt


def test_only_a_close_regular_hears_about_the_npc_s_own_day(client, provider):
    close = {"recognition": RULES["personalTalk"]["recognition"], "affinity": RULES["personalTalk"]["affinity"]}

    assert "share a little about your own day" in system(client, provider, relationship=close)
    assert "share a little about your own day" not in system(client, provider, relationship={**close, "affinity": 0})


def test_a_rude_line_a_moment_ago_keeps_the_npc_annoyed(client, provider):
    history = [{"who": "me", "text": "[withheld]", "rude": True}, {"who": "npc", "text": "Please be polite."}]

    assert "still annoyed" in system(client, provider, relationship={}, history=history)
    assert "still annoyed" not in system(client, provider, relationship={}, history=[])


def test_a_compliment_a_moment_ago_puts_the_npc_in_a_good_mood(client, provider):
    """백엔드가 의도 판정의 줄 사건을 history[].events로 보내면 대화 중 기분에 쓴다."""
    history = [{"who": "me", "text": "Your latte is the best!", "events": ["COMPLIMENT"]}, {"who": "npc", "text": "Aw, thanks!"}]

    assert "good mood" in system(client, provider, relationship={}, history=history)
    assert "good mood" not in system(client, provider, relationship={}, history=[{**history[0], "events": []}, history[1]])


def test_the_prompt_never_shows_the_numbers(client, provider):
    prompt = system(client, provider, relationship={"recognition": 37, "affinity": -23, "days_since_last_talk": 2}, memory=MEMORY)

    assert "affinity" not in prompt.lower()  # 'recognition'은 공통 규칙의 speech recognition에 원래 있다
    assert "37" not in prompt and "23" not in prompt


def test_without_a_relationship_the_memory_note_stays_as_before(client, provider):
    prompt = system(client, provider, memory=MEMORY)

    assert "you have talked 3 time(s) before" in prompt and "recognize" not in prompt


@pytest.mark.parametrize(
    ("field", "value"),
    [("recognition", 101), ("recognition", -1), ("affinity", 101), ("affinity", -101), ("days_since_last_talk", -1)],
)
def test_a_relationship_out_of_range_is_an_invalid_request(client, field, value):
    r = client.post("/v1/npc/reply", json={"npc": NPC, "history": [], "text": "hi", "tts": False, "relationship": {field: value}})

    assert r.status_code == 400


@pytest.mark.parametrize("npc", [NPC, WITH_PROFILE], ids=["role-line", "profile"])
def test_free_conversation_can_lead_or_follow_without_service_assumptions(client, provider, npc):
    prompt = system(client, provider, npc)
    assert "take the conversational lead" in prompt
    assert "follow their lead" in prompt
    assert "not a service encounter" in prompt
