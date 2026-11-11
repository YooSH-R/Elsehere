"""학습 보조: NPC 대사 뜻·단어 풀이(/v1/meaning), 추천 단어(/v1/suggest), 대화 난이도(level). 가짜 공급자라 진짜 호출 없음."""

import json

import pytest

from conftest import FakeTts, make_client, ok, stream

NPC = {"id": "mia", "name": "Mia", "role": "the barista at Maple Cafe.", "facts": "Menu: latte $5.", "scene": "Inside Maple Cafe."}
HISTORY = [
    {"who": "npc", "text": "Hi! What can I get for you?"},
    {"who": "me", "text": "one latte"},
    {"who": "npc", "text": "Great! For here or to go?"},
]


def user_msg(body: dict) -> str:
    return body["messages"][-1]["content"]


# ---------------------------------------------------------------------
# 뜻 보기
# ---------------------------------------------------------------------
def test_meaning_gives_korean_and_glossary_from_the_line(client, provider):
    seen = {}

    def answer(b):
        seen["body"] = b
        return ok(
            json.dumps(
                {
                    "meaning_ko": "좋아요! 매장에서 드실 건가요, 가져가실 건가요?",
                    "glossary": [{"text": "to go", "meaning_ko": "포장"}, {"text": "takeout", "meaning_ko": "포장 (대사에 없음)"}],
                }
            )
        )

    provider.answers["meaning"] = answer
    r = client.post("/v1/meaning", json={"npc": NPC, "text": "Great! For here or to go?", "level": 1})
    assert r.status_code == 200
    body = r.json()
    assert body["meaning_ko"] == "좋아요! 매장에서 드실 건가요, 가져가실 건가요?"
    assert body["glossary"] == [{"text": "to go", "meaning_ko": "포장"}]  # 대사에 없는 말은 버린다
    assert [u["step"] for u in body["usage"]] == ["meaning"]
    assert "Great! For here or to go?" in user_msg(seen["body"]) and "Mia" in user_msg(seen["body"])


def test_meaning_prompt_keeps_proper_names_in_english_in_curly_quotes_shown_as_double_quotes(client, provider):
    # 'Maple Town'을 '단풍 마을'처럼 옮기거나 '메이플 타운'처럼 소리로 적지 않고 원문 그대로 따옴표 안에 둔다.
    # JSON 문자열 안의 곧은 따옴표는 모델이 이스케이프하지 않으면 문자열이 닫혀 뜻이 이름 앞에서 잘린다.
    # 그래서 모델은 이스케이프가 필요 없는 굽은 따옴표로 쓰고, 화면에는 곧은 따옴표로 바꿔 보낸다
    seen = {}

    def answer(b):
        seen["system"] = b["messages"][0]["content"]
        return ok(json.dumps({"meaning_ko": "“Maple Town”에 오신 걸 환영해요!", "glossary": []}, ensure_ascii=False))

    provider.answers["meaning"] = answer
    r = client.post("/v1/meaning", json={"npc": NPC, "text": "Welcome to Maple Town!"}).json()
    assert r["meaning_ko"] == '"Maple Town"에 오신 걸 환영해요!'
    assert "exactly as written in English, in curly double quotes “”" in seen["system"]
    assert "“Maple Town”에 오신 걸 환영해요!" in seen["system"]
    assert '"Maple Town"' not in seen["system"]


def test_meaning_glossary_keeps_phrases_and_their_words_up_to_twenty_five_in_order(client, provider):
    # 화면이 대사의 단어를 하나하나 눌러 볼 수 있게 하고, 숙어 안의 단어(grab)도 숙어(grab a quick bite)와 따로 남긴다.
    # 두세 문장인 대사도 뒤쪽 단어가 잘리지 않게 25개까지 둔다
    line = (
        "All right, I need to check in early and grab a quick bite before the big meeting downtown tonight,"
        " so please save me a seat near the front and call my cell phone if the boss shows up late"
    )
    words = (
        "All right", "right", "need", "check in", "check", "early", "grab a quick bite", "grab", "quick", "bite",
        "before", "big meeting", "big", "meeting", "downtown", "tonight", "save", "seat", "near", "front",
        "call", "cell phone", "cell", "phone", "boss", "shows up", "shows", "late",
    )  # fmt: skip
    items = [{"text": w, "meaning_ko": "뜻"} for w in words]
    provider.answers["meaning"] = lambda b: ok(json.dumps({"meaning_ko": "좋아, 일찍 체크인하고 간단히 먹어야 해", "glossary": items}))
    r = client.post("/v1/meaning", json={"npc": NPC, "text": line}).json()
    assert [g["text"] for g in r["glossary"]] == list(words[:25])


