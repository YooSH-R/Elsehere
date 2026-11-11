package com.elsehere.dialogue.service;

import com.elsehere.common.error.BusinessException;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Async;
import org.springframework.stereotype.Component;

/**
 * NPC 대사(첫 인사·대답)를 저장한 직후 뜻 보기(CONV-04)를 미리 만들어 둔다. 학습자가 [뜻 보기]를 누를 때 AI를 기다리지 않게 하려는 것이다.
 *
 * <p>대답 응답을 기다리게 하지 않도록 요청 스레드 밖에서 실행하며, 대사를 저장한 트랜잭션이 끝난 뒤에 불러야 한다. 실패해도 대화에는 영향이 없고, 뜻은 학습자가 [뜻
 * 보기]를 누를 때 다시 만든다.
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class MeaningPreparation {

  private final TurnHelpService turnHelpService;

  /**
   * @param requestId 그 NPC 대사를 만든 요청(CONV-01·03)의 ID. AI 호출 기록에 쓴다
   */
  @Async
  public void prepare(Long userId, Long npcTurnId, UUID requestId) {
    try {
      turnHelpService.meaning(userId, npcTurnId, requestId);
    } catch (BusinessException exception) {
      log.warn(
          "meaning_preparation_failed turn_id={} code={}", npcTurnId, exception.getErrorCode());
    } catch (RuntimeException exception) {
      log.warn(
          "meaning_preparation_failed turn_id={} type={}",
          npcTurnId,
          exception.getClass().getSimpleName());
    }
  }
}
