"""모델 호출 (OpenAI 호환 API). 시간 상한·재시도·대체 모델·하루 한도 건너뛰기와 사용량 기록을 여기서 한다.

웹 프로토타입(프로토타입 브랜치의 prototype/mvp_web/server.py open_api·chat)을 비동기로 옮긴 것.
- 429/5xx면 잠깐 쉬고 한 번 더, 응답이 없으면(시간 상한) 바로 한 번 더, 그래도 안 되면 대체 모델로.
- 하루 한도("per day")에 걸린 모델은 한동안 시도하지 않고 바로 대체 모델로 (매번 쉬고 재시도하느라 턴이 6~12초 걸렸음).
- 형식 강제(json_schema) 출력이 깨져 400이 나면 json_object 모드로 한 번 더 (욕설이 든 대화에서 빈 응답 등).
"""

import asyncio
import contextvars
import json
import logging
import time
from collections.abc import AsyncIterator, Awaitable, Callable
from dataclasses import dataclass, field

import httpx

from .config import Settings

log = logging.getLogger("ai_worker")
RETRY_CODES = (429, 500, 502, 503)
# 요청 하나의 로그 줄에 덧붙일 기록 (main의 guard_and_trace가 요청마다 빈 dict를 넣는다). 시간 상한에 걸려 usage가 비는 요청에서도 남는다.
REQUEST_NOTES: contextvars.ContextVar[dict | None] = contextvars.ContextVar("request_notes", default=None)


def note_hedge(key: str, role: str):
    """같은 요청을 하나 더 보낸 호출(hedged)·그중 나중 요청의 답을 쓴 호출(hedge_won)을 역할 이름으로 요청 로그에 남긴다."""
    notes = REQUEST_NOTES.get()
    if notes is not None:
        notes.setdefault(key, []).append(role or "?")


class ModelOutputError(ValueError):
    """모델 출력이 정해진 형식이 아님 (재시도·대체 모델로도 고칠 수 없음).
    판정·대화 종료에서는 그 칸만 실패로 처리하고, 칸이 하나뿐인 힌트는 502 invalid_output."""


class ProviderError(Exception):
    """모델 공급자가 대체 모델까지 모두 실패. code는 API 오류 코드 (provider_unavailable / provider_limit).
    retryable: 다시 보내면 나아질 수 있는지. 공급자가 요청 자체를 거절하면(키·계정 오류 등) False (NPC 추론 계약 9절)."""

    def __init__(self, code: str, message: str, retryable: bool = True):
        super().__init__(message)
        self.code, self.message, self.retryable = code, message, retryable

    @property
    def http(self) -> int:
        return 429 if self.code == "provider_limit" else 503


class AudioRejected(ProviderError):
    """음성 인식 공급자가 녹음 자체를 받지 않음 (AUDIO_REJECTED). 다른 공급자도 같은 답이라 대체로 넘기지 않는다."""


AUDIO_REJECTED = (400, 413, 415, 422)


async def within(coro, budget_s: float):
    """요청 하나의 시간 상한. call_timeout_s는 호출 한 번의 상한이라 재시도·대체 모델을 거치면 훨씬 길어진다.
    넘으면 그 작업만 timeout 오류 (끊긴 호출의 사용량은 기록되지 않을 수 있음)."""
    try:
        return await asyncio.wait_for(coro, budget_s)
    except TimeoutError:
        raise ProviderError("timeout", f"{budget_s:g}초 안에 끝나지 않음") from None


@dataclass
class Usage:
    """요청 하나가 쓴 모델 호출 기록. 모든 응답의 usage로 Spring에 보낸다 (크레딧 차감·원장)."""

    items: list[dict] = field(default_factory=list)

    def add(self, step: str, model: str, requested: str, data: dict | None, latency_s: float, **extra):
        u = (data or {}).get("usage") or (data or {}).get("x_groq", {}).get("usage") or {}
        self.items.append(
            {
                "step": step,
                "model": model,
                "input_tokens": u.get("prompt_tokens", 0),
                "output_tokens": u.get("completion_tokens", 0),
                "latency_s": round(latency_s, 2),
                "fallback": model != requested,
                **extra,
            }
        )


