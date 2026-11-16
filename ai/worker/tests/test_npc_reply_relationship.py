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
    "requests": ["ordered a soy latte"],
}
MEMORY = {"visits": 3, "card": CARD}
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
    assert (
        "Their name: Minsu" in prompt
        and "says they love soy milk" in prompt
        and "asked for or did with you before: ordered a soy latte" in prompt
    )


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


STAGES = RULES["affinityStages"]
STAGE_LINES = {
    "hostile": "You really dislike this person",
    "cold": "You do not like this person much",
    "warm": "You like this person.",
    "close": "You are close with this person",
}


def stage_prompt(client, provider, affinity: int, **body) -> str:
    return system(client, provider, relationship={"recognition": RULES["seenBefore"], "affinity": affinity}, **body)


@pytest.mark.parametrize(("stage", "affinity"), [(stage, STAGES[stage]) for stage in STAGE_LINES])
def test_each_affinity_stage_has_its_own_attitude(client, provider, stage, affinity):
    prompt = stage_prompt(client, provider, affinity)

    assert STAGE_LINES[stage] in prompt
    assert all(line not in prompt for other, line in STAGE_LINES.items() if other != stage)


def test_a_neutral_stage_leaves_the_npc_to_its_own_personality(client, provider):
    prompt = stage_prompt(client, provider, 0)

    assert "How you treat this person:" not in prompt


@pytest.mark.parametrize(
    ("stage", "behaviors"),
    [
        ("hostile", ["curt", "steer back to business", "only the help your job requires", "do not call them by name", "Face: indifferent"]),
        ("cold", ["brief, formal greeting", "do not volunteer extra suggestions", "do not call them by name", "Face: indifferent"]),
        ("warm", ["glad to see them", "ask something back", "one small extra tip", "use their name", "Face: warm_smile"]),
        ("close", ["by name like a friend", "share a little", "insider tips", "Face: warm_smile"]),
    ],
)
def test_each_stage_spells_out_greeting_small_talk_help_name_and_face(client, provider, stage, behaviors):
    """단계마다 인사·잡담·도움·호칭·기본 표정을 구체적으로 적는다(한 줄 '조금 더 따뜻하게'로는 작은 모델이 하던 일만 함)."""
    prompt = stage_prompt(client, provider, STAGES[stage])

    assert all(behavior in prompt for behavior in behaviors), stage


@pytest.mark.parametrize("stage", ["warm", "close"])
def test_the_extra_help_never_promises_free_things(client, provider, stage):
    """덤은 말로 하는 도움(팁·추천·정보)만. 줄 수 없는 공짜 물건·할인은 약속하지 않는다(공통 규칙과 같음)."""
    assert "never free items or discounts" in stage_prompt(client, provider, STAGES[stage])


def test_the_attitude_sits_in_this_turn_with_the_mood(client, provider):
    """기분 줄처럼, 대답 바로 앞(이번 턴 칸)에 둬야 실제 모델이 따른다(S15P31A305-926 실제 모델 확인)."""
    turn = stage_prompt(client, provider, STAGES["hostile"]).split("## This turn")[1]

    assert "How you treat this person:" in turn and STAGE_LINES["hostile"] in turn


def test_a_close_stage_needs_a_face_they_recognize(client, provider):
    prompt = system(client, provider, relationship={"recognition": 0, "affinity": STAGES["close"]})

    assert STAGE_LINES["warm"] in prompt and STAGE_LINES["close"] not in prompt


def test_the_feeling_from_affinity_replaces_the_card_attitude(client, provider):
    """호감 문장과 기억 카드의 태도(말투 참고값)를 겹쳐 넣지 않는다. 호감이 기준 안이면 카드의 태도를 쓴다."""
    friendly = {**MEMORY, "card": {**CARD, "attitude": "friendly"}}

    liked = system(client, provider, relationship={"recognition": RULES["seenBefore"], "affinity": STAGES["warm"]}, memory=friendly)
    neutral = system(client, provider, relationship={"recognition": RULES["seenBefore"]}, memory=friendly)

    assert "You like this person." in liked and "You like this customer." not in liked
    assert "You like this customer." in neutral


