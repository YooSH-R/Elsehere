package com.elsehere.dialogue.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.service.NpcAudioRetention;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

/** NPC 대답 스트림이 만든 턴 전체 음성을 워커에서 받아 보관 상한 안에 저장한다. */
@Slf4j
@Component
@RequiredArgsConstructor
class NpcAudioReceiver {

  private final AiWorkerClient aiWorkerClient;
  private final NpcAudioRetention npcAudioRetention;

  /** 저장한 음성 키. 음성이 없거나, 받지 못하거나, 저장하지 못하면 자막만 보여 주도록 null이다. */
  String save(WorkerTrace trace, String audioId) {
    if (audioId == null) {
      return null;
    }
    try {
      return npcAudioRetention.save(aiWorkerClient.audio(trace, audioId));
    } catch (BusinessException exception) {
      log.warn("NPC 음성을 받지 못했습니다 code={}", exception.getErrorCode());
      return null;
    }
  }
}
