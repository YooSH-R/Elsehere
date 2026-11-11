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
# Whisper는 프롬프트 끝을 가장 많이 따른다. 영어 장면 단어·NPC 대사로 끝나면 한국어로 한 말('왜 뭐 불러주세요')을
# 영어 문장('Why? What do you say?')으로 바꿔 적어서, 맨 끝에 짧은 한국어 말을 둔다 (S15P31A305-787)
KOREAN_ENDING = "음, 잠깐만요."
STT_MAX_BYTES = 3 * 1024 * 1024
AUDIO_EXT = {"audio/webm": "webm", "audio/ogg": "ogg", "audio/mp4": "mp4", "audio/wav": "wav", "audio/x-wav": "wav", "audio/mpeg": "mp3"}
NORM = re.compile(r"[^a-z0-9가-힣 ]")


def stt_prompt(scene_words: list[str], npc_line: str = "") -> str:
    """장면 단어 보정: 지금 장면의 단어(메뉴·상품·거리 이름)를 프롬프트에 붙여 철자를 맞춘다.
    직전 NPC 대사는 그 뒤에 둔다. 소리가 비슷한 말(한국식 발음 '러닝' → running/learning)을 그 대사의 문맥에 맞춰 고르게 한다.
    맨 끝은 한국어 말(KOREAN_ENDING)이다."""
    return " ".join(part for part in (prompt_without_ending(scene_words, npc_line), KOREAN_ENDING) if part)


def prompt_without_ending(scene_words: list[str], npc_line: str = "") -> str:
    return " ".join(part for part in (STT_PROMPT, scene_word_list(scene_words), npc_line.strip()) if part)


def scene_word_list(scene_words: list[str]) -> str:
    # 40개면 기본 프롬프트·직전 NPC 대사와 합쳐도 Whisper 프롬프트 한도(224토큰, 넘으면 앞의 한영 혼합 예시부터 잘림) 안에 든다.
    # 카페(닉네임·NPC 이름 셋·마을 13·메뉴 14 = 31개)에 긴 NPC 대사를 붙여도 약 165토큰 (S15P31A305-747)
    words = [w.strip() for w in scene_words if w.strip()][:40]
    return ", ".join(words) + "." if words else ""


def normalize(text: str) -> str:
    return " ".join(NORM.sub("", text.lower()).split())


def is_prompt_echo(text: str, scene_words: list[str], npc_line: str = "") -> bool:
    """Whisper는 무음에 프롬프트 끝부분(마지막 문장, 장면 단어 목록, 직전 NPC 대사, 프롬프트 전체)을 되풀이한다. 그 끝부분과 같으면 버린다.
    프롬프트 앞·중간의 예시 문장('Can I get a muffin?')과 장면 단어 하나('Iced Americano.')는 학습자가 실제로 하는 말이라 거르지 않는다.
    짧은 말 'Latte.'도 진짜 발화일 수 있어 12자 미만은 거르지 않는다. 다만 맨 끝의 한국어 말('음, 잠깐만요.')은 짧아도 메아리로 본다.
    끝부분을 여러 번 이어 붙이기도 해서('My friend lives over there. My friend lives over there.') 같은 메아리의 반복도 버린다.
    학습자가 NPC 대사의 끝 문장을 그대로 따라 말해도 메아리와 구별할 수 없어 버린다."""
    words = normalize(text).split()
    endings = prompt_endings(scene_words, npc_line)
    singles = {normalize(w) for w in scene_words}
    for times in range(1, len(words) + 1):
        once = " ".join(words[: len(words) // times])
        long_enough = len(once) >= 12 or once.endswith(normalize(KOREAN_ENDING))
        if once.split() * times == words and long_enough and once not in singles and once in endings:
            return True
    return False


def prompt_endings(scene_words: list[str], npc_line: str) -> set[str]:
    """프롬프트 끝부분들. 문장 경계에서만 자른다("lives over there"처럼 문장 안에서 자른 끝은 실제 발화일 수 있다).
    장면 단어나 NPC 대사를 붙이기 전의 끝(기본 프롬프트의 마지막 문장 등)도 메아리로 나와서 넣는다.
    맨 끝의 한국어 말을 뺀 끝('I'm in no rush.')도 메아리로 나온다."""
    endings = set()
    prompts = [STT_PROMPT, prompt_without_ending(scene_words), prompt_without_ending(scene_words, npc_line)]
    prompts += [f"{prompt} {KOREAN_ENDING}" for prompt in prompts]
    for prompt in prompts:
        sentences = [part.strip() for part in re.split(r"[.!?]+", prompt) if part.strip()]
        endings.update(normalize(" ".join(sentences[i:])) for i in range(len(sentences)))
    return endings


async def stt(llm: LLM, audio: bytes, mime: str, scene_words: list[str], npc_line: str = "") -> dict:
    """mime은 AUDIO_EXT에 있는 형식만 받는다 (라우트가 매개변수를 떼고 걸러 415로 돌려줌)."""
    usage = Usage()
    out = await llm.transcribe(audio, f"speech.{AUDIO_EXT[mime]}", mime, stt_prompt(scene_words, npc_line), usage)
    text = "" if is_prompt_echo(out["text"], scene_words, npc_line) else out["text"]
    return {"text": text, "latency_s": out["latency_s"], "audio_s": out["audio_s"], "usage": usage.items}
