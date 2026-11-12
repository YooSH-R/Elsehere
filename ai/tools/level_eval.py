"""난이도별 NPC 대답 비교: 같은 장면을 난이도마다 게임 대답 경로(인격 카드 + 백엔드 페르소나 + 표정 표시 지시)로 만들고
길이·문장 구조·질문 방식·어휘 수준을 잰다. 중급 규칙(app.npc_prompt.MIDDLE)이 초급과 실제로 다른지 숫자로 확인한다.
장면 세트는 ai/tools/level_eval/cases.json.

    ai/worker/.venv/Scripts/python ai/tools/level_eval.py --levels 1,2 --runs 2 --out ai/tools/results/level_eval.jsonl

- 글로 세는 것: 단어 수, 문장 수, 문장당 단어 수, 이어 주는 말(because·so·which·if·when 등), 선택지 질문(or가 든 질문)과 열린 질문(wh 질문).
- 단어 빈도(wordfreq): 드문 단어(Zipf 4 미만) 비율과 평균 Zipf. 측정 도구에서만 쓰므로 따로 설치한다 (pip install wordfreq==3.1.1).
- 판정 모델이 매기는 것: 어휘·문법 CEFR 수준, 구동사 수, 관용구·속어 수, 학습자 말을 되짚어 확인했는지,
  다시 말해 달라고 했는지(broken 줄에서만 집계).
공용 키라 한 줄씩 순서대로 보낸다. 결과는 집계만 찍고 줄마다의 기록은 --out jsonl로.
"""

import argparse
import asyncio
import json
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "worker"))
import httpx
import npc_help_eval as H

from app import config
from app import prompts as P
from app.llm import model_body
from app.npc_emotion import EMOTION_PROMPT
from app.schemas import Line

CASES = Path(__file__).resolve().parent / "level_eval/cases.json"
WORD = re.compile(r"[A-Za-z]+(?:'[A-Za-z]+)?")
SENTENCE_END = re.compile(r"(?<=[.!?])\s+")
FACE_MARKER = re.compile(r"\[\[[^\]]*\]\]")
CONNECTORS = {"because", "so", "which", "if", "when", "while", "although", "though", "unless", "until", "since"}
WH_WORDS = {"how", "what", "why", "where", "when", "which", "who", "whose"}
CEFR = ["A1", "A2", "B1", "B2", "C1", "C2"]
# 이보다 빈도가 낮으면 드문 단어로 센다 (Zipf 4 = 백만 단어당 10회. get 6.3, grab 4.5, reckon 3.8, swamped 3.0)
RARE_ZIPF = 4.0

JUDGE_SYSTEM = """You rate one spoken line by a character in an English conversation game for language learners. Return JSON.

"cefr": the CEFR level a learner needs to understand this line comfortably when heard, judged from its vocabulary
and grammar together (A1, A2, B1, B2, C1 or C2).
"phrasal_verbs": how many phrasal verbs it uses (run out of, pick up, figure out ...).
"idioms": how many idioms or casual slang expressions it uses.
"asks_again": true if the character asks the other person to repeat, explain or say it another way, or checks what they
meant ("You mean ...?"), instead of just acting on a guess.
"echoes": true if the character repeats the other person's words back to check them ("A large coffee, no milk, right?").
"reason": one short sentence."""
JUDGE_SCHEMA = {
    "type": "object",
    "properties": {
        "cefr": {"type": "string", "enum": CEFR},
        "phrasal_verbs": {"type": "integer"},
        "idioms": {"type": "integer"},
        "asks_again": {"type": "boolean"},
        "echoes": {"type": "boolean"},
        "reason": {"type": "string"},
    },
    "required": ["cefr", "phrasal_verbs", "idioms", "asks_again", "echoes", "reason"],
    "additionalProperties": False,
}


def load_cases() -> list[dict]:
    return json.loads(CASES.read_text(encoding="utf-8"))["cases"]


def spoken(reply: str) -> str:
    """게임이 읽고 자막으로 보여 주는 말만 남긴다 (표정 표시 [[emotion:intensity]]는 빼기)."""
    return re.sub(r"\s+", " ", FACE_MARKER.sub("", reply)).strip()


def measure(text: str) -> dict:
    sentences = [s for s in SENTENCE_END.split(text.strip()) if WORD.search(s)]
    words = [w.lower() for w in WORD.findall(text)]
    questions = [s for s in sentences if s.rstrip().endswith("?")]
    choice = [q for q in questions if " or " in f" {q.lower()} "]
    open_questions = [q for q in questions if q not in choice and WH_WORDS & {w.lower() for w in WORD.findall(q)}]
    return {
        "words": len(words),
        "sentences": len(sentences),
        "connectors": sum(w in CONNECTORS for w in words),
        "choice_questions": len(choice),
        "open_questions": len(open_questions),
    }


