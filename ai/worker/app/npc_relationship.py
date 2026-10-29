"""사용자 × NPC 관계(알아봄·호감)를 공통 규칙(ai/npcs/rules/world-rules.json)으로 계산한다. 설계는 docs/npc-life-design.md 4절.

계산은 코드, 표현은 LLM이 맡는다. AI 워커는 관계를 저장하지 않는다: 요청에 지금 관계가 들어오고 응답에 변화량이 나가며,
백엔드가 저장값에 더한다. 날짜는 모두 게임 속 날짜(마지막 대화 뒤 지난 날 수)다.
- 알아봄(0~100): 얼굴을 아는 정도. 정상 종료된 대화마다 갈수록 덜 오르고, 며칠 여유 뒤 하루씩 준다
- 호감(−100~100): 좋게 보는 정도. 무례는 발화마다 바로 깎고(/v1/guard), 나머지는 이번 대화의 인상(사건 합)을 조금만 따라간다
"""

import json
from functools import cache

from .npc_persona import RULES, load_behaviors
from .npc_profile_models import NpcProfile

RECOGNITION_MAX = 100
AFFINITY_MAX = 100  # −100~100


def load_world_rules() -> dict:
    return json.loads((RULES / "world-rules.json").read_text(encoding="utf-8"))


# ---------- 성격 → 배수 ----------
def derive_rates(dimensions: dict[str, float], rules: dict) -> dict:
    """성격 5축에 공통 연결 규칙(rules["derivation"])을 적용해 생활 값 배수를 계산한다.
    결과는 행동 조각 affects와 같은 모양이다 (예: {"needRates": {"SOCIAL": 1.2}, "recognitionRate": 1.2})."""
    rates: dict = {}
    for rule in rules["derivation"]:
        value = round(rule["at0"] + (rule["at1"] - rule["at0"]) * dimensions[rule["axis"]], 2)
        group, _, key = rule["target"].partition(".")
        if key:
            rates.setdefault(group, {})[key] = value
        else:
            rates[group] = value
    return rates


MAX_SALIENCE_BONUS = 2  # 기억 세기는 1~3


def combine_rates(*parts: dict) -> dict:
    """성격에서 계산한 배수와 행동 조각 affects들을 하나로 합친다. 배수는 곱하고, 기억 세기 보너스(salienceBonus)만 더한다."""
    combined: dict = {}
    for part in parts:
        for key, value in part.items():
            if key == "salienceBonus":
                bonuses = combined.setdefault(key, {})
                for event, bonus in value.items():
                    bonuses[event] = min(bonuses.get(event, 0) + bonus, MAX_SALIENCE_BONUS)
            elif isinstance(value, dict):
                scales = combined.setdefault(key, {})
                for name, scale in value.items():
                    scales[name] = round(scales.get(name, 1) * scale, 2)
            else:
                combined[key] = round(combined.get(key, 1) * value, 2)
    return combined


def npc_rates(profile: NpcProfile | None, rules: dict, library: dict) -> dict:
    """대화 요청에 실려 오는 인격 카드에서 배수(알아보는 속도, 사건 반응 크기 등)를 계산한다.
    카드의 성격 5축과, 카드 행동 규칙에 들어 있는 행동 조각(app.npc_persona가 넣은 문장)을 쓴다. 생활 파일로 계산한 값과 같다.
    카드가 없거나 성격 5축이 비어 있으면 기본 배수(빈 dict, 모두 1)다."""
    if profile is None or profile.personality.dimensions is None:
        return {}
    dimensions = profile.personality.dimensions.model_dump()
    pieces = [behavior for behavior in library["behaviors"] if behavior["text"] in profile.behavior_rules]
    return combine_rates(derive_rates(dimensions, rules), *(piece.get("affects", {}) for piece in pieces))


