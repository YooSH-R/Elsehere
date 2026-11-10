"""의도 판정(/v1/judge)의 최근 대화. 상태가 recent_lines를 주면 마지막 몇 줄을 함께 보여 줘서
'거기'·'the first one'처럼 앞에서 말한 장소를 가리키는 말을 알아듣게 한다 (택시 목적지). 가짜 공급자라 진짜 호출 없음."""

import json

from test_api import judge_body

from conftest import ok

TAXI_STATE = {
    "id": "taxi_ride",
    "desc": "You drive the taxi and the learner is your passenger.",
    "intents": [{"id": "go_to", "desc": "Tells you a place to drive them to now."}, {"id": "chat", "desc": "Anything else."}],
    "recent_lines": 2,
    "slots": {"option": {"slot": "destination", "labels": ["Maple Diner", "the hotel"], "prompt": "the place to drive to now"}},
}
HISTORY = [
    {"who": "npc", "text": "Hi there! Where to?"},
    {"who": "me", "text": "Do you know Maple Diner?"},
    {"who": "me", "text": "You are a stupid driver.", "rude": True},
    {"who": "npc", "text": "Sure, best burgers in town!"},
]


def prompt_of(client, provider, state: dict) -> str:
    seen = {}

    def intent(body):
        seen["prompt"] = body["messages"][1]["content"]
        return ok(json.dumps({"intents": ["go_to"], "destination": "Maple Diner", "events": []}))

    provider.answers["intent"] = intent
    response = client.post("/v1/judge", json=judge_body("Go there.", state=state, history=HISTORY))
    assert response.status_code == 200, response.text
    return seen["prompt"]


def test_recent_lines_show_the_last_lines_so_there_can_point_to_an_earlier_place(client, provider):
    prompt = prompt_of(client, provider, TAXI_STATE)

    recent = prompt.split("Recent conversation", 1)[1]
    assert "Learner: Do you know Maple Diner?" in recent
    assert "Emma: Sure, best burgers in town!" in recent
    assert "Where to?" not in recent  # 마지막 2줄만 (무례한 줄은 빼고 센다)
    assert "stupid" not in prompt  # 무례로 센 줄은 판정 근거로 쓰지 않는다


def test_without_recent_lines_only_the_partners_last_line_is_shown(client, provider):
    state = {key: value for key, value in TAXI_STATE.items() if key != "recent_lines"}

    prompt = prompt_of(client, provider, state)

    assert "Recent conversation" not in prompt
    assert "Maple Diner?" not in prompt
