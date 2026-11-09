"""무례 판정 모델 측정. 서비스와 같은 판정(워커의 guard_messages·guard_result·model_params)으로 라벨 붙은 문장을 잰다.

    ai/worker/.venv/Scripts/python ai/tools/guard_eval.py --data ~/data/guard_eval/dev_own_v1_1.jsonl --model gpt-6-luna
    python ai/tools/guard_eval.py --data dev.jsonl --base-url http://<GPU 서버>:8000/v1 --key-env GUARD_VLLM_KEY --model openai/gpt-oss-120b --concurrency 16

서비스의 GPU LoRA(GUARD_GPU_MODEL)는 --criteria gpu(GUARD_SYSTEM_GPU)로 잰다. 서비스는 LoRA가 무례라고 한 줄만 구성의 판정 모델이
GUARD_SYSTEM(--criteria main, 기본)으로 다시 확인하므로, 서비스 판정은 두 결과가 모두 무례인 줄이다.

--provider는 ai/worker/llm_profiles.json의 공급자 이름(기본 gms)이고, 키는 그 공급자의 key_env에서 읽는다.
--base-url을 주면 그 주소(GPU 서버의 vLLM 같은 OpenAI 호환 서버)로 보내고, 키는 --key-env가 가리키는 환경 변수(.env 포함)에서 읽는다.
재시도·대체 모델 없이 그 모델 자신의 답만 잰다. 판정하지 못한 답(빈 답·거절·오류)은 따로 센다.

입력 jsonl 한 줄: text, prev(바로 앞 NPC 대사, 없으면 ""), rude(0/1), npc{id, name, role, facts, scene},
선택 history([{who: me|npc, text}], 있으면 prev 대신 서비스처럼 앞 대화 여러 줄).
화면에는 집계만 찍고, 문장별 결과는 --out jsonl에 문장 없이(id·rude·pred·raw·latency) 남긴다.
모델 고르기는 개발셋에서만 한다. 잠긴 테스트셋은 최종 평가에 한 번만 쓴다.
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
from app.guard import guard_result
from app.llm import model_body
from app.schemas import Line, Npc


def history_of(item: dict) -> list[Line]:
    """앞 대화. history(서비스처럼 학습자·NPC 여러 줄)가 있으면 그것, 없으면 바로 앞 NPC 대사(prev) 한 줄.
    ai/guard/train_lora.py의 history_of와 같게 둔다(그쪽은 torch가 필요해 테스트는 이쪽에서 한다)."""
    if item.get("history"):
        return [Line(**h) for h in item["history"]]
    return [Line(who="npc", text=item["prev"])] if item["prev"] else []


CRITERIA = {"main": P.GUARD_SYSTEM, "gpu": P.GUARD_SYSTEM_GPU}  # 서비스: GPU LoRA는 gpu, 구성의 판정 모델(단독·확인)은 main


def messages(item: dict, criteria: str = "main") -> list[dict]:
    return P.guard_messages(Npc.model_validate(item["npc"]), history_of(item), item["text"], CRITERIA[criteria])


async def judge(client: httpx.AsyncClient, body: dict, item: dict, timeout: float, criteria: str) -> dict:
    t = time.perf_counter()
    row = {"id": item.get("id"), "rude": item["rude"], "pred": None, "raw": "", "error": None}
    try:
        r = await client.post(
            "/chat/completions", json={**body, "messages": messages(item, criteria)}, timeout=httpx.Timeout(timeout, connect=10.0)
        )
        if r.status_code >= 400:
            row["error"] = f"http {r.status_code}: {r.text[:80]}"
        else:
            out = (r.json()["choices"][0]["message"].get("content") or "").strip()
            g = guard_result(out)
            row["raw"], row["error"] = out[:20], g["error_code"]
            row["pred"] = None if g["abusive"] is None else int(g["abusive"])
    except (httpx.TimeoutException, httpx.TransportError) as e:
        row["error"] = type(e).__name__
    row["latency_s"] = round(time.perf_counter() - t, 3)
    return row


def confusion(rows: list[dict], undecided_as: int | None) -> dict:
    """undecided_as가 None이면 판정한 문장만, 0이면 판정 못 함을 '무례 아님'으로 센다(서비스는 null을 무례로 세지 않음)."""
    pairs = [(r["rude"], r["pred"] if r["pred"] is not None else undecided_as) for r in rows]
    pairs = [(y, p) for y, p in pairs if p is not None]
    tp = sum(1 for y, p in pairs if y and p)
    fp = sum(1 for y, p in pairs if not y and p)
    fn = sum(1 for y, p in pairs if y and not p)
    precision = tp / (tp + fp) if tp + fp else 0.0
    recall = tp / (tp + fn) if tp + fn else 0.0
    f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0.0
    return {"n": len(pairs), "tp": tp, "fp": fp, "fn": fn, "precision": round(precision, 4), "recall": round(recall, 4), "f1": round(f1, 4)}


def pct(values: list[float], q: float) -> float:
    v = sorted(values)
    return v[min(len(v) - 1, round(q * (len(v) - 1)))]


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", required=True)
    ap.add_argument("--model", required=True)
    ap.add_argument("--provider", default="gms")
    ap.add_argument("--base-url", help="OpenAI 호환 주소. 주면 --provider 대신 쓴다")
    ap.add_argument("--key-env", help="--base-url 서버의 키를 담은 환경 변수 이름. 없으면 키 없이 보낸다")
    ap.add_argument("--extra-body", default="{}", help='요청에 더할 JSON (예: {"chat_template_kwargs": {"enable_thinking": false}})')
    ap.add_argument("--concurrency", type=int, default=1, help="동시에 보낼 요청 수. 공용 키(GMS)는 1~2로")
    ap.add_argument("--timeout", type=float, default=30.0)
    ap.add_argument("--limit", type=int, default=0, help="앞에서 N문장만 (시험용)")
    ap.add_argument("--criteria", choices=list(CRITERIA), default="main", help="판정 기준 문구. 서비스의 GPU LoRA를 잴 때는 gpu")
    ap.add_argument("--env", default=str(ROOT / ".env"))
    ap.add_argument("--out")
    a = ap.parse_args()

    config.load_dotenv(Path(a.env))
    base_url = (a.base_url or config.provider_base_url(a.provider)).rstrip("/")
    key = os.environ.get(a.key_env or "", "") if a.base_url else config.provider_key(a.provider)
    if not a.base_url and not key:
        sys.exit(f"{a.provider} 키가 없습니다: {' 또는 '.join(config.PROFILES['providers'][a.provider]['key_env'])}")
    items = [json.loads(line) for line in Path(a.data).expanduser().read_text(encoding="utf-8").splitlines() if line.strip()]
    items = items[: a.limit] if a.limit else items

    # 워커와 같은 요청: 온도 0, 출력 5토큰, 모델 계열별 칸(생각 끄기·출력 상한)은 llm_profiles.json model_params
    body = model_body({"model": a.model, "temperature": 0.0, "max_tokens": 5, "messages": []}, config.MODEL_PARAMS)
    body.update(json.loads(a.extra_body))
    gate = asyncio.Semaphore(a.concurrency)
    headers = {"Authorization": f"Bearer {key}"} if key else {}
    async with httpx.AsyncClient(base_url=base_url, headers=headers) as client:

        async def one(item: dict) -> dict:
            async with gate:
                return await judge(client, body, item, a.timeout, a.criteria)

        started = time.perf_counter()
        rows = await asyncio.gather(*(one(it) for it in items))
        wall = time.perf_counter() - started

    decided = confusion(rows, None)
    service = confusion(rows, 0)
    errors: dict[str, int] = {}
    for r in rows:
        if r["pred"] is None:
            errors[r["error"] or "?"] = errors.get(r["error"] or "?", 0) + 1
    lat = [r["latency_s"] for r in rows if r["pred"] is not None]
    print(
        f"{a.model} @ {base_url} · {Path(a.data).name} {len(rows)}문장 (무례 {sum(r['rude'] for r in rows)})\n"
        f"  판정한 문장: F1 {decided['f1']:.3f} · 정밀도 {decided['precision']:.1%} · 재현율 {decided['recall']:.1%} "
        f"(TP {decided['tp']} FP {decided['fp']} FN {decided['fn']}, n {decided['n']})\n"
        f"  서비스 기준(판정 못 함 = 무례 아님): F1 {service['f1']:.3f} · 정밀도 {service['precision']:.1%} · 재현율 {service['recall']:.1%}\n"
        f"  판정 못 함 {len(rows) - decided['n']} {errors or ''}\n"
        + (
            f"  지연 중앙 {statistics.median(lat):.2f}s · p90 {pct(lat, 0.9):.2f}s · 최대 {max(lat):.2f}s · 전체 {wall:.0f}s (동시 {a.concurrency})"
            if lat
            else ""
        )
    )
    if a.out:
        out = Path(a.out).expanduser()
        out.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")
        summary = {
            "model": a.model,
            "base_url": base_url,
            "data": Path(a.data).name,
            "decided": decided,
            "service": service,
            "undecided": errors,
        }
        out.with_suffix(".summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=1), encoding="utf-8")


if __name__ == "__main__":
    asyncio.run(main())