def test_a_strong_feeling_is_remembered_without_recognizing_them_even_when_disliked(client, provider):
    wary = {**MEMORY, "card": {**CARD, "events": ["was rude to me about the wait"], "attitude": "wary"}}

    prompt = system(client, provider, relationship={"recognition": 0, "affinity": STAGES["cold"]}, memory=wary)

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


# ---------- 이번 대화의 기분: 관계와 상관없이 ----------
RUDE_A_MOMENT_AGO = [{"who": "me", "text": "[withheld]", "rude": True}, {"who": "npc", "text": "Please be polite."}]
COMPLIMENT_A_MOMENT_AGO = [
    {"who": "me", "text": "Your latte is the best!", "events": ["COMPLIMENT"]},
    {"who": "npc", "text": "Aw, thanks!"},
]
NEED_LIMITS = rel.load_world_rules()["mood"]["needs"]
LOW = NEED_LIMITS["low"]
FULL = dict.fromkeys(("HUNGER", "ENERGY", "SOCIAL", "FUN", "WORK"), NEED_LIMITS["comfortable"])


@pytest.mark.parametrize("npc", [NPC, WITH_PROFILE], ids=["role-line", "profile"])
def test_a_first_meeting_still_stays_annoyed_after_a_rude_line(client, provider, npc):
    """처음 만난 대화에는 백엔드가 관계를 보내지 않는다. 기분은 이번 대화 이력으로 정하므로 관계 없이도 넣는다."""
    assert "still annoyed" in system(client, provider, npc, history=RUDE_A_MOMENT_AGO)


def test_a_first_meeting_still_shows_a_good_mood_after_a_compliment(client, provider):
    assert "good mood" in system(client, provider, history=COMPLIMENT_A_MOMENT_AGO)


def learner_lines(*events: str | None) -> list[dict]:
    """학습자 줄과 NPC 대답을 번갈아 둔 이력. None은 무례 줄, 빈 문자열은 사건 없는 평범한 줄."""
    lines = []
    for event in events:
        if event is None:
            lines.append({"who": "me", "text": "[withheld]", "rude": True})
        else:
            lines.append({"who": "me", "text": "Okay.", "events": [event] if event else []})
        lines.append({"who": "npc", "text": "Sure."})
    return lines


def mood_section(prompt: str) -> str:
    return prompt.split("How you feel right now:")[1].split("\n\n")[0]


def test_right_after_rudeness_the_reply_is_short_and_cold_with_a_frown(client, provider):
    mood = mood_section(system(client, provider, history=learner_lines(None)))

    assert "short and cold" in mood and "Face: frown" in mood


def test_as_annoyance_fades_the_reply_is_only_a_little_cool(client, provider):
    mood = mood_section(system(client, provider, history=learner_lines(None, "", "")))

    assert "a little cool" in mood and "Face: indifferent" in mood and "short and cold" not in mood


@pytest.mark.parametrize(
    ("event", "cause"),
    [("COMPLIMENT", "complimented you"), ("HELP", "offered to help you")],
)
def test_the_good_mood_names_what_brought_it_on(client, provider, event, cause):
    mood = mood_section(system(client, provider, history=learner_lines(event)))

    assert cause in mood and "Face: warm_smile" in mood


def test_good_things_one_after_another_show_a_really_good_mood(client, provider):
    mood = mood_section(system(client, provider, history=learner_lines("COMPLIMENT", "SMALL_TALK")))

    assert "really good mood" in mood and "chatting with you warmly" in mood


def test_a_starting_mood_from_needs_also_names_a_face(client, provider):
    mood = mood_section(system(client, provider, needs={**FULL, "ENERGY": LOW - 1}))

    assert "Face: calm" in mood


def test_the_mood_sits_in_this_turn_where_rudeness_is_allowed_to_matter(client, provider):
    """인격 카드가 있는 NPC는 '이번 턴' 칸이 '이 칸이 무례라고 하지 않으면 무례가 아니다'라고 한다. 기분 줄이 상황 칸에 있으면
    모델이 남은 짜증을 무시해서(실제 모델 확인), 대답 바로 앞인 이번 턴 칸에 넣는다."""
    turn = system(client, provider, history=learner_lines(None)).split("## This turn")[1]

    assert "How you feel right now:" in turn and "still annoyed" in turn


