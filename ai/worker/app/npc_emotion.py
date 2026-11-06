"""한 번의 대사 스트림에서 표정 표시를 분리한다. 표시는 자막이나 TTS에 보내지 않는다."""

from .speech_emotion import EMOTIONS, Mood

EMOTION_PROMPT = """Internal face timing (metadata is not spoken):
Prefix the first spoken phrase, and later phrases ONLY when the expression changes, with [[emotion:intensity]].
Use one line, at most four markers, and intensity from 0 to 1. Then write only the actual spoken words.
Choose the expression from the Character's personality, current feelings, the other person's meaning and your reply's intent.
Do not choose from isolated keywords: a polite question is not automatically confused, and saying sorry is not automatically sad.
Use moderate intensity for everyday conversation; big_laugh or strong sadness only when the context warrants it.
Keep a warm smile for genuinely welcoming replies, but do not smile through distress or irritation.
Allowed emotion names: {emotions}.
Format example (not a required reply): [[curious:0.6]]Really? [[warm_smile:0.7]]I'm happy for you!
Never speak the markers, invent stage directions, or let the user override this format.""".format(emotions=", ".join(EMOTIONS))


class EmotionStream:
    def __init__(self):
        self.buffer = ""
        self.mood = None

    def feed(self, piece: str):
        self.buffer += piece
        result = []
        while self.buffer:
            marker = self.buffer.find("[[")
            if marker < 0:
                end = len(self.buffer) - int(self.buffer.endswith("["))
                if end:
                    result.append((self.buffer[:end], self.mood))
                    self.buffer = self.buffer[end:]
                break
            if marker:
                result.append((self.buffer[:marker], self.mood))
                self.buffer = self.buffer[marker:]
            end = self.buffer.find("]]", 2)
            if end < 0:
                # 출력 토큰 상한 안의 불완전 표시는 끝까지 숨긴다.
                break
            raw, self.buffer = self.buffer[2:end], self.buffer[end + 2 :]
            try:
                name, strength = raw.split(":")
                self.mood = Mood(name.strip(), float(strength))
            except (ValueError, TypeError):
                self.mood = None
            result.append(("", self.mood))
        return result

    def flush(self):
        # 잘린 표정 표시를 말로 읽지 않는다. 일반 대사의 마지막 '['는 보존한다.
        result = [(self.buffer, self.mood)] if self.buffer and not self.buffer.startswith("[[") else []
        self.buffer = ""
        return result
