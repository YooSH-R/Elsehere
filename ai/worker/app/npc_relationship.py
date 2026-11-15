"""사용자 × NPC 관계(알아봄·호감)를 공통 규칙(ai/npcs/rules/world-rules.json)으로 계산한다. 설계는 docs/npc-life-design.md 4절.

계산은 코드, 표현은 LLM이 맡는다. AI 워커는 관계를 저장하지 않는다: 요청에 지금 관계가 들어오고 응답에 변화량이 나가며,
백엔드가 저장값에 더한다. 날짜는 모두 게임 속 날짜(마지막 대화 뒤 지난 날 수)다.
- 알아봄(0~100): 얼굴을 아는 정도. 정상 종료된 대화마다 갈수록 덜 오르고, 며칠 여유 뒤 하루씩 준다
- 호감(−100~100): 좋게 보는 정도. 대화 중 발화마다 바로 바뀐다: 무례는 /v1/guard가 깎고, 칭찬·인사 같은 사건은 /v1/judge가 올린다.
  대화가 끝난 뒤 이번 대화가 좋았으면 /v1/memory가 한 번 더 올린다
"""

import json
from functools import cache

from .npc_persona import RULES, load_behaviors
from .npc_profile_models import NpcProfile

RECOGNITION_MAX = 100


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
    """이번 대화에서 판정된 사건들의 인상. 같은 사건은 한 번씩 세고 NPC의 사건 반응 크기(rates["eventScale"])를 곱한다.
    무례는 따로 깎으므로(rude_affinity_delta) 넣지 않고, 공통 규칙에 없는 사건은 버린다."""
    effects, scales = rules["conversationEvents"], rates.get("eventScale", {})
    counted = {event for event in events if event in effects and event != "RUDE"}
    return sum(effects[event].get("impression", 0) * scales.get(event, 1) for event in counted)


def rude_affinity_delta(rates: dict, rules: dict) -> int:
    """무례한 말 한 번에 바로 깎는 호감(/v1/guard). 백엔드가 무례를 저장하면서 함께 더한다(하한 −100은 백엔드가 지킨다).
    다른 사건이 바로 올리는 호감(line_affinity_delta)과 같은 계산이다: 무례 인상 × affinityFollowRate × NPC 반응 크기(rates["eventScale"]).
    지금 호감을 모르고 계산하므로 끝(−100)에 가까울수록 덜 깎는 완충은 없다."""
    scale = rates.get("eventScale", {}).get("RUDE", 1)
    return round(rules["conversationEvents"]["RUDE"]["impression"] * rules["relationship"]["affinityFollowRate"] * scale)


def line_affinity_delta(earlier_events: list[str], events: list[str], rates: dict, rules: dict) -> int:
    """학습자 줄 하나의 사건으로 바로 올리는 호감(/v1/judge). 백엔드가 무례가 아닌 턴을 저장하면서 더한다(상한 100은 백엔드가 지킨다).
    사건 인상 × affinityFollowRate × NPC 반응 크기이고, 이번 대화의 앞선 줄에서 센 사건(earlier_events)은 다시 세지 않는다.
    앞선 줄까지와 이 줄까지의 몫을 각각 반올림해 차이를 돌려주므로, 한 대화에서 더한 합은 대화 전체 인상을 한 번에 반올림한 값과 같다.
    무례 감점처럼 지금 호감을 모르고 계산하므로 끝(100)에 가까울수록 덜 오르는 완충은 없다."""
    follow = rules["relationship"]["affinityFollowRate"]
    before = impression(earlier_events, rates, rules)
    after = impression([*earlier_events, *events], rates, rules)
    return round(after * follow) - round(before * follow)


def good_conversation_affinity_delta(impression: str, learner_turns: list["LearnerTurn"], rules: dict) -> int:
    """대화가 끝난 뒤 이번 대화의 인상(/v1/memory 모델이 고름)으로 올리는 호감. 좋았고(good) 학습자가 충분히 말했으며
    무례로 확정된 줄이 없을 때만 공통 규칙의 값을 준다. 나쁜 대화도 깎지 않는다(무례는 대화 중에 이미 깎음).
    백엔드가 기억 카드를 저장하면서 함께 더한다(상한 100은 백엔드가 지킨다)."""
    relationship = rules["relationship"]
    long_enough = len(learner_turns) >= relationship["goodConversationMinLearnerLines"]
    polite = not any(rude for rude, _ in learner_turns)
    return relationship["goodConversationAffinity"] if impression == "good" and long_enough and polite else 0


MOOD_MAX = 3  # 기분 세기 0~3


LearnerTurn = tuple[bool, list[str]]  # 이력 속 학습자 줄 하나: (Spring이 확정한 무례인지, 의도 판정이 고른 사건)


def turn_events(turn: LearnerTurn) -> list[str]:
    """그 줄에서 센 사건. 무례로 확정된 줄은 RUDE만 세고 다른 사건(욕하면서 칭찬 등)은 버린다."""
    rude, events = turn
    return ["RUDE"] if rude else list(dict.fromkeys(event for event in events if event != "RUDE"))


