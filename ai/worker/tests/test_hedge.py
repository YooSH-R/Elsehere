"""답이 늦은 호출에 같은 요청을 하나 더 보내기 (2026-10-22 gms-luna 혼잡 대응). 가짜 공급자라 진짜 호출 없음."""

import asyncio
import time

import httpx

from conftest import make_client, ok

NPC = {"id": "emma", "name": "Emma", "role": "the barista.", "facts": "", "scene": "Cafe."}


def guard(provider, **kw):
    """무례 판정(/v1/guard) 한 번. 한 번에 받는 호출이라 늦으면 같은 요청을 하나 더 보낸다."""
    with make_client(provider, **kw) as c:
        t = time.perf_counter()
        r = c.post("/v1/guard", json={"npc": NPC, "history": [], "text": "one latte please"}).json()
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
    provider.answers["guard"], seen = nth_slow([0.6, 0.0])
    r, spent = guard(provider, hedge_after_s=0.1)
    assert r["guard"]["abusive"] is False and len(seen) == 2
    assert spent < 0.5
    assert r["usage"][0]["hedged"] is True


def test_fast_call_is_sent_once(provider):
    provider.answers["guard"], seen = nth_slow([0.0])
    r, _ = guard(provider, hedge_after_s=0.1)
    assert len(seen) == 1 and "hedged" not in r["usage"][0]


def test_off_when_zero(provider):
    provider.answers["guard"], seen = nth_slow([0.3])
    r, _ = guard(provider, hedge_after_s=0)
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

    provider.answers["guard"] = handler
    r, _ = guard(provider, hedge_after_s=0.1)
    assert r["guard"]["abusive"] is True and len(seen) == 2


def test_guard_budget_still_cuts_both(provider):
    """둘 다 늦으면 무례 판정 시간 상한(GUARD_BUDGET_S)에 그대로 걸린다."""
    provider.answers["guard"], seen = nth_slow([0.8, 0.8])
    r, spent = guard(provider, hedge_after_s=0.1, guard_budget_s=0.3)
    assert r["guard"]["abusive"] is None and r["guard"]["error_code"] == "timeout" and len(seen) == 2
    assert spent < 0.7


STATE = {"id": "ordering", "desc": "waiting", "intents": [{"id": "order_items", "desc": "orders"}, {"id": "unclear", "desc": "?"}]}


def judge(provider, **kw):
    """의도 판정(/v1/judge) 한 번. NPC 대답이 이 결과를 기다리므로 다른 호출보다 일찍 같은 요청을 더 보낸다."""
    with make_client(provider, **kw) as c:
        t = time.perf_counter()
        r = c.post("/v1/judge", json={"npc": NPC, "state": STATE, "history": [], "text": "one latte please"}).json()
        return r, time.perf_counter() - t


def test_judge_uses_its_own_earlier_hedge(provider):
    provider.answers["intent"], seen = nth_slow([0.6, 0.0], '{"intent": "order_items"}')
    r, spent = judge(provider, hedge_after_s=5.0, judge_hedge_after_s=0.1)
    assert r["intent"]["id"] == "order_items" and len(seen) == 2
    assert spent < 0.5
    assert r["usage"][0]["hedged"] is True


def test_judge_hedge_does_not_change_other_calls(provider):
    provider.answers["guard"], seen = nth_slow([0.3])
    r, _ = guard(provider, hedge_after_s=5.0, judge_hedge_after_s=0.1)
    assert len(seen) == 1 and "hedged" not in r["usage"][0]


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
