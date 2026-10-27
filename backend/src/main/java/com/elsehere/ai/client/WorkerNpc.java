package com.elsehere.ai.client;

/**
 * 워커 요청의 NPC. 워커는 상태를 저장하지 않으므로 매 요청에 담아 보낸다.
 *
 * @param id NPC 식별자. npcs.code를 쓴다
 * @param ttsSpeed NPC의 타고난 말 속도. NPC 대답은 워커가 난이도 배율을 곱한다
 */
public record WorkerNpc(
    String id,
    String name,
    String role,
    String facts,
    String scene,
    String ttsVoice,
    double ttsSpeed) {}
