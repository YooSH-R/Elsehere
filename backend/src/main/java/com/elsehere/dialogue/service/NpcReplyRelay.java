package com.elsehere.dialogue.service;

import com.elsehere.ai.client.NpcReplyChunk;
import com.elsehere.ai.client.NpcReplyListener;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.common.error.BusinessException;
import lombok.extern.slf4j.Slf4j;

/**
 * 워커 NPC 대답 스트림의 글자 조각과 문장 조각 음성을 {@link TurnEvents}로 옮긴다. 대화 시작(CONV-01)의 첫 인사와 발화 전송(CONV-03)의
 * 대답이 같이 쓴다.
 */
@Slf4j
final class NpcReplyRelay {

  private NpcReplyRelay() {}

  /** {@code events}가 null이면 조각을 받지 않는다. */
  static NpcReplyListener to(AiGateway aiGateway, WorkerTrace trace, TurnEvents events) {
    if (events == null) {
      return NpcReplyListener.NONE;
    }
    return new NpcReplyListener() {
      @Override
      public void onDelta(String text) {
        events.delta(text);
      }

      @Override
      public void onChunk(NpcReplyChunk chunk) {
        events.audio(chunk.seq(), chunk.text(), chunkAudio(aiGateway, trace, chunk.audioId()));
      }
    };
  }

  // 조각 음성은 워커 메모리에서 바로 받아 넘긴다(같은 서버라 조각마다 수 ms). 받지 못한 조각은 자막만 보여 주도록 음성 없이 넘긴다.
  private static byte[] chunkAudio(AiGateway aiGateway, WorkerTrace trace, String audioId) {
    if (audioId == null) {
      return null;
    }
    try {
      return aiGateway.audio(trace, audioId);
    } catch (BusinessException exception) {
      log.warn("npc_chunk_audio_download_failed code={}", exception.getErrorCode());
      return null;
    }
  }
}
