"""NPC 대답 스트림: 첫 글자가 늦으면 같은 요청 스트림을 하나 더 열어 먼저 글자를 낸 쪽을 쓴다 (GMS 스트리밍 꼬리 지연 대응).
가짜 공급자라 진짜 호출 없음."""

import asyncio
import json

import httpx

from conftest import make_client, stream

NPC = {"id": "mia", "name": "Mia", "role": "the barista.", "facts": "", "scene": "Cafe."}
BODY = {"npc": NPC, "history": [], "text": "one latte", "tts": False}


def events(resp) -> list[tuple[str, dict]]:
    out = []
    for block in resp.text.strip().split("\n\n"):
        lines = dict(line.split(": ", 1) for line in block.splitlines() if ": " in line)
        out.append((lines["event"], json.loads(lines["data"])))
    return out


class SlowBody(httpx.AsyncByteStream):
    """응답 머리는 바로 오고, 첫 글자는 delay초 뒤에 오는 스트림. 닫혔는지 기록한다."""

    def __init__(self, delay: float, pieces: list[str], log: list):
        self.delay, self.pieces, self.log = delay, pieces, log

    async def __aiter__(self):
        await asyncio.sleep(self.delay)
        for p in self.pieces:
            yield (f"data: {json.dumps({'choices': [{'delta': {'content': p}}]})}\n\n").encode()
        yield (
            f"data: {json.dumps({'choices': [], 'usage': {'prompt_tokens': 300, 'completion_tokens': 20}})}\n\ndata: [DONE]\n\n"
        ).encode()

    async def aclose(self):
        self.log.append("closed")


def answers(*plans):
    """plans[i]: i번째 NPC 요청의 (머리 지연, 첫 글자 지연, 대답 조각)."""
    seen, closed = [], []

    async def handler(b):
        i = len(seen)
        seen.append(i)
        head, first, pieces = plans[min(i, len(plans) - 1)]
        await asyncio.sleep(head)
        return httpx.Response(200, stream=SlowBody(first, pieces, closed))

    return handler, seen, closed


def run(provider, **kw):
    with make_client(provider, **kw) as c:
        return events(c.post("/v1/npc/reply", json=BODY))


def npc_usage(ev):
    return next(u for u in ev[-1][1]["usage"] if u["step"] == "npc")


def test_slow_first_text_is_raced_by_second_stream(provider):
    provider.answers["npc"], seen, closed = answers((0, 0.8, ["Slow one."]), (0, 0, ["Fast one."]))
    ev = run(provider, stream_hedge_after_s=0.1)
    done = ev[-1][1]
    assert ev[-1][0] == "done" and done["text"] == "Fast one."
    assert done["first_token_s"] < 0.6 and len(seen) == 2
    assert npc_usage(ev)["hedged"] is True
    assert "closed" in closed  # 진 쪽 스트림은 닫는다


def test_slow_headers_are_raced_too(provider):
    """응답 머리 자체가 늦어도 (GMS는 첫 글자가 나올 때 머리를 보냄) 같은 기준으로 하나 더."""
    provider.answers["npc"], seen, _ = answers((0.8, 0, ["Slow one."]), (0, 0, ["Fast one."]))
    ev = run(provider, stream_hedge_after_s=0.1)
    assert ev[-1][1]["text"] == "Fast one." and len(seen) == 2


def test_fast_stream_is_sent_once(provider):
    provider.answers["npc"], seen, _ = answers((0, 0, ["Hi there."]))
    ev = run(provider, stream_hedge_after_s=0.1)
    assert ev[-1][1]["text"] == "Hi there." and len(seen) == 1 and "hedged" not in npc_usage(ev)


def test_whole_reply_is_from_one_stream(provider):
    """여러 조각이어도 이긴 스트림 하나의 글자만 이어진다 (두 스트림이 섞이지 않음)."""
    provider.answers["npc"], _, _ = answers((0, 0.8, ["A1 ", "A2."]), (0, 0, ["B1 ", "B2 ", "B3."]))
    ev = run(provider, stream_hedge_after_s=0.1)
    assert "".join(d["text"] for name, d in ev if name == "delta") == "B1 B2 B3."


def test_off_when_zero(provider):
    provider.answers["npc"], seen, _ = answers((0, 0.3, ["Slow one."]), (0, 0, ["Fast one."]))
    ev = run(provider, stream_hedge_after_s=0)
    assert ev[-1][1]["text"] == "Slow one." and len(seen) == 1


def test_both_slow_still_hit_first_text_budget(provider):
    provider.answers["npc"], seen, _ = answers((0, 1.0, ["Slow one."]), (0, 1.0, ["Slow two."]))
    ev = run(provider, stream_hedge_after_s=0.1, reply_first_budget_s=0.4)
    assert ev[-1] == ("error", ev[-1][1]) and ev[-1][1]["code"] == "timeout" and len(seen) == 2


def test_first_stream_error_after_hedge_waits_for_second(provider):
    seen = []

    async def handler(b):
        i = len(seen)
        seen.append(i)
        if i == 0:
            await asyncio.sleep(0.3)
            return httpx.Response(503, text="down")
        await asyncio.sleep(0.4)
        return stream(["From second."])

    provider.answers["npc"] = handler
    ev = run(provider, stream_hedge_after_s=0.1, retry_pause_s=0.0)
    assert ev[-1][0] == "done" and ev[-1][1]["text"] == "From second."
