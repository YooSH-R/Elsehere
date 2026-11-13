package com.elsehere.ai.service;

import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.client.WorkerUsage;
import com.elsehere.ai.entity.AiRequest;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.repository.AiRequestRepository;
import java.time.Clock;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;

/**
 * AI 호출 기록(ai_requests)을 남긴다. 호출이 실패해 다음 처리가 취소돼도 비용 근거는 남도록 별도 트랜잭션으로 저장한다. 기록은 {@link AiGateway}만
 * 남긴다.
 *
 * <p>사용자 한도 예약·차감은 아직 하지 않는다(API 명세 1.4).
 */
@Service
@RequiredArgsConstructor
class AiRequestRecorder {

  private final AiRequestRepository aiRequestRepository;
  private final Clock clock;

  @Transactional(propagation = Propagation.REQUIRES_NEW)
  void record(
      WorkerTrace trace, AiRequestKind kind, List<WorkerUsage> usage, AiRequestStatus status) {
    AiRequest request =
        aiRequestRepository
            .findByRequestIdAndKind(trace.requestId(), kind)
            .orElseGet(
                () ->
                    aiRequestRepository.save(
                        AiRequest.start(
                            trace.requestId(), trace.userId(), trace.conversationId(), kind)));
    request.settle(usage, status, clock.instant());
  }
}
