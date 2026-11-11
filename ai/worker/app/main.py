"""AI 워커 (FastAPI). Spring만 부르는 내부 서버: 상태를 저장하지 않고 판단 재료와 말·소리만 만든다.

    uvicorn app.main:app --port 8020          (ai/worker 에서)

명세: docs/api/ai-worker.md (실행 방법은 ai/worker/README.md). 게임 상태·전환 규칙·크레딧·기록은 Spring이 가진다.
"""

import asyncio
import json
import logging
import secrets
import sys
import time
from contextlib import asynccontextmanager

import httpx
from fastapi import Body, FastAPI, File, Form, Request, UploadFile
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse, Response, StreamingResponse
from pydantic import ValidationError
from starlette.exceptions import HTTPException as StarletteHTTPException

from . import feedback_bridge, guard, npc_persona, services, stt
from .config import Settings
from .guard_local import LocalGuard
from .llm import LLM, REQUEST_NOTES, ModelOutputError, ProviderError, Usage
from .npc import reply_events
from .npc_profile_models import NpcProfile
from .npc_respond import ContractError, load_payload, parse_request, readable_request_id, respond
from .schemas import (
    FollowUpRequest,
    GuardRequest,
    HintRequest,
    JudgeRequest,
    LevelTestSpeakingRequest,
    MeaningRequest,
    MemoryRequest,
    PersonaDeriveRequest,
    PersonaGenerateRequest,
    RelationshipRequest,
    ReplyRequest,
    SuggestRequest,
    TtsRequest,
)
from .speech_emotion import Mood, attach_emotion
from .tts import AudioStore, GpuSynth, Kokoro, join_wavs

log = logging.getLogger("ai_worker")
PUBLIC = {"/health"}  # Docker 상태 확인용. 나머지는 모두 내부 토큰 필요


class ApiError(Exception):
    def __init__(self, code: str, message: str, http: int):
        super().__init__(message)
        self.code, self.message, self.http = code, message, http


def error(code: str, message: str, http: int) -> JSONResponse:
    return JSONResponse({"error": {"code": code, "message": message}}, status_code=http)


def sse(name: str, data: dict) -> str:
    return f"event: {name}\ndata: {json.dumps(data, ensure_ascii=False)}\n\n"


