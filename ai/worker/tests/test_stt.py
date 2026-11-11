"""음성 인식 /v1/stt 테스트. Whisper는 가짜 공급자가 답한다."""

import httpx
import pytest
from fastapi.testclient import TestClient

from app.config import Settings, profile_models
from app.main import create_app
from app.stt import is_prompt_echo
from conftest import TOKEN

WHISPER_OK = {"text": "Muffin.", "duration": 0.6}
AUDIO = {"file": ("a.webm", b"x" * 100, "audio/webm")}


def test_stt_filters_prompt_echo_and_sends_scene_words(client, provider):
    provider.answers["stt"] = lambda b: httpx.Response(200, json={"text": "Pain reliever, cough syrup.", "duration": 2.0})
    r = client.post(
        "/v1/stt", files={"file": ("a.webm", b"x" * 100, "audio/webm")}, data={"scene_words": '["pain reliever", "cough syrup"]'}
    ).json()
    assert r["text"] == "" and r["audio_s"] == 2.0  # 무음에 프롬프트 끝(장면 단어 목록)을 되풀이한 것
    assert b"pain reliever, cough syrup" in provider.last_stt  # 장면 단어 보정
    provider.answers["stt"] = lambda b: httpx.Response(200, json={"text": "Muffin.", "duration": 0.6})
    assert client.post("/v1/stt", files={"file": ("a.webm", b"x" * 100, "audio/webm")}).json()["text"] == "Muffin."


def test_stt_sends_up_to_40_scene_words(client, provider):
    # Spring은 닉네임·NPC 이름·같은 장소 NPC 이름·장소 단어·마을 단어를 보낸다. 카페는 31개라 30개 상한이면 마을 단어가 잘렸다 (S15P31A305-747)
    provider.answers["stt"] = lambda b: httpx.Response(200, json=WHISPER_OK)
    words = [f"word{i}" for i in range(1, 42)]
    client.post("/v1/stt", files=AUDIO, data={"scene_words": ", ".join(words)})
    prompt = provider.last_stt.decode()
    assert "word39, word40." in prompt and "word41" not in prompt


def test_stt_prompt_ends_with_npc_line(client, provider):
    # 한국식 발음 '러닝'은 running·learning 둘 다 된다. 직전 NPC 대사("not running")를 프롬프트 끝에 두면 그 문맥에 맞춰 고른다
    provider.answers["stt"] = lambda b: httpx.Response(200, json=WHISPER_OK)
    client.post("/v1/stt", files=AUDIO, data={"scene_words": "latte", "npc_line": " I'm just taking a walk, not running. "})
    prompt = provider.last_stt.decode()
    assert "My friend lives over there. latte. I'm just taking a walk, not running." in prompt


def test_stt_prompt_ends_with_korean_after_npc_line(client, provider):
    # 영어 장면 단어·NPC 대사로 끝나면 한국어로 한 말('왜 뭐 불러주세요')을 영어 문장('Why? What do you say?')으로 바꿔 적었다.
    # 맨 끝에 짧은 한국어 말을 두면 한글로 남고, 영어 녹음은 한글로 바뀌지 않았다 (본인 dev 녹음 284개, S15P31A305-787)
    provider.answers["stt"] = lambda b: httpx.Response(200, json=WHISPER_OK)
    client.post("/v1/stt", files=AUDIO, data={"scene_words": "latte", "npc_line": "I'm in no rush."})
    assert "latte. I'm in no rush. 음, 잠깐만요.\r\n" in provider.last_stt.decode()  # prompt 칸 값의 끝
    client.post("/v1/stt", files=AUDIO)
    assert "My friend lives over there. 음, 잠깐만요.\r\n" in provider.last_stt.decode()


@pytest.mark.parametrize(
    ("text", "echo"),
    [
        ("음, 잠깐만요.", True),  # 짧은 소리('아')에 끝의 한국어 말을 되풀이했다
        ("음, 잠깐만요. 음, 잠깐만요.", True),
        ("I'm in no rush. 음, 잠깐만요.", True),
        ("잠깐만요.", False),  # 학습자가 실제로 하는 한국어 말일 수 있다
        ("음, 잠깐만요. I want a latte.", False),  # 메아리 뒤 진짜 발화
    ],
)
def test_prompt_echo_includes_korean_ending(text, echo):
    assert is_prompt_echo(text, ["latte"], "I'm in no rush.") is echo