def english_zipf(word: str) -> float:
    """영어 단어 빈도(Zipf, 백만 단어당 10회 ≈ 4). 측정 도구에서만 쓰는 의존성이라 부를 때 불러온다."""
    from wordfreq import zipf_frequency

    return zipf_frequency(word, "en")


def rare_words(text: str, zipf=english_zipf) -> dict:
    """빈도가 RARE_ZIPF 아래인 단어를 센다. 문장 중간의 대문자 단어(I 빼고)는 장소·사람 이름이라 뺀다."""
    counted = []
    for sentence in SENTENCE_END.split(text.strip()):
        for position, token in enumerate(WORD.findall(sentence)):
            if position > 0 and token[0].isupper() and token != "I":
                continue
            counted.append(zipf(token.lower()))
    return {"words": len(counted), "rare": sum(z < RARE_ZIPF for z in counted), "zipf_total": sum(counted)}


async def ask(client: httpx.AsyncClient, s: config.Settings, body: dict) -> str:
    r = await client.post("/chat/completions", json=model_body(body, s.model_params))
    r.raise_for_status()
    return r.json()["choices"][0]["message"]["content"]


async def run_case(
    client: httpx.AsyncClient,
    s: config.Settings,
    case: dict,
    level: int,
    reply_client: httpx.AsyncClient | None = None,
    reply_model: str | None = None,
) -> dict:
    """reply_client·reply_model을 주면 대답만 그 공급자·모델로 만들고 판정은 구성의 npc 모델이 한다 (GPU 서버 모델 비교)."""
    npc = H.npc_of(case["npc"])
    history = [Line(who=h["who"], text=h["text"]) for h in case["history"]]
    msgs = P.npc_messages(npc, None, history, case["text"], "", level, None, H.LEARNER)
    msgs[0] = {**msgs[0], "content": msgs[0]["content"] + "\n\n" + EMOTION_PROMPT}  # 게임 대답 스트림(app.npc)과 같게
    row = {"id": case["id"], "kind": case["kind"], "npc": case["npc"], "level": level}
    try:
        model = reply_model or s.models["npc"]
        body = {"model": model, "temperature": 0.6, "max_tokens": P.LEVEL_MAX_TOKENS[level] + 64, "messages": msgs}
        reply = spoken((await ask(reply_client or client, s, body)).split("\n")[0].strip('"'))
        row["reply"] = reply
        row.update(measure(reply))
        row["vocabulary"] = rare_words(reply)
        judge = {
            "model": s.models["npc"],
            "temperature": 0.0,
            "max_tokens": 200,
            "messages": [
                {"role": "system", "content": JUDGE_SYSTEM},
                {"role": "user", "content": f"The learner said: {case['text']}\n\nLine to rate:\n{reply}"},
            ],
            "response_format": {"type": "json_schema", "json_schema": {"name": "out", "schema": JUDGE_SCHEMA, "strict": True}},
        }
        row.update(json.loads(await ask(client, s, judge)))
    except (httpx.HTTPError, ValueError, KeyError, IndexError) as e:
        row["error"] = f"{type(e).__name__}: {str(e)[:120]}"
    return row


def mean(values: list[float]) -> float:
    return sum(values) / len(values) if values else 0.0


def rate(rows: list[dict], key: str) -> float:
    """대답들의 단어 빈도 합을 단어 수로 나눈 값 (key가 rare면 드문 단어 비율, zipf_total이면 평균 Zipf)."""
    words = sum(r["vocabulary"]["words"] for r in rows)
    return sum(r["vocabulary"][key] for r in rows) / words if words else 0.0


