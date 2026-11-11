"""ai/tools/level_eval.py: 난이도별 NPC 대답 비교의 장면 세트와 글 계산. 실제 모델은 부르지 않는다."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import level_eval as L
import npc_help_eval as H


def test_cases_use_npcs_the_game_sends_and_known_kinds():
    cases = L.load_cases()
    assert len({c["id"] for c in cases}) == len(cases)
    for c in cases:
        assert c["npc"] in H.PERSONAS, c["id"]
        assert c["kind"] in {"talk", "broken"}, c["id"]
        assert c["text"].strip(), c["id"]
    assert any(c["kind"] == "broken" for c in cases)


def test_measures_counts_words_sentences_and_joining_words():
    m = L.measure("It's just past the laundromat, so keep walking. You'll see it if you look left!")

    assert m["words"] == 15
    assert m["sentences"] == 2
    assert m["connectors"] == 2  # so, if


def test_a_question_with_or_is_a_choice_and_a_wh_question_is_open():
    choice = L.measure("Sure. Hot or iced?")
    open_question = L.measure("Sure thing. So how do you take your coffee?")
    statement = L.measure("Coming right up.")

    assert (choice["choice_questions"], choice["open_questions"]) == (1, 0)
    assert (open_question["choice_questions"], open_question["open_questions"]) == (0, 1)
    assert (statement["choice_questions"], statement["open_questions"]) == (0, 0)


def test_rare_words_skip_names_and_count_words_below_the_frequency_line():
    zipf = {"we": 7.0, "are": 7.0, "swamped": 3.0, "near": 5.0, "the": 7.5, "laundromat": 2.7}.get

    rare = L.rare_words("We are swamped near the Suds Laundromat.", zipf)

    # 문장 첫 단어(We)는 세고, 문장 중간의 대문자 단어(Suds·Laundromat)는 장소·사람 이름이라 뺀다
    assert rare == {"words": 5, "rare": 1, "zipf_total": 7.0 + 7.0 + 3.0 + 5.0 + 7.5}


def test_face_markers_are_not_spoken_words():
    assert L.spoken("[[warm_smile:0.6]]Hey there! [[curious:0.4]]What brings you in?") == "Hey there! What brings you in?"
