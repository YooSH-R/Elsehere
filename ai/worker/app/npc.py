"""NPC 대답 스트림. 이벤트: delta(글자 조각) → audio(읽을 조각의 음성, seq 순서) → done 또는 error.

조각 나누기는 웹 프로토타입 화면(prototype/mvp_web/static/index.html)과 같다: 문장이 끝날 때마다 한 조각, 첫 조각만 12자 이상이면 쉼표에서도 끊어
첫 소리를 당긴다. 한 조각 합성이 상한(기본 2.5초)을 넘기면 그 조각은 tts: false (화면이 자막만 또는 브라우저 음성).
늦은 조각도 합성이 끝나면 done의 턴 전체 음성(다시 듣기용)에는 넣는다.
"""

import asyncio
import json
import logging
import re
import time
import wave

from . import prompts as P
from . import services
from .config import Settings
from .llm import LLM, ModelOutputError, ProviderError, Usage, within
from .npc_emotion import EMOTION_PROMPT, EmotionStream, reaction_instruction, response_mood
from .schemas import Line, ReplyRequest, SuggestRequest
from .speech_emotion import attach_emotion
from .tts import AudioStore, Kokoro, join_wavs

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


async def reply_events(llm: LLM, tts: Kokoro | None, store: AudioStore, s: Settings, req: ReplyRequest):
    usage, out, chunks = Usage(), asyncio.Queue(), asyncio.Queue()
    t0 = time.perf_counter()
    st = {"full": "", "first": None, "error": None, "seq": 0, "words": None}
    wavs: dict[int, bytes | None] = {}  # 조각 번호 → 합성한 음성 (실패·늦음은 None). 턴 전체 음성을 만든다
    late: dict[int, asyncio.Task] = {}  # 상한을 넘겨 자막만 보낸 조각의 합성 (턴 전체 음성에는 끝나는 대로 넣는다)
    synthesis_tasks: list[asyncio.Task] = []
    last_audio_requested_at = t0
    use_tts = req.tts and tts is not None
    closing = False
    moods = {}
    speech_phrases = []

    def schedule(text: str, mood=None):
        nonlocal last_audio_requested_at
        mood = response_mood(req.reaction, mood)
        task = None
        last_audio_requested_at = time.perf_counter()
        if use_tts:
            task = asyncio.create_task(tts.asynth(text, req.npc.tts_voice, P.LEVEL_SPEECH_SPEED[req.level]))
            synthesis_tasks.append(task)
        chunks.put_nowait((st["seq"], text, task, time.perf_counter()))
        moods[st["seq"]] = mood
        speech_phrases.append({"text": text, "emotion": mood.emotion if mood else None, "intensity": mood.intensity if mood else 0})
        st["seq"] += 1

    async def run_llm():
        sp = Splitter()
        expressions = EmotionStream()
        current_mood = None
        msgs = P.npc_messages(req.npc, req.memory, req.history, req.text, req.instruction, req.level, req.relationship, req.learner)
        msgs[0] = {**msgs[0], "content": msgs[0]["content"] + "\n\n" + EMOTION_PROMPT}
        if note := reaction_instruction(req.reaction):
            msgs[0]["content"] += "\n" + note
        # 최대 네 개 표정 표시의 토큰을 따로 확보한다. 대사 길이 규칙은 기존 수준별 프롬프트를 유지한다.
        gen = llm.stream("npc", "npc", msgs, usage, max_tokens=P.LEVEL_MAX_TOKENS[req.level] + 64)
        stopped, finished = False, False

        def consume(parts):
            nonlocal current_mood
            for text, mood in parts:
                mood = response_mood(req.reaction, mood)
                if mood != current_mood:
                    for part in sp.flush():
                        schedule(part, current_mood)
                    current_mood = mood
                if not text:
                    continue
                if st["first"] is None:
                    st["first"] = round(time.perf_counter() - t0, 2)
                st["full"] += text
                if req.stream_chunks:
                    out.put_nowait(("delta", {"text": text}))
                for part in sp.feed(text):
                    schedule(part, current_mood)

        def finish():
            nonlocal finished
            if finished or closing:
                return
            finished = True
            consume(expressions.flush())
            for c in sp.flush():  # 오류로 끊겨도 이미 보낸 글자는 읽어 준다
                schedule(c, current_mood)
            chunks.put_nowait(None)
            if req.suggest_words and not st["error"] and st["full"].strip():  # 추천 단어는 남은 음성 합성과 동시에
                st["words"] = asyncio.create_task(suggest_words(st["full"].strip().strip('"')))

        try:
            try:
                async for piece in first_within(gen, s.reply_first_budget_s, t0):
                    if stopped:  # 끝까지 읽어야 마지막에 오는 사용량(토큰 수)을 받는다 (크레딧 차감)
                        continue
                    if not st["full"] and not expressions.buffer:
                        piece = piece.lstrip().lstrip('"')
                    stopped = "\n" in piece  # 한 줄만 (모델이 줄을 바꿔 다른 사람 대사를 이어 쓰는 경우)
                    piece = piece.split("\n")[0]
                    if piece:
                        consume(expressions.feed(piece))
                    if stopped and st["full"].strip():
                        finish()  # 마지막 조각의 합성은 바로 시작
            except ProviderError as e:
                st["error"] = e
            finally:
                await gen.aclose()
            if not st["error"] and not st["full"].strip():
                st["full"] = FALLBACK_REPLY
                if req.stream_chunks:
                    await out.put(("delta", {"text": FALLBACK_REPLY}))
                schedule(FALLBACK_REPLY)
        finally:
            finish()

    async def suggest_words(said: str) -> dict:
        history = [*req.history, Line(who="me", text=req.text), Line(who="npc", text=said)][-40:]
        call = services.suggest(llm, SuggestRequest(npc=req.npc, goal=req.goal, history=history, level=req.level))
        try:  # 상한 안에 못 끝내면 timeout: done(대답·턴 전체 음성)을 추천 단어 때문에 늦추지 않는다
            res = await within(call, s.words_budget_s)
        except ProviderError as e:
            return {"words": None, "error": e.code}
        except ModelOutputError:
            return {"words": None, "error": "invalid_output"}
        except Exception:  # 덤으로 붙는 기능이라 어떤 실패든 대답은 끝까지 보낸다 (원인은 로그)
            log.warning(json.dumps({"event": "words_failed"}), exc_info=True)
            return {"words": None, "error": "internal_error"}
        usage.items.extend(res["usage"])
        return {"words": res["words"], "error": None}

    async def run_audio():
        prev_done = time.perf_counter()
        while (item := await chunks.get()) is not None:
            seq, text, task, ready = item
            ev = {"seq": seq, "text": text, "audio_id": None, "duration_s": None, "tts": False}
            if task:
                # 합성은 한 번에 하나라 앞 조각이 끝난 때부터 잰다 (재생은 앞 조각이 끝나야 이어지므로)
                left = s.tts_chunk_budget_s - (time.perf_counter() - max(ready, prev_done))
                left = min(left, last_audio_requested_at + s.tts_full_budget_s - time.perf_counter())
                try:
                    wav, dur, _ = await asyncio.wait_for(asyncio.shield(task), max(left, 0.01))
                    wav = with_emotion(wav, seq)
                    if req.stream_chunks:
                        ev.update(audio_id=store.put(wav), duration_s=round(dur, 2), tts=True)
                    wavs[seq] = wav
                except TimeoutError:  # 늦은 조각은 바로 재생 없이 자막만 (tts: false)
                    log.info(json.dumps({"event": "tts_chunk_late", "seq": seq}))
                    late[seq] = task
                except Exception:  # 합성 실패도 그 조각만 자막으로 (대답 스트림은 이어 감)
                    log.warning(json.dumps({"event": "tts_chunk_failed", "seq": seq}), exc_info=True)
            prev_done = time.perf_counter()
            if req.stream_chunks:
                await out.put(("audio", ev))
        await out.put(("_end", None))

    def with_emotion(wav, seq):
        try:
            return attach_emotion(wav, moods.get(seq))
        except (ValueError, wave.Error, EOFError):
            log.warning("NPC emotion timing rejected; preserving the synthesized voice", exc_info=True)
            return wav

    async def whole_turn_audio() -> dict:
        """다시 듣기용 턴 전체 음성: 모든 조각을 합성했을 때만 (하나라도 빠지면 Spring이 /v1/tts로 다시 만든다).
        느린 음성 때문에 완성된 대사까지 무한정 기다리지 않는다. 음성이 빠진 대사는 기존 다시 듣기로 합성할 수 있다."""
        if late:
            remaining = max(0, last_audio_requested_at + s.tts_full_budget_s - time.perf_counter())
            _, pending = await asyncio.wait(late.values(), timeout=remaining)
            if pending:
                log.info(json.dumps({"event": "tts_full_late", "pending_chunks": len(pending)}))
                return {"audio_full_id": None, "audio_full_s": None}
        for seq, task in late.items():
            try:
                wavs[seq] = with_emotion((await task)[0], seq)
            except Exception:  # 합성 실패는 그 조각만 빠진다
                log.warning(json.dumps({"event": "tts_chunk_failed", "seq": seq}), exc_info=True)
        parts = [wavs.get(i) for i in range(st["seq"])]
        if not parts or None in parts:
            return {"audio_full_id": None, "audio_full_s": None}
        try:
            wav, dur = join_wavs(parts)
        except (wave.Error, EOFError):  # 이어 붙일 수 없는 조각: 턴 전체 음성만 없이 대답은 끝까지
            log.warning(json.dumps({"event": "audio_full_failed", "chunks": len(parts)}), exc_info=True)
            return {"audio_full_id": None, "audio_full_s": None}
        return {"audio_full_id": store.put(wav), "audio_full_s": round(dur, 2)}

    workers = [asyncio.create_task(run_llm()), asyncio.create_task(run_audio())]
    try:
        while (ev := await out.get())[0] != "_end":
            yield ev
        await asyncio.gather(*workers)
        if st["words"] and st["error"]:  # 대답이 실패하면 이미 시작한 추천 단어도 보내지 않는다
            st["words"].cancel()
        elif st["words"]:
            yield "words", await st["words"]
        if st["error"]:
            yield "error", {"code": st["error"].code, "message": st["error"].message, "usage": usage.items}
        else:
            full_audio = await whole_turn_audio()
            yield (
                "done",
                {
                    "text": st["full"].strip().strip('"'),
                    "speech_phrases": speech_phrases,
                    "first_token_s": st["first"],
                    "total_s": round(time.perf_counter() - t0, 2),
                    **full_audio,
                    "usage": usage.items,
                },
            )
    finally:
        closing = True
        tasks = [*workers, *synthesis_tasks]
        if st["words"]:
            tasks.append(st["words"])
        for task in tasks:  # 연결 종료·음성 상한 뒤에 남은 작업이 다음 대화를 붙잡지 않게 한다.
            task.cancel()
        await asyncio.gather(*tasks, return_exceptions=True)
