"""NPC 음성으로 읽을 글 다듬기 (app.speech_text). 화면 자막·대답 원문은 그대로이고, Kokoro에 넘기는 글만 바꾼다.

숫자는 그대로 두고 단위만 붙인다: Kokoro(espeak)는 "13 dollars", "10 o'clock", "6 30"을 바르게 읽지만
"$13"은 "dollar thirteen", "10:00"은 "ten zero zero"로 읽는다 (S15P31A305-346 왕복 평가·음소 확인)."""

import io
import wave

import pytest

from app.speech_text import for_speech
from app.tts import Kokoro


@pytest.mark.parametrize(
    ("text", "spoken"),
    [
        ("Your total is $13. Card or cash?", "Your total is 13 dollars. Card or cash?"),
        ("Large is $1 more.", "Large is 1 dollar more."),
        ("That comes to $8.50.", "That comes to 8 dollars and 50 cents."),
        ("Here's your change: $2.05.", "Here's your change: 2 dollars and 5 cents."),
        ("It's $4.00 even.", "It's 4 dollars even."),
        ("Just $0.75 extra.", "Just 75 cents extra."),
        ("That's $1,200 a month.", "That's 1200 dollars a month."),
    ],
)
def test_prices_are_read_as_dollars_and_cents(text, spoken):
    assert for_speech(text) == spoken


@pytest.mark.parametrize(
    ("text", "spoken"),
    [
        ("Breakfast is from 6:30 to 10:00.", "Breakfast is from 6 30 to 10 o'clock."),
        ("Your message came at 9:05.", "Your message came at 9 oh 5."),
        ("Check-out is by 11:00 a.m.", "Check-out is by 11 a.m."),
        ("It’s 3:30, so the lunch rush is over.", "It’s 3 30, so the lunch rush is over."),
    ],
)
def test_clock_times_are_read_as_spoken(text, spoken):
    assert for_speech(text) == spoken


@pytest.mark.parametrize(
    ("text", "spoken"),
    [
        ("Have you tried our *Maple Latte*?", "Have you tried our Maple Latte?"),
        ("This one is called **Sunrise Blend**, a light roast.", "This one is called Sunrise Blend, a light roast."),
    ],
)
def test_markdown_asterisks_are_not_read(text, spoken):
    assert for_speech(text) == spoken


@pytest.mark.parametrize(
    ("text", "spoken"),
    [
        ("아이스 라떼요? An iced latte, got it.", "An iced latte, got it."),
        ("Oh, 김치? I love kimchi!", "Oh? I love kimchi!"),
        ("You said 감사합니다? That means thank you!", "You said? That means thank you!"),
        ("My friend recommended *삼국지* to me.", "My friend recommended to me."),
        ("She said “안녕” to me.", "She said to me."),
    ],
)
def test_korean_is_left_to_subtitles(text, spoken):
    assert for_speech(text) == spoken


@pytest.mark.parametrize(
    "text",
    ["Hi! Welcome in. What can I get for you today?", "Check-in is from 3 p.m.", "Your room is 305, on the third floor."],
)
def test_plain_english_is_unchanged(text):
    assert for_speech(text) == text


def test_text_with_nothing_to_read_becomes_empty():
    assert for_speech("감사합니다!") == ""
    assert for_speech("**") == ""


class RecordingEngine:
    """Kokoro 엔진 자리. 받은 글만 적고 실제 합성은 하지 않는다 (테스트 환경에 numpy·모델이 없음)."""

    def __init__(self):
        self.texts = []

    def create(self, text, voice, speed, lang):
        self.texts.append(text)
        raise RuntimeError("합성은 하지 않음")


def test_kokoro_reads_spoken_text_instead_of_subtitle():
    engine = RecordingEngine()
    kokoro = Kokoro(engine, "fake.onnx")

    with pytest.raises(RuntimeError):
        kokoro.synth("That's $13, hon.", "af_heart", 1.0)

    assert engine.texts == ["That's 13 dollars, hon."]


def test_kokoro_returns_short_silence_when_nothing_to_read():
    engine = RecordingEngine()
    kokoro = Kokoro(engine, "fake.onnx")

    wav, seconds, _ = kokoro.synth("감사합니다!", "af_heart", 1.0)

    assert engine.texts == []
    with wave.open(io.BytesIO(wav)) as w:  # 턴 전체 음성으로 이어 붙일 수 있게 Kokoro와 같은 형식
        assert (w.getframerate(), w.getnchannels(), w.getsampwidth()) == (24000, 1, 2)
        assert w.getnframes() == round(24000 * seconds) > 0