def test_stt_filters_npc_line_echo(client, provider):
    # 무음에 직전 NPC 대사의 끝 문장을 되풀이한다 (GPU turbo-lr1e4 재현: "I'm in no rush. I'm in no rush.")
    provider.answers["stt"] = lambda b: httpx.Response(200, json={"text": "I'm in no rush. I'm in no rush.", "duration": 2.0})
    r = client.post("/v1/stt", files=AUDIO, data={"npc_line": "I'm just taking a walk, not running. I'm in no rush."})
    assert r.json()["text"] == ""


@pytest.mark.parametrize(
    ("text", "echo"),
    [
        ("I'm in no rush.", True),  # NPC 대사 끝 문장
        ("I'm just taking a walk, not running. I'm in no rush.", True),  # NPC 대사 전체
        ("Iced Americano, latte. I'm just taking a walk, not running. I'm in no rush.", True),  # 장면 단어부터 끝까지
        ("My friend lives over there.", True),  # 기본 프롬프트 끝 문장은 NPC 대사가 있어도 메아리
        ("Your walking is weird.", False),  # NPC 대사 문맥에서 나온 진짜 발화
        ("not running", False),  # 12자 미만은 진짜 발화일 수 있다
    ],
)
def test_prompt_echo_includes_npc_line_ending(text, echo):
    assert is_prompt_echo(text, ["Iced Americano", "latte"], "I'm just taking a walk, not running. I'm in no rush.") is echo


def test_stt_keeps_realistic_prompt_sentence_suffix(client, provider):
    provider.answers["stt"] = lambda b: httpx.Response(200, json={"text": "lives over there", "duration": 1.5})
    response = client.post("/v1/stt", files=AUDIO)
    assert response.status_code == 200
    assert response.json()["text"] == "lives over there"


def test_stt_prompt_writes_menu_example_in_english(client, provider):
    # 한글 예시 '아이스 라떼'는 한국식 발음 외래어('Iced americano')까지 '아이스트 아메라카노'처럼 한글로 끌어당겼다 (dev 재현).
    provider.answers["stt"] = lambda b: httpx.Response(200, json=WHISPER_OK)
    client.post("/v1/stt", files=AUDIO)
    prompt = provider.last_stt.decode()
    assert "an iced latte" in prompt and "아이스 라떼" not in prompt


def test_stt_prompt_keeps_korean_talk_examples(client, provider):
    # 한국어 예시가 '음, 하나?'뿐이면 짧은 한국어 문장('니가 그거에 대해 뭘 알아')을 영어 문장으로 번역해 적었다 (dev 재현, S15P31A305-556).
    # 메뉴가 아닌 한국어 말('음, 그러니까...', '잠깐만요.')을 앞뒤에 두면 한글로 남고 영어 메뉴는 그대로 영어로 나온다
    provider.answers["stt"] = lambda b: httpx.Response(200, json=WHISPER_OK)
    client.post("/v1/stt", files=AUDIO)
    prompt = provider.last_stt.decode()
    assert "음, 그러니까... I want" in prompt
    assert "음, 하나?" in prompt and "잠깐만요. My friend" in prompt


def test_stt_prompt_has_no_bus_to_pull_both_toward(client, provider):
    # 프롬프트 끝 문장 'The bus stop is over there.'가 한국식 발음 both('보쓰')를 bus로 끌어당겨
    # 운영 빨래방에서 "Both, please"가 "Bus, please"로 적혔다 (S15P31A305-639).
    # 끝 문장을 빼면 영어를 한글로 적는 일이 늘어서, 짧고 헷갈리는 단어가 없는 영어 문장으로 바꾼다
    provider.answers["stt"] = lambda b: httpx.Response(200, json=WHISPER_OK)
    client.post("/v1/stt", files=AUDIO)
    prompt = provider.last_stt.decode()
    assert "bus" not in prompt.lower()
    assert "잠깐만요. My friend lives over there." in prompt