def test_on_the_role_line_prompt_the_mood_comes_right_before_the_reply(client, provider):
    prompt = system(client, provider, NPC, history=learner_lines(None))

    assert "still annoyed" in prompt.split("IMPORTANT for your next reply")[1]


def test_the_face_rules_point_to_the_mood_lines(client, provider):
    """표정 고르기 지시가 지금 기분 줄의 표정을 따르게 한다(앞 턴의 기분이 다음 대답 표정에도 이어지게)."""
    assert "How you feel right now" in system(client, provider).split("Internal face timing")[1]


@pytest.mark.parametrize("npc", [NPC, WITH_PROFILE], ids=["role-line", "profile"])
def test_a_low_need_colors_even_the_first_greeting(client, provider, npc):
    """대화를 시작할 때의 욕구로 바탕 기분을 정한다. 아직 아무 말도 오가지 않은 첫 인사에도 드러난다."""
    prompt = system(client, provider, npc, needs={**FULL, "ENERGY": LOW - 1})

    assert "You are tired" in prompt


def test_the_starting_mood_comes_with_the_relationship_too(client, provider):
    prompt = system(client, provider, relationship={"recognition": 0}, needs={**FULL, "HUNGER": LOW - 1})

    assert "You are hungry" in prompt and "You do not recognize this person" in prompt


def test_an_npc_whose_needs_are_all_met_is_relaxed(client, provider):
    assert "easy mood" in system(client, provider, needs=FULL)


def test_a_relaxed_mood_keeps_the_face_of_the_personality(client, provider):
    """욕구로 정한 여유로움은 웃는 얼굴을 지정하지 않는다. 무뚝뚝한 NPC(Jake)도 욕구가 넉넉하면 warm_smile로
    시작하던 문제(실제 모델 확인). 웃음은 성격과 이번 대화의 사건(칭찬 등)이 정한다."""
    mood = mood_section(system(client, provider, needs=FULL))

    assert "warm_smile" not in mood and "usual face" in mood


def test_a_lonely_npc_starts_a_little_sad_instead_of_smiling(client, provider):
    """외로움(사교 욕구 낮음)은 외로움 전용 표정이 없어 가장 가까운 sad로 시작한다. 밝아지는 건 성격만큼만."""
    mood = mood_section(system(client, provider, needs={**FULL, "SOCIAL": LOW - 1}))

    assert "warm_smile" not in mood and "Face: sad" in mood


def test_without_needs_or_events_there_is_no_mood_line(client, provider):
    """욕구가 오지 않고(이전 앱) 기분이 드러날 일도 없으면 지시문은 지금과 같다."""
    assert "How you feel right now:" not in system(client, provider, history=[], memory=MEMORY)


@pytest.mark.parametrize(("need", "value"), [("ENERGY", 101), ("ENERGY", -1), ("SLEEP", 50)])
def test_needs_out_of_range_or_unknown_are_an_invalid_request(client, need, value):
    r = client.post("/v1/npc/reply", json={"npc": NPC, "history": [], "text": "hi", "tts": False, "needs": {need: value}})

    assert r.status_code == 400


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


def test_the_backend_instruction_comes_before_attitude_and_mood(client, provider):
    """퀘스트 단계 지시(빠진 정보 묻기·물건 건네기 등)는 꼭 해야 한다. 적대·짜증·성격 줄이 이를 덮지 않게, 이번 턴 칸에 지시가 먼저라고 적는다."""
    prompt = system(
        client,
        provider,
        relationship={"recognition": 30, "affinity": -40},
        history=learner_lines(None),
        instruction="Ask how they want to pay.",
    )
    turn = prompt.split("## This turn")[1]

    assert "Whatever the IMPORTANT instruction asks you to say or do comes first" in turn


def test_without_attitude_mood_or_temperament_there_is_no_extra_priority_line(client, provider):
    assert "Whatever the IMPORTANT instruction asks" not in system(client, provider, NPC, history=[])