def create_app(
    settings: Settings | None = None,
    transport: httpx.AsyncBaseTransport | None = None,
    tts: Kokoro | None | bool = True,
    guard_local: LocalGuard | None | bool = True,
) -> FastAPI:
    """settings·transport·tts·guard_local은 테스트에서 바꿔 끼운다 (True면 설정의 모델 파일을 읽음, None이면 없음)."""

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        s = settings or Settings.from_env()
        app.state.settings = s
        app.state.llm = LLM(s, transport)
        if tts is True:
            gpu = GpuSynth.from_settings(s)
            app.state.tts = await asyncio.to_thread(Kokoro.load, s.kokoro_model, s.kokoro_voices, s.kokoro_threads, gpu)
        else:
            app.state.tts = tts
        if s.tts_require_timing and not getattr(app.state.tts, "phoneme_timing_ready", False):
            raise RuntimeError("TTS phoneme timing is required: install the duration-output Kokoro model and voices.")
        app.state.guard_local = await asyncio.to_thread(LocalGuard.load, s.guard_local_model) if guard_local is True else guard_local
        if s.guard_local_model and not app.state.guard_local:
            log.warning(
                json.dumps(
                    {
                        "event": "guard_local_unavailable",
                        "path": s.guard_local_model,
                        "why": "폴더가 없거나 torch·transformers 미설치 (requirements-guard.txt). 판정은 전부 LLM",
                    },
                    ensure_ascii=False,
                )
            )
        app.state.audio = AudioStore(s.audio_ttl_s, max_bytes=int(s.audio_max_mb * 1_000_000))
        gl = app.state.guard_local
        log.info(
            json.dumps(
                {
                    "event": "start",
                    "profile": s.profile,
                    "models": s.models,
                    "stt_fallback": s.stt_fallback,
                    "stt_chain": [f"{provider}:{model}" for provider, model in app.state.llm.stt_steps()],
                    "tts": bool(app.state.tts),
                    "tts_phoneme_timing": bool(getattr(app.state.tts, "phoneme_timing_ready", False)),
                    "tts_gpu": bool(s.tts_gpu_base_url),
                    "guard_local": gl and {"model": gl.name, "context": gl.context, "t_lo": s.guard_t_lo, "t_hi": s.guard_t_hi},
                },
                ensure_ascii=False,
            )
        )

        async def expire_audio():
            while True:
                await asyncio.sleep(min(30.0, max(0.01, s.audio_ttl_s)))
                app.state.audio.expire()

        cleanup = asyncio.create_task(expire_audio())
        try:
            yield
        finally:
            cleanup.cancel()
            await asyncio.gather(cleanup, return_exceptions=True)
            await app.state.llm.aclose()

    app = FastAPI(title="AI 워커", version="0.1.0", lifespan=lifespan)

    @app.middleware("http")
    async def guard_and_trace(request: Request, call_next):
        """내부 토큰 확인 + 추적 id(X-Request-Id·X-User-Id·X-Conv-Id)를 로그에 남긴다 (Spring 턴 기록과 잇는 열쇠)."""
        rid = request.headers.get("X-Request-Id") or secrets.token_hex(8)
        t = time.perf_counter()
        notes: dict = {}
        REQUEST_NOTES.set(notes)  # 모델 호출이 여기에 재요청 기록을 남긴다 (hedged·hedge_won)
        if request.url.path not in PUBLIC and not secrets.compare_digest(
            request.headers.get("X-Internal-Token", ""), request.app.state.settings.token
        ):
            resp = error("unauthorized", "내부 토큰이 틀렸습니다", 401)
        else:
            resp = await call_next(request)
        resp.headers["X-Request-Id"] = rid
        if request.url.path == "/health" and resp.status_code == 200:
            return resp
        log.info(
            json.dumps(
                {
                    "request_id": rid,
                    "user_id": request.headers.get("X-User-Id"),
                    "conv_id": request.headers.get("X-Conv-Id"),
                    "method": request.method,
                    "path": request.url.path,
                    "status": resp.status_code,
                    "latency_s": round(time.perf_counter() - t, 3),
                    **notes,
                },
                ensure_ascii=False,
            )
        )
        return resp

    @app.exception_handler(RequestValidationError)
    async def bad_request(_: Request, e: RequestValidationError):
        where = "; ".join(f"{'.'.join(str(x) for x in err['loc'][1:])}: {err['msg']}" for err in e.errors()[:5])
        return error("invalid_request", f"요청 형식 오류 ({where})", 400)

    @app.exception_handler(ProviderError)
    async def provider_failed(_: Request, e: ProviderError):
        return error(e.code, e.message, e.http)

    @app.exception_handler(ApiError)
    async def api_failed(_: Request, e: ApiError):
        return error(e.code, e.message, e.http)

    @app.exception_handler(ModelOutputError)
    async def output_invalid(_: Request, e: ModelOutputError):
        return error("invalid_output", str(e), 502)

    @app.exception_handler(StarletteHTTPException)
    async def http_failed(_: Request, e: StarletteHTTPException):
        code = {404: "not_found", 405: "method_not_allowed"}.get(e.status_code, f"http_{e.status_code}")
        return error(code, str(e.detail), e.status_code)

    @app.exception_handler(Exception)
    async def internal_failed(request: Request, e: Exception):
        # 프로그램 오류: 원인은 로그에만 남기고 Spring에는 정해진 형식으로
        log.error(json.dumps({"event": "internal_error", "path": request.url.path}), exc_info=e)
        return error("internal_error", "AI 워커 내부 오류", 500)

    @app.get("/health")
    async def health(request: Request):
        st = request.app.state
        gl = st.guard_local
        return {
            "ok": True,
            "tts": bool(st.tts),
            "tts_phoneme_timing": bool(getattr(st.tts, "phoneme_timing_ready", False)),
            "profile": st.settings.profile,
            "models": st.settings.models,
            "guard_local": gl and {"model": gl.name, "t_lo": st.settings.guard_t_lo, "t_hi": st.settings.guard_t_hi},
        }

    @app.post("/v1/judge")
    async def judge(req: JudgeRequest, request: Request):
        return await services.judge(request.app.state.llm, req)

    @app.post("/v1/guard")
    async def guard_line(req: GuardRequest, request: Request):
        return await guard.guard(request.app.state.llm, req, request.app.state.guard_local)

    @app.post("/v1/npc/reply")
    async def npc_reply(req: ReplyRequest, request: Request):
        st = request.app.state

        async def body():
            async for name, data in reply_events(st.llm, st.tts, st.audio, st.settings, req):
                yield sse(name, data)

        # nginx·Spring이 버퍼링하지 않게 (첫 글자가 바로 가야 함)
        return StreamingResponse(body(), media_type="text/event-stream", headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"})

    @app.post("/internal/ai/npc/respond")
    async def npc_respond(request: Request):
        """계약 v2 추론 API: 첫 대사(NPC_OPENING)와 후속 대사(USER_MESSAGE)를 같은 경로로.
        FastAPI 자동 검사는 Worker 공통 오류 형식(400)으로 나가므로, 본문을 직접 받아 계약 형식으로 검사한다.
        오류는 모두 계약 9절 형식({requestId, error: {code, message, retryable}})으로 답한다."""
        payload = None
        try:
            payload = load_payload(await request.body())
            req = parse_request(payload)
            st = request.app.state
            return (await respond(st.llm, req, st.settings.respond_budget_s)).model_dump()
        except ContractError as e:
            return JSONResponse(e.body(), status_code=e.http)
        except Exception:  # 프로그램 오류: 원인은 로그에만 남기고 백엔드에는 정해진 형식으로
            log.error(json.dumps({"event": "internal_error", "path": request.url.path}), exc_info=True)
            e = ContractError(500, "INTERNAL_ERROR", "NPC 추론 내부 오류", False, readable_request_id(payload))
            return JSONResponse(e.body(), status_code=e.http)

    @app.post("/v1/npc/persona/generate")
    async def persona_generate(req: PersonaGenerateRequest, request: Request):
        """어드민 랜덤 생성: 고정 입력과 DB 후보로 인격 카드 초안을 만든다. 백엔드가 받아 npcs에 저장한다 (워커는 저장하지 않음)."""
        ingredients = npc_persona.with_candidates(npc_persona.load_ingredients(), req.candidates.as_ingredients())
        try:
            config, life = npc_persona.generate(
                req.npc_id,
                req.role,
                req.place,
                ingredients,
                npc_persona.load_behaviors(),
                mbti=req.mbti,
                fixed=req.fixed.as_profile_fields(),
                taken_names=set(req.taken_names),
            )
        except ValueError as e:  # 그 장소의 직업·고를 이름·성격 특징 후보가 없음
            raise ApiError("invalid_request", str(e), 400) from e
        usage = Usage()
        if req.narrate:
            config = await npc_persona.narrate(config, request.app.state.llm, usage)
        return {"npc_configuration": config, "mbti": life["mbti"], "usage": usage.items}

    @app.post("/v1/npc/persona/derive")
    async def persona_derive(req: PersonaDeriveRequest):
        """어드민에서 MBTI를 바꾼 카드의 자동 칸(성격 5축·행동 조각)만 다시 계산한다. 손으로 쓴 글은 그대로."""
        try:
            NpcProfile.model_validate(req.profile)
        except ValidationError as e:
            where = "; ".join(f"{'.'.join(str(part) for part in err['loc'])}: {err['msg']}" for err in e.errors()[:5])
            raise ApiError("invalid_request", f"인격이 공통 인격 모델에 맞지 않음 ({where})", 400) from e
        life = {"npcId": req.npc_id, "mbti": req.mbti}
        return {"profile": npc_persona.with_derived_traits(req.profile, life, npc_persona.load_behaviors())}

    @app.get("/v1/audio/{audio_id}")
    async def audio(audio_id: str, request: Request):
        wav = request.app.state.audio.get(audio_id)
        if wav is None:
            raise ApiError("not_found", "음성 조각이 없거나 보관 시간이 지났습니다", 404)
        return Response(wav, media_type="audio/wav")

    @app.post("/v1/tts")
    async def tts_one(req: TtsRequest, request: Request):
        engine = request.app.state.tts
        if not engine:
            raise ApiError("tts_unavailable", "음성 합성 모델이 없습니다", 503)
        t = time.perf_counter()
        if req.phrases:
            parts, hit = [], True
            for phrase in req.phrases:
                part, _, cached = await engine.asynth(phrase.text.strip(), req.voice, req.speed)
                mood = Mood(phrase.emotion, phrase.intensity) if phrase.emotion else None
                parts.append(attach_emotion(part, mood))
                hit = hit and cached
            wav, dur = join_wavs(parts)
        else:
            wav, dur, hit = await engine.asynth(req.text.strip(), req.voice, req.speed)
        return Response(
            wav,
            media_type="audio/wav",
            headers={"X-Audio-S": f"{dur:.3f}", "X-Cache": "hit" if hit else "miss", "X-Synth-S": f"{time.perf_counter() - t:.3f}"},
        )

    @app.post("/v1/stt")
    async def transcribe(request: Request, file: UploadFile = File(...), scene_words: str = Form(""), npc_line: str = Form("")):
        mime = (file.content_type or "").split(";")[0].strip()  # "audio/webm;codecs=opus" → "audio/webm"
        if mime not in stt.AUDIO_EXT:
            raise ApiError("audio_format_unsupported", f"지원하지 않는 녹음 형식입니다 ({file.content_type})", 415)
        audio = await file.read(stt.STT_MAX_BYTES + 1)
        if not audio:
            raise ApiError("invalid_request", "녹음이 비어 있습니다", 400)
        if len(audio) > stt.STT_MAX_BYTES:
            raise ApiError("audio_too_large", "녹음이 3MB를 넘습니다", 413)
        try:  # JSON 배열 또는 쉼표로 구분한 글
            words = json.loads(scene_words) if scene_words.strip().startswith("[") else scene_words.split(",")
        except ValueError:
            raise ApiError("invalid_request", "scene_words는 JSON 배열이나 쉼표로 구분한 글이어야 합니다", 400) from None
        return await stt.stt(request.app.state.llm, audio, mime, [str(w) for w in words], npc_line)

    @app.post("/v1/hint")
    async def hint(req: HintRequest, request: Request):
        return await services.hint(request.app.state.llm, req)

    @app.post("/v1/meaning")
    async def meaning(req: MeaningRequest, request: Request):
        return await services.meaning(request.app.state.llm, req)

    @app.post("/v1/level-test/speaking")
    async def level_test_speaking(req: LevelTestSpeakingRequest, request: Request):
        """레벨 테스트 말하기 문항마다 통과 여부와 한국어 피드백. 백엔드가 테스트를 끝낼 때 한 번 부른다 (워커는 저장하지 않음)."""
        return await services.level_test_speaking(request.app.state.llm, req)

    @app.post("/v1/suggest")
    async def suggest(req: SuggestRequest, request: Request):
        return await services.suggest(request.app.state.llm, req)

    @app.post("/v1/feedback")
    async def feedback(request: Request, payload: dict = Body(...)):
        # 입력·출력은 ai/feedback 계약 0.3.0 (검증도 그 모듈이 한다)
        try:
            return await feedback_bridge.feedback(request.app.state.llm, payload)
        except ValidationError as e:
            where = "; ".join(f"{'.'.join(str(x) for x in err['loc'])}: {err['msg']}" for err in e.errors()[:5])
            raise ApiError("invalid_request", f"피드백 요청이 ai/feedback 계약과 다름 ({where})", 400) from None

    @app.post("/v1/memory")
    async def memory(req: MemoryRequest, request: Request):
        return await services.memory(request.app.state.llm, req)

    @app.post("/v1/npc/follow-up")
    async def follow_up(req: FollowUpRequest, request: Request):
        """끝난 대화에서 NPC가 곧 건네기로 한 것 (음료·음식·빨래 등). 백엔드가 준비 시각을 정해 저장한다 (워커는 저장하지 않음)."""
        return await services.follow_up(request.app.state.llm, req)

    @app.post("/v1/npc/relationship")
    async def relationship_after_conversation(req: RelationshipRequest):
        """정상 종료된 대화 하나를 사용자 × NPC 알아봄에 반영한다. 모델을 부르지 않는다. 백엔드가 받아 저장한다 (워커는 저장하지 않음)."""
        return services.relationship(req)

    return app


logging.basicConfig(level=logging.INFO, stream=sys.stdout, format="%(message)s")
logging.getLogger("httpx").setLevel(logging.WARNING)
app = create_app()
