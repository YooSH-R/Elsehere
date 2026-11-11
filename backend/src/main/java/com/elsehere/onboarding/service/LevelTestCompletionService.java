package com.elsehere.onboarding.service;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.LevelTestSpeakingResult;
import com.elsehere.ai.client.WorkerLevelTestSpeakingItem;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.onboarding.dto.LevelTestResultResponse;
import java.util.List;
import java.util.Set;
import java.util.UUID;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

/**
 * 레벨 테스트 완료(ONB-05). 답한 말하기 문항을 AI에 판정받고 그 결과로 테스트를 끝낸다.
 *
 * <p>AI를 기다리는 동안 DB 트랜잭션을 잡지 않도록 답 조회 → AI 판정 → 완료 저장을 나눈다. 판정에 실패하면 테스트는 진행 중으로 남아 다시 완료할 수 있다.
 */
@Service
@RequiredArgsConstructor
public class LevelTestCompletionService {

  private final LevelTestService levelTestService;
  private final AiGateway aiGateway;

  public LevelTestResultResponse complete(Long userId, Long testId) {
    List<LevelTestSpeakingAnswer> answers = levelTestService.speakingAnswers(userId, testId);
    SpeakingJudgement judgement =
        answers.isEmpty() ? SpeakingJudgement.none() : judge(userId, answers);
    return levelTestService.complete(userId, testId, judgement);
  }

  private SpeakingJudgement judge(Long userId, List<LevelTestSpeakingAnswer> answers) {
    List<WorkerLevelTestSpeakingItem> items =
        answers.stream()
            .map(
                answer ->
                    new WorkerLevelTestSpeakingItem(
                        answer.item().itemCode(),
                        answer.item().question(),
                        answer.item().cues(),
                        answer.item().criteria(),
                        answer.answer()))
            .toList();
    LevelTestSpeakingResult result =
        aiGateway.judgeLevelTestSpeaking(new WorkerTrace(UUID.randomUUID(), userId, null), items);
    Set<String> passed =
        result.results().stream()
            .filter(LevelTestSpeakingResult.ItemResult::passed)
            .map(LevelTestSpeakingResult.ItemResult::itemCode)
            .collect(Collectors.toSet());
    return new SpeakingJudgement(passed, result.feedbackKo());
  }
}
