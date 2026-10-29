package com.elsehere.admin.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

/**
 * NPC 페르소나 수정. AI 워커가 대화 프롬프트에 그대로 넣는 세 값이다(docs/api/ai-worker.md의 npc).
 *
 * @param role NPC가 누구인지 한 문장(예: the barista at the corner cafe.). 이름 뒤에 이어 붙는다.
 * @param facts NPC가 알고 있는 사실·지켜야 할 규칙. 비우면 없음으로 저장한다.
 * @param scene NPC가 있는 장소 설명. 비우면 없음으로 저장한다.
 */
public record PersonaRequest(
    @NotBlank @Size(max = 200) String role,
    @Size(max = 2000) String facts,
    @Size(max = 300) String scene) {}
