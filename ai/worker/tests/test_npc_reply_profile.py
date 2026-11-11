"""대답 경로(/v1/npc/reply)의 인격 프로필. 백엔드가 인격 설정(npc-configurations.json)의 npcProfile을 npc.profile에 그대로 실어 보내면
공통 규칙과 인격으로 대답하고, 없으면 지금 프롬프트 그대로 대답한다. 가짜 공급자라 진짜 호출 없음."""

import json
from pathlib import Path

import pytest

from app.npc_prompt import MIDDLE
from conftest import ok, stream

CONFIGURATIONS = Path(__file__).resolve().parents[3] / "backend/src/main/resources/content/npc-configurations.json"
SERVICE_NPCS = [
    "ARCADE_STAFF",
    "BAKERY_STAFF",
    "BANK_STAFF",
    "BARBER_STAFF",
    "CAFE_BARISTA",
    "CAFE_SERVER",
    "CAFE_STAFF",
    "DINER_STAFF",
    "FIRE_CAPTAIN",
    "FIRE_FIGHTER",
    "FIRE_ROOKIE",
    "FLOWER_SHOP_STAFF",
    "HOME_STORE_STAFF",
    "HOTEL_BELLHOP",
    "HOTEL_CONCIERGE",
    "HOTEL_NIGHT_MANAGER",
    "HOTEL_RECEPTIONIST",
    "HOTEL_STAFF",
    "LAUNDROMAT_STAFF",
    "POLICE_OFFICER",
    "POLICE_PATROL_OFFICER",
    "POLICE_ROOKIE",
    "POLICE_SERGEANT",
    "POST_OFFICE_STAFF",
    "SHOP_NIGHT_CLERK",
    "SHOP_PHARMACIST",
    "SHOP_STAFF",
    "TAXI_DRIVER",
    "TOWN_EVENING_WALKER",
    "TOWN_GRANDFATHER",
    "TOWN_GRANDMOTHER",
    "TOWN_HANDYMAN",
    "TOWN_PASSERBY",
    "TOWN_RETIREE",
    "TOWN_SHOPPER",
    "TOWN_STUDENT",
]
NPC = {
    "id": "CAFE_STAFF",
    "name": "Mia",
    "role": "the barista at the corner cafe.",
    "facts": "Menu: latte $5.",
    "scene": "The counter of the corner cafe.",
}


def profile_of(npc_id: str) -> dict:
    """백엔드가 그대로 실어 보낼 인격: 설정 파일에서 그 NPC의 npcProfile."""
    configurations = json.loads(CONFIGURATIONS.read_text(encoding="utf-8"))
    return next(configuration["npcProfile"] for configuration in configurations if configuration["npcId"] == npc_id)


def reply_request(client, provider, npc: dict, **body) -> dict:
    """대답을 한 번 요청하고 모델에 보낸 요청 본문을 돌려준다."""
    seen = {}

    def answer(b):
        seen["body"] = b
        return stream(["Hi there!"])

    provider.answers["npc"] = answer
    r = client.post("/v1/npc/reply", json={"npc": npc, "history": [], "text": "hi", "tts": False, **body})
    assert r.status_code == 200
    return seen["body"]


def system_of(body: dict) -> str:
    return body["messages"][0]["content"]


def test_without_a_profile_the_reply_keeps_the_current_prompt(client, provider):
    system = system_of(reply_request(client, provider, NPC))
    assert system.startswith("You are Mia, the barista at the corner cafe.") and "## Character" not in system


@pytest.mark.parametrize("with_profile", [False, True])
def test_current_game_time_takes_precedence_over_rubys_morning_routine(client, provider, with_profile):
    npc = {**NPC, "name": "Ruby", "scene": "Town street. Current game time: day 2, 23:00 (24-hour clock)."}
    if with_profile:
        npc["profile"] = profile_of("TOWN_RETIREE")
    system = system_of(reply_request(client, provider, npc))
    assert "Current game time: day 2, 23:00" in system
    assert "Use the current game time" in system
    assert "takes precedence over routines, profile greetings and earlier dialogue" in system
    assert "Never infer the current time from a character's usual morning or evening activity" in system


@pytest.mark.parametrize("npc_id", SERVICE_NPCS)
def test_the_profile_from_the_backend_settings_becomes_the_character(client, provider, npc_id):
    profile = profile_of(npc_id)
    system = system_of(reply_request(client, provider, {**NPC, "id": npc_id, "profile": profile}))
    assert "## Rules" in system and "## Character" in system
    assert f"Name: {profile['name']}" in system and profile["personality"]["traits"][0]["name"] in system
    assert "You are Mia, the barista" not in system  # 한 줄짜리 역할 대신 인격


def test_the_common_rules_come_before_the_character_and_this_turn_comes_last(client, provider):
    system = system_of(reply_request(client, provider, {**NPC, "profile": profile_of("CAFE_STAFF")}))
    headers = ["## Rules", "## Character", "## Town", "## Situation", "## Learner level", "## This turn"]
    assert [system.index(header) for header in headers] == sorted(system.index(header) for header in headers)


