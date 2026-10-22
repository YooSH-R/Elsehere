"""모델 공급자 호출 지연 측정 (2026-10-22, gms-luna 첫 실사용에서 판정이 가끔 8~15초 걸려서).

    ai/worker/.venv/Scripts/python ai/tools/llm_latency.py --profile gms-luna --n 30
    ... --mode pair      의도·무례를 게임처럼 동시에 (기본: 하나씩)
    ... --mode stream    NPC 대답 스트리밍: 응답 머리가 온 시각과 첫 글자가 온 시각을 따로
    ... --env <저장소>/.env  키를 읽을 .env (기본: 이 저장소 루트)

재시도·대체 모델 없이 호출 하나의 실제 지연을 잰다 (시간 제한 --timeout, 기본 30초). 그래야 응답이 늦은 호출이
결국 언제 돌아오는지(느린 것인지, 멈춘 것인지) 보인다. 문장은 직접 지어낸 것만 쓴다 (팀원 기록·AI Hub 데이터 금지).
공용 키라 순서대로 보낸다 (pair도 한 번에 둘까지). 결과는 집계만 찍고, 줄마다의 기록은 --out jsonl로.
"""

import argparse
import asyncio
import json
import os
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "ai/worker"))
import httpx

from app import config
from app import prompts as P
from app.llm import model_body
from app.schemas import Line, Npc, State

NPC = Npc(
    id="emma",
    name="Emma",
    role="the barista at Maple Cafe.",
    facts="Menu: americano $4, latte $5, muffin $3.",
    scene="Inside Maple Cafe, a cozy coffee shop.",
)
STATE = State.model_validate(
    {
        "id": "ordering",
        "desc": "The barista is waiting for the order.",
        "intents": [
            {"id": "order_items", "desc": "orders one or more menu items"},
            {"id": "ask_menu", "desc": "asks what is on the menu"},
            {"id": "small_talk", "desc": "chats about something else"},
            {"id": "unclear", "desc": "cannot tell"},
        ],
        "slots": {
            "items": {
                "catalog": ["americano", "latte", "muffin"],
                "iced_ok": ["americano", "latte"],
                "catalog_word": "menu",
                "vague_words": "'coffee' or 'drink'",
            },
            "option": {"slot": "choice", "labels": ["here", "to_go"], "prompt": "where they will have it"},
        },
    }
)
HISTORY = [Line(who="npc", text="Hi there! Welcome to Maple Cafe. What can I get for you today?")]
LINES = [
    "one iced latte please",
    "can I get a muffin and an americano to go",
    "what do you recommend?",
    "hurry up, I don't have all day",
    "you are so slow, are you new here?",
    "uh... latte, hot, for here",
    "nice weather today, right?",
    "give me the cheapest thing, now",
]


def pct(v: list[float], q: float) -> float:
    v = sorted(v)
    return v[min(len(v) - 1, round(q * (len(v) - 1)))]


async def call(client: httpx.AsyncClient, s: config.Settings, role: str, text: str, timeout: float) -> dict:
    if role == "guard":
        body = {"model": s.models["guard"], "temperature": 0.0, "max_tokens": 5, "messages": P.guard_messages(NPC, HISTORY, text)}
    else:
        body = {
            "model": s.models["intent"],
            "temperature": 0.0,
            "max_tokens": 200,
            "messages": P.intent_messages(NPC, STATE, HISTORY, text),
            "response_format": {"type": "json_schema", "json_schema": {"name": "out", "schema": P.intent_schema(STATE), "strict": True}},
        }
    b = model_body(body, s.model_params)
    t = time.perf_counter()
    try:
        r = await client.post("/chat/completions", json=b, timeout=httpx.Timeout(timeout, connect=10.0))
        out = {"role": role, "status": r.status_code, "s": round(time.perf_counter() - t, 3)}
        if r.status_code >= 400:
            out["error"] = r.text[:120]
    except httpx.TimeoutException as e:
        out = {"role": role, "status": None, "s": round(time.perf_counter() - t, 3), "error": f"timeout {type(e).__name__}"}
    except httpx.TransportError as e:
        out = {"role": role, "status": None, "s": round(time.perf_counter() - t, 3), "error": f"{type(e).__name__}: {str(e)[:80]}"}
    return out