def report(rows: list[dict], levels: list[int]):
    print(f"{'':<22}" + "".join(f"{'난이도 ' + str(level):>12}" for level in levels))
    done = {level: [r for r in rows if r["level"] == level and not r.get("error")] for level in levels}
    talk = {level: [r for r in done[level] if r["kind"] == "talk"] for level in levels}
    broken = {level: [r for r in done[level] if r["kind"] == "broken"] for level in levels}

    def line(name: str, value):
        print(f"{name:<22}" + "".join(f"{value(level):>12}" for level in levels))

    line("대답 수 (실패)", lambda lv: f"{len(done[lv])} ({sum(r['level'] == lv for r in rows) - len(done[lv])})")
    line("단어 / 대답", lambda lv: f"{mean([r['words'] for r in talk[lv]]):.1f}")
    line("문장 / 대답", lambda lv: f"{mean([r['sentences'] for r in talk[lv]]):.2f}")
    line("단어 / 문장", lambda lv: f"{sum(r['words'] for r in talk[lv]) / max(sum(r['sentences'] for r in talk[lv]), 1):.1f}")
    line("이어 주는 말 / 대답", lambda lv: f"{mean([r['connectors'] for r in talk[lv]]):.2f}")
    line("구동사 / 대답", lambda lv: f"{mean([r['phrasal_verbs'] for r in talk[lv]]):.2f}")
    line("관용구·속어 / 대답", lambda lv: f"{mean([r['idioms'] for r in talk[lv]]):.2f}")
    line("선택지 질문", lambda lv: f"{sum(r['choice_questions'] for r in talk[lv])}")
    line("열린 질문", lambda lv: f"{sum(r['open_questions'] for r in talk[lv])}")
    line("드문 단어 비율 (Zipf<4)", lambda lv: f"{rate(talk[lv], 'rare'):.1%}")
    line("평균 Zipf (낮을수록 어려움)", lambda lv: f"{rate(talk[lv], 'zipf_total'):.2f}")
    line("되짚어 확인 (talk 줄)", lambda lv: f"{sum(r['echoes'] for r in talk[lv])}/{len(talk[lv])}")
    line("CEFR 평균 (A1=1)", lambda lv: f"{mean([CEFR.index(r['cefr']) + 1 for r in talk[lv]]):.2f}")
    for cefr in CEFR:
        line(f"  {cefr}", lambda lv, c=cefr: f"{sum(r['cefr'] == c for r in talk[lv])}")
    line("되묻기 (broken 줄)", lambda lv: f"{sum(r['asks_again'] for r in broken[lv])}/{len(broken[lv])}")


def show(rows: list[dict], levels: list[int]):
    """줄마다 난이도별 첫 대답을 나란히 보여 준다 (숫자만으로 놓치는 말투 확인용)."""
    for case_id in dict.fromkeys(r["id"] for r in rows):
        print(f"\n[{case_id}]")
        for level in levels:
            first = next((r for r in rows if r["id"] == case_id and r["level"] == level), None)
            if first:
                print(f"  {level}: {first.get('reply', first.get('error'))}")


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--levels", default="1,2", help="비교할 난이도 (쉼표로)")
    ap.add_argument("--runs", type=int, default=2, help="줄마다 대답을 몇 번 만들지 (temperature 0.6이라 흔들림을 본다)")
    ap.add_argument("--profile", default="gms-luna")
    ap.add_argument("--only", help="줄 id 고르기 (쉼표로)")
    ap.add_argument("--env", default=str(H.ROOT / ".env"))
    ap.add_argument("--show", action="store_true", help="줄마다 난이도별 대답도 찍기")
    ap.add_argument(
        "--reply-gpu-model",
        help="대답만 GPU 서버 vLLM 모델로 만들기 (예: Qwen/Qwen3.8-27B, GUARD_GPU_BASE_URL·TOKEN 필요). 판정은 구성의 npc 모델 그대로",
    )
    ap.add_argument("--reply-model", help="대답만 같은 공급자의 다른 모델로 만들기 (예: gpt-6-luna). 채점·판정은 구성의 npc 모델 그대로")
    ap.add_argument("--out")
    a = ap.parse_args()
    config.load_dotenv(Path(a.env))
    os.environ.setdefault("AI_WORKER_TOKEN", "level-eval")
    os.environ["LLM_PROFILE"] = a.profile
    s = config.Settings.from_env()

    levels = [int(level) for level in a.levels.split(",")]
    cases = load_cases()
    if a.only:
        cases = [c for c in cases if c["id"] in a.only.split(",")]
    rows = []
    async with httpx.AsyncClient(
        base_url=s.base_url, headers={"Authorization": f"Bearer {s.api_key}", "User-Agent": "ai-worker/0.1"}, timeout=60
    ) as client:
        reply_client = None
        if a.reply_gpu_model:
            gpu = s.providers["gpu_llm"]
            reply_client = httpx.AsyncClient(base_url=gpu["base_url"], headers={"Authorization": f"Bearer {gpu['api_key']}"}, timeout=60)
        try:
            for case in cases:
                for level in levels:
                    for _ in range(a.runs):
                        rows.append(await run_case(client, s, case, level, reply_client, a.reply_gpu_model or a.reply_model))
        finally:
            if reply_client:
                await reply_client.aclose()
    print(
        f"{a.profile} 대답 {a.reply_gpu_model or a.reply_model or s.models['npc']}·판정 {s.models['npc']}, 줄 {len(cases)} × 난이도 {len(levels)} × {a.runs}회"
    )
    report(rows, levels)
    if a.show:
        show(rows, levels)
    if a.out:
        Path(a.out).parent.mkdir(parents=True, exist_ok=True)
        Path(a.out).write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")


if __name__ == "__main__":
    asyncio.run(main())
