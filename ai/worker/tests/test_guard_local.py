"""무례 판정 단계적 구조 (로컬 판정기 + LLM) 테스트. 가짜 판정기라 torch·모델 파일 없이 돈다."""

import httpx
import pytest

from app import config
from app.config import Settings
from app.guard_local import LocalGuard
from conftest import TOKEN, make_client, ok

NPC = {"id": "emma", "name": "Emma", "role": "the barista at Maple Cafe.", "facts": "Menu: latte $5.", "scene": "Inside Maple Cafe."}
TH = {"guard_t_lo": 0.05, "guard_t_hi": 0.95}


def body(text, history=None):
    return {
        "npc": NPC,
        "text": text,
        "history": history if history is not None else [{"who": "npc", "text": "Hi! What can I get for you?"}],
    }


class FakeLocal:
    """score: 문장 → 점수 (예외를 던지면 로컬 고장)."""

    name, context = "fake", False

    def __init__(self, score):
        self.fn, self.seen = score, []

    def score(self, text, prev):
        self.seen.append((text, prev))
        return self.fn(text)


@pytest.fixture
def answers(provider):
    provider.answers["guard"] = lambda b: ok("no")
    return provider


def guard(provider, text, local, history=None, **kw):
    with make_client(provider, guard_local=local, **{**TH, **kw}) as c:
        return c.post("/v1/guard", json=body(text, history)).json()


def guard_calls(provider):
    return [c for c in provider.calls if c[0] == "guard"]


def test_off_by_default_keeps_old_response(answers):
    """플래그가 꺼져 있으면(기본) 예전과 같은 응답: 판정은 LLM, guard에 새 칸 없음."""
    r = guard(answers, "커피 주세요", None)
    assert r["guard"] == {"abusive": False, "error": None, "error_code": None}
    assert len(guard_calls(answers)) == 1


def test_confident_rude_skips_llm(answers):
    local = FakeLocal(lambda t: 0.99)
    r = guard(answers, "닥치고 커피 내놔", local)
    assert r["guard"] == {"abusive": True, "error": None, "error_code": None, "source": "local", "score": 0.99, "local_error": None}
    assert guard_calls(answers) == [] and "guard" not in {u["step"] for u in r["usage"]}
    assert local.seen == [("닥치고 커피 내놔", "Hi! What can I get for you?")]  # 바로 앞 NPC 대사를 함께 넘김


def test_confident_polite_skips_llm(answers):
    r = guard(answers, "아이스 라떼 하나요", FakeLocal(lambda t: 0.01))
    assert r["guard"]["abusive"] is False and r["guard"]["source"] == "local"
    assert guard_calls(answers) == []


def test_thresholds_are_inclusive(answers):
    """s ≥ t_hi → 무례, s ≤ t_lo → 아님 (cascade_eval.py와 같은 경계)."""
    assert guard(answers, "가", FakeLocal(lambda t: 0.95))["guard"]["source"] == "local"
    assert guard(answers, "가", FakeLocal(lambda t: 0.05))["guard"]["source"] == "local"
    assert guard_calls(answers) == []


def test_unsure_goes_to_llm(answers):
    answers.answers["guard"] = lambda b: ok("yes")
    r = guard(answers, "아 씨 또 틀렸네", FakeLocal(lambda t: 0.5))
    assert r["guard"] == {"abusive": True, "error": None, "error_code": None, "source": "llm", "score": 0.5, "local_error": None}
    assert len(guard_calls(answers)) == 1


def test_english_goes_to_llm_without_local(answers):
    """한글이 없으면 로컬 판정기(한국어 모델)는 보지 않는다."""
    local = FakeLocal(lambda t: 0.99)
    r = guard(answers, "one latte please", local)
    assert r["guard"] == {"abusive": False, "error": None, "error_code": None, "source": "llm", "score": None, "local_error": None}
    assert local.seen == [] and len(guard_calls(answers)) == 1


def test_vowel_jamo_counts_as_hangul(answers):
    """'ㅠㅠ'처럼 모음 자모만 있어도 로컬로 (cascade_eval.py 라우팅과 같게)."""
    local = FakeLocal(lambda t: 0.01)
    assert guard(answers, "sorry ㅠㅠ", local)["guard"]["source"] == "local"


def test_local_failure_falls_back_to_llm(answers):
    def broken(t):
        raise RuntimeError("model gone")

    r = guard(answers, "커피 주세요", FakeLocal(broken))
    assert r["guard"]["abusive"] is False and r["guard"]["source"] == "llm" and r["guard"]["score"] is None
    assert r["guard"]["local_error"] == "RuntimeError: model gone" and len(guard_calls(answers)) == 1


def test_llm_failure_after_unsure_keeps_score(answers):
    """중간 구간에서 LLM이 실패하면 미판정(null). 로컬 점수는 기록에 남는다."""
    answers.answers["guard"] = lambda b: httpx.Response(503, text="down")
    r = guard(answers, "아 씨 또 틀렸네", FakeLocal(lambda t: 0.5))
    assert r["guard"]["abusive"] is None and r["guard"]["error_code"] == "provider_unavailable"
    assert r["guard"]["source"] == "llm" and r["guard"]["score"] == 0.5


def test_no_npc_line_yet_passes_empty_prev(answers):
    local = FakeLocal(lambda t: 0.01)
    guard(answers, "안녕하세요", local, history=[])
    assert local.seen == [("안녕하세요", "")]


def test_health_shows_local_guard(provider):
    with make_client(provider, guard_local=FakeLocal(lambda t: 0.0), **TH) as c:
        assert c.get("/health").json()["guard_local"] == {"model": "fake", "t_lo": 0.05, "t_hi": 0.95}
    with make_client(provider) as c:
        assert c.get("/health").json()["guard_local"] is None


def test_load_without_model_is_off(tmp_path):
    assert LocalGuard.load("") is None and LocalGuard.load(str(tmp_path / "없음")) is None


# ---------------------------------------------------------------------
# 환경 변수
# ---------------------------------------------------------------------
@pytest.fixture
def env(monkeypatch):
    e = {"AI_WORKER_TOKEN": TOKEN, "LLM_PROFILE": "groq", "GROQ_API_KEY": "q"}
    monkeypatch.setattr(config, "load_dotenv", lambda *a, **kw: None)
    monkeypatch.setattr(config.os, "environ", e)
    return e


def test_env_off_by_default(env):
    s = Settings.from_env()
    assert s.guard_local_model == "" and (s.guard_t_lo, s.guard_t_hi) == (0.0, 1.0)


def test_env_reads_thresholds(env):
    env.update({"GUARD_LOCAL_MODEL": "runs/v2-mix/best", "GUARD_LOCAL_T_LO": "0.05", "GUARD_LOCAL_T_HI": "0.95"})
    s = Settings.from_env()
    assert (s.guard_local_model, s.guard_t_lo, s.guard_t_hi) == ("runs/v2-mix/best", 0.05, 0.95)


@pytest.mark.parametrize("lo,hi", [(None, None), ("0.05", None), ("0.9", "0.1"), ("0.5", "0.5"), ("-0.1", "0.9")])
def test_env_rejects_missing_or_bad_thresholds(env, lo, hi):
    env["GUARD_LOCAL_MODEL"] = "runs/v2-mix/best"
    env.update({k: v for k, v in (("GUARD_LOCAL_T_LO", lo), ("GUARD_LOCAL_T_HI", hi)) if v is not None})
    with pytest.raises(RuntimeError, match="GUARD_LOCAL|기준값"):
        Settings.from_env()
