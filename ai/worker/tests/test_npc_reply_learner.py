"""대답 경로(/v1/npc/reply)의 학습자 호칭. 백엔드가 학습자 캐릭터 기본형(F·M)과 닉네임을 learner에 실어 보내면 그 캐릭터에 맞는 호칭만 쓰게 하고,
모르면 성별을 짐작한 호칭을 쓰지 않게 한다. 여성 캐릭터를 호텔 직원이 "Mr."로 부르던 문제(S15P31A305-398). 가짜 공급자라 진짜 호출 없음."""

import pytest
from test_npc_reply_profile import NPC, profile_of, reply_request, system_of

HOTEL = {**NPC, "id": "HOTEL_STAFF", "profile": profile_of("HOTEL_STAFF")}


def other_person(system: str) -> str:
    """시스템 프롬프트에서 학습자 호칭 부분."""
    return system.split("## The other person")[1].split("##")[0]


def test_a_woman_character_is_never_called_sir_or_mr(client, provider):
    part = other_person(system_of(reply_request(client, provider, HOTEL, learner={"avatar_type": "F"})))
    assert "woman" in part and "ma'am" in part and "Ms." in part
    assert 'never "sir" or "Mr."' in part


def test_a_man_character_is_never_called_ma_am_or_ms(client, provider):
    part = other_person(system_of(reply_request(client, provider, HOTEL, learner={"avatar_type": "M"})))
    assert "man" in part and "sir" in part and "Mr." in part
    assert 'never "ma\'am" or "Ms."' in part


def test_without_the_character_the_npc_does_not_guess_a_gendered_title(client, provider):
    part = other_person(system_of(reply_request(client, provider, HOTEL)))
    assert "Do not use gendered titles" in part


def test_the_nickname_is_given_but_only_said_when_it_is_a_real_english_name(client, provider):
    system = system_of(reply_request(client, provider, HOTEL, learner={"nickname": "Dabin", "avatar_type": "F"}))
    part = other_person(system)
    assert '"Dabin"' in part and "real name written in English letters" in part


def test_the_other_person_comes_after_the_character_and_before_the_situation(client, provider):
    system = system_of(reply_request(client, provider, HOTEL, learner={"avatar_type": "F"}))
    assert system.index("## Character") < system.index("## The other person") < system.index("## Situation")


def test_without_a_profile_the_short_prompt_also_gets_the_title_rule(client, provider):
    system = system_of(reply_request(client, provider, NPC, learner={"avatar_type": "F"}))
    assert system.startswith("You are Mia, the barista at the corner cafe.")
    assert 'never "sir" or "Mr."' in other_person(system)


@pytest.mark.parametrize("learner", [{"avatar_type": "X"}, {"nickname": "n" * 33}])
def test_an_unknown_character_or_too_long_nickname_is_an_invalid_request(client, learner):
    r = client.post("/v1/npc/reply", json={"npc": HOTEL, "history": [], "text": "hi", "tts": False, "learner": learner})
    assert r.status_code == 400 and r.json()["error"]["code"] == "invalid_request"
