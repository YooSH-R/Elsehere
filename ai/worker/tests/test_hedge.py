"""답이 늦은 호출에 같은 요청을 하나 더 보내기 (2026-10-22 gms-luna 혼잡 대응). 가짜 공급자라 진짜 호출 없음."""
import asyncio
import json
import time

import httpx

from conftest import make_client, ok

NPC = {"id": "emma", "name": "Emma", "role": "the barista.", "facts": "", "scene": "Cafe."}
STATE = {"id": "ordering", "desc": "waiting", "intents": [{"id": "unclear", "desc": "?"}]}


def judge(provider, **kw):
    with make_client(provider, **kw) as c:
        t = time.perf_counter()
        r = c.post("/v1/judge", json={"npc": NPC, "state": STATE, "history": [], "text": "one latte please"}).json()
        return r, time.perf_counter() - t


def nth_slow(delays, answer="no"):
    """n번째 무례 판정 요청은 delays[n]초 뒤에 답한다."""
    seen = []

    async def handler(b):
        i = len(seen)
        seen.append(i)
        await asyncio.sleep(delays[i] if i < len(delays) else 0)
        return ok(answer)
    return handler, seen


def test_slow_call_is_raced_by_second_request(provider):
    provider.answers["intent"] = lambda b: ok(json.dumps({"intents": ["unclear"]}))
    provider.answers["guard"], seen = nth_slow([0.6, 0.0])
    r, spent = judge(provider, hedge_after_s=0.1)
    assert r["guard"]["abusive"] is False and len(seen) == 2
    assert spent < 0.5
    g = next(u for u in r["usage"] if u["step"] == "guard")
    assert g["hedged"] is True and "hedged" not in next(u for u in r["usage"] if u["step"] == "intent")


def test_fast_call_is_sent_once(provider):
    provider.answers["intent"] = lambda b: ok(json.dumps({"intents": ["unclear"]}))
    provider.answers["guard"], seen = nth_slow([0.0])
    r, _ = judge(provider, hedge_after_s=0.1)
    assert len(seen) == 1 and "hedged" not in next(u for u in r["usage"] if u["step"] == "guard")


def test_off_when_zero(provider):
    provider.answers["intent"] = lambda b: ok(json.dumps({"intents": ["unclear"]}))
    provider.answers["guard"], seen = nth_slow([0.3])
    r, _ = judge(provider, hedge_after_s=0)
    assert len(seen) == 1 and r["guard"]["abusive"] is False


def test_failed_first_waits_for_second(provider):
    """먼저 보낸 요청이 늦게 실패하면 더 보낸 요청의 답을 쓴다."""
    seen = []

    async def handler(b):
        i = len(seen)
        seen.append(i)
        if i == 0:
            await asyncio.sleep(0.3)
            return httpx.Response(503, text="down")
        await asyncio.sleep(0.4)
        return ok("yes")
    provider.answers["intent"] = lambda b: ok(json.dumps({"intents": ["unclear"]}))
    provider.answers["guard"] = handler
    r, _ = judge(provider, hedge_after_s=0.1)
    assert r["guard"]["abusive"] is True and len(seen) == 2


def test_judge_budget_still_cuts_both(provider):
    """둘 다 늦으면 판정 단계 시간 상한(JUDGE_BUDGET_S)에 그대로 걸린다."""
    provider.answers["intent"] = lambda b: ok(json.dumps({"intents": ["unclear"]}))
    provider.answers["guard"], seen = nth_slow([0.8, 0.8])
    r, spent = judge(provider, hedge_after_s=0.1, judge_budget_s=0.3)
    assert r["guard"]["abusive"] is None and r["guard"]["error_code"] == "timeout" and len(seen) == 2
    assert spent < 0.7


def test_npc_stream_is_not_hedged(provider):
    seen = []

    async def handler(b):
        seen.append(1)
        await asyncio.sleep(0.3)
        from conftest import stream
        return stream(["Sure", "."])
    provider.answers["npc"] = handler
    with make_client(provider, hedge_after_s=0.1) as c:
        c.post("/v1/npc/reply", json={"npc": NPC, "history": [], "text": "hi", "tts": False})
    assert len(seen) == 1
