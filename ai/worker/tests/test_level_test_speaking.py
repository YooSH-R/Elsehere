"""레벨 테스트 말하기 문항 판정(/v1/level-test/speaking). 가짜 공급자라 진짜 호출 없음."""

import json

import pytest

from conftest import ok

BAG = {
    "item_code": "EN_LT_S1",
    "situation": "카페에 가방을 두고 왔어요. 가방이 어떻게 생겼는지 말하고 찾아 달라고 하세요.",
    "cues": ["파란 숄더백", "빨간 열쇠고리", "안에 여권 지갑과 책"],
    "criteria": "Says they left or lost a bag at the cafe and describes it with all three details "
    "(a blue shoulder bag, a red keyring, a passport wallet and a book inside), in English sentences rather than a list of words.",
    "answer": "I think I left my bag here. It's a blue shoulder bag with a red keyring. My passport wallet and a book are in it.",
}
LIBRARY = {
    "item_code": "EN_LT_S2",
    "situation": "길을 잃었어요. 지나가는 사람에게 걸어서 도서관에 가는 길을 물어보세요.",
    "cues": ["도서관", "걸어서"],
    "criteria": "Asks a passerby how to get to the library on foot, in an English question.",
    "answer": "library... walk",
}
FEEDBACK = "가방 설명은 잘했어요. 길을 물을 때 How do I get to…? 표현을 연습해 보세요."


def user_msg(body: dict) -> str:
    return body["messages"][-1]["content"]


def judged(results: list[dict], feedback: str = FEEDBACK) -> str:
    return json.dumps({"results": results, "feedback_ko": feedback}, ensure_ascii=False)


def test_speaking_gives_pass_and_fail_in_request_order_with_feedback(client, provider):
    # 모델이 순서를 바꿔 답해도 응답은 요청 문항 순서
    provider.answers["level_test_speaking"] = lambda b: ok(
        judged([{"item_code": "EN_LT_S2", "passed": False}, {"item_code": "EN_LT_S1", "passed": True}])
    )
    r = client.post("/v1/level-test/speaking", json={"items": [BAG, LIBRARY]})
    assert r.status_code == 200
    body = r.json()
    assert body["results"] == [{"item_code": "EN_LT_S1", "passed": True}, {"item_code": "EN_LT_S2", "passed": False}]
    assert body["feedback_ko"] == FEEDBACK
    assert [u["step"] for u in body["usage"]] == ["level_test_speaking"]


def test_speaking_prompt_has_each_items_criteria_and_answer(client, provider):
    seen = {}
    unanswered = LIBRARY | {"item_code": "EN_LT_S3", "answer": ""}

    def answer(b):
        seen["body"] = b
        return ok(judged([{"item_code": code, "passed": False} for code in ("EN_LT_S1", "EN_LT_S2", "EN_LT_S3")]))

    provider.answers["level_test_speaking"] = answer
    r = client.post("/v1/level-test/speaking", json={"items": [BAG, LIBRARY, unanswered]})
    assert r.status_code == 200
    msg = user_msg(seen["body"])
    for item in (BAG, LIBRARY):
        assert item["criteria"] in msg and item["answer"] in msg and item["situation"] in msg
    assert "빨간 열쇠고리" in msg
    assert "(no answer)" in msg  # 빈 답은 빈 줄이 아니라 답하지 않았다고 알린다
    assert seen["body"]["temperature"] == 0


@pytest.mark.parametrize(
    "out",
    [
        judged([{"item_code": "EN_LT_S1", "passed": True}]),  # 문항 빠짐
        judged(
            [
                {"item_code": "EN_LT_S1", "passed": True},
                {"item_code": "EN_LT_S2", "passed": False},
                {"item_code": "EN_LT_S9", "passed": True},
            ]
        ),
        judged([{"item_code": "EN_LT_S1", "passed": "yes"}, {"item_code": "EN_LT_S2", "passed": False}]),  # 불이 아님
        judged([{"item_code": "EN_LT_S1", "passed": True}, {"item_code": "EN_LT_S2", "passed": False}], feedback="  "),
        '{"results": []}',
        "not json",
    ],
    ids=["missing_item", "unknown_item", "passed_not_bool", "blank_feedback", "no_feedback", "not_json"],
)
def test_speaking_output_that_misses_items_or_breaks_format_is_invalid_output(client, provider, out):
    provider.answers["level_test_speaking"] = lambda b: ok(out)
    r = client.post("/v1/level-test/speaking", json={"items": [BAG, LIBRARY]})
    assert r.status_code == 502 and r.json()["error"]["code"] == "invalid_output"


@pytest.mark.parametrize(
    "items",
    [
        [],
        [BAG | {"item_code": f"EN_LT_S{n}"} for n in range(1, 7)],  # 6개
        [BAG, BAG],  # 같은 문항 코드
    ],
    ids=["no_items", "six_items", "same_item_code"],
)
def test_speaking_request_needs_one_to_five_distinct_items(client, items):
    r = client.post("/v1/level-test/speaking", json={"items": items})
    assert r.status_code == 400 and r.json()["error"]["code"] == "invalid_request"
