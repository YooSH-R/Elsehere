package com.elsehere.ai.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoMoreInteractions;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.PerformanceIntentResult;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.client.WorkerUsage;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.common.error.BusinessException;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class PerformanceIntentGatewayTest {
  private final AiWorkerClient worker = mock(AiWorkerClient.class);
  private final AiRequestRecorder recorder = mock(AiRequestRecorder.class);
  private final AiGateway gateway = new AiGateway(worker, recorder);
  private final WorkerTrace trace = new WorkerTrace(UUID.randomUUID(), 7L, 70L);
  private final List<WorkerUsage> usage =
      List.of(new WorkerUsage("performance_intent", "fake", 80, 8, null, null, null));

  @Test
  void successfulClassificationRecordsOnlyIntentUsage() {
    when(worker.performanceIntent(trace, "Encore?"))
        .thenReturn(new PerformanceIntentResult("REPLAY_RAP", usage));
    assertThat(gateway.performanceIntent(trace, "Encore?").intent()).isEqualTo("REPLAY_RAP");
    verify(worker).performanceIntent(trace, "Encore?");
    verify(recorder).record(trace, AiRequestKind.JUDGE, usage, AiRequestStatus.OK);
    verifyNoMoreInteractions(worker, recorder);
  }

  @Test
  void invalidIntentRecordsFailureWithoutGeneratingSpeech() {
    when(worker.performanceIntent(trace, "Encore?"))
        .thenReturn(new PerformanceIntentResult("UNKNOWN", usage));
    assertThatThrownBy(() -> gateway.performanceIntent(trace, "Encore?"))
        .isInstanceOf(BusinessException.class);
    verify(worker).performanceIntent(trace, "Encore?");
    verify(recorder).record(trace, AiRequestKind.JUDGE, usage, AiRequestStatus.FAILED);
    verifyNoMoreInteractions(worker, recorder);
  }
}