def moods(learner_turns: list[LearnerTurn], rates: dict, rules: dict) -> dict[str, float]:
    """이번 대화에서 지금 기분 세기(감정마다 0~3). 학습자가 말할 때마다(지금 말 포함) 기분이 decayPerTurn × NPC의 그 감정
    가라앉는 속도(rates["moodDecay"], 짜증은 신경성으로 정함)씩 가라앉고, 그 줄 사건의 기분 효과(conversationEvents의 mood)
    × NPC 반응 크기(rates["eventScale"])가 더해진다. 사과는 짜증을 줄인다."""
    effects, scales = rules["conversationEvents"], rates.get("eventScale", {})
    speeds, decay = rates.get("moodDecay", {}), rules["mood"]["decayPerTurn"]

    def settle(feelings: dict[str, float]) -> dict[str, float]:
        return {emotion: max(0, value - decay * speeds.get(emotion, 1)) for emotion, value in feelings.items()}

    feelings: dict[str, float] = {}
    for turn in learner_turns:
        feelings = settle(feelings)
        for event in turn_events(turn):
            for emotion, amount in effects.get(event, {}).get("mood", {}).items():
                feelings[emotion] = min(MOOD_MAX, max(0, feelings.get(emotion, 0) + amount * scales.get(event, 1)))
    return settle(feelings)  # 지금 학습자가 한 말만큼 가라앉음


def latest_cause(learner_turns: list[LearnerTurn], emotion: str, rules: dict) -> str | None:
    """그 기분을 가장 최근에 올린 사건. 말투가 이유에 맞게 나오도록 지시문에 함께 넣는다(칭찬받아서·도와준다고 해서 등)."""
    effects = rules["conversationEvents"]
    raised = [event for turn in learner_turns for event in turn_events(turn) if effects.get(event, {}).get("mood", {}).get(emotion, 0) > 0]
    return raised[-1] if raised else None


def person_state(relationship: dict, profile: NpcProfile | None, rules: dict, library: dict) -> dict:
    """대답 지시문에 말로 풀어 넣을 이 사람과의 관계. 숫자는 지시문에 넣지 않는다. 이번 대화의 기분은 mood_state가 따로 정한다.
    - known: 얼마나 알아보는지 new(처음 봄)·familiar(본 적 있음)·regular(단골). 오래 안 온 날만큼 줄어든 알아봄으로 정한다
    - stage: 호감 단계 hostile(적대)·cold(냉담)·neutral(보통)·warm(호의)·close(친밀). 경계는 relationship.affinityStages
    - personal: 자기 이야기를 조금 할 만큼 가까운지. 기준(알아봄·호감)에 NPC의 personalTalkScale을 곱한다(거리를 두는 성격은 늦게)"""
    thresholds = rules["relationship"]
    rates = npc_rates(profile, rules, library)
    recognition = recognition_today(relationship.get("recognition", 0), relationship.get("days_since_last_talk", 0), rules)
    affinity = relationship.get("affinity", 0)
    known = "regular" if recognition >= thresholds["regular"] else "familiar" if recognition >= thresholds["seenBefore"] else "new"
    personal, personal_scale = thresholds["personalTalk"], rates.get("personalTalkScale", 1)
    return {
        "known": known,
        "stage": affinity_stage(affinity, known != "new", rules),
        "personal": recognition >= personal["recognition"] * personal_scale and affinity >= personal["affinity"] * personal_scale,
    }


def affinity_stage(affinity: int, recognized: bool, rules: dict) -> str:
    """호감 단계. 친밀은 얼굴을 알아볼 때만이고, 처음 보거나 오래 안 와 잊은 얼굴이면 호의로 대한다(모르는 얼굴에게 친구처럼 굴면 어색함)."""
    stages = rules["relationship"]["affinityStages"]
    if affinity <= stages["hostile"]:
        return "hostile"
    if affinity <= stages["cold"]:
        return "cold"
    if affinity >= stages["close"] and recognized:
        return "close"
    return "warm" if affinity >= stages["warm"] else "neutral"


def reply_stage(relationship: dict | None, profile: NpcProfile | None) -> str:
    """대답 경로의 호감 단계. 관계가 없으면(처음 만남, 연습·복습 대화) 보통. 표정 제한(app.npc_emotion.mood_face)에 쓴다."""
    if relationship is None:
        return "neutral"
    return person_state(relationship, profile, *service_rules())["stage"]


WARM_NEEDS = {"SOCIAL"}  # 낮으면 말 상대가 반가운 욕구. 짜증 난 동안에는 반대인 말투라 넣지 않는다


