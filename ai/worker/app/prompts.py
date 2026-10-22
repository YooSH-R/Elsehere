"""프롬프트와 출력 형식. 웹 프로토타입(프로토타입 브랜치의 prototype/mvp_web/server.py)에서 회귀 테스트로 다듬은 문장을 옮겼다.
문장을 바꾸면 그 회귀 테스트(prototype/mvp_web/tests/replay.py)로 먼저 확인할 것. 테스트 문장을 예시로 넣지 않는다 (과적합)."""

import json
import re

from .schemas import Line, Memory, Npc, State

TOWN = "Maple Town, a small, friendly town in the United States."
HANGUL = re.compile(r"[가-힣ㄱ-ㅎ]")
# 행동 지시(*총을 꺼낸다*)는 언어 학습과 상관없어 받지 않는다: 지우고 말만 판정, 행동만 있으면 모델을 부르지 않음
ACTION = re.compile(r"\*[^*\n]{1,200}\*")
ACTION_ONLY_KO = "행동 묘사(*...*)는 쓸 수 없어요. 하고 싶은 말을 영어로 해 보세요."
ACTION_PART_KO = "행동 묘사(*...*)는 빼고 말만 전달했어요."


def strip_actions(text: str) -> tuple[str, bool]:
    """반환: (행동을 뺀 말, 행동이 있었는지)."""
    spoken = re.sub(r"\s+", " ", ACTION.sub(" ", text)).strip()
    return spoken, spoken != text.strip()


def last_npc(history: list[Line]) -> str:
    return next((h.text for h in reversed(history) if h.who == "npc"), "(none)")


# ---------- 판정 ----------
GUARD_SYSTEM = """You moderate a role-play in an English-speaking practice game. A Korean learner talks with a person in a small American town.
Decide one thing: is the learner's latest message abusive or offensive in this conversation?
Abusive means: insulting, swearing at, name-calling, mocking (teasing them again and again), rudely ordering around,
threatening, sexually harassing, or hateful. English and Korean (including slang) both count.
It also counts when the insult is aimed at a group the person belongs to or cares about (their team, fans, job, country, family),
or when it is a slur or hateful word about any group of people.
Making fun of how they look or how old they are counts, even when said as a joke (with "lol", "ㅋㅋ", "^^"), and so does sarcastic fake praise meant to mock them.
In Korean, ordering a stranger around in condescending 반말 (with a dismissive "~(이)나" or a sharp "야") counts as rudely ordering around.
Insults about their mother or family are always abusive, even very short ones ("yo mama", "느금마").
Threats count even without swear words: robbery or extortion ("hand over the cash", "지갑 털기 전에 내놔"), intimidation ("call anyone and you'll regret it").
Read the recent conversation: a calm-sounding line is still a threat if it continues a threat.
Not abusive: normal requests, complaints said politely, refusing politely, jokes or idioms like "I could die for a latte",
saying who they are (even "I'm a police officer"), talking to themselves in frustration without swear words.
Swear words and vulgar words always count, even when aimed at a situation, a thing, or themselves instead of the person.
Answer with one word only: yes (abusive) or no."""

INTENT_SYSTEM = """You label one line said by a Korean learner of English (CEFR A2-B1) in a role-play game.
The line comes from speech recognition: it may be broken English, unfinished, or mix Korean words (for example "아이스 라떼 하나" means one iced latte).
Use the partner's last line to understand short answers (for example "yes, please" after "Another iced latte?" orders an iced latte).
Judge only what the learner's line itself does. If you cannot tell, choose "unclear". Do not guess."""

TRANSLATE_SYSTEM = """You help a Korean learner of English in a role-play.
Translate what the LEARNER just said into natural English, in the learner's own voice (I/me). It may be all Korean or mixed.
Keep exactly the meaning and the tone: if it is rude, keep it rude; do not soften, fix, or add anything.
Never answer it and never write what the other person would say.
If it is not real words (random letters or typos you cannot read), output only: -
Short and simple (CEFR A2-B1). Output only the English sentence.
Example: partner asked "For here or to go?", learner said "여기서 먹고 가지 뭐" -> I'll just eat here."""


def guard_messages(npc: Npc, history: list[Line], text: str) -> list[dict]:
    recent = "\n".join(f"{'Learner' if h.who == 'me' else npc.name}: {h.text}" for h in history[-4:])
    return [
        {"role": "system", "content": GUARD_SYSTEM},
        {
            "role": "user",
            "content": f"The person: {npc.name}, {npc.role}\nAbout them: {npc.facts}\nRecent conversation:\n{recent}\n\n"
            f"Learner's latest message: {text}",
        },
    ]


