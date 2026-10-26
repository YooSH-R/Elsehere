"""NPC 대답용 시스템 프롬프트. 모든 NPC가 같은 틀을 쓰고, NPC마다 다른 것은 백엔드가 보내는 데이터뿐이다.

순서: ① 공통 규칙 ② 인격(npcProfile) ③ 상황(scenario) ④ 학습 수준(userLevel) ⑤ 이번 턴(turnType).
위쪽일수록 우선하고 덜 바뀐다. 인격·상황은 캐릭터 데이터일 뿐이라 공통 규칙을 바꾸지 못한다.
계약: docs/api/npc-ai-contracts/02_npc_inference_api_spec_v2.md
"""

from app.npc_profile_models import NpcProfile


def one_line(text: str) -> str:
    """값 안의 줄바꿈·연속 공백을 한 칸으로 편다. 프로필 값이 새 줄을 열어 '## Rules' 같은 다른 부분인 척하지 못하게."""
    return " ".join(text.split())


def bullets(title: str, items: list[str]) -> list[str]:
    """'제목:' 줄과 '- 항목' 줄들. 항목이 없으면 제목도 쓰지 않는다 (미설정 칸을 언급하지 않음)."""
    return [f"{title}:", *(f"- {one_line(item)}" for item in items)] if items else []


def labeled(pairs: list[tuple[str, str | None]]) -> list[str]:
    """('이름', 값) 중 값이 있는 것만 '이름: 값'으로."""
    return [f"{label}: {value}" for label, value in pairs if value]


def character_section(profile: NpcProfile) -> str:
    """② 인격 부분. 스키마 순서대로 칸마다 한 줄씩, 비어 있거나 생략된 칸은 뺀다. schemaVersion·profileVersion은 관리용이라 넣지 않는다."""
    lines = ["## Character", f"Name: {one_line(profile.name)}", f"Occupation: {one_line(profile.occupation)}"]
    if profile.age_range:
        lines.append(f"Age: {one_line(profile.age_range)}")

    personality = profile.personality
    lines += bullets("Personality", [f"{trait.name}: {trait.description}" for trait in personality.traits])
    if personality.dimensions:  # 창작용 상대값(0~1). 미설정이면 추정하지 않고 아예 뺀다
        scores = ", ".join(f"{name} {value:g}" for name, value in personality.dimensions.model_dump().items())
        lines.append(f"Temperament (relative 0 to 1, not test scores): {scores}")
    lines += bullets("Values", profile.values)
    lines += bullets("Motivations", profile.motivations)
    lines += bullets("Fears", profile.fears or [])

    social = profile.social_behavior
    if social:
        lines += bullets(
            "With people",
            labeled(
                [
                    ("First meeting", social.first_meeting),
                    ("Getting familiar", social.familiarity_expression),
                    ("In a conflict", social.conflict_response),
                    ("Boundaries", "; ".join(social.boundaries)),
                ]
            ),
        )

    speech = profile.speech_style
    catchphrases = ", ".join(f'"{phrase}"' for phrase in speech.catchphrases or [])
    lines += bullets(
        "Speech style",
        labeled(
            [
                ("Tone", speech.tone),
                ("Formality", speech.formality),
                ("Talkativeness", speech.verbosity),
                ("Humor", speech.humor),
                ("Directness", speech.directness),
                ("Catchphrases (only now and then, not in every reply)", catchphrases),  # 매 턴 반복하는 틀이 아니다 (설정 명세)
            ]
        ),
    )

    background = profile.background
    if background:
        if background.summary:
            lines.append(f"Background: {one_line(background.summary)}")
        lines += bullets("Past experiences", [f"{e.event} Because of it: {e.influence}" for e in background.experiences])
        if background.interests:
            lines.append(f"Interests: {', '.join(one_line(interest) for interest in background.interests)}")
    lines += bullets("Habits", [f"{habit.trigger}: {habit.tendency}" for habit in profile.habits or []])
    lines += bullets("Behavior rules", profile.behavior_rules)
    return "\n".join(lines)
