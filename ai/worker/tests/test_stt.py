"""음성 인식 /v1/stt 테스트. Whisper는 가짜 공급자가 답한다."""

import httpx
import pytest
from fastapi.testclient import TestClient

from app.config import Settings, profile_models
from app.main import create_app
from app.stt import is_prompt_echo, stt_prompt
from conftest import TOKEN

WHISPER_OK = {"text": "Muffin.", "duration": 0.6}
AUDIO = {"file": ("a.webm", b"x" * 100, "audio/webm")}


def test_stt_filters_prompt_echo_and_sends_scene_words(client, provider):
    provider.answers["stt"] = lambda b: httpx.Response(200, json={"text": "Can I get a muffin, 음, 하나?", "duration": 2.0})
    r = client.post(
        "/v1/stt", files={"file": ("a.webm", b"x" * 100, "audio/webm")}, data={"scene_words": '["pain reliever", "cough syrup"]'}
    ).json()
    assert r["text"] == "" and r["audio_s"] == 2.0  # 무음에 프롬프트를 되풀이한 것
    assert b"pain reliever, cough syrup" in provider.last_stt  # 장면 단어 보정
    provider.answers["stt"] = lambda b: httpx.Response(200, json={"text": "Muffin.", "duration": 0.6})
    assert client.post("/v1/stt", files={"file": ("a.webm", b"x" * 100, "audio/webm")}).json()["text"] == "Muffin."


def test_stt_rejects_big_or_empty_audio(client):
    assert client.post("/v1/stt", files={"file": ("a.webm", b"", "audio/webm")}).status_code == 400
    # 백엔드 AUDIO_TOO_LARGE(413)와 같은 상태로 돌려준다
    r = client.post("/v1/stt", files={"file": ("a.webm", b"x" * (3 * 1024 * 1024 + 1), "audio/webm")})
    assert r.status_code == 413 and r.json()["error"]["code"] == "audio_too_large" and "3MB" in r.json()["error"]["message"]


def test_stt_rejects_unsupported_format_without_calling_provider(client, provider):
    # 백엔드 AUDIO_FORMAT_UNSUPPORTED(415)와 같은 상태로 돌려준다. 모르는 형식을 webm으로 여겨 보내지 않는다
    r = client.post("/v1/stt", files={"file": ("a.flac", b"x" * 100, "audio/flac")})
    assert r.status_code == 415 and r.json()["error"]["code"] == "audio_format_unsupported"
    # 파일 부분에 형식을 붙이지 않은 요청(application/octet-stream)도 같다
    assert client.post("/v1/stt", files={"file": ("a", b"x" * 100, "application/octet-stream")}).status_code == 415
    assert provider.calls == []


def test_stt_accepts_format_with_codec_parameter(client, provider):
    # 브라우저 녹음기(MediaRecorder)는 형식 뒤에 코덱을 붙여 보낸다
    provider.answers["stt"] = lambda b: httpx.Response(200, json={"text": "Muffin.", "duration": 0.6})
    r = client.post("/v1/stt", files={"file": ("a.webm", b"x" * 100, "audio/webm;codecs=opus")})
    assert r.status_code == 200 and r.json()["text"] == "Muffin."


def gms_profile_client(groq, gms, **kw) -> tuple[TestClient, list]:
    """GMS 구성처럼: 대화는 gms.test, 음성 인식은 groq.test, 대체는 GMS whisper-1. groq·gms는 함수(request) → 응답.
    반환: (클라이언트, 음성 인식 요청 [(호스트, read 시간 상한)])."""
    seen = []

    def handler(request: httpx.Request) -> httpx.Response:
        seen.append((request.url.host, request.extensions["timeout"]["read"]))
        return (groq if request.url.host == "groq.test" else gms)(request)

    models, role_provider = profile_models("gms-luna")
    s = Settings(
        token=TOKEN,
        base_url="http://gms.test/v1",
        api_key="g",
        profile="gms-luna",
        models=models,
        role_provider=role_provider,
        providers={"groq": {"base_url": "http://groq.test/v1", "api_key": "q"}},
        **{"stt_fallback": {"provider": "gms", "model": "whisper-1"}, "call_timeout_s": 1.0, "retry_pause_s": 0.0, **kw},
    )
    return TestClient(create_app(s, httpx.MockTransport(handler), False), headers={"X-Internal-Token": TOKEN}), seen


