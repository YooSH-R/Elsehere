package com.elsehere.ai.service;

import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import java.util.List;
import java.util.function.Supplier;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

/**
 * AI 워커 호출이 오류로 끝나도 호출 기록(ai_requests)이 빠지지 않게 한다. 성공한 호출은 응답을 본 뒤 부르는 쪽이 {@link
 * AiRequestRecorder#record}로 남긴다.
 */
@Component
@RequiredArgsConstructor
public class AiFailureRecorder {

  private final AiRequestRecorder aiRequestRecorder;

  /** 워커를 부른다. 오류로 끝나면 FAILED로 기록하고 그 오류를 그대로 던진다. */
  public <T> T recordIfFails(WorkerTrace trace, AiRequestKind kind, Supplier<T> call) {
    try {
      return call.get();
    } catch (RuntimeException exception) {
      aiRequestRecorder.record(trace, kind, List.of(), AiRequestStatus.FAILED);
      throw exception;
    }
  }
}