def response_json(r: httpx.Response) -> dict:
    """공급자 응답 본문(JSON 객체). JSON이 아니거나 객체가 아니면 공급자 오류 (판정에서는 그 칸만 실패)."""
    try:
        data = r.json()
    except ValueError:
        raise ProviderError("provider_unavailable", f"공급자 응답이 JSON이 아님: {r.text[:80]!r}") from None
    if not isinstance(data, dict):
        raise ProviderError("provider_unavailable", "공급자 응답이 JSON 객체가 아님")
    return data


def message_text(data: dict) -> str:
    """chat 응답의 choices[0].message.content. 칸이 없으면 공급자 오류 (content가 null이면 빈 답)."""
    choices = data.get("choices")
    message = choices[0].get("message") if isinstance(choices, list) and choices and isinstance(choices[0], dict) else None
    if not isinstance(message, dict):
        raise ProviderError("provider_unavailable", f"공급자 응답에 choices[0].message가 없음: {str(data)[:80]}")
    return (message.get("content") or "").strip()


def stream_data(line: str) -> dict | None:
    """SSE 한 줄 → data 객체 (data 줄이 아니거나 [DONE]이면 None). data가 JSON 객체가 아니면 공급자 오류."""
    line = line.strip()
    if not line.startswith("data:") or line[5:].strip() == "[DONE]":
        return None
    try:
        data = json.loads(line[5:])
    except ValueError:
        raise ProviderError("provider_unavailable", "스트림 data 줄이 JSON이 아님") from None
    if not isinstance(data, dict):
        raise ProviderError("provider_unavailable", "스트림 data 줄이 JSON 객체가 아님")
    return data


def stream_piece(data: dict) -> str:
    choices = data.get("choices")
    first = choices[0] if isinstance(choices, list) and choices and isinstance(choices[0], dict) else {}
    delta = first.get("delta") if isinstance(first.get("delta"), dict) else {}
    return delta.get("content") or ""


async def race(
    start: Callable[[], Awaitable], after: float, ok: Callable[[object], bool], discard: Callable[[object], Awaitable], role: str = ""
):
    """start()로 시작한 작업이 after초 안에 끝나지 않으면 같은 작업을 하나 더 시작해 먼저 ok인 결과를 쓴다 (after가 0이면 하나만).
    ok가 아닌 결과(예: 503 응답)는 남은 쪽을 기다리고, 둘 다 ok가 아니면 마지막 결과를, 결과 없이 둘 다 실패하면 마지막 오류를 낸다.
    반환: (결과, 하나 더 시작했나). 쓰지 않은 결과는 discard로 정리하고 끝나지 않은 작업은 취소한다 —
    위에서 취소돼 나갈 때도 같다 (취소 직전에 끝난 응답이 열린 채 남지 않게). role은 요청 로그에 남길 이름."""
    tasks, winner = [asyncio.ensure_future(start())], None
    try:
        done, _ = await asyncio.wait(tasks, timeout=after or None)
        if not done:
            tasks.append(asyncio.ensure_future(start()))
            note_hedge("hedged", role)
        pending, last_error, last_bad = set(tasks), None, None
        while pending:
            done, pending = await asyncio.wait(pending, return_when=asyncio.FIRST_COMPLETED)
            for t in sorted(done, key=tasks.index):
                if t.exception() is not None:
                    last_error = t.exception()
                elif ok(t.result()):
                    winner = t
                    if t is not tasks[0]:
                        note_hedge("hedge_won", role)
                    return t.result(), len(tasks) > 1
                else:
                    last_bad = t
        if last_bad is not None:
            winner = last_bad
            return last_bad.result(), len(tasks) > 1
        raise last_error
    finally:
        for t in tasks:
            if t is winner:
                continue
            if not t.done():
                t.cancel()
            elif not t.cancelled() and t.exception() is None:
                await discard(t.result())


