package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.BDDMockito.given;
import static org.mockito.Mockito.mock;

import ch.qos.logback.classic.Level;
import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.logging.LogCapture;
import com.elsehere.conversation.service.NpcAudioRetention;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class NpcAudioReceiverTest {

  private static final WorkerTrace TRACE = new WorkerTrace(UUID.randomUUID(), 7L, 70L);
  private static final byte[] WAV = {82, 73, 70, 70};

  private final AiGateway aiGateway = mock(AiGateway.class);
  private final NpcAudioRetention npcAudioRetention = mock(NpcAudioRetention.class);
  private final NpcAudioReceiver npcAudioReceiver =
      new NpcAudioReceiver(aiGateway, npcAudioRetention);

  @Test
  @DisplayName("NPC 음성을 받아 저장하면 크기와 받기·저장 시간을 개발 서버(DEBUG)에 남긴다")
  void save_logsTimingAtDebug() {
    given(aiGateway.audio(TRACE, "audio-1")).willReturn(WAV);
    given(npcAudioRetention.save(WAV)).willReturn("npc-audio-1.wav");

    try (LogCapture logs = LogCapture.atDebug(NpcAudioReceiver.class)) {
      assertThat(npcAudioReceiver.save(TRACE, "audio-1")).isEqualTo("npc-audio-1.wav");

      assertThat(logs.messages(Level.DEBUG))
          .singleElement()
          .asString()
          .matches("npc_audio_saved bytes=4 download_ms=\\d+ save_ms=\\d+");
    }
  }

  @Test
  @DisplayName("보관 상한 때문에 저장하지 못하면 저장 로그를 남기지 않고 자막만 보이도록 null이다")
  void save_notStored_logsNothing() {
    given(aiGateway.audio(TRACE, "audio-1")).willReturn(WAV);
    given(npcAudioRetention.save(WAV)).willReturn(null);

    try (LogCapture logs = LogCapture.atDebug(NpcAudioReceiver.class)) {
      assertThat(npcAudioReceiver.save(TRACE, "audio-1")).isNull();

      assertThat(logs.messages(Level.DEBUG)).isEmpty();
    }
  }
}
