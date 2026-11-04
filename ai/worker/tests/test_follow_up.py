"""/v1/npc/follow-up: 대화가 끝난 뒤 NPC가 곧 건네기로 한 것. 실제 외부 모델을 부르지 않는다."""

import json

from conftest import ok

NPC = {"id": "CAFE_STAFF", "name": "Mia", "role": "the barista at Corner Cafe.", "scene": "Corner Cafe."}
ORDER = [
    {"who": "npc", "text": "Hi! What can I get for you?"},
    {"who": "me", "text": "One iced latte, please."},
    {"who": "npc", "text": "Sure, coming right up. It'll be ready in a minute."},
]


def owes(item="iced latte", item_ko="아이스 라테", wait="QUICK", **kw):
    return lambda body: ok(json.dumps({"owes": True, "item": item, "item_ko": item_ko, "wait": wait, **kw}))


def test_an_order_being_prepared_comes_back_as_a_follow_up(client, provider):
    provider.answers["follow_up"] = owes(item="  iced latte ", item_ko=" 아이스 라테 ")
    r = client.post("/v1/npc/follow-up", json={"npc": NPC, "history": ORDER})
    assert r.status_code == 200
    body = r.json()
    assert body["follow_up"] == {"item": "iced latte", "item_ko": "아이스 라테", "wait": "QUICK"}
    assert [u["step"] for u in body["usage"]] == ["follow_up"]
    assert [step for step, _ in provider.calls] == ["follow_up"]


def test_nothing_owed_is_null(client, provider):
    provider.answers["follow_up"] = lambda body: ok(json.dumps({"owes": False, "item": "", "item_ko": "", "wait": "QUICK"}))
    r = client.post("/v1/npc/follow-up", json={"npc": NPC, "history": ORDER})
    assert r.status_code == 200 and r.json()["follow_up"] is None


def test_owing_without_a_name_for_the_item_is_invalid_output(client, provider):
    provider.answers["follow_up"] = owes(item_ko=" ")
    r = client.post("/v1/npc/follow-up", json={"npc": NPC, "history": ORDER})
    assert r.status_code == 502 and r.json()["error"]["code"] == "invalid_output"


def test_a_conversation_the_learner_never_spoke_in_is_rejected_without_calling_the_model(client, provider):
    r = client.post("/v1/npc/follow-up", json={"npc": NPC, "history": [ORDER[0]]})
    assert r.status_code == 400
    assert provider.calls == []


def test_the_model_sees_who_where_and_the_conversation_but_not_rude_wording(client, provider):
    seen = {}

    def answer(body):
        seen["user"] = body["messages"][1]["content"]
        return ok(json.dumps({"owes": False, "item": "", "item_ko": "", "wait": "QUICK"}))

    provider.answers["follow_up"] = answer
    history = [*ORDER, {"who": "me", "text": "Hurry up, idiot.", "rude": True}]
    client.post("/v1/npc/follow-up", json={"npc": NPC, "history": history})
    assert "Mia, the barista at Corner Cafe." in seen["user"]
    assert "Place: Corner Cafe." in seen["user"]
    assert "Customer: One iced latte, please." in seen["user"]
    assert "Mia: Sure, coming right up." in seen["user"]
    assert "idiot" not in seen["user"] and "[RUDE: content withheld]" in seen["user"]
