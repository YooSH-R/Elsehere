"""기억 카드의 requests: 학습자가 NPC에게 요청하고 함께 한 일(주문·길 묻기·구매·짐 맡기기 등)을 모델이 자유 문장으로 적고,
다음 방문의 대답 지시에 넣어 "지난번엔 라테 드셨죠", 단골 메뉴 권하기가 나오게 한다. 요청 종류마다 변수를 만들지 않는다.
가짜 공급자라 진짜 호출 없음."""

import json

from test_npc_reply_profile import NPC, profile_of, reply_request, system_of

from app import npc_relationship as rel
from conftest import ok

RULES = rel.load_world_rules()["relationship"]
CARD = {
    "name": "Minsu",
    "claims": [],
    "events": [],
    "attitude": "neutral",
    "struggled": [],
    "requests": ["ordered an iced vanilla latte", "asked the way to the bank"],
}
WITH_PROFILE = {**NPC, "profile": profile_of("CAFE_STAFF")}


def memory_request(old=None):
    return {
        "npc": NPC,
        "outcome": "The learner ended the conversation normally.",
        "history": [{"who": "me", "text": "One iced vanilla latte, please."}, {"who": "npc", "text": "Coming right up!"}],
        "memory_card": old,
    }


def test_the_memory_model_is_asked_to_write_what_they_requested(client, provider):
    seen = {}

    def answer(body):
        seen["body"] = body
        return ok(json.dumps({**CARD, "impression": "neutral"}))

    provider.answers["memory"] = answer
    assert client.post("/v1/memory", json=memory_request()).status_code == 200

    schema = seen["body"]["response_format"]["json_schema"]["schema"]
    assert "requests" in schema["required"] and schema["properties"]["requests"]["type"] == "array"
    assert "requests:" in seen["body"]["messages"][0]["content"]


def test_requests_are_kept_on_the_card_with_the_same_limits_as_other_lists(client, provider):
    many = [f"ordered drink number {i}" for i in range(7)]
    card = {**CARD, "requests": ["call me at 010-1234-5678 about the cake", " ", *many]}
    provider.answers["memory"] = lambda body: ok(json.dumps({**card, "impression": "neutral"}))

    result = client.post("/v1/memory", json=memory_request()).json()["memory_card"]

    assert result["requests"] == many[:5]  # 연락처·빈 항목을 빼고 최대 5개


def test_a_card_without_requests_breaks_the_contract(client, provider):
    broken = {key: value for key, value in CARD.items() if key != "requests"}
    provider.answers["memory"] = lambda body: ok(json.dumps({**broken, "impression": "neutral"}))

    response = client.post("/v1/memory", json=memory_request())

    assert response.status_code == 502 and response.json()["error"]["code"] == "invalid_output"


def visit_prompt(client, provider, recognition: int) -> str:
    return system_of(
        reply_request(client, provider, WITH_PROFILE, relationship={"recognition": recognition}, memory={"visits": 2, "card": CARD})
    )


def test_a_familiar_face_only_lightly_checks_something_asked_for_more_than_once(client, provider):
    """한 번 온 손님의 주문을 두 번째 방문에 권하면 단골 대접이라 어색하다(설계 5절: 한 번 시키면 다음엔 못 떠올리고, 되풀이하면 떠올림).
    본 적 있는 얼굴에는 두 번 이상 한 요청만 가볍게 확인하게 하고, 먼저 권하지 않게 한다."""
    prompt = visit_prompt(client, provider, RULES["seenBefore"])

    assert "What they asked for or did with you before: ordered an iced vanilla latte; asked the way to the bank" in prompt
    assert "only bring up something they asked for more than once" in prompt and "never say it is free" in prompt
    assert "suggest what they usually get first" not in prompt


def test_a_regular_is_offered_what_they_usually_get(client, provider):
    prompt = visit_prompt(client, provider, RULES["regular"])

    assert "if they ask what to get or seem undecided, suggest what they usually get first" in prompt
    assert "only bring up something they asked for more than once" not in prompt


def test_the_memory_model_writes_how_often_a_request_happened(client, provider):
    """본 적 있는 얼굴이 '두 번 이상'을 알아볼 수 있게, 기억 모델이 되풀이된 요청에 횟수를 적는다."""
    seen = {}

    def answer(body):
        seen["system"] = body["messages"][0]["content"]
        return ok(json.dumps({**CARD, "impression": "neutral"}))

    provider.answers["memory"] = answer
    client.post("/v1/memory", json=memory_request())

    assert "says how often" in seen["system"]


def test_a_new_face_does_not_recall_what_they_asked_for(client, provider):
    prompt = system_of(reply_request(client, provider, WITH_PROFILE, relationship={"recognition": 0}, memory={"visits": 2, "card": CARD}))

    assert "iced vanilla latte" not in prompt


def test_last_order_is_gone_from_the_reply_prompt(client, provider):
    """서버가 늘 null로 보내던 last_order는 정리했다. 이전 Spring이 보내도 무시한다."""
    memory = {"visits": 2, "last_order": "a flat white", "card": {**CARD, "requests": []}}
    prompt = system_of(reply_request(client, provider, WITH_PROFILE, relationship={"recognition": RULES["seenBefore"]}, memory=memory))

    assert "Last time they ordered" not in prompt and "flat white" not in prompt
