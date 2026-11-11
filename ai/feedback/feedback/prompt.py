import json
from pathlib import Path

from .models import INPUT, contains_korean
from .situation_catalog import situation_candidates
from .spoken_usage import spoken_candidates

PROMPT_VERSION = "feedback_prompt_v0.11.0"
SYSTEM_PATH = Path(__file__).resolve().parent.parent / "prompts" / "feedback_system.ko.md"


def build_messages(payload):
    request = INPUT.validate_python(payload)
    # 미확정/차단/실패 입력 및 수정 전 STT 원문을 모델에 보내지 않는다.
    final_turns = [turn for turn in request.turns if turn.text_status == "final" and turn.speaker in ("user", "npc")]
    used = {aid for turn in final_turns for aid in turn.assistance_ids}
    profiles = {}
    profile_ids = {}
    compact_turns = []
    for turn in final_turns:
        difficulty = turn.difficulty.model_dump()
        profile_key = json.dumps(difficulty, sort_keys=True)
        if profile_key not in profile_ids:
            profile_id = f"d{len(profiles) + 1}"
            profile_ids[profile_key] = profile_id
            profiles[profile_id] = difficulty
        compact_turns.append(
            {
                "turn_id": turn.turn_id,
                "speaker": turn.speaker,
                "input_type": turn.input_type,
                "final_text": turn.final_text,
                "difficulty_id": profile_ids[profile_key],
                "assistance_ids": turn.assistance_ids,
            }
        )
    body = {
        "context": request.context.model_dump(),
        "required_review_turn_ids": [turn.turn_id for turn in final_turns if turn.speaker == "user"],
        "english_expression_turn_ids": [
            turn.turn_id for turn in final_turns if turn.speaker == "user" and contains_korean(turn.final_text)
        ],
        "difficulty_profiles": profiles,
        "turns": compact_turns,
        "assistance_events": [event.model_dump() for event in request.assistance_events if event.assistance_id in used],
        "spoken_usage_candidates": spoken_candidates(request),
        "situation_expression_candidates": situation_candidates(request),
    }
    system = SYSTEM_PATH.read_text(encoding="utf-8")
    return [
        {"role": "system", "content": system},
        {"role": "user", "content": json.dumps(body, ensure_ascii=False, separators=(",", ":"))},
    ]
