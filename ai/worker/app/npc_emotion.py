"""한 번의 대사 스트림에서 표정 표시를 분리한다. 표시는 자막이나 TTS에 보내지 않는다."""

from .speech_emotion import EMOTIONS, Mood


def response_mood(reaction: str | None, proposed: Mood | None) -> Mood | None:
    """서버가 확정한 무례 단계는 모델이 웃는 표정을 골라도 낮추지 않는다."""
    forced = {"rude_warning": "frown", "rude_repeated": "angry", "rude_final": "furious"}.get(reaction)
    if forced:
        return Mood(forced, 1)
    if reaction == "apology_after_rude":
        return Mood("relieved", 1)
    if reaction == "compliment":
        if proposed and proposed.emotion in ("shy", "laugh", "big_laugh"):
            return proposed
        return Mood("laugh", 1)
    return proposed


CHEERFUL = {"warm_smile", "laugh", "big_laugh", "playful", "shy"}
COLD = {"frown", "angry", "furious", "indifferent"}
SCOWL = {"frown", "angry", "furious"}  # 호감이 높은 단계의 기본 표정에서 막는 찌푸린 얼굴(무표정은 성격일 수 있어 둔다)
ANNOYED_FACE = {"strong": "frown", "mild": "indifferent"}
DISTANT_STAGES, FRIENDLY_STAGES = {"hostile", "cold"}, {"warm", "close"}
DEFAULT_INTENSITY = 0.5


def mood_face(feeling: dict, proposed: Mood | None, stage: str = "neutral") -> Mood | None:
    """서버가 반응(reaction)을 정하지 않은 턴에, 이번 대화의 기분(app.npc_relationship.mood_state)과 호감 단계에 반대인 표정을 막는다.
    - 기분이 먼저: 짜증이 남은 동안 웃지 않고(강하면 frown, 약하면 indifferent), 기분 좋은 동안 찌푸리지 않는다(warm_smile)
    - 기분이 없으면 호감 단계의 기본 표정: 적대·냉담은 웃지 않고(indifferent), 호의·친밀은 찌푸리지 않는다(warm_smile). 보통은 그대로
    분노를 강제하지 않고, 어울리는 다른 표정(concerned·curious 등)은 그대로 둔다. 표시가 없으면 Unity가 기본 기분(인자한 미소)으로
    재생하므로, 웃으면 안 될 때는 차가운 얼굴을 정해 준다."""
    intensity = proposed.intensity if proposed else DEFAULT_INTENSITY
    no_smile = proposed is None or proposed.emotion in CHEERFUL
    if feeling["annoyed"]:
        return Mood(ANNOYED_FACE[feeling["annoyed"]], intensity) if no_smile else proposed
    if feeling["pleased"]:
        return Mood("warm_smile", intensity) if proposed is not None and proposed.emotion in COLD else proposed
    if stage in DISTANT_STAGES and no_smile:
        return Mood("indifferent", intensity)
    if stage in FRIENDLY_STAGES and proposed is not None and proposed.emotion in SCOWL:
        return Mood("warm_smile", intensity)
    return proposed


def reaction_instruction(reaction: str | None) -> str:
    return {
        "rude_warning": "The server confirmed the first rude turn. Show displeasure with frown and set a calm, firm boundary.",
        "rude_repeated": "The server confirmed the SECOND rude turn. Show clear anger, not a polite smile or teasing. Be firm without insulting the learner.",
        "rude_final": "The server confirmed the THIRD rude turn. Show strong anger and follow the supplied ending instruction. Never threaten or insult the learner.",
        "compliment": "The server confirmed a genuine compliment, not a routine greeting. Show delighted appreciation using the existing laugh face, or shy for a personal compliment that makes you bashful. Reserve big_laugh for genuinely hilarious or exuberant moments. These are face cues, not instructions to add spoken laughter. Do not use the same routine warm_smile for every compliment.",
        "apology_after_rude": "The server confirmed an apology after rudeness. Accept it with guarded relief; the face relaxes but does not jump to a broad smile or laughter. Follow the existing conversation rules.",
    }.get(reaction, "")


EMOTION_PROMPT = """Internal face timing (metadata is not spoken):
Prefix the first spoken phrase, and later phrases ONLY when the expression changes, with [[emotion:intensity]].
Use one line, at most four markers, and intensity from 0 to 1. Then write only the actual spoken words.
Choose the expression from the Character's personality, current feelings, the other person's meaning and your reply's intent.
If "How you feel right now" gives a Face, start with that face and keep later markers in that mood unless something new changes it.
Do not choose from isolated keywords: a polite question is not automatically confused, and saying sorry is not automatically sad.
Use moderate intensity for everyday conversation; big_laugh or strong sadness only when the context warrants it.
Keep a warm smile for genuinely welcoming replies, but do not smile through distress or irritation.
Use laugh for visible joy, shy for bashful personal appreciation, and relieved for tension easing; preserve warm_smile for ordinary warmth.
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