def test_with_a_profile_the_place_facts_memory_level_and_instruction_still_reach_the_reply(client, provider):
    body = reply_request(
        client,
        provider,
        {**NPC, "profile": profile_of("CAFE_STAFF")},
        memory={"visits": 2, "last_order": "a latte", "card": {"name": "Minsu", "claims": [], "events": [], "struggled": []}},
        instruction="Ask what size they want.",
        level=1,
    )
    system = system_of(body)
    assert "Maple Town" in system and "The counter of the corner cafe." in system  # 장소
    assert "Menu: latte $5." in system  # 백엔드가 보낸 사실
    assert "Their name: Minsu" in system and "Last time they ordered a latte" in system  # 기억
    assert "each under 12 words" in system and body["max_tokens"] == 154  # 난이도 1 대사 규칙 + 표정 표시 64토큰
    assert "Ask what size they want." in system  # 이번 대답에서 할 일


@pytest.mark.parametrize("npc_id", SERVICE_NPCS)
def test_with_a_profile_every_npc_knows_the_whole_town(client, provider, npc_id):
    """택시 기사가 마을에 하나뿐인 카페를 '어느 카페요?'라고 되묻고, 호텔 직원이 우산 파는 곳·밥집을 모른다고 했다.
    모든 NPC가 같은 마을 안내를 받아 3D 마을에 있는 곳으로 안내한다."""
    system = system_of(reply_request(client, provider, {**NPC, "id": npc_id, "profile": profile_of(npc_id)}))
    town = system.split("## Town")[1].split("## ")[0]
    assert "Corner Cafe" in town and "DRUGSTORE" in town and "Maple Diner" in town and "police station" in town
    assert "There are no other shops, restaurants or attractions" in town  # 없는 곳은 지어내지 않게


def test_with_a_profile_every_npc_can_tell_where_places_are_including_the_fountain(client, provider):
    """마을 안내에 분수대 공원이 없고 '그 밖엔 아무것도 없다'고만 해서 NPC가 분수대를 모른다고 했다(2026-11-05).
    3D 마을(TownScene)에 있는 공원·서점·아이스크림 트럭까지 적고, 길을 알려 줄 수 있게 장소마다 어디쯤인지 적는다."""
    system = system_of(reply_request(client, provider, {**NPC, "profile": profile_of("HOTEL_STAFF")}))
    town = system.split("## Town")[1].split("## ")[0]
    assert "fountain" in town and "benches" in town
    assert "Paper & Leaf Books" in town and "ice cream truck" in town
    assert "crossroads" in town  # 길 안내의 기준점 (본길 가운데 신호등 교차로)
    park = next(line for line in town.splitlines() if "fountain" in line)
    assert "across from Maple Bakery" in park


def test_with_a_profile_the_rules_do_not_tell_the_npc_to_say_it_is_not_sure(client, provider):
    """Facts에 없으면 '잘 모르겠다·도와줄 수 없다'고 하라던 규칙 때문에 룸서비스·와이파이·담당자 호출까지 막혔다."""
    rules = system_of(reply_request(client, provider, {**NPC, "profile": profile_of("HOTEL_STAFF")})).split("## Character")[0]
    assert "say you are not sure" not in rules
    assert "Never end with only" in rules and "concrete next step" in rules


def test_with_a_profile_the_npc_contacts_off_screen_people_in_the_same_reply(client, provider):
    """NPC가 매니저·요리사·친구처럼 게임에 나오지 않는 사람에게 '연락해 볼게요'라고만 하고 끝나, 그 연락이 영영 오지 않았다.
    연락이 필요하면 같은 대답 안에서 바로 하고 그 사람의 답을 전한다. 나중에 연락하겠다는 약속과 메시지 남기기는 하지 않는다."""
    rules = system_of(reply_request(client, provider, {**NPC, "profile": profile_of("HOTEL_STAFF")})).split("## Character")[0]
    assert "take a message" not in rules
    assert "right now, in this reply" in rules and "pass on their answer" in rules
    assert "Never promise to contact someone later" in rules


@pytest.mark.parametrize("instruction", ["", "Ask what size they want."])
def test_with_a_profile_this_turn_says_their_words_were_not_rude_unless_the_backend_said_so(client, provider, instruction):
    """무례를 못 참는 카페 사장이 판정기가 무례 아님으로 본 재촉("hurry up")에 정중히 말하라고 했다.
    무례일 때만 백엔드가 경고 지시문을 보내므로, 대답 바로 앞 칸에서 그때만 무례로 대하라고 한다."""
    system = system_of(reply_request(client, provider, {**NPC, "profile": profile_of("CAFE_STAFF")}, instruction=instruction))
    this_turn = system.split("## This turn")[1]
    assert "Unless this section says they were rude, their words were not rude" in this_turn


