package com.elsehere.dialogue.service;

import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.service.NpcAudioRetention;
import java.util.concurrent.TimeUnit;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

/** NPC 대답 스트림이 만든 턴 전체 음성을 워커에서 받아 보관 상한 안에 저장한다. */
@Slf4j
@Component
@RequiredArgsConstructor
class NpcAudioReceiver {

  private final AiGateway aiGateway;
  private final NpcAudioRetention npcAudioRetention;

  /** 저장한 음성 키. 음성이 없거나, 받지 못하거나, 저장하지 못하면 자막만 보여 주도록 null이다. */
  String save(WorkerTrace trace, String audioId) {
    if (audioId == null) {
      return null;
    }
    try {
      long startedAt = System.nanoTime();
      byte[] wav = aiGateway.audio(trace, audioId);
      long downloadedAt = System.nanoTime();
      String key = npcAudioRetention.save(wav);
      if (key != null) {
        // 대화 시작·대답이 오래 걸릴 때 어느 단계인지 보도록 개발 서버(DEBUG)에서만 남긴다.
        log.debug(
            "npc_audio_saved bytes={} download_ms={} save_ms={}",
            wav.length,
            TimeUnit.NANOSECONDS.toMillis(downloadedAt - startedAt),
            TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - downloadedAt));
      }
      return key;
    } catch (BusinessException exception) {
      log.warn("npc_audio_download_failed code={}", exception.getErrorCode());
      return null;
    }
  }
}
