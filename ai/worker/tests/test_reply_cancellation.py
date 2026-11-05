"""연결이 끝나면 더 이상 쓰지 않을 음성 합성을 정리한다."""

import asyncio

from app.config import Settings
from app.npc import reply_events
from app.schemas import ReplyRequest
from app.tts import AudioStore


def test_closing_reply_cancels_all_chunk_synthesis():
    async def scenario():
        started = asyncio.Event()
        synthesis = []

        class Llm:
            async def stream(self, *args, **kwargs):
                yield "Hello there. Another sentence. "
                await asyncio.Event().wait()

        class Tts:
            async def asynth(self, *args):
                synthesis.append(asyncio.current_task())
                started.set()
                await asyncio.Event().wait()

        request = ReplyRequest(npc={"id": "emma", "name": "Emma", "role": "barista", "facts": "", "scene": "cafe"}, text="Hi")
        events = reply_events(Llm(), Tts(), AudioStore(), Settings(token="test"), request)
        try:
            await anext(events)
            await asyncio.wait_for(started.wait(), 1)
            await events.aclose()
            await asyncio.sleep(0)
            assert len(synthesis) == 2
            assert all(task.cancelled() for task in synthesis)
        finally:
            await events.aclose()
            for task in synthesis:
                task.cancel()
            await asyncio.gather(*synthesis, return_exceptions=True)

    asyncio.run(scenario())