@pytest.mark.parametrize("level", [1, 2, 3])
def test_with_a_profile_the_level_rule_leaves_the_tone_to_the_character(client, provider, level):
    """원래 난이도 문장은 따뜻함·유머를 내라고 해서, 불친절한 인격(Jake)과 부딪히지 않게 말투를 정하지 않는 규칙을 쓴다."""
    system = system_of(reply_request(client, provider, {**NPC, "profile": profile_of("HOTEL_STAFF")}, level=level))
    level_part = system.split("## Learner level")[1].split("## This turn")[0]
    assert "warmth" not in level_part and "humor" not in level_part


@pytest.mark.parametrize("level", [1, 2, 3])
def test_every_level_asks_for_the_idioms_and_slang_a_local_really_uses(client, provider, level):
    """관용구·속어를 막으면 짧은 대답에 인물다운 표현이 들어갈 자리가 없어 모든 NPC가 같은 매장 직원 말투가 된다(2026-11-04 결정).
    초급도 문장 길이는 지키고, 모르는 표현은 학습자가 뜻 보기(CONV-04)의 풀이로 익힌다. 인격 카드가 없는 NPC도 같다."""
    for npc in ({**NPC, "profile": profile_of("HOTEL_STAFF")}, NPC):
        system = system_of(reply_request(client, provider, npc, level=level))

        assert "idioms" in system and "slang" in system, npc["id"]
        assert "No idioms" not in system and "No slang" not in system and "Skip the heavy slang" not in system, npc["id"]


@pytest.mark.parametrize("level", [1, 2, 3])
def test_every_level_points_to_the_characters_own_catchphrases(client, provider, level):
    """말버릇은 긴 인격 칸 속 한 줄이라, 대답 길이를 정하는 학습 수준 문장에서 함께 가리키지 않으면 거의 쓰지 않는다
    (대답 128개 중 8개, npc_voice_eval). 관용구를 넣으라는 문장이 인물 자신의 말버릇도 가리킨다."""
    system = system_of(reply_request(client, provider, {**NPC, "profile": profile_of("CAFE_BARISTA")}, level=level))
    level_part = system.split("## Learner level")[1].split("## This turn")[0]

    assert "catchphrases" in level_part


def test_this_turn_asks_for_an_unused_catchphrase_that_fits(client, provider):
    """인격 칸·학습 수준 문장만으로는 말버릇이 대답 128개 중 10개였고, 대답 바로 앞 칸에서 한 번 더 말하면 18개였다
    (같은 대화에서 되풀이 0, npc_voice_eval). 말버릇이 없는 인물에게는 이 줄을 넣지 않는다."""
    with_phrases = profile_of("CAFE_BARISTA")
    without_phrases = profile_of("CAFE_BARISTA")
    del without_phrases["speechStyle"]["catchphrases"]

    this_turn = system_of(reply_request(client, provider, {**NPC, "profile": with_phrases})).split("## This turn")[1]
    plain_turn = system_of(reply_request(client, provider, {**NPC, "profile": without_phrases})).split("## This turn")[1]

    assert "if one of your catchphrases fits this reply and you have not said it in this conversation yet, use it" in this_turn
    assert "catchphrases" not in plain_turn


def test_intermediate_level_rule_is_the_same_with_or_without_a_profile(client, provider):
    """중급(S15P31A305-751)은 인격 카드가 있든 없든 같은 규칙으로 말한다. 경로마다 다르면 NPC에 따라 난이도가 달라진다."""
    with_profile = system_of(reply_request(client, provider, {**NPC, "profile": profile_of("HOTEL_STAFF")}, level=2))
    without_profile = system_of(reply_request(client, provider, NPC, level=2))

    assert MIDDLE in with_profile and MIDDLE in without_profile


@pytest.mark.parametrize(("path", "value"), [("name", None), ("nickname", "Mimi")])
def test_a_profile_outside_the_shared_schema_is_an_invalid_request(client, path, value):
    profile = profile_of("CAFE_STAFF")
    if value is None:
        del profile[path]  # 필수 칸 빠짐
    else:
        profile[path] = value  # 스키마에 없는 칸
    r = client.post("/v1/npc/reply", json={"npc": {**NPC, "profile": profile}, "history": [], "text": "hi", "tts": False})
    assert r.status_code == 400 and r.json()["error"]["code"] == "invalid_request"


def test_other_requests_accept_the_same_npc_with_its_profile(client, provider):
    """백엔드는 모든 워커 요청에 같은 npc를 보낸다. 대답 외 요청(힌트 등)도 인격이 실린 npc를 받는다."""
    say_this = [{"en": "One latte, please.", "ko": "라테 하나 주세요."}]
    provider.answers["hint"] = lambda b: ok(json.dumps({"say_this": say_this, "keywords": [{"en": "latte", "ko": "라테"}]}))
    r = client.post("/v1/hint", json={"npc": {**NPC, "profile": profile_of("CAFE_STAFF")}, "goal": "order", "history": []})
    assert r.status_code == 200
