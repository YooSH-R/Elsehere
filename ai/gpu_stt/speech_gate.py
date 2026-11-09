"""말소리 없는 녹음 거르기. server.py가 받아 적기 전에 부른다 (S15P31A305-704).

Whisper large-v3-turbo는 '말소리 없음' 확률(no_speech_prob)을 내지 못해 무음·잡음에도 0.0이고, 그런 녹음에
"I want... I want...", "감사합니다" 같은 문장을 지어낸다. 학습 전 원본도, 프롬프트가 없어도 같다.
같은 계산으로 small은 0.84~0.89, large-v3는 0.80~0.86이 나와 계산 위치가 아니라 turbo 모델의 성질이다.
그래서 Whisper의 판정 대신 faster-whisper에 들어 있는 Silero VAD로 말소리 구간이 하나라도 있는지 본다.
"""

import numpy as np
from faster_whisper.vad import VadOptions, get_speech_timestamps

# Silero 기본 문턱. 0.3~0.7 모두 잡음 21종을 걸렀고 짧은 단어·낭독은 0.3과 0.5에서 같은 수가 통과했다(0.7은 낭독을 더 놓침)
SPEECH = VadOptions(threshold=0.5, speech_pad_ms=0)


def heard_speech(audio: np.ndarray) -> bool:
    """16kHz 모노 float32 녹음에 말소리 구간이 하나라도 있으면 True."""
    return bool(get_speech_timestamps(audio, SPEECH))