def test_meaning_prompt_asks_for_every_easy_word_and_the_words_inside_each_phrase(client, provider):
    # 모델이 just·still·here 같은 쉬운 말을 빼거나, 숙어(new here)만 주고 그 안의 단어를 빼던 것을 막는다
    seen = {}
    provider.answers["meaning"] = lambda b: (seen.update(body=b), ok(json.dumps({"meaning_ko": "뜻", "glossary": []})))[1]
    client.post("/v1/meaning", json={"npc": NPC, "text": "Hi! I'm just out for a walk. I'm still new here."})
    system = seen["body"]["messages"][0]["content"]
    assert "never leave a word out because it is easy" in system
    assert '"new here", "new", "here"' in system
    assert "At most 25 items" in system


@pytest.mark.parametrize("out", ["not json", "[]", '{"glossary": []}', '{"meaning_ko": "", "glossary": []}'])
def test_meaning_output_that_breaks_format_is_invalid_output(client, provider, out):
    provider.answers["meaning"] = lambda b: ok(out)
    r = client.post("/v1/meaning", json={"npc": NPC, "text": "Great!"})
    assert r.status_code == 502 and r.json()["error"]["code"] == "invalid_output"


def test_meaning_blank_line_is_invalid_request(client):
    r = client.post("/v1/meaning", json={"npc": NPC, "text": "  "})
    assert r.status_code == 400


# ---------------------------------------------------------------------
# 추천 단어
# ---------------------------------------------------------------------
def test_suggest_gives_three_words_for_the_reply(client, provider):
    seen = {}

    def answer(b):
        seen["body"] = b
        return ok(json.dumps({"words": ["for here", " to go ", "For here", 7, "please", "thanks"]}))

    provider.answers["suggest"] = answer
    r = client.post("/v1/suggest", json={"npc": NPC, "history": HISTORY, "goal": "choose for here or to go", "level": 1})
    assert r.status_code == 200
    body = r.json()
    assert body["words"] == ["for here", "to go", "please"]  # 공백 정리·중복(대소문자 무시)·문자열 아닌 것 빼고 3개까지
    assert [u["step"] for u in body["usage"]] == ["suggest"]
    msg = user_msg(seen["body"])
    assert "Great! For here or to go?" in msg and "choose for here or to go" in msg and "A1-A2" in msg


def test_suggest_without_npc_line_still_works(client, provider):
    provider.answers["suggest"] = lambda b: ok(json.dumps({"words": ["hello", "latte", "please"]}))
    r = client.post("/v1/suggest", json={"npc": NPC, "history": []})
    assert r.status_code == 200 and r.json()["words"] == ["hello", "latte", "please"]


def test_suggest_pairs_useful_vocabulary_with_meanings_and_drops_personal_details(client, provider):
    seen = {}

    def answer(body):
        seen["body"] = body
        return ok(
            json.dumps(
                {
                    "words": [
                        {"text": "Kim", "meaning_ko": "김", "kind": "personal_detail"},
                        {"text": "under the name of", "meaning_ko": "~ 이름으로", "kind": "vocabulary"},
                        {"text": " Under the name of ", "meaning_ko": "중복", "kind": "vocabulary"},
                        {"text": "booking confirmation", "meaning_ko": "예약 확인서", "kind": "vocabulary"},
                        {"text": "surname", "meaning_ko": "성", "kind": "vocabulary"},
                    ]
                },
                ensure_ascii=False,
            )
        )

    provider.answers["suggest"] = answer
    response = client.post("/v1/suggest", json={"npc": NPC, "history": HISTORY})

    assert response.status_code == 200
    assert response.json()["words"] == [
        "under the name of — ~ 이름으로",
        "booking confirmation — 예약 확인서",
        "surname — 성",
    ]
    assert len(provider.calls) == 1  # 뜻을 붙이려고 별도 번역 호출을 하지 않는다.
    prompt = seen["body"]["messages"][0]["content"]
    assert "Do not invent names, dates, room numbers or prices" in prompt
    assert "not words mechanically extracted from a suggested sentence" in prompt


