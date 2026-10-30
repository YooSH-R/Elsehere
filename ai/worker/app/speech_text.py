"""NPC 음성으로 읽을 글 다듬기. 화면 자막·대답 원문은 그대로 두고 Kokoro에 넘기는 글만 바꾼다.

Kokoro(espeak 음소화)는 "$13"을 "dollar thirteen", "10:00"을 "ten zero zero"로 읽고, 별표를 "asterisk"로, 한글은
언어 전환 표시 "(ko)…(enus)"까지 소리로 읽는다 (S15P31A305-346 왕복 평가·음소 확인). 숫자 자체는 바르게 읽으므로
단위만 붙여 둔다: "13 dollars", "10 o'clock", "6 30". 한글은 자막으로만 보여 준다.
"""

import re

PRICE = re.compile(r"\$\s?(\d{1,3}(?:,\d{3})+|\d+)(?:\.(\d{1,2}))?(?!\d)")
CLOCK = re.compile(r"\b([01]?\d|2[0-3]):([0-5]\d)\b(\s*[ap]\.?m\.?)?", re.IGNORECASE)
HANGUL = re.compile(r"[ᄀ-ᇿ㄰-㆏가-힣]+")
EMPTY_QUOTES = re.compile(r"“\s*”|\"\s*\"")
SPACE_BEFORE_MARK = re.compile(r"\s+([,.!?;:])")
PAUSE_BEFORE_END = re.compile(r"[,;:]+([.!?])")  # 한글을 빼고 남은 "Oh,?" → "Oh?"
LEADING_MARKS = re.compile(r"^[\s,.!?;:]+")


def for_speech(text: str) -> str:
    """Kokoro에 넘길 글. 읽을 것이 없으면 빈 문자열."""
    text = text.replace("*", "")
    text = HANGUL.sub("", text)
    text = PRICE.sub(_price, text)
    text = CLOCK.sub(_clock, text)
    text = EMPTY_QUOTES.sub("", text)
    text = SPACE_BEFORE_MARK.sub(r"\1", text)
    text = PAUSE_BEFORE_END.sub(r"\1", text)
    text = LEADING_MARKS.sub("", text)
    return " ".join(text.split())


def _price(match: re.Match) -> str:
    dollars = int(match.group(1).replace(",", ""))
    cents = int(match.group(2).ljust(2, "0")) if match.group(2) else 0
    parts = []
    if dollars or not cents:
        parts.append(f"{dollars} dollar" + ("" if dollars == 1 else "s"))
    if cents:
        parts.append(f"{cents} cent" + ("" if cents == 1 else "s"))
    return " and ".join(parts)


def _clock(match: re.Match) -> str:
    hour, minute, meridiem = match.group(1), match.group(2), match.group(3) or ""
    if minute == "00":
        return f"{hour}{meridiem}" if meridiem else f"{hour} o'clock"
    if minute.startswith("0"):
        return f"{hour} oh {minute[1]}{meridiem}"
    return f"{hour} {minute}{meridiem}"
