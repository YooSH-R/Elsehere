package com.elsehere.onboarding.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyList;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.LevelTestSpeakingResult;
import com.elsehere.ai.client.WorkerLevelTestSpeakingItem;
import com.elsehere.onboarding.dto.LevelTestResultResponse;
import com.elsehere.onboarding.entity.LevelTestStatus;
import java.util.List;
import java.util.Set;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

// 판정 결과로 레벨을 정하는 규칙은 LevelTestServiceTest에서 DB와 함께 보고, 여기서는 AI 판정을 묻고 넘기는 연결만 본다.
@ExtendWith(MockitoExtension.class)
class LevelTestCompletionServiceTest {

  private final LevelTestCatalog catalog = new LevelTestCatalog();

  @Mock private LevelTestService levelTestService;

  @Mock private AiGateway aiGateway;

  @Test
  @DisplayName("답한 말하기 문항을 상황·말할 정보·판정 기준과 함께 AI에 보내고, 통과한 문항과 피드백으로 완료한다")
  void complete_judgesSpeakingAnswersWithAi() {
    LevelTestItem bag = catalog.findItem("EN_LT_S1").orElseThrow();
    given(levelTestService.speakingAnswers(1L, 9L))
        .willReturn(List.of(new LevelTestSpeakingAnswer(bag, "I left my blue bag here.")));
    given(aiGateway.judgeLevelTestSpeaking(any(), anyList()))
        .willReturn(
            new LevelTestSpeakingResult(
                List.of(new LevelTestSpeakingResult.ItemResult("EN_LT_S1", true)),
                "설명을 잘했어요.",
                List.of()));
    LevelTestResultResponse done =
        new LevelTestResultResponse(9L, LevelTestStatus.DONE, 4, 1, 3, 1, "설명을 잘했어요.");
    given(levelTestService.complete(1L, 9L, new SpeakingJudgement(Set.of("EN_LT_S1"), "설명을 잘했어요.")))
        .willReturn(done);

    LevelTestResultResponse result = completionService().complete(1L, 9L);

    assertThat(result).isEqualTo(done);
    @SuppressWarnings("unchecked")
    ArgumentCaptor<List<WorkerLevelTestSpeakingItem>> sent = ArgumentCaptor.forClass(List.class);
    then(aiGateway).should().judgeLevelTestSpeaking(any(), sent.capture());
    assertThat(sent.getValue())
        .containsExactly(
            new WorkerLevelTestSpeakingItem(
                "EN_LT_S1",
                bag.question(),
                bag.cues(),
                bag.criteria(),
                "I left my blue bag here."));
  }

  @Test
  @DisplayName("답한 말하기 문항이 없으면 AI를 부르지 않고 말하기 통과 없이 완료한다")
  void completeWithoutSpeakingAnswers_skipsAi() {
    given(levelTestService.speakingAnswers(1L, 9L)).willReturn(List.of());
    LevelTestResultResponse done =
        new LevelTestResultResponse(9L, LevelTestStatus.DONE, 4, 1, 4, 0, null);
    given(levelTestService.complete(1L, 9L, SpeakingJudgement.none())).willReturn(done);

    assertThat(completionService().complete(1L, 9L)).isEqualTo(done);
    then(aiGateway).should(never()).judgeLevelTestSpeaking(any(), anyList());
  }

  private LevelTestCompletionService completionService() {
    return new LevelTestCompletionService(levelTestService, aiGateway);
  }
}