def low_needs(needs: dict[str, float], rules: dict) -> list[tuple[str, bool]]:
    """대화를 시작할 때 기준(mood.needs.low)보다 낮은 욕구를 (욕구, 아주 낮은지)로. 가장 낮은 욕구 하나만 고르고,
    그 욕구가 아주 낮으면(veryLow 미만) 다음으로 낮은 욕구까지 두 개를 고른다. 지시가 많아지면 작은 모델이 하나도 제대로 따르지 않는다."""
    limits = rules["mood"]["needs"]
    lows = sorted((value, need) for need, value in needs.items() if value < limits["low"])
    picked = lows[:2] if lows and lows[0][0] < limits["veryLow"] else lows[:1]
    return [(need, value < limits["veryLow"]) for value, need in picked]


def mood_state(
    learner_turns: list[LearnerTurn], needs: dict[str, float] | None, profile: NpcProfile | None, rules: dict, library: dict
) -> dict:
    """대답 지시문에 말로 풀어 넣을 이번 대화의 기분. 관계(알아봄·호감)와 상관없어 처음 만나도, 연습·복습 대화에서도 정한다.
    - 바탕 기분: 대화를 시작할 때의 욕구(needs, 대화 중에는 게임 시간이 멈춰 그대로)로 정한다. 욕구가 오지 않으면 없다
      - low_needs: 기준보다 낮은 욕구 [(욕구, 아주 낮은지)] (low_needs 참고)
      - content: 보낸 욕구가 모두 넉넉해 여유로운지
    - 사건 기분: 이번 대화의 사건(무례·칭찬 등)으로 짜증 나 있는지(annoyed)·기분이 좋은지(pleased). 드러나지 않으면 None,
      드러나면 세기에 따라 mild·strong이다. 둘 다면 더 큰 쪽만. pleased_by는 기분 좋음을 가장 최근에 올린 사건
    서로 반대인 말투 지시를 함께 주지 않도록, 짜증 난 동안에는 여유로움과 말 상대가 반가움을 빼고 기분이 좋으면 여유로움을 겹쳐 넣지 않는다."""
    now = moods(learner_turns, npc_rates(profile, rules, library), rules)
    annoyed_level, pleased_level = now.get("ANNOYED", 0), now.get("PLEASED", 0)
    annoyed = mood_strength(annoyed_level, rules) if annoyed_level >= pleased_level else None
    pleased = mood_strength(pleased_level, rules) if pleased_level > annoyed_level else None

    needs = needs or {}
    lows = low_needs(needs, rules)
    content = bool(needs) and all(value >= rules["mood"]["needs"]["comfortable"] for value in needs.values())
    if annoyed:
        lows = [(need, strong) for need, strong in lows if need not in WARM_NEEDS]
    return {
        "low_needs": lows,
        "content": content and not annoyed and not pleased,
        "annoyed": annoyed,
        "pleased": pleased,
        "pleased_by": latest_cause(learner_turns, "PLEASED", rules) if pleased else None,
    }


def reply_mood(history: list, needs: dict[str, float] | None, profile: NpcProfile | None) -> dict:
    """대답 경로(/v1/npc/reply)의 이번 대화 기분. 지시문(app.prompts)과 표정 제한(app.npc_emotion.mood_face)이 같은 값을 쓴다.
    history는 요청의 대화 줄(app.schemas.Line)이고 학습자 줄의 무례·사건만 본다."""
    learner_turns = [(line.rude, line.events) for line in history if line.who == "me"]
    return mood_state(learner_turns, needs, profile, *service_rules())


def mood_strength(level: float, rules: dict) -> str | None:
    """기분 세기(0~3)를 말투에 드러나는 단계로. showsAt 미만은 None, strongAt 이상은 strong, 그 사이는 mild."""
    mood = rules["mood"]
    return "strong" if level >= mood["strongAt"] else "mild" if level >= mood["showsAt"] else None


@cache
def service_rules() -> tuple[dict, dict]:
    """대답 경로에서 쓰는 공통 규칙과 행동 조각 모음. 요청마다 파일을 읽지 않도록 한 번만 읽는다(바꾸지 말고 읽기만)."""
    return load_world_rules(), load_behaviors()


def after_conversation(relationship: dict | None, rates: dict, rules: dict) -> dict:
    """정상 종료된 대화 하나로 바뀌는 알아봄의 변화량. 백엔드는 저장값에 더한다(종료 처리가 겹쳐도 앞 대화 결과를 덮어쓰지 않게).
    새 값을 정수로 반올림한 뒤 저장값을 빼므로, 더한 결과는 새 값을 그대로 저장할 때와 같다.
    호감은 대화 중에 발화마다 바꾸고(line_affinity_delta, rude_affinity_delta) 좋은 대화 몫은 /v1/memory가 주므로(good_conversation_affinity_delta)
    여기서는 계산하지 않는다.
    relationship: 저장된 관계와 마지막 대화 뒤 지난 게임 날 수 {recognition, affinity, days_since_last_talk}. 처음 만나면 None."""
    before = relationship or {}
    stored = before.get("recognition", 0)
    recognition = recognition_today(stored, before.get("days_since_last_talk", 0), rules)
    return {"recognition_delta": round(grow_recognition(recognition, rates.get("recognitionRate", 1), rules)) - stored}
