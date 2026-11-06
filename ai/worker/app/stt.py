"""음성 인식 (/v1/stt). 녹음 크기·형식 검사는 라우트(main.py)에서 하고, 여기서는 장면 단어 보정과 무음 거르기를 한다.
명세: docs/api/ai-worker.md"""

import re

from .llm import LLM, Usage

# Whisper 프롬프트: 한영 혼합 예시를 주면 "muffin 하나"의 muffin을 한글로 바꾸지 않는다 (asr_eval 음성으로 확인).
# 메뉴 예시는 영어로 쓴다. 한글 '아이스 라떼'는 한국식 발음 'Iced americano'를 '아이스트 아메라카노'로 끌어당겼다 (S15P31A305-397)
# 한국어 예시가 '음, 하나'뿐이면 짧은 한국어 문장을 영어 문장으로 번역해 적어서, 메뉴가 아닌 한국어 말을 앞뒤에 둔다 (S15P31A305-556)
# 끝 문장 'The bus stop is over there.'는 한국식 발음 both를 bus로 끌어당겼다. 끝 영어 문장을 빼면 영어를 한글로 적는 일이 늘어서,
# 짧고 헷갈리는 단어가 없는 영어 문장으로 바꾼다 (S15P31A305-639)
STT_PROMPT = "음, 그러니까... I want... an iced latte... Can I get a muffin, 음, 하나? 잠깐만요. My friend lives over there."
STT_MAX_BYTES = 3 * 1024 * 1024
AUDIO_EXT = {"audio/webm": "webm", "audio/ogg": "ogg", "audio/mp4": "mp4", "audio/wav": "wav", "audio/x-wav": "wav", "audio/mpeg": "mp3"}
NORM = re.compile(r"[^a-z0-9가-힣 ]")


def stt_prompt(scene_words: list[str]) -> str:
    """장면 단어 보정: 지금 장면의 단어(메뉴·상품·거리 이름)를 프롬프트에 붙여 철자를 맞춘다."""
    words = [w.strip() for w in scene_words if w.strip()][:30]
    return STT_PROMPT + (" " + ", ".join(words) + "." if words else "")


def normalize(text: str) -> str:
    return " ".join(NORM.sub("", text.lower()).split())


def is_prompt_echo(text: str, scene_words: list[str]) -> bool:
    """Whisper는 무음에 프롬프트 끝부분(마지막 문장, 장면 단어 목록, 프롬프트 전체)을 되풀이한다. 그 끝부분과 같으면 버린다.
    프롬프트 앞·중간의 예시 문장('Can I get a muffin?')과 장면 단어 하나('Iced Americano.')는 학습자가 실제로 하는 말이라 거르지 않는다.
    짧은 말 'Latte.'도 진짜 발화일 수 있어 12자 미만은 거르지 않는다.
    끝부분을 여러 번 이어 붙이기도 해서('My friend lives over there. My friend lives over there.') 같은 메아리의 반복도 버린다."""
    words = normalize(text).split()
    for times in range(1, len(words) + 1):
        once = words[: len(words) // times]
        if once * times == words and is_one_echo(" ".join(once), scene_words):
            return True
    return False


def is_one_echo(t: str, scene_words: list[str]) -> bool:
    if len(t) < 12 or t in {normalize(w) for w in scene_words}:
        return False
    endings = (normalize(STT_PROMPT), normalize(stt_prompt(scene_words)))
    return any(p == t or p.endswith(" " + t) for p in endings)


async def stt(llm: LLM, audio: bytes, mime: str, scene_words: list[str]) -> dict:
    """mime은 AUDIO_EXT에 있는 형식만 받는다 (라우트가 매개변수를 떼고 걸러 415로 돌려줌)."""
    usage = Usage()
    out = await llm.transcribe(audio, f"speech.{AUDIO_EXT[mime]}", mime, stt_prompt(scene_words), usage)
    text = "" if is_prompt_echo(out["text"], scene_words) else out["text"]
    return {"text": text, "latency_s": out["latency_s"], "audio_s": out["audio_s"], "usage": usage.items}
