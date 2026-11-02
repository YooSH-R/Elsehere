package com.elsehere.dialogue.service;

import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.conversation.dto.NpcSummaryResponse;

/**
 * 대화에 필요한 NPC 정보. AI 호출 동안 트랜잭션을 열어 두지 않도록 엔티티에서 미리 꺼내 둔다.
 *
 * @param voiceId 음성 합성 목소리
 * @param approachLine 행인 말걸기 대사. 말을 걸지 않는 NPC는 null
 * @param approachAudioKey 말걸기 대사의 미리 만든 음성. 없으면 null
 */
public record NpcContext(
    Long npcId,
    NpcSummaryResponse summary,
    String sceneCode,
    boolean sceneOpen,
    WorkerNpc workerNpc,
    String voiceId,
    String approachLine,
    String approachAudioKey) {}