async def call_stream(client: httpx.AsyncClient, s: config.Settings, text: str, timeout: float) -> list[dict]:
    """NPC 대답 스트림 하나: 응답 머리(headers)·첫 글자(first)·끝(total) 시각. 재시도·대체 모델 없이."""
    msgs = P.npc_messages(NPC, None, HISTORY, text, "Just reply naturally.", 1)
    body = {
        "model": s.models["npc"],
        "temperature": 0.6,
        "max_tokens": P.LEVEL_MAX_TOKENS[1],
        "messages": msgs,
        "stream": True,
        "stream_options": {"include_usage": True},
    }
    b = model_body(body, s.model_params)
    t, head, first = time.perf_counter(), None, None
    try:
        req = client.build_request("POST", "/chat/completions", json=b, timeout=httpx.Timeout(timeout, connect=10.0))
        r = await client.send(req, stream=True)
        head = time.perf_counter() - t
        try:
            if r.status_code >= 400:
                return [{"role": "headers", "status": r.status_code, "s": round(head, 3), "error": (await r.aread())[:120].decode()}]
            async for line in r.aiter_lines():
                if not line.startswith("data:") or line[5:].strip() == "[DONE]":
                    continue
                choices = json.loads(line[5:]).get("choices") or []
                if first is None and choices and (choices[0].get("delta", {}).get("content") or "").strip():
                    first = time.perf_counter() - t
        finally:
            await r.aclose()
    except (httpx.TimeoutException, httpx.TransportError) as e:
        return [{"role": "first", "status": None, "s": round(time.perf_counter() - t, 3), "error": type(e).__name__}]
    total = time.perf_counter() - t
    return [
        {"role": "headers", "status": 200, "s": round(head, 3)},
        {"role": "first", "status": 200 if first else None, "s": round(first or total, 3), "error": None if first else "no text"},
        {"role": "total", "status": 200, "s": round(total, 3)},
    ]


def report(rows: list[dict], label: str):
    ok = [r["s"] for r in rows if r["status"] == 200]
    bad = [r for r in rows if r["status"] != 200]
    if not ok:
        print(label, "성공 없음", bad[:3])
        return
    slow = {th: sum(1 for x in ok if x > th) for th in (3, 5, 8)}
    print(
        f"{label}: n {len(rows)}, 성공 {len(ok)}, p50 {statistics.median(ok):.2f}s, p90 {pct(ok, 0.9):.2f}s, "
        f"p99 {pct(ok, 0.99):.2f}s, 최대 {max(ok):.2f}s, 3·5·8초 초과 {slow[3]}·{slow[5]}·{slow[8]}, 실패 {len(bad)}"
        + (f" {sorted({b.get('error', '')[:40] for b in bad})}" if bad else "")
    )


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--profile", default="gms-luna")
    ap.add_argument("--n", type=int, default=30, help="역할마다 호출 수 (pair면 쌍 수)")
    ap.add_argument("--mode", choices=["single", "pair", "stream"], default="single")
    ap.add_argument("--timeout", type=float, default=30.0)
    ap.add_argument("--fresh", action="store_true", help="호출마다 새 연결 (프로토타입 server.py의 urllib처럼)")
    ap.add_argument("--env", default=str(ROOT / ".env"))
    ap.add_argument("--out")
    a = ap.parse_args()
    config.load_dotenv(Path(a.env))
    os.environ.setdefault("AI_WORKER_TOKEN", "latency-test")
    os.environ["LLM_PROFILE"] = a.profile
    s = config.Settings.from_env()
    mk = lambda: httpx.AsyncClient(base_url=s.base_url, headers={"Authorization": f"Bearer {s.api_key}", "User-Agent": "ai-worker/0.1"})
    shared = mk()
    rows = []
    for i in range(a.n):
        text = LINES[i % len(LINES)]
        client = mk() if a.fresh else shared
        if a.mode == "stream":
            rows += await call_stream(client, s, text, a.timeout)
        elif a.mode == "pair":
            got = await asyncio.gather(call(client, s, "intent", text, a.timeout), call(client, s, "guard", text, a.timeout))
            for g in got:
                g["pair"] = i
            rows += got
        else:
            rows += [await call(client, s, "guard", text, a.timeout), await call(client, s, "intent", text, a.timeout)]
        if a.fresh:
            await client.aclose()
    await shared.aclose()
    tag = f"{a.profile} {a.mode}{' fresh' if a.fresh else ''}"
    for role in ("headers", "first", "total") if a.mode == "stream" else ("guard", "intent"):
        report([r for r in rows if r["role"] == role], f"{tag} {role}")
    if a.mode == "pair":
        worst = [max(r["s"] for r in rows if r.get("pair") == i) for i in range(a.n)]
        print(
            f"{tag} 쌍(둘 중 느린 쪽, 게임의 판정 단계): p50 {statistics.median(worst):.2f}s, p90 {pct(worst, 0.9):.2f}s, 최대 {max(worst):.2f}s, "
            f"5초 초과 {sum(1 for w in worst if w > 5)}/{a.n}"
        )
    if a.out:
        Path(a.out).write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")


if __name__ == "__main__":
    asyncio.run(main())
