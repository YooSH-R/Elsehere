"""NPC 인격 카드 조합 API: 어드민 랜덤 생성에서 백엔드가 부른다(S15P31A305-290). 워커는 저장하지 않는다.
- POST /v1/npc/persona/generate: 역할·장소와 고정 입력·DB 후보 목록으로 인격 카드 초안을 만든다(선택: LLM 사연)
- POST /v1/npc/persona/derive: 저장된 카드의 MBTI를 바꾸면 자동 칸(성격 5축·행동 조각)만 다시 계산한다
가짜 공급자라 진짜 모델을 부르지 않는다."""

import json
from pathlib import Path

import pytest

from app import npc_persona
from app.npc_profile_models import NpcConfiguration
from conftest import ok

GENERATE, DERIVE = "/v1/npc/persona/generate", "/v1/npc/persona/derive"
REQUEST = {"npc_id": "GEN_001", "role": "STAFF", "place": "CAFE"}
STORY = {
    "summary": "Henry grew up helping in his aunt's kitchen and still bakes the way she taught him.",
    "experiences": [{"event": "He once burned a whole batch on a busy morning.", "influence": "He now stays calm and starts over."}],
}
MIA = json.loads((Path(__file__).resolve().parents[2] / "npcs/CAFE_STAFF.json").read_text(encoding="utf-8"))


def made(client, **body) -> dict:
    r = client.post(GENERATE, json={**REQUEST, **body})
    assert r.status_code == 200, r.text
    return r.json()


def test_a_draft_needs_only_the_npc_id_role_and_place(client):
    """MBTI를 비우면 npcId 씨앗으로 고르고 함께 돌려준다. 결과는 공통 인격 모델을 지키는 초안(isActive false, 목소리 미연결)."""
    body = made(client)
    config = body["npc_configuration"]

    NpcConfiguration.model_validate(config)
    assert npc_persona.MBTI.match(body["mbti"])
    assert config["npcId"] == "GEN_001" and config["defaultLocationId"] == "CAFE"
    assert config["isActive"] is False and config["voiceId"] is None
    assert made(client) == body  # 같은 요청이면 같은 NPC


def test_fixed_inputs_are_kept_and_the_rest_is_chosen(client):
    body = made(client, mbti="ISTP-T", fixed={"name": "Zed", "age_range": "40s", "interests": ["chess"]})
    profile = body["npc_configuration"]["npcProfile"]

    assert body["mbti"] == "ISTP-T"
    assert profile["personality"]["dimensions"] == {axis: npc_persona.mbti_to_dimensions("ISTP-T")[axis] for axis in npc_persona.AXES}
    assert (profile["name"], profile["ageRange"], profile["background"]["interests"]) == ("Zed", "40s", ["chess"])


def test_candidates_from_the_admin_database_are_used(client):
    """백엔드가 DB의 활성 후보를 실어 보내면 그 목록에서 고르고, 다른 NPC가 쓰는 이름은 고르지 않는다."""
    candidates = {
        "names": ["Zed", "Yuna"],
        "interests": [{"text": "chess"}],
        "values": [{"text": "Being on time", "leans": {"conscientiousness": "high"}}],
    }

    profile = made(client, mbti="ISTJ-A", candidates=candidates, taken_names=["Zed"])["npc_configuration"]["npcProfile"]

    assert profile["name"] == "Yuna"
    assert profile["background"]["interests"] == ["chess"]
    assert profile["values"] == ["Being on time"]


@pytest.mark.parametrize(
    ("body", "said"),
    [
        ({"role": "STAFF", "place": "SHOP"}, "SHOP"),  # 그 장소에서 할 직업 후보가 없음
        ({"mbti": "ABCD"}, "mbti"),
        ({"candidates": {"names": ["Zed"]}, "taken_names": ["Zed"]}, "이름"),  # 고를 이름이 없음
        (
            {
                "mbti": "INTJ-T",
                "candidates": {"traits": [{"name": "Calm", "description": "Stays steady.", "leans": {"neuroticism": "low"}}]},
            },
            "성격 특징",
        ),
    ],
)
def test_a_request_that_cannot_make_an_npc_is_an_invalid_request(client, body, said):
    r = client.post(GENERATE, json={**REQUEST, **body})

    assert r.status_code == 400 and r.json()["error"]["code"] == "invalid_request"
    assert said in r.json()["error"]["message"]


def test_narrate_lets_the_llm_write_only_the_backstory(client, provider):
    provider.answers["npc"] = lambda body: ok(json.dumps(STORY))
    skeleton = made(client)["npc_configuration"]

    narrated = made(client, narrate=True)["npc_configuration"]

    background = {**skeleton["npcProfile"]["background"], **STORY}
    assert narrated == {**skeleton, "npcProfile": {**skeleton["npcProfile"], "background": background}}


def test_a_backstory_outside_the_profile_model_is_an_invalid_output(client, provider):
    provider.answers["npc"] = lambda body: ok(json.dumps({**STORY, "summary": "x" * 1501}))

    r = client.post(GENERATE, json={**REQUEST, "narrate": True})

    assert r.status_code == 502 and r.json()["error"]["code"] == "invalid_output"


def test_derive_recomputes_only_the_auto_fields_when_the_mbti_changes(client):
    """어드민에서 Mia의 MBTI를 바꾸면 성격 5축과 행동 조각만 다시 계산한다. 손으로 쓴 글은 그대로."""
    r = client.post(DERIVE, json={"npc_id": "CAFE_STAFF", "mbti": "ISTP-T", "profile": MIA["npcProfile"]})

    assert r.status_code == 200
    expected = npc_persona.with_derived_traits(MIA["npcProfile"], {"npcId": "CAFE_STAFF", "mbti": "ISTP-T"}, npc_persona.load_behaviors())
    assert r.json()["profile"] == expected
    assert expected["personality"]["traits"] == MIA["npcProfile"]["personality"]["traits"]


def test_derive_rejects_a_profile_outside_the_shared_model(client):
    profile = {**MIA["npcProfile"], "nickname": "Mimi"}

    r = client.post(DERIVE, json={"npc_id": "CAFE_STAFF", "mbti": "ESFJ-T", "profile": profile})

    assert r.status_code == 400 and r.json()["error"]["code"] == "invalid_request"
