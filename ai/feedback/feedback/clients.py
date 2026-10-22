"""피드백 모델 호출 경계와 외부 서비스 없는 고정 응답 구현."""
import json
from typing import Callable, Protocol


class ModelClient(Protocol):
    def complete(self, messages: list[dict[str, str]]) -> str:
        """모델 JSON 본문을 문자열로 반환. HTTP 응답 전체가 아니다."""
        ...


class ProviderCallError(RuntimeError):
    """공급자 오류. 실제 HTTP 어댑터는 민감한 응답 본문을 노출하지 않는다."""


class ModelNotConfigured(RuntimeError):
    pass


class FixtureClient:
    """직접 작성한 고정 응답 재생기. 새 대화를 이해하거나 AI를 호출하지 않는다."""
    def __init__(self, body: dict):
        self.body = body
        self.calls = 0

    def complete(self, messages):
        self.calls += 1
        return json.dumps(self.body, ensure_ascii=False)


class CallableClient:
    """팀 GMS 호출 함수를 주입하는 연결점. callable(messages) -> JSON 문자열."""
    def __init__(self, call: Callable[[list[dict[str, str]]], str]):
        self.call = call

    def complete(self, messages):
        return self.call(messages)


class UnconfiguredClient:
    def complete(self, messages):
        raise ModelNotConfigured(
            "GMS 호출이 설정되지 않았습니다. 실제 모드는 stub으로 자동 대체하지 않습니다."
        )