def test_suggest_does_not_show_a_structured_word_without_a_korean_meaning(client, provider):
    provider.answers["suggest"] = lambda b: ok(
        json.dumps(
            {
                "words": [
                    {"text": "Kim", "meaning_ko": "김", "kind": "personal_detail"},
                    {"text": "surname", "meaning_ko": "", "kind": "vocabulary"},
                ]
            }
        )
    )
    response = client.post("/v1/suggest", json={"npc": NPC, "history": HISTORY})
    assert response.status_code == 502


@pytest.mark.parametrize("out", ["not json", "{}", '{"words": "latte"}', '{"words": []}'])
def test_suggest_output_that_breaks_format_is_invalid_output(client, provider, out):
    provider.answers["suggest"] = lambda b: ok(out)
    r = client.post("/v1/suggest", json={"npc": NPC, "history": HISTORY})
    assert r.status_code == 502 and r.json()["error"]["code"] == "invalid_output"


# ---------------------------------------------------------------------
# 난이도
# ---------------------------------------------------------------------
def reply_response(provider, level=None, npc=NPC, tts=None, extra=None):
    seen = {}

    def answer(b):
        seen["body"] = b
        return stream(["Sure. ", "One latte."])

    provider.answers["npc"] = answer
    body = {"npc": npc, "history": [], "text": "one latte", "tts": tts is not None} | ({"level": level} if level else {})
    with make_client(provider, tts=tts) as c:
        r = c.post("/v1/npc/reply", json=body | (extra or {}))
    return r, seen.get("body")


def reply(provider, level=None, npc=NPC, tts=None, extra=None):
    r, body = reply_response(provider, level, npc, tts, extra)
    assert r.status_code == 200
    return body


@pytest.mark.parametrize(
    ("level", "rule", "max_tokens"),
    [(1, "nervous beginner", 154), (2, "intermediate learner", 184), (3, "at full speed", 234)],
)
def test_reply_follows_level(provider, level, rule, max_tokens):
    b = reply(provider, level)
    assert rule in b["messages"][0]["content"] and b["max_tokens"] == max_tokens


def test_reply_default_level_is_intermediate(provider):
    b = reply(provider)
    # 대사 길이 규칙을 유지하며 표정 표시용 최대 64토큰만 별도로 확보한다.
    assert "intermediate learner" in b["messages"][0]["content"] and b["max_tokens"] == 184


@pytest.mark.parametrize("level", [1, 2, 3])
@pytest.mark.parametrize("speech_speed", [0.8, 1.2, 1.5])
def test_reply_voice_speed_follows_learner_setting_not_level_or_npc(provider, level, speech_speed):
    """말 빠르기는 학습자가 설정창에서 고른 값이다(S15P31A305-778). 난이도와 NPC의 호환용 tts_speed는 쓰지 않는다."""
    tts = FakeTts()
    reply(provider, level, npc=NPC | {"tts_speed": 1.0}, tts=tts, extra={"learner": {"speech_speed": speech_speed}})
    assert tts.speeds and all(s == speech_speed for s in tts.speeds)


@pytest.mark.parametrize("level", [1, 2])
def test_reply_voice_speed_defaults_to_1_1_without_learner_setting(provider, level):
    tts = FakeTts()
    reply(provider, level, tts=tts)
    assert tts.speeds and all(s == 1.1 for s in tts.speeds)


@pytest.mark.parametrize("speech_speed", [0.7, 1.6])
def test_reply_rejects_speech_speed_outside_setting_range(provider, speech_speed):
    r, _ = reply_response(provider, tts=FakeTts(), extra={"learner": {"speech_speed": speech_speed}})
    assert r.status_code == 400


@pytest.mark.parametrize(("level", "learner"), [(1, "very simple (CEFR A1-A2)"), (2, "(CEFR B1)")])
def test_hint_follows_level(client, provider, level, learner):
    seen = {}

    def answer(b):
        seen["body"] = b
        return ok(json.dumps({"say_this": [{"en": "For here.", "ko": "여기서요."}], "keywords": [{"en": "for here", "ko": "매장"}]}))

    provider.answers["hint"] = answer
    client.post("/v1/hint", json={"npc": NPC, "goal": "choose", "history": HISTORY, "level": level})
    assert learner in user_msg(seen["body"])


@pytest.mark.parametrize("level", [0, 4, "hard"])
def test_unknown_level_is_invalid_request(client, level):
    r = client.post("/v1/npc/reply", json={"npc": NPC, "history": [], "text": "hi", "level": level})
    assert r.status_code == 400
