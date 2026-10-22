"""대화 종료 피드백: ai/feedback 모듈(계약 0.3.0)을 그대로 부르고, 모델 호출만 워커의 구성으로 한다.

입력·출력 형식, 프롬프트, 근거 검증은 그 모듈이 정한다(ai/feedback/README.md). 모듈은 동기 함수(generate_feedback)라
스레드에서 돌리고, 그 안의 모델 호출(complete)은 워커 이벤트 루프의 LLM.chat(feedback 역할: 구성의 모델·재시도·대체 모델)으로 넘긴다.
워커를 배포할 때 ai/feedback 폴더도 함께 둔다.
"""

import asyncio
import sys
from concurrent.futures import ThreadPoolExecutor
from functools import partial
from pathlib import Path

FEEDBACK_DIR = Path(__file__).resolve().parents[2] / "feedback"  # ai/feedback
if str(FEEDBACK_DIR) not in sys.path:  # 모듈이 'from feedback.…' 절대 경로로 불러서 폴더를 경로에 둔다
    sys.path.insert(0, str(FEEDBACK_DIR))

from feedback import generate_feedback
from feedback.clients import ProviderCallError
from feedback.gms import feedback_response_format

from . import services
from .llm import LLM, ProviderError, Usage

MAX_OUTPUT_TOKENS = 6000  # 모듈의 GmsSettings.max_output_tokens와 같게
TEMPERATURE = 1.0  # 모듈의 GMS 호출은 temperature를 보내지 않는다 (= 공급자 기본값 1.0)
# 피드백은 수십 초 걸리는 동기 호출이라 기본 스레드 묶음(Kokoro 합성·로컬 판정기가 씀)을 차지하지 않게 따로 둔다
FEEDBACK_POOL = ThreadPoolExecutor(max_workers=4, thread_name_prefix="feedback")


class WorkerModel:
    """ai/feedback의 ModelClient. 스레드에서 불리고, 호출은 워커 이벤트 루프로 넘긴다."""

    def __init__(self, llm: LLM, loop: asyncio.AbstractEventLoop, usage: Usage):
        self.llm, self.loop, self.usage = llm, loop, usage

    def complete(self, messages: list[dict[str, str]]) -> str:
        # 모듈이 대화마다 만드는 출력 형식(strict json_schema)을 그대로 쓴다
        schema = feedback_response_format(messages)["json_schema"]["schema"]
        # 길게 생성하므로 짧은 호출 상한·같은 요청 더 보내기 대신 피드백 전용 상한
        call = self.llm.chat(
            "feedback",
            "feedback",
            messages,
            self.usage,
            TEMPERATURE,
            MAX_OUTPUT_TOKENS,
            schema,
            timeout_s=self.llm.s.feedback_timeout_s,
            hedge=False,
        )
        # 재시도·대체 모델을 거쳐도 전체를 FEEDBACK_TIMEOUT_S 안에 (호출 한 번의 상한만으로는 최대 약 4배까지 늘어남)
        whole = services.within(call, self.llm.s.feedback_timeout_s)
        try:
            return asyncio.run_coroutine_threadsafe(whole, self.loop).result()
        except ProviderError as e:  # 모듈은 TimeoutError를 AI_TIMEOUT, ProviderCallError를 AI_PROVIDER_ERROR로 돌려준다
            if e.code == "timeout":
                raise TimeoutError(e.message) from e
            raise ProviderCallError(e.code) from e


async def feedback(llm: LLM, payload: dict) -> dict:
    """반환: {"feedback": 모듈 출력(status·result·problem), "usage": [...]}. 입력이 계약과 다르면 pydantic.ValidationError.
    공급자 실패·출력 형식 오류는 모듈이 status: failed로 돌려준다 (HTTP는 200). 사용량 한도 판단은 Spring이 먼저 한다."""
    usage = Usage()
    model = WorkerModel(llm, asyncio.get_running_loop(), usage)
    result = await asyncio.get_running_loop().run_in_executor(FEEDBACK_POOL, partial(generate_feedback, payload, model, ai_allowed=True))
    return {"feedback": result, "usage": usage.items}
