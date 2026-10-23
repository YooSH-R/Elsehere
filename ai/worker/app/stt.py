"""음성 인식 (/v1/stt). 녹음 크기·형식 검사는 라우트(main.py)에서 하고, 여기서는 장면 단어 보정과 무음 거르기를 한다.
명세: docs/api/ai-worker.md"""

import re

from .llm import LLM, Usage

# Whisper 프롬프트: 한영 혼합 예시를 주면 "muffin 하나"의 muffin을 한글로 바꾸지 않는다 (asr_eval 음성으로 확인)
STT_PROMPT = "I want... 아이스 라떼... Can I get a muffin, 음, 하나? The bus stop is over there."
STT_MAX_BYTES = 3 * 1024 * 1024
AUDIO_EXT = {"audio/webm": "webm", "audio/ogg": "ogg", "audio/mp4": "mp4", "audio/wav": "wav", "audio/x-wav": "wav", "audio/mpeg": "mp3"}
NORM = re.compile(r"[^a-z0-9가-힣 ]")


def stt_prompt(scene_words: list[str]) -> str:
    """장면 단어 보정: 지금 장면의 단어(메뉴·상품·거리 이름)를 프롬프트에 붙여 철자를 맞춘다."""
    words = [w.strip() for w in scene_words if w.strip()][:30]
    return STT_PROMPT + (" " + ", ".join(words) + "." if words else "")


def is_prompt_echo(text: str, prompt: str) -> bool:
    """Whisper는 무음에 프롬프트를 되풀이한다. 프롬프트의 긴 조각과 같으면 버린다.
    (짧은 말 'Latte.'는 프롬프트 단어와 같아도 진짜 발화일 수 있어 12자 미만은 거르지 않는다)"""
    t = NORM.sub("", text.lower()).strip()
    return len(t) >= 12 and t in NORM.sub("", prompt.lower())


async def stt(llm: LLM, audio: bytes, mime: str, scene_words: list[str]) -> dict:
    """mime은 AUDIO_EXT에 있는 형식만 받는다 (라우트가 매개변수를 떼고 걸러 415로 돌려줌)."""
    usage = Usage()
    prompt = stt_prompt(scene_words)
    out = await llm.transcribe(audio, f"speech.{AUDIO_EXT[mime]}", mime, prompt, usage)
    text = "" if is_prompt_echo(out["text"], prompt) else out["text"]
    return {"text": text, "latency_s": out["latency_s"], "audio_s": out["audio_s"], "usage": usage.items}
