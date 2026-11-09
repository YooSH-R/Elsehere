"""ai/tools/guard_eval.py: 평가 문장을 서비스와 같은 판정 요청으로 바꾸는지. 실제 모델은 부르지 않는다."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import guard_eval as G

from app import prompts as P
from app.schemas import Line, Npc

NPC = {"id": "diner", "name": "Sam", "role": "the cashier at a burger diner.", "facts": "", "scene": "Diner."}


def test_item_with_only_prev_is_judged_after_that_npc_line():
    item = {"text": "라떼 하나", "prev": "What can I get you?", "npc": NPC}
    assert G.messages(item) == P.guard_messages(Npc(**NPC), [Line(who="npc", text="What can I get you?")], "라떼 하나")


def test_gpu_criteria_match_what_the_service_sends_to_the_gpu_model():
    """서비스는 GPU LoRA에 GUARD_SYSTEM_GPU, 구성의 판정 모델(단독·확인)에 GUARD_SYSTEM을 보낸다 (--criteria)."""
    item = {"text": "감자", "prev": "", "npc": NPC}
    assert G.messages(item, "gpu")[0]["content"] == P.GUARD_SYSTEM_GPU
    assert G.messages(item)[0]["content"] == P.GUARD_SYSTEM


def test_item_without_prev_has_no_history():
    item = {"text": "hello", "prev": "", "npc": NPC}
    assert G.messages(item) == P.guard_messages(Npc(**NPC), [], "hello")


def test_item_history_is_used_like_the_service_conversation():
    """서비스는 앞 대화 여러 줄(학습자·NPC)을 넣는다. history가 있으면 prev 대신 그 줄들을 쓴다."""
    history = [
        {"who": "me", "text": "구급차 부르지 마"},
        {"who": "npc", "text": "Stay seated."},
        {"who": "me", "text": "배고파"},
        {"who": "npc", "text": "Here you go."},
    ]
    item = {"text": "맛있다", "prev": "Here you go.", "history": history, "npc": NPC}
    want = P.guard_messages(Npc(**NPC), [Line(**h) for h in history], "맛있다")
    assert G.messages(item) == want
    assert "Learner: 구급차 부르지 마" in want[1]["content"]
