package com.elsehere.ai.client;

/**
 * NPC 대답의 문장 조각 하나(워커 {@code audio} 이벤트).
 *
 * @param seq 조각 순서. 0부터
 * @param text 이 조각의 대사(자막)
 * @param audioId 조각 음성 ID. 합성이 늦거나 실패했으면 null이며, 워커 메모리에 10분만 남는다
 */
public record NpcReplyChunk(int seq, String text, String audioId) {}
