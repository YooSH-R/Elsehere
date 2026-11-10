package com.elsehere.onboarding.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyDouble;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.Optional;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

@ExtendWith(MockitoExtension.class)
class LevelTestSpeechServiceTest {

  private static final byte[] WAV = {1, 2, 3};

  private final LevelTestCatalog catalog = new LevelTestCatalog();

  @Mock private NpcRepository npcRepository;

  @Mock private AiGateway aiGateway;

  @Test
  @DisplayName("듣기 문항 대사를 그 NPC 목소리로 한 번만 만들고, 다시 달라고 하면 만든 음성을 그대로 돌려준다")
  void speech_synthesizesOnceInTheNpcVoice() {
    LevelTestItem item = catalog.findItem("EN_LT_L2").orElseThrow();
    Npc gus = mock(Npc.class);
    given(gus.getVoiceId()).willReturn("am_adam");
    given(npcRepository.findByCode(item.npcCode())).willReturn(Optional.of(gus));
    given(aiGateway.synthesize(any(), eq(item.script()), eq("am_adam"), anyDouble()))
        .willReturn(WAV);
    LevelTestSpeechService speechService = speechService();

    byte[] first = speechService.speech(1L, "EN_LT_L2");
    byte[] second = speechService.speech(2L, "EN_LT_L2");

    assertThat(first).isEqualTo(WAV);
    assertThat(second).isEqualTo(WAV);
    then(aiGateway).should().synthesize(any(), eq(item.script()), eq("am_adam"), anyDouble());
  }

  @Test
  @DisplayName("말하기 문항이나 없는 문항의 음성은 NOT_FOUND로 응답하고 음성을 만들지 않는다")
  void speechOfNonListeningItem_isNotFound() {
    LevelTestSpeechService speechService = speechService();

    for (String itemCode : new String[] {"EN_LT_S1", "EN_LT_X9"}) {
      assertThatThrownBy(() -> speechService.speech(1L, itemCode))
          .isInstanceOfSatisfying(
              BusinessException.class,
              exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    }
    then(aiGateway).should(never()).synthesize(any(), anyString(), anyString(), anyDouble());
  }

  private LevelTestSpeechService speechService() {
    return new LevelTestSpeechService(catalog, npcRepository, aiGateway);
  }
}