def model_body(body: dict, params: list[dict]) -> dict:
    """모델 계열마다 다른 요청 칸 (llm_profiles.json model_params): 생각(reasoning) 끄기·줄이기,
    gpt-oss는 생각에도 토큰을 써서 출력 상한을 넉넉히, GPT-5 계열은 max_completion_tokens·temperature 없음."""
    b = dict(body)
    for rule in params:
        if rule["match"] in b["model"]:
            b.update(rule.get("set", {}))
            if rule.get("min_max_tokens"):
                b["max_tokens"] = max(b.get("max_tokens", 0), rule["min_max_tokens"])
            for k in rule.get("drop", []):
                b.pop(k, None)
            for old, new in rule.get("rename", {}).items():
                if old in b:
                    b[new] = b.pop(old)
            break
    return b


class LLM:
    def __init__(self, settings: Settings, transport: httpx.AsyncBaseTransport | None = None):
        self.s = settings

        def client(base_url: str, key: str) -> httpx.AsyncClient:
            return httpx.AsyncClient(
                base_url=base_url, transport=transport, headers={"Authorization": f"Bearer {key}", "User-Agent": "ai-worker/0.1"}
            )

        self.client = client(settings.base_url, settings.api_key)  # 구성의 기본 공급자
        self.others = {name: client(p["base_url"], p["api_key"]) for name, p in settings.providers.items()}
        self.exhausted: dict[str, float] = {}  # 모델 → 이 시각까지 건너뜀
        self.clock = time.monotonic  # 음성 인식 공급자 건너뛰기 시각 (테스트에서 바꿔 끼움)
        self.stt_failures: dict[str, int] = {}  # 음성 인식 공급자 → 연속 실패 수
        self.stt_skip_until: dict[str, float] = {}  # 음성 인식 공급자 → 이 시각(clock)까지 건너뜀
        self.gpu_failures: dict[str, int] = {}  # GPU 서버 판정("guard"·"judge") → 연속 실패 수
        self.gpu_skip_until: dict[str, float] = {}  # GPU 서버 판정 → 이 시각(clock)까지 건너뜀

    async def aclose(self):
        for c in (self.client, *self.others.values()):
            await c.aclose()

    def model(self, role: str) -> str:
        return self.s.models[role]

    def client_for(self, role: str) -> httpx.AsyncClient:
        """역할이 쓰는 공급자 (예: GMS 구성에서도 stt는 Groq)."""
        return self.others.get(self.s.role_provider.get(role, ""), self.client)

    async def _send(
        self, client: httpx.AsyncClient, b: dict, timeout: httpx.Timeout, stream: bool, hedge_after_s: float = 0, role: str = ""
    ) -> tuple[httpx.Response, bool]:
        """요청 하나. 한 번에 받는 호출이 hedge_after_s 안에 답이 없으면 같은 요청을 하나 더 보내 먼저 온 답을 쓴다 (0이면 하나만).
        반환: (응답, 더 보냈나). 둘 다 실패하면 마지막 오류를 던진다. 스트림은 응답 머리가 먼저 와서 늦음을 알 수 없어 그대로."""

        def start_request():
            return client.send(client.build_request("POST", "/chat/completions", json=b, timeout=timeout), stream=stream)

        after = 0 if stream else hedge_after_s
        return await race(start_request, after, lambda r: r.status_code < 400, lambda r: r.aclose(), role)

    async def _open(
        self, body: dict, stream: bool = False, role: str = "", timeout_s: float | None = None, hedge_after_s: float = 0
    ) -> tuple[httpx.Response, str, bool]:
        """대체 모델 사슬을 따라 첫 성공 응답을 연다. 반환: (응답, 실제로 쓴 모델, 같은 요청을 더 보냈나)."""
        model, last, limited, tried = body["model"], "", False, set()
        client = self.client_for(role)
        timeout_s = timeout_s or self.s.call_timeout_s
        timeout = httpx.Timeout(timeout_s, connect=5.0)
        while model and model not in tried:  # 대체 모델끼리 서로를 가리켜도(20b ↔ 120b) 한 번씩만
            tried.add(model)
            if self.exhausted.get(model, 0) > time.time():
                last, limited = last or f"{model} 하루 한도 소진", True
                model = self.s.fallback.get(model)
                continue
            b = model_body({**body, "model": model}, self.s.model_params)
            for attempt in range(2):
                try:
                    r, hedged = await self._send(client, b, timeout, stream, hedge_after_s, role)
                except httpx.TimeoutException as e:
                    # 오류 없이 30초 넘게 멈춘 호출이 있었다 (2026-10-13). 짧게 끊고 다시 보낸다
                    last = f"응답 없음({timeout_s}s): {type(e).__name__}"
                    continue
                except httpx.TransportError as e:
                    last = f"연결 오류: {type(e).__name__}: {str(e)[:80]}"
                    continue
                if r.status_code < 400:
                    return r, model, hedged
                msg = (await r.aread())[:400].decode("utf-8", "replace")
                await r.aclose()
                last = f"{r.status_code}: {msg[:160]}"
                if r.status_code not in RETRY_CODES:
                    raise ProviderError("provider_unavailable", f"모델 서버 오류 {last}", retryable=False)
                if r.status_code == 429:
                    limited = True
                    if "per day" in msg:  # 하루 한도: 재시도해도 소용없음
                        self.exhausted[model] = time.time() + self.s.exhausted_skip_s
                        break
                if attempt == 0:
                    await asyncio.sleep(min(float(r.headers.get("retry-after") or self.s.retry_pause_s), 5))
            model = self.s.fallback.get(model)
        raise ProviderError("provider_limit" if limited else "provider_unavailable", f"모든 모델이 응답하지 않음 ({last})")

    async def chat(
        self,
        step: str,
        role: str,
        messages: list[dict],
        usage: Usage,
        temperature: float = 0.0,
        max_tokens: int = 200,
        schema: dict | None = None,
        timeout_s: float | None = None,
        hedge: bool = True,
        hedge_after_s: float | None = None,
    ) -> str:
        """한 번에 받는 호출. 반환: 모델 출력 문자열 (schema가 있으면 JSON 문자열).
        timeout_s: 이 호출의 상한 (없으면 CALL_TIMEOUT_S), hedge: 늦을 때 같은 요청 하나 더 보내기 (길게 생성하는 호출은 끔),
        hedge_after_s: 더 보내는 기준 (없으면 HEDGE_AFTER_S)."""
        after = (self.s.hedge_after_s if hedge_after_s is None else hedge_after_s) if hedge else 0
        requested = self.model(role)
        body = {"model": requested, "temperature": temperature, "max_tokens": max_tokens, "messages": messages}
        if schema:
            body["response_format"] = {"type": "json_schema", "json_schema": {"name": "out", "schema": schema, "strict": True}}
        t = time.perf_counter()
        try:
            r, used, hedged = await self._open(body, role=role, timeout_s=timeout_s, hedge_after_s=after)
        except ProviderError as e:
            if not schema or "validate JSON" not in e.message:
                raise
            body["response_format"] = {"type": "json_object"}
            body["messages"] = [
                {
                    **messages[0],
                    "content": messages[0]["content"] + "\n\nReply with only a JSON object matching this schema:\n" + json.dumps(schema),
                },
                *messages[1:],
            ]
            r, used, hedged = await self._open(body, role=role, timeout_s=timeout_s, hedge_after_s=after)
        data = response_json(r)
        usage.add(step, used, requested, data, time.perf_counter() - t, **({"hedged": True} if hedged else {}))  # 늦은 쪽 토큰은 모름
        return message_text(data)

    async def stream(self, step: str, role: str, messages: list[dict], usage: Usage, temperature: float = 0.6, max_tokens: int = 120):
        """글자 조각을 받는 대로 내보낸다. 대체 모델은 첫 응답 전에만 (이미 보낸 글자를 바꿀 수 없으니)."""
        requested = self.model(role)
        body = {
            "model": requested,
            "temperature": temperature,
            "max_tokens": max_tokens,
            "messages": messages,
            "stream": True,
            "stream_options": {"include_usage": True},
        }
        t, first, last_data = time.perf_counter(), None, None
        r, used, got, lines, hedged = await self._open_stream(body, role)

        async def datas():
            for data in got:  # 첫 글자까지 이미 읽은 것
                yield data
            async for line in lines:
                data = stream_data(line)
                if data is not None:
                    yield data

        try:
            async for data in datas():
                if data.get("usage") or data.get("x_groq", {}).get("usage"):
                    last_data = data
                piece = stream_piece(data)
                if piece:
                    if first is None:
                        first = time.perf_counter() - t
                    yield piece
        except (httpx.TimeoutException, httpx.TransportError) as e:
            raise ProviderError("provider_unavailable", f"대답 도중 끊김: {type(e).__name__}") from None
        finally:
            await r.aclose()
            usage.add(
                step,
                used,
                requested,
                last_data,
                time.perf_counter() - t,
                first_token_s=round(first, 2) if first is not None else None,
                **({"hedged": True} if hedged else {}),
            )

    async def _first_text(self, body: dict, role: str) -> tuple[httpx.Response, str, list[dict], AsyncIterator[str]]:
        """스트림을 열고 첫 글자 조각이 올 때까지 읽는다. 반환: (응답, 실제로 쓴 모델, 그때까지의 data, 남은 줄).
        중간에 취소되면(다른 쪽이 이김) 응답을 닫는다."""
        r, used, _ = await self._open(body, stream=True, role=role)
        lines, got = r.aiter_lines(), []
        try:
            async for line in lines:
                data = stream_data(line)
                if data is None:
                    continue
                got.append(data)
                if stream_piece(data):
                    break
        except (httpx.TimeoutException, httpx.TransportError) as e:
            await r.aclose()
            raise ProviderError("provider_unavailable", f"대답 첫 글자 전에 끊김: {type(e).__name__}") from None
        except BaseException:
            await r.aclose()
            raise
        return r, used, got, lines

    async def _open_stream(self, body: dict, role: str) -> tuple[httpx.Response, str, list[dict], AsyncIterator[str], bool]:
        """첫 글자가 stream_hedge_after_s 안에 안 오면 같은 요청 스트림을 하나 더 열어, 먼저 첫 글자를 낸 쪽을 쓴다.
        GMS는 첫 글자가 나올 때 응답 머리를 보내므로 머리가 늦은 경우도 같이 잡힌다. 진 쪽은 닫는다.
        반환: _first_text의 값 + 하나 더 보냈는지."""
        res, hedged = await race(
            lambda: self._first_text(body, role), self.s.stream_hedge_after_s, lambda _: True, lambda res: res[0].aclose(), role
        )
        return (*res, hedged)

    def stt_steps(self) -> list[tuple[str, str]]:
        """음성 인식을 부를 (공급자, 모델) 순서. STT_CHAIN이 없으면 구성의 음성 인식 다음에 대체(stt_fallback) 하나."""
        if self.s.stt_chain:
            return [(step["provider"], step["model"]) for step in self.s.stt_chain]
        steps = [(self.s.role_provider.get("stt", ""), self.model("stt"))]
        if self.s.stt_fallback:
            steps.append((self.s.stt_fallback["provider"], self.s.stt_fallback["model"]))
        return steps

    def stt_timeout(self, position: int, count: int) -> float:
        """첫 공급자는 짧게(뒤가 있을 때), 중간은 stt_next_timeout_s, 마지막(또는 하나뿐)은 30초."""
        if position == count - 1:
            return 30.0
        return self.s.stt_first_timeout_s if position == 0 else self.s.stt_next_timeout_s

    def note_stt_failure(self, provider: str):
        """연속 실패가 stt_breaker_failures에 닿으면 그 공급자를 stt_breaker_skip_s 동안 건너뛴다 (GMS 크레딧·턴 지연을 아끼려고)."""
        if self.s.stt_breaker_failures <= 0:
            return
        count = self.stt_failures.get(provider, 0) + 1
        if count >= self.s.stt_breaker_failures:
            self.stt_skip_until[provider] = self.clock() + self.s.stt_breaker_skip_s
            log.warning(json.dumps({"event": "stt_skip", "provider": provider, "skip_s": self.s.stt_breaker_skip_s}, ensure_ascii=False))
            count = 0
        self.stt_failures[provider] = count

    async def guard_chat(self, messages: list[dict], usage: Usage, gpu_messages: list[dict] | None = None) -> str:
        """무례 판정 호출. GPU 서버 판정 모델(guard_gpu_model)이 있으면 gpu_messages로 먼저 부르고 실패하면 구성의 guard 모델에
        messages로 넘긴다 (_gpu_first). gpu_messages가 없으면(GPU 모델이 잘못 읽는 줄) 처음부터 구성의 guard 모델로 판정한다."""
        s = self.s
        return await self._gpu_first(
            "guard",
            s.guard_gpu_model if gpu_messages else "",
            s.guard_gpu_timeout_s,
            (s.guard_breaker_failures, s.guard_breaker_skip_s),
            lambda: self._chat_once("gpu_llm", s.guard_gpu_model, "guard", gpu_messages, usage),
            lambda: self.chat("guard", "guard", messages, usage, 0.0, 5),
            usage,
        )

    async def intent_chat(self, messages: list[dict], usage: Usage, schema: dict, hedge_after_s: float | None = None) -> str:
        """의도 판정 호출 (JSON 문자열). GPU 서버 의도 판정 모델(judge_gpu_model)이 있으면 먼저 부르고 실패하거나
        JSON이 아닌 답이면 구성의 intent 모델 (_gpu_first). 형식 강제는 둘 다 같은 schema로 한다.
        hedge_after_s는 구성의 intent 모델 호출에만 쓴다 (GPU 호출은 짧은 상한 하나).
        출력 상한은 의도·고친 문장과 함께 모든 발화의 다른 표현(최대 3개, 발화 전체를 다시 쓴 문장)이 들어가도 잘리지 않게 둔다."""
        s = self.s
        max_tokens = 320
        return await self._gpu_first(
            "judge",
            s.judge_gpu_model,
            s.judge_gpu_timeout_s,
            (s.judge_breaker_failures, s.judge_breaker_skip_s),
            lambda: self._chat_once("gpu_llm", s.judge_gpu_model, "intent", messages, usage, max_tokens, schema),
            lambda: self.chat("intent", "intent", messages, usage, 0.0, max_tokens, schema, hedge_after_s=hedge_after_s),
            usage,
            check=json.loads,
        )

    async def _gpu_first(
        self,
        name: str,
        model: str,
        timeout_s: float,
        breaker: tuple[int, float],
        gpu_call: Callable[[], Awaitable[str]],
        profile_call: Callable[[], Awaitable[str]],
        usage: Usage,
        check: Callable[[str], object] = lambda out: None,
    ) -> str:
        """GPU 서버 모델(model)이 있으면 먼저 한 번(짧은 상한 timeout_s, 재시도 없음) 부르고, 실패·시간 초과·check가
        ValueError를 낸 답이면 구성의 모델(profile_call)로 넘긴다. 연속 실패가 breaker[0]에 닿으면 GPU를 breaker[1]초 건너뛴다.
        GPU 서버는 역방향 SSH 터널로만 닿고 사용 허용도 확정 전이라, 끊겨도 판정은 GMS 모델로 이어 간다."""
        if not model or self.gpu_skip_until.get(name, 0.0) > self.clock():
            return await profile_call()
        try:
            out = await within(gpu_call(), timeout_s)
            check(out)
        except (ProviderError, ValueError) as e:
            self._note_gpu_failure(name, *breaker)
            why = e.message if isinstance(e, ProviderError) else f"형식이 맞지 않는 답: {e}"[:160]
            log.warning(json.dumps({"event": f"{name}_fallback", "model": model, "why": why}, ensure_ascii=False))
            out = await profile_call()
            usage.items[-1]["fallback"] = True
            return out
        self.gpu_failures[name] = 0
        return out

    async def _chat_once(
        self, provider: str, model: str, step: str, messages: list[dict], usage: Usage, max_tokens: int = 5, schema: dict | None = None
    ) -> str:
        """다른 공급자의 모델 하나를 재시도·대체 모델 없이 한 번 부른다 (실패하면 부른 쪽이 다음으로 넘긴다)."""
        body = {"model": model, "temperature": 0.0, "max_tokens": max_tokens, "messages": messages}
        if schema:
            body["response_format"] = {"type": "json_schema", "json_schema": {"name": "out", "schema": schema, "strict": True}}
        b = model_body(body, self.s.model_params)
        t = time.perf_counter()
        try:
            r = await self.others[provider].post("/chat/completions", json=b)
        except (httpx.TimeoutException, httpx.TransportError) as e:
            raise ProviderError("provider_unavailable", f"{model} 연결 오류: {type(e).__name__}") from None
        if r.status_code >= 400:
            raise ProviderError("provider_unavailable", f"{model} {r.status_code}: {r.text[:160]}")
        data = response_json(r)
        usage.add(step, model, model, data, time.perf_counter() - t)
        return message_text(data)

    def _note_gpu_failure(self, name: str, failures: int, skip_s: float):
        if failures <= 0:
            return
        count = self.gpu_failures.get(name, 0) + 1
        if count >= failures:
            self.gpu_skip_until[name] = self.clock() + skip_s
            log.warning(json.dumps({"event": f"{name}_gpu_skip", "skip_s": skip_s}, ensure_ascii=False))
            count = 0
        self.gpu_failures[name] = count

    async def transcribe(self, audio: bytes, filename: str, mime: str, prompt: str, usage: Usage) -> dict:
        """음성 인식 (Whisper). 공급자를 순서대로 부르고, 한도·오류·응답 없음이면 다음 공급자로 넘긴다
        (예: GPU 서버 자체 모델 → GMS whisper-1 → Groq). 녹음 자체 문제(AudioRejected)는 다음도 같은 답이라 넘기지 않는다."""
        steps = self.stt_steps()
        t = time.perf_counter()
        call = (filename, audio, mime, prompt)
        failures: list[str] = []
        for position, (provider, model) in enumerate(steps):
            is_last = position == len(steps) - 1
            if not is_last and self.stt_skip_until.get(provider, 0.0) > self.clock():
                continue
            t_call = time.perf_counter()
            try:
                data = await self._transcribe_once(
                    self.others.get(provider, self.client), model, self.stt_timeout(position, len(steps)), *call
                )
            except AudioRejected:
                raise
            except ProviderError as e:
                self.note_stt_failure(provider)
                failures.append(e.message if position == 0 else f"대체 {model}: {e.message}")
                if is_last:
                    raise ProviderError(e.code, " / ".join(failures)) from None
                log.warning(
                    json.dumps(
                        {"event": "stt_fallback", "model": model, "to": steps[position + 1][1], "why": e.message}, ensure_ascii=False
                    )
                )
                continue
            self.stt_failures[provider] = 0
            now = time.perf_counter()
            usage.items.append(
                {
                    "step": "stt",
                    "model": model,
                    "audio_s": data.get("duration"),
                    "latency_s": round(now - t_call, 2),
                    "fallback": position > 0,
                }
            )
            # 응답의 latency_s는 실패한 앞 호출까지 포함한 전체 시간, usage는 성공한 호출 하나의 시간
            return {"text": (data.get("text") or "").strip(), "audio_s": data.get("duration"), "latency_s": round(now - t, 2)}

    async def _transcribe_once(
        self, client: httpx.AsyncClient, model: str, timeout_s: float, filename: str, audio: bytes, mime: str, prompt: str
    ) -> dict:
        try:
            r = await client.post(
                "/audio/transcriptions",
                timeout=httpx.Timeout(timeout_s, connect=5.0),
                files={"file": (filename, audio, mime)},
                data={"model": model, "response_format": "verbose_json", "temperature": "0", "prompt": prompt},
            )
        except (httpx.TimeoutException, httpx.TransportError) as e:
            raise ProviderError("provider_unavailable", f"음성 인식 응답 없음 {model}: {type(e).__name__}") from None
        if r.status_code in AUDIO_REJECTED:
            raise AudioRejected("provider_unavailable", f"음성 인식 오류 {model} {r.status_code}: {r.text[:160]}")
        if r.status_code >= 400:
            raise ProviderError(
                "provider_limit" if r.status_code == 429 else "provider_unavailable",
                f"음성 인식 오류 {model} {r.status_code}: {r.text[:160]}",
            )
        return response_json(r)
