package com.elsehere.conversation.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyDouble;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.BDDMockito.given;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.SpeechResponse;
import com.elsehere.conversation.dto.SpeechStatus;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.test.context.TestPropertySource;

// 음성 보관 상한이 다 찬 환경. 상한을 아주 작게 두어 다시 만든 음성을 저장할 수 없게 한다.
@DialogueServiceTest
@TestPropertySource(properties = "elsehere.npc-audio.max-bytes=1")
class SpeechServiceStorageLimitTest {

  private static final byte[] WAV = {82, 73, 70, 70};

  @Autowired private SpeechService speechService;

  @Autowired private AiWorkerClient aiWorkerClient;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("다시 만든 음성을 상한 때문에 저장하지 못하면 오류 대신 FAILED로 응답하고 자막만 보여 준다")
  void regenerateSpeech_cannotStore_returnsFailed() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Turn turn = npcTurn(entityManager, conversation(entityManager, userId, "CAFE_STAFF"), 1, "Hi!");
    entityManager.flush();
    given(aiWorkerClient.synthesize(any(), anyString(), anyString(), anyDouble())).willReturn(WAV);

    SpeechResponse response =
        speechService.regenerateSpeech(userId, turn.getId(), UUID.randomUUID());

    assertThat(response.status()).isEqualTo(SpeechStatus.FAILED);
    assertThatThrownBy(() -> speechService.npcSpeech(userId, turn.getId()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }
}
