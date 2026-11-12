"""ai/tools/npc_voice_eval.py: 앞에서 한 질문을 다시 물은 대답 찾기. 실제 모델은 부르지 않는다."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_voice_eval as V


def test_finds_a_question_asked_again_in_other_words():
    replies = [
        "[[warm_smile:0.5]]Welcome to Town Bank. What brings you to town?",
        "Moving is a big step. Take your time.",
        "No worries, I can go slowly. What brings you to town, then?",
    ]

    assert V.repeated_questions(replies) == [2]


def test_new_questions_and_repeated_statements_are_not_counted():
    replies = [
        "Welcome in! What can I get started for you?",
        "Welcome in! It's pretty quiet in the mornings. Have you tried the bakery yet?",
        "No problem at all. What kind of work do you do?",
    ]

    assert V.repeated_questions(replies) == []