@pytest.mark.parametrize(
    ("text", "echo"),
    [
        ("lives over there", False),  # 예시 끝의 단어 일부는 실제 발화일 수 있다
        ("friend lives over there", False),
        ("My friend lives over there.", True),  # Whisper가 무음에 되풀이하는 프롬프트 마지막 문장
        ("음, 그러니까... I want... an iced latte... Can I get a muffin, 음, 하나? 잠깐만요. My friend lives over there.", True),
        ("잠깐만요. My friend lives over there.", True),
        ("Iced Americano, latte.", True),  # 장면 단어 목록
        # 무음에 메아리를 여러 번 이어 붙이기도 한다 (dev에서 입력칸에 들어옴)
        ("My friend lives over there. My friend lives over there.", True),
        ("Iced Americano, latte. Iced Americano, latte. Iced Americano, latte.", True),
        ("My friend lives over there. I want a latte.", False),  # 메아리 뒤 진짜 발화
        ("The bus stop is over there.", False),  # 예전 끝 문장은 이제 진짜 발화
        ("Iced Americano. Iced Americano.", False),  # 장면 단어 하나를 되풀이한 주문
        ("Iced Americano.", False),  # 장면 단어 하나는 진짜 주문
        ("Can I get a muffin?", False),  # 프롬프트 앞·중간의 예시 문장은 학습자가 실제로 하는 말
        ("I want an iced latte.", False),
        ("Latte.", False),
        ("I want a latte and a muffin", False),
    ],
)
def test_prompt_echo_is_only_the_prompt_ending(text, echo):
    assert is_prompt_echo(text, ["Iced Americano", "latte"]) is echo


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


GPU_CHAIN = [
    {"provider": "gpu", "model": "turbo-lr1e4"},
    {"provider": "gms", "model": "whisper-1"},
    {"provider": "groq", "model": "whisper-large-v3-turbo"},
]


def chain_client(answers: dict, **kw) -> tuple[TestClient, list]:
    """STT_CHAIN 구성: GPU 서버(gpu.test) → GMS(gms.test, 기본 공급자) → Groq(groq.test). answers[호스트] = 함수(request) → 응답.
    반환: (클라이언트, 음성 인식 요청 [(호스트, read 시간 상한)])."""
    seen = []

    def handler(request: httpx.Request) -> httpx.Response:
        seen.append((request.url.host, request.extensions["timeout"]["read"]))
        return answers[request.url.host](request)

    models, role_provider = profile_models("gms-luna")
    s = Settings(
        token=TOKEN,
        base_url="http://gms.test/v1",
        api_key="g",
        profile="gms-luna",
        models=models,
        role_provider=role_provider,
        providers={"groq": {"base_url": "http://groq.test/v1", "api_key": "q"}, "gpu": {"base_url": "http://gpu.test/v1", "api_key": "t"}},
        **{"stt_chain": GPU_CHAIN, "stt_first_timeout_s": 3.0, "call_timeout_s": 1.0, "retry_pause_s": 0.0, **kw},
    )
    return TestClient(create_app(s, httpx.MockTransport(handler), False), headers={"X-Internal-Token": TOKEN}), seen


def test_stt_chain_gpu_ok_calls_only_gpu():
    c, seen = chain_client({"gpu.test": answer_ok, "gms.test": fail(500), "groq.test": fail(500)})
    with c:
        r = c.post("/v1/stt", files=AUDIO).json()
    assert [h for h, _ in seen] == ["gpu.test"]
    assert (r["text"], r["usage"][0]["model"], r["usage"][0]["fallback"]) == ("Muffin.", "turbo-lr1e4", False)