# ---------- 관계 ----------
def recognition_today(recognition: float, days_since_last_talk: int, rules: dict) -> float:
    """마지막 대화 뒤 graceDays까지는 그대로, 그 뒤로는 하루에 recognitionDropPerDay씩 준다."""
    relationship = rules["relationship"]
    faded_days = max(0, days_since_last_talk - relationship["graceDays"])
    return max(0, recognition - faded_days * relationship["recognitionDropPerDay"])


def grow_recognition(recognition: float, rate: float, rules: dict) -> float:
    """정상 종료된 대화 한 번. 기본값 × (1 − 현재/100) × NPC 배수만큼 올라 갈수록 덜 오른다."""
    step = rules["relationship"]["recognitionPerConversation"] * (1 - recognition / RECOGNITION_MAX) * rate
    return min(RECOGNITION_MAX, recognition + step)


def impression(events: list[str], rates: dict, rules: dict) -> float:
    """이번 대화의 인상. 판정된 사건을 대화에서 한 번씩 세고 NPC의 사건 반응 크기(rates["eventScale"])를 곱한다.
    무례는 발화마다 이미 깎았으므로(rude_affinity_delta) 넣지 않고, 공통 규칙에 없는 사건은 버린다."""
    effects, scales = rules["conversationEvents"], rates.get("eventScale", {})
    counted = {event for event in events if event in effects and event != "RUDE"}
    return sum(effects[event].get("impression", 0) * scales.get(event, 1) for event in counted)


def rude_affinity_delta(rates: dict, rules: dict) -> int:
    """무례한 말 한 번에 바로 깎는 호감(/v1/guard). 백엔드가 무례를 저장하면서 함께 더한다(하한 −100은 백엔드가 지킨다).
    대화 끝에 인상을 따라가던 몫과 같은 크기다: 무례 인상 × affinityFollowRate × NPC 반응 크기(rates["eventScale"]).
    지금 호감을 모르고 계산하므로 끝(−100)에 가까울수록 덜 깎는 완충은 없다."""
    scale = rates.get("eventScale", {}).get("RUDE", 1)
    return round(rules["conversationEvents"]["RUDE"]["impression"] * rules["relationship"]["affinityFollowRate"] * scale)


def follow_affinity(affinity: float, impression: float, rules: dict) -> float:
    """호감은 인상의 affinityFollowRate만큼만 따라간다. 끝(±100)에 가까울수록 그쪽으로는 덜 움직인다
    (좋아하는 사람이 칭찬해도 조금 오르고, 좋아하던 사람에게 나쁜 인상을 받으면 그대로 깎인다)."""
    change = impression * rules["relationship"]["affinityFollowRate"]
    toward_end = affinity if change > 0 else -affinity
    change *= 1 - max(0, toward_end) / AFFINITY_MAX
    return max(-AFFINITY_MAX, min(AFFINITY_MAX, affinity + change))


MOOD_MAX = 3  # 기분 세기 0~3


LearnerTurn = tuple[bool, list[str]]  # 이력 속 학습자 줄 하나: (Spring이 확정한 무례인지, 의도 판정이 고른 사건)


def turn_events(turn: LearnerTurn) -> list[str]:
    """그 줄에서 센 사건. 무례로 확정된 줄은 RUDE만 세고 다른 사건(욕하면서 칭찬 등)은 버린다."""
    rude, events = turn
    return ["RUDE"] if rude else list(dict.fromkeys(event for event in events if event != "RUDE"))


