"""NPC 대답 스트림. 이벤트: delta(글자 조각) → audio(읽을 조각의 음성, seq 순서) → done 또는 error.

조각 나누기는 웹 프로토타입 화면(prototype/mvp_web/static/index.html)과 같다: 문장이 끝날 때마다 한 조각, 첫 조각만 12자 이상이면 쉼표에서도 끊어
첫 소리를 당긴다. 한 조각 합성이 상한(기본 2.5초)을 넘기면 그 조각은 tts: false (화면이 자막만 또는 브라우저 음성).
"""

import asyncio
import json
import logging
import re
import time

from . import prompts as P
from .config import Settings
from .llm import LLM, ProviderError, Usage
from .schemas import ReplyRequest
from .tts import AudioStore, Kokoro

log = logging.getLogger("ai_worker")

SENTENCE = re.compile(r"^.*?[.!?]\s")
FIRST = re.compile(r"^.{12,}?[,;:]\s")
FALLBACK_REPLY = "Sorry, could you say that again?"


class Splitter:
    def __init__(self):
        self.buf, self.first = "", True

    def feed(self, piece: str) -> list[str]:
        self.buf += piece
        out = []
        while m := (SENTENCE.match(self.buf) or (FIRST.match(self.buf) if self.first else None)):
            out.append(m.group(0).strip())
            self.buf, self.first = self.buf[m.end() :], False
        return [c for c in out if c]

    def flush(self) -> list[str]:
        rest, self.buf = self.buf.strip().strip('"').strip(), ""
        return [rest] if rest else []


async def first_within(gen, budget_s: float, t0: float):
    """첫 글자(공백 아닌 조각)까지만 시간 상한. 재시도·대체 모델을 거치느라 늦어지는 것까지 포함한다.
    이미 말하기 시작한 대답은 끊지 않는다."""
    it, started = gen.__aiter__(), False
    while True:
        try:
            nxt = it.__anext__()
            piece = await (nxt if started else asyncio.wait_for(nxt, max(budget_s - (time.perf_counter() - t0), 0.01)))
        except StopAsyncIteration:
            return
        except TimeoutError:
            raise ProviderError("timeout", f"대답 첫 글자가 {budget_s:g}초 안에 오지 않음") from None
        started = started or bool(piece.strip())
        yield piece


def _quiet(task: asyncio.Task):
    """상한을 넘겨 버린 합성 작업의 오류를 조용히 (저장본은 캐시에 남아 다음에 바로 씀)."""
    if not task.cancelled():
        task.exception()


async def reply_events(llm: LLM, tts: Kokoro | None, store: AudioStore, s: Settings, req: ReplyRequest):
    usage, out, chunks = Usage(), asyncio.Queue(), asyncio.Queue()
    t0 = time.perf_counter()
    st = {"full": "", "first": None, "error": None, "seq": 0}
    use_tts = req.tts and tts is not None

    def schedule(text: str):
        task = None
        if use_tts:
            task = asyncio.create_task(tts.asynth(text, req.npc.tts_voice))
            task.add_done_callback(_quiet)
        chunks.put_nowait((st["seq"], text, task, time.perf_counter()))
        st["seq"] += 1

    async def run_llm():
        sp = Splitter()
        gen = llm.stream("npc", "npc", P.npc_messages(req.npc, req.memory, req.history, req.text, req.instruction), usage)
        stopped, finished = False, False

        def finish():
            nonlocal finished
            if finished:
                return
            finished = True
            for c in sp.flush():  # 오류로 끊겨도 이미 보낸 글자는 읽어 준다
                schedule(c)
            chunks.put_nowait(None)

        try:
            try:
                async for piece in first_within(gen, s.reply_first_budget_s, t0):
                    if stopped:  # 끝까지 읽어야 마지막에 오는 사용량(토큰 수)을 받는다 (크레딧 차감)
                        continue
                    if not st["full"]:
                        piece = piece.lstrip().lstrip('"')
                    stopped = "\n" in piece  # 한 줄만 (모델이 줄을 바꿔 다른 사람 대사를 이어 쓰는 경우)
                    piece = piece.split("\n")[0]
                    if piece:
                        if st["first"] is None:
                            st["first"] = round(time.perf_counter() - t0, 2)
                        st["full"] += piece
                        await out.put(("delta", {"text": piece}))
                        for c in sp.feed(piece):
                            schedule(c)
                    if stopped and st["full"].strip():
                        finish()  # 마지막 조각의 합성은 바로 시작
            except ProviderError as e:
                st["error"] = e
            finally:
                await gen.aclose()
            if not st["error"] and not st["full"].strip():
                st["full"] = FALLBACK_REPLY
                await out.put(("delta", {"text": FALLBACK_REPLY}))
                schedule(FALLBACK_REPLY)
        finally:
            finish()

    async def run_audio():
        prev_done = time.perf_counter()
        while (item := await chunks.get()) is not None:
            seq, text, task, ready = item
            ev = {"seq": seq, "text": text, "audio_id": None, "duration_s": None, "tts": False}
            if task:
                # 합성은 한 번에 하나라 앞 조각이 끝난 때부터 잰다 (재생은 앞 조각이 끝나야 이어지므로)
                left = s.tts_chunk_budget_s - (time.perf_counter() - max(ready, prev_done))
                try:
                    wav, dur, _ = await asyncio.wait_for(asyncio.shield(task), max(left, 0.01))
                    ev.update(audio_id=store.put(wav), duration_s=round(dur, 2), tts=True)
                except TimeoutError:  # 늦은 조각은 자막만 (tts: false)
                    log.info(json.dumps({"event": "tts_chunk_late", "seq": seq}))
                except Exception:  # 합성 실패도 그 조각만 자막으로 (대답 스트림은 이어 감)
                    log.warning(json.dumps({"event": "tts_chunk_failed", "seq": seq}), exc_info=True)
            prev_done = time.perf_counter()
            await out.put(("audio", ev))
        await out.put(("_end", None))

    workers = [asyncio.create_task(run_llm()), asyncio.create_task(run_audio())]
    try:
        while (ev := await out.get())[0] != "_end":
            yield ev
        await asyncio.gather(*workers)
        if st["error"]:
            yield "error", {"code": st["error"].code, "message": st["error"].message, "usage": usage.items}
        else:
            yield (
                "done",
                {
                    "text": st["full"].strip().strip('"'),
                    "first_token_s": st["first"],
                    "total_s": round(time.perf_counter() - t0, 2),
                    "usage": usage.items,
                },
            )
    finally:
        for w in workers:  # Spring이 중간에 끊으면 모델 호출도 멈춘다
            w.cancel()