@pytest.mark.parametrize("how", [500, 429, "timeout"])
def test_stt_chain_gpu_fails_goes_to_gms(how):
    # GPU 서버가 꺼졌거나(터널 끊김) 느리면 짧게 기다리고 GMS로. Groq는 GMS까지 실패할 때만
    c, seen = chain_client({"gpu.test": fail(how), "gms.test": answer_ok, "groq.test": fail(500)})
    with c:
        r = c.post("/v1/stt", files=AUDIO)
    assert r.status_code == 200 and [h for h, _ in seen] == ["gpu.test", "gms.test"]
    assert seen[0][1] == 3.0  # 첫 공급자는 짧은 상한
    (u,) = r.json()["usage"]
    assert (u["model"], u["fallback"]) == ("whisper-1", True)


def test_stt_chain_last_provider_waits_longest():
    c, seen = chain_client({"gpu.test": fail("timeout"), "gms.test": fail(503), "groq.test": answer_ok}, stt_next_timeout_s=6.0)
    with c:
        r = c.post("/v1/stt", files=AUDIO)
    assert r.status_code == 200 and r.json()["usage"][0]["model"] == "whisper-large-v3-turbo"
    assert seen == [("gpu.test", 3.0), ("gms.test", 6.0), ("groq.test", 30.0)]


def test_stt_chain_all_fail_reports_every_provider():
    c, seen = chain_client({"gpu.test": fail(500), "gms.test": fail(503), "groq.test": fail(429)})
    with c:
        r = c.post("/v1/stt", files=AUDIO)
    assert r.status_code == 429 and r.json()["error"]["code"] == "provider_limit"  # 마지막 공급자의 오류
    msg = r.json()["error"]["message"]
    assert all(m in msg for m in ("turbo-lr1e4", "whisper-1", "whisper-large-v3-turbo"))
    assert len(seen) == 3


def test_stt_chain_does_not_pass_on_rejected_audio():
    c, seen = chain_client({"gpu.test": fail(400), "gms.test": answer_ok, "groq.test": answer_ok})
    with c:
        r = c.post("/v1/stt", files=AUDIO)
    assert r.status_code == 503 and [h for h, _ in seen] == ["gpu.test"]


def test_stt_chain_skips_gpu_for_a_while_after_repeated_failures():
    # GPU 서버가 꺼져 있으면 매 턴 상한(3초)을 기다리지 않고 잠시 바로 GMS로. 정해진 시간이 지나면 다시 시도
    c, seen = chain_client(
        {"gpu.test": fail(500), "gms.test": answer_ok, "groq.test": fail(500)}, stt_breaker_failures=2, stt_breaker_skip_s=60.0
    )
    now = [1000.0]
    with c:
        c.app.state.llm.clock = lambda: now[0]
        for _ in range(2):
            c.post("/v1/stt", files=AUDIO)
        assert [h for h, _ in seen] == ["gpu.test", "gms.test", "gpu.test", "gms.test"]
        seen.clear()
        r = c.post("/v1/stt", files=AUDIO).json()
        assert seen == [("gms.test", 8.0)]  # GPU를 건너뛰어도 GMS의 상한은 순서상 자리 그대로
        assert (r["usage"][0]["model"], r["usage"][0]["fallback"]) == ("whisper-1", True)
        seen.clear()
        now[0] += 61
        c.post("/v1/stt", files=AUDIO)
        assert [h for h, _ in seen] == ["gpu.test", "gms.test"]


def test_stt_chain_success_resets_failure_count():
    answers = iter([fail(500), answer_ok, fail(500), answer_ok])
    c, seen = chain_client({"gpu.test": lambda r: next(answers)(r), "gms.test": answer_ok, "groq.test": fail(500)}, stt_breaker_failures=2)
    with c:
        for _ in range(4):
            c.post("/v1/stt", files=AUDIO)
    assert [h for h, _ in seen].count("gpu.test") == 4  # 실패 사이에 성공이 있으면 연속 실패가 아니다


def test_stt_last_provider_is_never_skipped():
    c, seen = chain_client({"gpu.test": fail(500), "gms.test": fail(500), "groq.test": fail(503)}, stt_breaker_failures=1)
    with c:
        for _ in range(2):
            r = c.post("/v1/stt", files=AUDIO)
    assert r.status_code == 503 and [h for h, _ in seen][-1] == "groq.test" and [h for h, _ in seen][3:] == ["groq.test"]