def moods(learner_turns: list[LearnerTurn], rates: dict, rules: dict) -> dict[str, float]:
    """이번 대화에서 지금 기분 세기(감정마다 0~3). 학습자가 말할 때마다(지금 말 포함) 모든 기분이 decayPerTurn씩 가라앉고,
    그 줄 사건의 기분 효과(conversationEvents의 mood) × NPC 반응 크기(rates["eventScale"])가 더해진다. 사과는 짜증을 줄인다."""
    effects, scales, decay = rules["conversationEvents"], rates.get("eventScale", {}), rules["mood"]["decayPerTurn"]
    feelings: dict[str, float] = {}
    for turn in learner_turns:
        feelings = {emotion: max(0, value - decay) for emotion, value in feelings.items()}
        for event in turn_events(turn):
            for emotion, amount in effects.get(event, {}).get("mood", {}).items():
                feelings[emotion] = min(MOOD_MAX, max(0, feelings.get(emotion, 0) + amount * scales.get(event, 1)))
    return {emotion: max(0, value - decay) for emotion, value in feelings.items()}  # 지금 학습자가 한 말만큼 가라앉음


def person_state(relationship: dict, learner_turns: list[LearnerTurn], profile: NpcProfile | None, rules: dict, library: dict) -> dict:
    """대답 지시문에 말로 풀어 넣을 지금 상태. 숫자는 지시문에 넣지 않는다.
    - known: 얼마나 알아보는지 new(처음 봄)·familiar(본 적 있음)·regular(단골). 오래 안 온 날만큼 줄어든 알아봄으로 정한다
    - feeling: liked·disliked·None(티 나지 않음)
    - personal: 자기 이야기를 조금 할 만큼 가까운지
    - annoyed·pleased: 이번 대화의 사건으로 짜증 나 있는지·기분이 좋은지. 둘 다면 더 큰 쪽만 (반대인 말투 지시를 함께 주지 않음)"""
    thresholds = rules["relationship"]
    recognition = recognition_today(relationship.get("recognition", 0), relationship.get("days_since_last_talk", 0), rules)
    affinity = relationship.get("affinity", 0)
    known = "regular" if recognition >= thresholds["regular"] else "familiar" if recognition >= thresholds["seenBefore"] else "new"
    feeling = "liked" if affinity >= thresholds["liked"] else "disliked" if affinity <= thresholds["disliked"] else None
    personal = thresholds["personalTalk"]
    now = moods(learner_turns, npc_rates(profile, rules, library), rules)
    annoyed, pleased = now.get("ANNOYED", 0), now.get("PLEASED", 0)
    shows = rules["mood"]["showsAt"]
    return {
        "known": known,
        "feeling": feeling,
        "personal": recognition >= personal["recognition"] and affinity >= personal["affinity"],
        "annoyed": annoyed >= shows and annoyed >= pleased,
        "pleased": pleased >= shows and pleased > annoyed,
    }


@cache
def service_rules() -> tuple[dict, dict]:
    """대답 경로에서 쓰는 공통 규칙과 행동 조각 모음. 요청마다 파일을 읽지 않도록 한 번만 읽는다(바꾸지 말고 읽기만)."""
    return load_world_rules(), load_behaviors()


def after_conversation(relationship: dict | None, events: list[str], rates: dict, rules: dict) -> dict:
    """정상 종료된 대화 하나로 바뀌는 관계의 변화량. 백엔드는 저장값에 더한다(종료 처리가 겹쳐도 앞 대화 결과를 덮어쓰지 않게).
    새 값을 정수로 반올림한 뒤 저장값을 빼므로, 더한 결과는 새 값을 그대로 저장할 때와 같다.
    relationship: 저장된 관계와 마지막 대화 뒤 지난 게임 날 수 {recognition, affinity, days_since_last_talk}. 처음 만나면 None."""
    before = relationship or {}
    stored_recognition, stored_affinity = before.get("recognition", 0), before.get("affinity", 0)
    recognition = recognition_today(stored_recognition, before.get("days_since_last_talk", 0), rules)
    new_recognition = round(grow_recognition(recognition, rates.get("recognitionRate", 1), rules))
    new_affinity = round(follow_affinity(stored_affinity, impression(events, rates, rules), rules))
    return {"recognition_delta": new_recognition - stored_recognition, "affinity_delta": new_affinity - stored_affinity}