def intent_schema(state: State) -> dict:
    """판정 출력 형식: 허용된 의도 하나 + (사는 장소면) 상품 목록 + (있으면) 선택지."""
    props = {"intents": {"type": "array", "items": {"type": "string", "enum": [i.id for i in state.intents + state.more_intents]}}}
    s = state.slots
    if s.items:
        item = (
            {"name": {"type": "string", "enum": s.items.catalog}}
            | ({"iced": {"type": "boolean"}} if s.items.iced_ok else {})
            | {"qty": {"type": "integer"}}
        )
        props["items"] = {
            "type": "array",
            "items": {"type": "object", "properties": item, "required": list(item), "additionalProperties": False},
        }
    if s.option:
        props[s.option.slot] = {"type": "string", "enum": [*s.option.labels, "none"]}
    return {"type": "object", "properties": props, "required": list(props), "additionalProperties": False}


def intent_messages(npc: Npc, state: State, history: list[Line], text: str) -> list[dict]:
    lines = "\n".join(f"- {i.id}: {i.desc}" for i in state.intents)
    later = "\n".join(f"- {i.id}: {i.desc}" for i in state.more_intents)
    extra, s = "", state.slots
    if s.items:
        it = s.items
        iced = '; "iced" true only if they said iced/ice/cold or accepted an iced drink' if it.iced_ok else ""
        extra = (
            f"\nCurrent order: {state.current.get('order') or 'nothing yet'}."
            '\nFor order_items fill "items" with the COMPLETE order after this line (keep earlier items unless they change or remove them; '
            f"{it.catalog_word}: " + ", ".join(it.catalog) + iced + "). "
            "Use the item their words clearly mean, even with another word or a shorter name, when only one item fits; "
            "words like 'that', 'it' or 'yes' mean the item the partner just offered. "
            f"A general word like {it.vague_words} that could mean more than one item is not enough: choose unclear. "
            'For every other intent, "items": [].'
        )
    if s.option:
        o = s.option
        extra += f'\nFill "{o.slot}" with ' + " or ".join(f'"{k}"' for k in o.labels) + f' if they said {o.prompt}, otherwise "none".'
    user = (
        f"Partner: {npc.name}, {npc.role}\nSituation now: {state.desc}\n"
        f"Partner's last line: {last_npc(history)}\nLearner's line: {text}\n\n"
        f'List in "intents" what the line does, in the order they say it. Usually one; more only when the line clearly does several things. Choose from:\n{lines}'
        + (f"\nIf the same line goes on to do more, these can come after the first one:\n{later}" if later else "")
        + extra
    )
    return [{"role": "system", "content": INTENT_SYSTEM}, {"role": "user", "content": user}]


def translate_messages(npc: Npc, scene: str, history: list[Line], text: str) -> list[dict]:
    user = f"Place: {scene}\nThe person: {npc.name}, {npc.role}\nTheir last line: {last_npc(history)}\nLearner's message: {text}"
    return [{"role": "system", "content": TRANSLATE_SYSTEM}, {"role": "user", "content": user}]


# ---------- NPC 대답 ----------
def memory_note(m: Memory | None) -> str:
    """NPC 지시문에 넣을 '이 손님에 대해 기억하는 것'. visits: 한 마디 이상 나눈 지난 대화 수."""
    if not m or m.visits < 1:
        return ""
    c = m.card or {}
    lines = [f"You remember this customer: you have talked {m.visits} time(s) before."]
    if c.get("name"):
        lines.append(f"- Their name: {c['name']}")
    if c.get("claims"):
        lines.append("- They told you: " + "; ".join(c["claims"]))
    if c.get("events"):
        lines.append("- What happened before: " + "; ".join(c["events"]))
    if c.get("attitude") == "wary":
        lines.append("- You are wary of them because of what happened. Stay polite but a little careful.")
    elif c.get("attitude") == "friendly":
        lines.append("- You like this customer.")
    if c.get("struggled"):
        lines.append(
            "- English they found hard last time: " + "; ".join(c["struggled"]) + " (use one of these naturally if it fits; never teach)"
        )
    if m.last_order:
        lines.append(f"- Last time they ordered {m.last_order}.")
    return "\n".join(lines)


