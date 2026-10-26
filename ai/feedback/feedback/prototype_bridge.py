"""독립 플레이 검증판에서 프로토타입 대화를 피드백 계약으로 연결한다."""

from .costs import estimate_observed_gms_credits
from .models import INPUT
from .prompt import PROMPT_VERSION
from .service import generate_feedback


def build_prototype_input(conversation, npc, scenario, model):
    # 검증판은 힌트·번역을 차단한다. 기존 수집 zip의 도움 사용 여부를 추정하지 않는다.
    level = conversation["level"]
    difficulty = {
        "settings_version": "prototype_level_v1",
        "vocabulary_profile": {1: "easy", 2: "normal", 3: "hard"}[level],
        "speech_rate_multiplier": {1: 0.85, 2: 1.0, 3: 1.06}[level],
        "hint_policy": "on_request",
    }
    turns = []
    for index, entry in enumerate(conversation["history"], 1):
        learner = entry["who"] == "me"
        turns.append(
            {
                "turn_id": f"t{index:02d}",
                "sequence": index,
                "speaker": "user" if learner else "npc",
                "input_type": ("voice" if entry.get("input_mode") in ("voice", "voice_edited") else "text") if learner else "generated",
                "text_status": "blocked" if learner and entry.get("rude") is True else "final",
                "original_transcript": None,
                "final_text": entry["text"],
                "difficulty": difficulty,
                "assistance_ids": [],
            }
        )
    payload = {
        "schema_version": "0.3.0",
        "request_id": "feedback_" + conversation["id"],
        "scope": {"user_id": conversation["uid"], "npc_id": conversation["npc"], "conversation_id": conversation["id"]},
        "conversation_revision": len(turns),
        "analysis_config_version": f"{PROMPT_VERSION}:{model}",
        "context": {
            "scene_id": scenario["place"],
            "mode": "free_chat",
            "scene_description": scenario["title_ko"] + ": " + npc["scene"],
            "target_language": "en-US",
            "explanation_language": "ko-KR",
            "end_reason": "npc_end" if conversation.get("closed") else "user_end",
            "tutorial_goal": None,
        },
        "turns": turns,
        "assistance_events": [],
    }
    return INPUT.validate_python(payload).model_dump(mode="json")


def generate_prototype_feedback(conversation, npc, scenario, client):
    payload = build_prototype_input(conversation, npc, scenario, client.settings.model)
    response = generate_feedback(payload, client, ai_allowed=True)
    # 유효 발화가 없으면 generate_feedback이 모델을 호출하지 않아 last_call은 이전 대화의 값일 수 있다.
    call = {}
    if any(turn["speaker"] == "user" and turn["text_status"] == "final" for turn in payload["turns"]):
        call = client.last_call or {}
    usage = call.get("usage") or {}
    return response | {
        "latency_s": call.get("elapsed_s"),
        "feedback_call": call,
        "credit_estimate": {"credits": estimate_observed_gms_credits(call), "basis": "observed_gpt54_v1", "actual_confirmed": False},
        "usage": {
            "role": "feedback",
            "model": (call.get("returned_model") or client.settings.model) if call else None,
            "input_tokens": usage.get("input_tokens"),
            "output_tokens": usage.get("output_tokens"),
            "cached_input_tokens": usage.get("cached_input_tokens"),
            "latency_s": call.get("elapsed_s"),
            "actual_gms_credits": call.get("actual_gms_credits"),
            "fallback": False,
        },
    }
