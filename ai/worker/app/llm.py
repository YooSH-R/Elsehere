"""모델 호출 (OpenAI 호환 API). 시간 상한·재시도·대체 모델·하루 한도 건너뛰기와 사용량 기록을 여기서 한다.

웹 프로토타입(프로토타입 브랜치의 prototype/mvp_web/server.py open_api·chat)을 비동기로 옮긴 것.
- 429/5xx면 잠깐 쉬고 한 번 더, 응답이 없으면(시간 상한) 바로 한 번 더, 그래도 안 되면 대체 모델로.
- 하루 한도("per day")에 걸린 모델은 한동안 시도하지 않고 바로 대체 모델로 (매번 쉬고 재시도하느라 턴이 6~12초 걸렸음).
- 형식 강제(json_schema) 출력이 깨져 400이 나면 json_object 모드로 한 번 더 (욕설이 든 대화에서 빈 응답 등).
"""

import asyncio
import json
import time
from dataclasses import dataclass, field

import httpx

from .config import Settings

RETRY_CODES = (429, 500, 502, 503)


class ModelOutputError(ValueError):
    """모델 출력이 정해진 형식이 아님 (재시도·대체 모델로도 고칠 수 없음).
    판정·대화 종료에서는 그 칸만 실패로 처리하고, 칸이 하나뿐인 힌트는 502 invalid_output."""


class ProviderError(Exception):
    """모델 공급자가 대체 모델까지 모두 실패. code는 API 오류 코드 (provider_unavailable / provider_limit)."""

    def __init__(self, code: str, message: str):
        super().__init__(message)
        self.code, self.message = code, message

    @property
    def http(self) -> int:
        return 429 if self.code == "provider_limit" else 503


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

    async def aclose(self):
        for c in (self.client, *self.others.values()):
            await c.aclose()

    def model(self, role: str) -> str:
        return self.s.models[role]

    def client_for(self, role: str) -> httpx.AsyncClient:
        """역할이 쓰는 공급자 (예: GMS 구성에서도 stt는 Groq)."""
        return self.others.get(self.s.role_provider.get(role, ""), self.client)

    async def _send(self, client: httpx.AsyncClient, b: dict, timeout: httpx.Timeout, stream: bool) -> tuple[httpx.Response, bool]:
        """요청 하나. 한 번에 받는 호출이 hedge_after_s 안에 답이 없으면 같은 요청을 하나 더 보내 먼저 온 답을 쓴다.
        반환: (응답, 더 보냈나). 둘 다 실패하면 마지막 오류를 던진다. 스트림은 응답 머리가 먼저 와서 늦음을 알 수 없어 그대로."""
        send = lambda: asyncio.ensure_future(
            client.send(client.build_request("POST", "/chat/completions", json=b, timeout=timeout), stream=stream)
        )
        first = send()
        if stream or not self.s.hedge_after_s:
            return await first, False
        tasks = [first]
        try:
            done, _ = await asyncio.wait({first}, timeout=self.s.hedge_after_s)
            if done:
                return first.result(), False
            tasks.append(send())
            pending, last, failed = set(tasks), None, []  # failed: 오류 상태 코드로 끝난 응답 (남은 쪽을 기다림)
            while pending:
                done, pending = await asyncio.wait(pending, return_when=asyncio.FIRST_COMPLETED)
                got = [t.result() for t in done if t.exception() is None]
                last = next((t.exception() for t in done if t.exception() is not None), last)
                win = next((r for r in got if r.status_code < 400), None)
                failed += [r for r in got if r is not win and r.status_code >= 400]
                for r in got:
                    if win and r is not win:  # 같이 끝난 다른 응답은 닫는다
                        await r.aclose()
                if win:
                    for r in failed:
                        await r.aclose()
                    return win, True
            if failed:  # 둘 다 안 됐으면 오류 응답을 돌려줘 _open이 상태 코드대로 재시도·대체 모델을 정하게
                for r in failed[:-1]:
                    await r.aclose()
                return failed[-1], True
            raise last
        finally:  # 이긴 쪽이 정해졌거나 위에서 취소되면(판정 시간 상한 등) 남은 요청은 끊는다
            for t in tasks:
                if not t.done():
                    t.cancel()

    async def _open(self, body: dict, stream: bool = False, role: str = "") -> tuple[httpx.Response, str, bool]:
        """대체 모델 사슬을 따라 첫 성공 응답을 연다. 반환: (응답, 실제로 쓴 모델, 같은 요청을 더 보냈나)."""
        model, last, limited, tried = body["model"], "", False, set()
        client = self.client_for(role)
        timeout = httpx.Timeout(self.s.call_timeout_s, connect=5.0)
        while model and model not in tried:  # 대체 모델끼리 서로를 가리켜도(20b ↔ 120b) 한 번씩만
            tried.add(model)
            if self.exhausted.get(model, 0) > time.time():
                last, limited = last or f"{model} 하루 한도 소진", True
                model = self.s.fallback.get(model)
                continue
            b = model_body({**body, "model": model}, self.s.model_params)
            for attempt in range(2):
                try:
                    r, hedged = await self._send(client, b, timeout, stream)
                except httpx.TimeoutException as e:
                    # 오류 없이 30초 넘게 멈춘 호출이 있었다 (2026-10-13). 짧게 끊고 다시 보낸다
                    last = f"응답 없음({self.s.call_timeout_s}s): {type(e).__name__}"
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
                    raise ProviderError("provider_unavailable", f"모델 서버 오류 {last}")
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
    ) -> str:
        """한 번에 받는 호출. 반환: 모델 출력 문자열 (schema가 있으면 JSON 문자열)."""
        requested = self.model(role)
        body = {"model": requested, "temperature": temperature, "max_tokens": max_tokens, "messages": messages}
        if schema:
            body["response_format"] = {"type": "json_schema", "json_schema": {"name": "out", "schema": schema, "strict": True}}
        t = time.perf_counter()
        try:
            r, used, hedged = await self._open(body, role=role)
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
            r, used, hedged = await self._open(body, role=role)
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
        r, used, _ = await self._open(body, stream=True, role=role)
        try:
            async for line in r.aiter_lines():
                data = stream_data(line)
                if data is None:
                    continue
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
                step, used, requested, last_data, time.perf_counter() - t, first_token_s=round(first, 2) if first is not None else None
            )

    async def transcribe(self, audio: bytes, filename: str, mime: str, prompt: str, usage: Usage) -> dict:
        """음성 인식 (Whisper). 대체 모델 없음: 실패하면 바로 오류."""
        model = self.model("stt")
        t = time.perf_counter()
        try:
            r = await self.client_for("stt").post(
                "/audio/transcriptions",
                timeout=httpx.Timeout(30.0, connect=5.0),
                files={"file": (filename, audio, mime)},
                data={"model": model, "response_format": "verbose_json", "temperature": "0", "prompt": prompt},
            )
        except (httpx.TimeoutException, httpx.TransportError) as e:
            raise ProviderError("provider_unavailable", f"음성 인식 응답 없음: {type(e).__name__}") from None
        if r.status_code >= 400:
            raise ProviderError(
                "provider_limit" if r.status_code == 429 else "provider_unavailable", f"음성 인식 오류 {r.status_code}: {r.text[:160]}"
            )
        data = response_json(r)
        latency = time.perf_counter() - t
        usage.items.append(
            {"step": "stt", "model": model, "audio_s": data.get("duration"), "latency_s": round(latency, 2), "fallback": False}
        )
        return {"text": (data.get("text") or "").strip(), "audio_s": data.get("duration"), "latency_s": round(latency, 2)}