def npc_system(n: Npc, memory: str, note: str) -> str:
    return f"""You are {n.name}, {n.role}
Place: {TOWN} {n.scene}
What you know: {n.facts}{chr(10) + memory if memory else ""}

You are talking with a Korean person practicing English (CEFR A2-B1). Their words come from speech recognition and may be broken English, Korean, or a mix.
Understand Korean too (including slang and insults), but always reply in English.

How to reply:
- Answer what they just said, like a real person here. 1-2 short, simple sentences (max 25 words).
- If their English has mistakes or Korean words, understand the meaning and naturally use the correct English words in your reply. Never explain grammar.
- If they ask about something your facts don't cover, answer like a real person in your job would, with small realistic details. Never invent menu items, prices, discounts, or free things.
- Never repeat a sentence you already said. Do not push them to order in every reply.
- Stay in character. Never mention English lessons, practice, or that you are an AI.
- Plain spoken words only: no emojis, lists, or stage directions.

IMPORTANT for your next reply: {note or "Just reply naturally."}"""


def npc_messages(n: Npc, memory: Memory | None, history: list[Line], text: str, instruction: str) -> list[dict]:
    msgs = [{"role": "system", "content": npc_system(n, memory_note(memory), instruction)}]
    for h in history[-12:]:
        msgs.append({"role": "assistant" if h.who == "npc" else "user", "content": h.text})
    msgs.append({"role": "user", "content": text})
    return msgs


# ---------- 힌트 ----------
HINT_SYSTEM = """You help a Korean learner of English (CEFR A2-B1) who is stuck in a role-play. The learner is the one speaking to the partner.
Write what the LEARNER could say next: reply naturally to the partner's latest line and do the goal.

Return JSON:
- "say_this": 2 options, each 1 short sentence (max 12 words) in the learner's own voice (I/me), with "ko": its Korean meaning.
  Make the first very simple and the second a little more natural.
- "keywords": 2-3 useful English words or phrases from your sentences, with "ko".
Never write what the partner would say."""
_PAIR = {
    "type": "array",
    "items": {
        "type": "object",
        "properties": {"en": {"type": "string"}, "ko": {"type": "string"}},
        "required": ["en", "ko"],
        "additionalProperties": False,
    },
}
HINT_SCHEMA = {
    "type": "object",
    "properties": {"say_this": _PAIR, "keywords": _PAIR},
    "required": ["say_this", "keywords"],
    "additionalProperties": False,
}


def hint_messages(npc: Npc, scene: str, goal: str, history: list[Line]) -> list[dict]:
    convo = "\n".join(f"{'Learner' if h.who == 'me' else 'Partner'}: {h.english or h.text}" for h in history[-6:])
    user = f"Place: {scene}\nPartner: {npc.name}, {npc.role}\n\nConversation so far:\n{convo}\n\nGoal for the learner's next line: {goal}"
    return [{"role": "system", "content": HINT_SYSTEM}, {"role": "user", "content": user}]


# ---------- 대화 종료: 피드백 + 기억 카드 ----------
FEEDBACK_SYSTEM = """You give short, kind feedback in Korean to a Korean learner of English (CEFR A2-B1) after a role-play conversation.
You get the situation, whether the goal was reached (decided by the game, trust it), and the conversation. Learner lines are numbered L1, L2, ...
Learner lines come from speech recognition and may mix Korean.

Rules:
- Judge communication and naturalness separately. If the meaning got across, say so even if the English was not perfect.
- Do not judge pronunciation (you only see text).
- "better" must keep exactly what the learner meant, in natural, simple English (CEFR A2-B1). Never change it into a different answer or add things they did not say
  (if they said goodbye, "better" is still a goodbye). If the line was already natural, return it unchanged and leave "note" empty.
- Lines marked [RUDE] were insulting or threatening: "better" is a polite way to say the same feeling or need (drop the insult), and "note" says briefly in Korean why it was rude. Never call a [RUDE] line natural. Prefer a [RUDE] line for "improve".
- Pick "improve" only from lines that really need a fix. If every line was natural, pick the line that could sound a little more natural.
- Korean text: short, friendly, polite (해요체). No long grammar lectures.

Return JSON:
- "good": one sentence in Korean about what the learner did well (specific to this conversation).
- "improve": the single most useful fix: "line" (the L number), "better" (English), "why" (one short Korean sentence).
- "lines": for every learner line in order: "line", "better", "note" (one short Korean sentence or "")."""
FEEDBACK_SCHEMA = {
    "type": "object",
    "properties": {
        "good": {"type": "string"},
        "improve": {
            "type": "object",
            "properties": {"line": {"type": "integer"}, "better": {"type": "string"}, "why": {"type": "string"}},
            "required": ["line", "better", "why"],
            "additionalProperties": False,
        },
        "lines": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {"line": {"type": "integer"}, "better": {"type": "string"}, "note": {"type": "string"}},
                "required": ["line", "better", "note"],
                "additionalProperties": False,
            },
        },
    },
    "required": ["good", "improve", "lines"],
    "additionalProperties": False,
}

