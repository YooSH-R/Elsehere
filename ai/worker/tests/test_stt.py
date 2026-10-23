"""음성 인식 /v1/stt 테스트. Whisper는 가짜 공급자가 답한다."""

import httpx

from app.stt import is_prompt_echo, stt_prompt


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


def test_prompt_echo_rule():
    p = stt_prompt(["latte"])
    assert is_prompt_echo("The bus stop is over there.", p)
    assert not is_prompt_echo("Latte.", p) and not is_prompt_echo("I want a latte and a muffin", p)
