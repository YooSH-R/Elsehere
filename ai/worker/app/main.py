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

from . import feedback_bridge, services
from .config import Settings
from .guard_local import LocalGuard
from .llm import LLM, ModelOutputError, ProviderError
from .npc import reply_events
from .schemas import HintRequest, JudgeRequest, MeaningRequest, MemoryRequest, ReplyRequest, SuggestRequest, TtsRequest
from .tts import AudioStore, Kokoro

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
        app.state.tts = await asyncio.to_thread(Kokoro.load, s.kokoro_model, s.kokoro_voices) if tts is True else tts
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
                    "tts": bool(app.state.tts),
                    "guard_local": gl and {"model": gl.name, "context": gl.context, "t_lo": s.guard_t_lo, "t_hi": s.guard_t_hi},
                },
                ensure_ascii=False,
            )
        )
        yield
        await app.state.llm.aclose()

    app = FastAPI(title="AI 워커", version="0.1.0", lifespan=lifespan)

    @app.middleware("http")
    async def guard_and_trace(request: Request, call_next):
        """내부 토큰 확인 + 추적 id(X-Request-Id·X-User-Id·X-Conv-Id)를 로그에 남긴다 (Spring 턴 기록과 잇는 열쇠)."""
        rid = request.headers.get("X-Request-Id") or secrets.token_hex(8)
        t = time.perf_counter()
        if request.url.path not in PUBLIC and not secrets.compare_digest(
            request.headers.get("X-Internal-Token", ""), request.app.state.settings.token
        ):
            resp = error("unauthorized", "내부 토큰이 틀렸습니다", 401)
        else:
            resp = await call_next(request)
        resp.headers["X-Request-Id"] = rid
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
            "profile": st.settings.profile,
            "models": st.settings.models,
            "guard_local": gl and {"model": gl.name, "t_lo": st.settings.guard_t_lo, "t_hi": st.settings.guard_t_hi},
        }

    @app.post("/v1/judge")
    async def judge(req: JudgeRequest, request: Request):
        return await services.judge(request.app.state.llm, req, request.app.state.guard_local)

    @app.post("/v1/npc/reply")
    async def npc_reply(req: ReplyRequest, request: Request):
        st = request.app.state

        async def body():
            async for name, data in reply_events(st.llm, st.tts, st.audio, st.settings, req):
                yield sse(name, data)

        # nginx·Spring이 버퍼링하지 않게 (첫 글자가 바로 가야 함)
        return StreamingResponse(body(), media_type="text/event-stream", headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"})

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
        wav, dur, hit = await engine.asynth(req.text.strip(), req.voice, req.speed)
        return Response(
            wav,
            media_type="audio/wav",
            headers={"X-Audio-S": f"{dur:.3f}", "X-Cache": "hit" if hit else "miss", "X-Synth-S": f"{time.perf_counter() - t:.3f}"},
        )

    @app.post("/v1/stt")
    async def stt(request: Request, file: UploadFile = File(...), scene_words: str = Form("")):
        mime = (file.content_type or "").split(";")[0].strip()  # "audio/webm;codecs=opus" → "audio/webm"
        if mime not in services.AUDIO_EXT:
            raise ApiError("audio_format_unsupported", f"지원하지 않는 녹음 형식입니다 ({file.content_type})", 415)
        audio = await file.read(services.STT_MAX_BYTES + 1)
        if not audio:
            raise ApiError("invalid_request", "녹음이 비어 있습니다", 400)
        if len(audio) > services.STT_MAX_BYTES:
            raise ApiError("audio_too_large", "녹음이 3MB를 넘습니다", 413)
        try:  # JSON 배열 또는 쉼표로 구분한 글
            words = json.loads(scene_words) if scene_words.strip().startswith("[") else scene_words.split(",")
        except ValueError:
            raise ApiError("invalid_request", "scene_words는 JSON 배열이나 쉼표로 구분한 글이어야 합니다", 400) from None
        return await services.stt(request.app.state.llm, audio, mime, [str(w) for w in words])

    @app.post("/v1/hint")
    async def hint(req: HintRequest, request: Request):
        return await services.hint(request.app.state.llm, req)

    @app.post("/v1/meaning")
    async def meaning(req: MeaningRequest, request: Request):
        return await services.meaning(request.app.state.llm, req)

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

    return app


logging.basicConfig(level=logging.INFO, stream=sys.stdout, format="%(message)s")
app = create_app()