MEMORY_SYSTEM = """You keep the memory a character in a role-play game has about one customer (a Korean learner of English) across visits.
You get what the character remembered before and the conversation that just ended. Return the updated memory.
- name: the name the customer gave for themselves. If none this time, keep the old one. Unknown: "".
- claims: what the customer said about themselves, written as claims, not facts ("says they are a police officer"). Max 5, newest first.
  Never keep phone numbers, emails, addresses or other contact details.
- events: what happened between them, short, from the character's view ("was rude and threatened me, so I asked them to leave"). Max 5, newest first; keep important old ones.
- attitude: how the character feels about them now: friendly, neutral, or wary (wary after rudeness or threats; it can soften if they were kind later).
- struggled: English expressions the customer clearly had trouble with in this conversation (short phrases). Max 5.
Only write what the conversation shows. Do not invent. Each item is a short English phrase (max 12 words)."""
MEMORY_SCHEMA = {
    "type": "object",
    "properties": {
        "name": {"type": "string"},
        "claims": {"type": "array", "items": {"type": "string"}},
        "events": {"type": "array", "items": {"type": "string"}},
        "attitude": {"type": "string", "enum": ["friendly", "neutral", "wary"]},
        "struggled": {"type": "array", "items": {"type": "string"}},
    },
    "required": ["name", "claims", "events", "attitude", "struggled"],
    "additionalProperties": False,
}
CARD_LISTS = {"claims": 5, "events": 5, "struggled": 5}
CONTACT = re.compile(r"\d{2,4}[-. ]?\d{3,4}[-. ]?\d{4}|[\w.]+@[\w.]+")  # 연락처는 저장하지 않는다


def said_line(h: Line) -> str:
    return h.text + (f"  (meant: {h.english})" if h.english and h.english != h.text else "") + (" [RUDE]" if h.rude else "")


def feedback_messages(npc: Npc, title: str, goal_reached: bool, outcome: str, history: list[Line]) -> list[dict]:
    rows, k = [], 0
    for h in history:
        if h.who == "me":
            k += 1
            rows.append(f"L{k} Learner: {said_line(h)}")
        else:
            rows.append(f"{npc.name}: {h.text}")
    user = (
        f"Situation: {title} — talking with {npc.name}, {npc.role}\n"
        f"Goal reached: {'yes' if goal_reached else 'no'} ({outcome})\n\nConversation:\n" + "\n".join(rows)
    )
    return [{"role": "system", "content": FEEDBACK_SYSTEM}, {"role": "user", "content": user}]


def memory_messages(npc: Npc, old: dict | None, outcome: str, history: list[Line]) -> list[dict]:
    rows = [("Customer: " + said_line(h)) if h.who == "me" else f"{npc.name}: {h.text}" for h in history]
    user = (
        f"Character: {npc.name}, {npc.role}\nWhat {npc.name} remembered before: {json.dumps(old or {}, ensure_ascii=False)}\n"
        f"How this conversation ended: {outcome}\n\nConversation:\n" + "\n".join(rows)
    )
    return [{"role": "system", "content": MEMORY_SYSTEM}, {"role": "user", "content": user}]


def clean_card(card: dict) -> dict:
    """기억 카드 정리: 칸 수·길이 제한, 정해진 태도 값, 연락처 제거."""
    out = {
        "name": str(card.get("name") or "").strip()[:40],
        "attitude": card.get("attitude") if card.get("attitude") in ("friendly", "neutral", "wary") else "neutral",
    }
    if CONTACT.search(out["name"]):
        out["name"] = ""
    for k, n in CARD_LISTS.items():
        items = [str(x).strip()[:100] for x in (card.get(k) or []) if str(x).strip()]
        out[k] = [x for x in items if not CONTACT.search(x)][:n]
    return out