def fail(how):
    def answer(request: httpx.Request) -> httpx.Response:
        if how == "timeout":
            raise httpx.ReadTimeout("느림", request=request)
        if how == "not_json":
            return httpx.Response(200, text="<html>")
        return httpx.Response(how, json={"error": {"message": "x"}})

    return answer


def answer_ok(_):
    return httpx.Response(200, json=WHISPER_OK)


@pytest.mark.parametrize("how", [429, 500, 503, 401, "timeout", "not_json"])
def test_stt_falls_back_to_gms_when_groq_fails(how):
    # 배포하면 모든 사용자가 Groq 키 하나의 한도를 나눠 쓴다. 한도·오류·응답 없음이면 GMS whisper-1로 한 번 더
    c, seen = gms_profile_client(fail(how), answer_ok)
    with c:
        r = c.post("/v1/stt", files=AUDIO)
    assert r.status_code == 200 and r.json()["text"] == "Muffin."
    assert [h for h, _ in seen] == ["groq.test", "gms.test"]
    (u,) = r.json()["usage"]
    assert (u["step"], u["model"], u["fallback"], u["audio_s"]) == ("stt", "whisper-1", True, 0.6)


@pytest.mark.parametrize("status", [400, 413, 415, 422])
def test_stt_does_not_fall_back_when_audio_rejected(status):
    # 녹음 자체가 문제면 GMS도 같은 답이라 넘기지 않는다 (크레딧·시간 낭비)
    c, seen = gms_profile_client(fail(status), answer_ok)
    with c:
        r = c.post("/v1/stt", files=AUDIO)
    assert r.status_code == 503 and r.json()["error"]["code"] == "provider_unavailable"
    assert [h for h, _ in seen] == ["groq.test"]


def test_stt_groq_ok_does_not_call_gms():
    c, seen = gms_profile_client(answer_ok, fail(500))
    with c:
        r = c.post("/v1/stt", files=AUDIO).json()
    assert [h for h, _ in seen] == ["groq.test"]
    assert (r["usage"][0]["model"], r["usage"][0]["fallback"]) == ("whisper-large-v3-turbo", False)


@pytest.mark.parametrize(("gms", "status", "code"), [(503, 503, "provider_unavailable"), (429, 429, "provider_limit")])
def test_stt_both_fail_reports_both(gms, status, code):
    c, seen = gms_profile_client(fail(429), fail(gms))
    with c:
        r = c.post("/v1/stt", files=AUDIO)
    assert r.status_code == status and r.json()["error"]["code"] == code
    msg = r.json()["error"]["message"]
    assert "429" in msg and "whisper-1" in msg and str(gms) in msg
    assert len(seen) == 2


def test_stt_without_fallback_fails_once():
    c, seen = gms_profile_client(fail(429), answer_ok, stt_fallback={})
    with c:
        r = c.post("/v1/stt", files=AUDIO)
    assert r.status_code == 429 and r.json()["error"]["code"] == "provider_limit"
    assert len(seen) == 1


def test_stt_first_provider_has_short_timeout_when_fallback_exists():
    # Groq가 멈춰도 오래 기다리지 않고 넘긴다. 대체 쪽과 대체가 없을 때는 예전처럼 30초
    c, seen = gms_profile_client(fail("timeout"), answer_ok, stt_first_timeout_s=4.0)
    with c:
        c.post("/v1/stt", files=AUDIO)
    assert seen == [("groq.test", 4.0), ("gms.test", 30.0)]
    c, seen = gms_profile_client(answer_ok, answer_ok, stt_fallback={})
    with c:
        c.post("/v1/stt", files=AUDIO)
    assert seen == [("groq.test", 30.0)]


def test_prompt_echo_rule():
    p = stt_prompt(["latte"])
    assert is_prompt_echo("The bus stop is over there.", p)
    assert not is_prompt_echo("Latte.", p) and not is_prompt_echo("I want a latte and a muffin", p)
