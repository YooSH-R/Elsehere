package com.elsehere.ai.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.BDDMockito.given;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.HintResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.MeaningResult;
import com.elsehere.ai.client.MemoryCardResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.NpcReplyChunk;
import com.elsehere.ai.client.NpcReplyListener;
import com.elsehere.ai.client.RelationshipChange;
import com.elsehere.ai.client.SttResult;
import com.elsehere.ai.client.SuggestResult;
import com.elsehere.ai.client.WorkerFeedback;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.client.WorkerUsage;
import com.elsehere.ai.entity.AiRequest;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.repository.AiRequestRepository;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.util.List;
import java.util.UUID;
import java.util.function.BiFunction;
import java.util.stream.Stream;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.TransactionDefinition;
import org.springframework.transaction.support.TransactionTemplate;

// 호출 기록은 별도 트랜잭션으로 커밋되어 테스트가 끝나도 남으므로 요청마다 새 요청 ID로 찾는다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  AiGateway.class,
  AiRequestRecorder.class
})
class AiGatewayTest {

  private static final List<WorkerUsage> USAGE =
      List.of(new WorkerUsage("npc", "gpt-test", 100, 20, null, null, null));

  // 판정을 기다리지 않고 먼저 시작했다가 판정 뒤에 버린 대답의 조각 받기
  private static final NpcReplyListener DISCARDED =
      new NpcReplyListener() {
        @Override
        public void onDelta(String text) {}

        @Override
        public void onChunk(NpcReplyChunk chunk) {}

        @Override
        public boolean discarded() {
          return true;
        }
      };

  @MockitoBean private AiWorkerClient aiWorkerClient;

  @Autowired private AiGateway aiGateway;

  @Autowired private AiRequestRepository aiRequestRepository;

  @Autowired private PlatformTransactionManager transactionManager;

  private final WorkerTrace trace = new WorkerTrace(UUID.randomUUID(), 7L, 70L);

  static Stream<Arguments> recordedCalls() {
    return Stream.of(
        call(
            "의도 판정",
            AiRequestKind.JUDGE,
            (client, trace) -> client.judge(trace, null, null, List.of(), "hi"),
            (gateway, trace) -> gateway.judge(trace, null, null, List.of(), "hi"),
            new JudgeResult("hi", false, null, null, USAGE)),
        call(
            "무례 판정",
            AiRequestKind.GUARD,
            (client, trace) -> client.guard(trace, null, List.of(), "hi"),
            (gateway, trace) -> gateway.guard(trace, null, List.of(), "hi"),
            new GuardResult(new GuardResult.Guard(false, null), USAGE)),
        call(
            "NPC 대답",
            AiRequestKind.NPC,
            (client, trace) ->
                client.reply(
                    trace, null, null, List.of(), "hi", "", 1, null, NpcReplyListener.NONE),
            (gateway, trace) -> gateway.reply(trace, null, null, List.of(), "hi", "", 1, null),
            new NpcReply("Hello!", "audio-1", null, USAGE)),
        call(
            "음성 인식",
            AiRequestKind.STT,
            (client, trace) ->
                client.transcribe(trace, new byte[] {1}, "audio/webm", null, List.of()),
            (gateway, trace) ->
                gateway.transcribe(trace, new byte[] {1}, "audio/webm", null, List.of()),
            new SttResult("hi", 1.5, USAGE)),
        call(
            "뜻 보기",
            AiRequestKind.MEANING,
            (client, trace) -> client.meaning(trace, null, "Hello!", 1),
            (gateway, trace) -> gateway.meaning(trace, null, "Hello!", 1),
            new MeaningResult("안녕하세요!", List.of(), USAGE)),
        call(
            "힌트",
            AiRequestKind.HINT,
            (client, trace) -> client.hint(trace, null, List.of(), "", 1),
            (gateway, trace) -> gateway.hint(trace, null, List.of(), "", 1),
            new HintResult(List.of(new HintResult.Phrase("Hi.", "안녕.")), List.of(), USAGE)),
        call(
            "추천 단어",
            AiRequestKind.HINT,
            (client, trace) -> client.suggest(trace, null, List.of(), "", 1),
            (gateway, trace) -> gateway.suggest(trace, null, List.of(), "", 1),
            new SuggestResult(List.of("room"), USAGE)),
        call(
            "음성 합성",
            AiRequestKind.TTS,
            (client, trace) -> client.synthesize(trace, "Hello!", "voice", 1.0),
            (gateway, trace) -> gateway.synthesize(trace, "Hello!", "voice", 1.0),
            new byte[] {1}),
        call(
            "기억 카드",
            AiRequestKind.MEMORY,
            (client, trace) -> client.memoryCard(trace, null, "met", List.of(), null),
            (gateway, trace) -> gateway.memoryCard(trace, null, "met", List.of(), null),
            new MemoryCardResult("{}", USAGE)),
        call(
            "피드백",
            AiRequestKind.FEEDBACK,
            (client, trace) -> client.feedback(trace, null),
            (gateway, trace) -> gateway.feedback(trace, null),
            completedFeedback()));
  }

  @ParameterizedTest(name = "{0}")
  @MethodSource("recordedCalls")
  @DisplayName("워커가 답하면 결과를 그대로 돌려주고 호출 종류·사용자·대화·사용량을 OK로 남긴다")
  void success_recordsOk(
      String name,
      AiRequestKind kind,
      BiFunction<AiWorkerClient, WorkerTrace, Object> worker,
      BiFunction<AiGateway, WorkerTrace, Object> gateway,
      Object result) {
    given(worker.apply(aiWorkerClient, trace)).willReturn(result);

    assertThat(gateway.apply(aiGateway, trace)).isSameAs(result);

    AiRequest request = request(kind);
    assertThat(request.getStatus()).isEqualTo(AiRequestStatus.OK);
    assertThat(request.getUserId()).isEqualTo(7L);
    assertThat(request.getConversationId()).isEqualTo(70L);
  }

  @ParameterizedTest(name = "{0}")
  @MethodSource("recordedCalls")
  @DisplayName("워커 오류로 끝난 호출은 그 종류로 FAILED를 남기고 오류를 그대로 던진다")
  void workerError_recordsFailedAndRethrows(
      String name,
      AiRequestKind kind,
      BiFunction<AiWorkerClient, WorkerTrace, Object> worker,
      BiFunction<AiGateway, WorkerTrace, Object> gateway,
      Object result) {
    given(worker.apply(aiWorkerClient, trace))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    assertThatThrownBy(() -> gateway.apply(aiGateway, trace))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.AI_TEMPORARILY_UNAVAILABLE);

    assertThat(request(kind).getStatus()).isEqualTo(AiRequestStatus.FAILED);
  }

  @Test
  @DisplayName("사용량은 모델·입력/출력 토큰·음성 길이로 남긴다")
  void success_recordsUsage() {
    given(aiWorkerClient.transcribe(trace, new byte[] {1}, "audio/webm", null, List.of()))
        .willReturn(
            new SttResult(
                "hi",
                1.5,
                List.of(new WorkerUsage("stt", "whisper", null, null, 1.5, null, null))));

    aiGateway.transcribe(trace, new byte[] {1}, "audio/webm", null, List.of());

    AiRequest request = request(AiRequestKind.STT);
    assertThat(request.getModel()).isEqualTo("whisper");
    assertThat(request.getAudioMs()).isEqualTo(1500);
  }

  @Test
  @DisplayName("NPC 대답 스트림이 오류로 끝나면 그때까지 쓴 사용량으로 FAILED를 남기고 워커 오류 코드를 API 오류 코드로 바꿔 던진다")
  void reply_streamError_recordsFailedWithUsageAndThrowsMappedCode() {
    given(
            aiWorkerClient.reply(
                trace, null, null, List.of(), "hi", "", 1, null, NpcReplyListener.NONE))
        .willReturn(new NpcReply(null, null, "provider_limit", USAGE));

    assertThatThrownBy(() -> aiGateway.reply(trace, null, null, List.of(), "hi", "", 1, null))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.AI_RATE_LIMITED);

    AiRequest request = request(AiRequestKind.NPC);
    assertThat(request.getStatus()).isEqualTo(AiRequestStatus.FAILED);
    assertThat(request.getInputTokens()).isEqualTo(100);
  }

  @Test
  @DisplayName("부른 쪽이 버린 NPC 대답은 끝까지 와도 같은 요청 ID로 다시 부른 대답의 기록을 덮지 않도록 남기지 않는다")
  void reply_discardedByCaller_isNotRecorded() {
    NpcReply reply = new NpcReply("Hello!", "audio-1", null, USAGE);
    given(aiWorkerClient.reply(trace, null, null, List.of(), "hi", "", 1, null, DISCARDED))
        .willReturn(reply);

    assertThat(aiGateway.reply(trace, null, null, List.of(), "hi", "", 1, null, DISCARDED))
        .isSameAs(reply);

    assertThat(aiRequestRepository.findByRequestIdAndKind(trace.requestId(), AiRequestKind.NPC))
        .isEmpty();
  }

  @Test
  @DisplayName("부른 쪽이 버린 NPC 대답이 오류로 끝나도 기록하지 않고 오류만 그대로 던진다")
  void reply_discardedByCallerThenFails_isNotRecorded() {
    BusinessException failure = new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE);
    given(aiWorkerClient.reply(trace, null, null, List.of(), "hi", "", 1, null, DISCARDED))
        .willThrow(failure);

    assertThatThrownBy(
            () -> aiGateway.reply(trace, null, null, List.of(), "hi", "", 1, null, DISCARDED))
        .isSameAs(failure);

    assertThat(aiRequestRepository.findByRequestIdAndKind(trace.requestId(), AiRequestKind.NPC))
        .isEmpty();
  }

  @Test
  @DisplayName("힌트에 추천 문장이 하나도 없으면 FAILED를 남기고 AI_RESPONSE_INVALID다")
  void hint_withoutSentence_recordsFailedAndThrowsInvalid() {
    given(aiWorkerClient.hint(trace, null, List.of(), "", 1))
        .willReturn(new HintResult(List.of(), List.of(), USAGE));

    assertThatThrownBy(() -> aiGateway.hint(trace, null, List.of(), "", 1))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.AI_RESPONSE_INVALID);

    assertThat(request(AiRequestKind.HINT).getStatus()).isEqualTo(AiRequestStatus.FAILED);
  }

  @Test
  @DisplayName("추천 단어가 하나도 없으면 힌트 호출로 FAILED를 남기고 AI_RESPONSE_INVALID다")
  void suggest_withoutWords_recordsFailedAndThrowsInvalid() {
    given(aiWorkerClient.suggest(trace, null, List.of(), "", 1))
        .willReturn(new SuggestResult(List.of(), USAGE));

    assertThatThrownBy(() -> aiGateway.suggest(trace, null, List.of(), "", 1))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.AI_RESPONSE_INVALID);

    assertThat(request(AiRequestKind.HINT).getStatus()).isEqualTo(AiRequestStatus.FAILED);
  }

  @Test
  @DisplayName("피드백이 completed가 아니면(평가할 발화 부족·생성 실패) 결과는 그대로 돌려주고 FAILED를 남긴다")
  void feedback_notCompleted_returnsResultAndRecordsFailed() {
    WorkerFeedback insufficient =
        new WorkerFeedback("insufficient_evidence", "NO_USABLE_USER_TURNS", null, null, USAGE);
    given(aiWorkerClient.feedback(trace, null)).willReturn(insufficient);

    assertThat(aiGateway.feedback(trace, null)).isSameAs(insufficient);

    assertThat(request(AiRequestKind.FEEDBACK).getStatus()).isEqualTo(AiRequestStatus.FAILED);
  }

  @Test
  @DisplayName("모델을 부르지 않는 관계 갱신과 음성 파일 받기는 기록하지 않는다")
  void relationshipAndAudio_areNotRecorded() {
    RelationshipChange change = new RelationshipChange(1);
    given(aiWorkerClient.relationship(trace, null, null, List.of())).willReturn(change);
    given(aiWorkerClient.audio(trace, "audio-1")).willReturn(new byte[] {1});

    assertThat(aiGateway.relationship(trace, null, null, List.of())).isSameAs(change);
    assertThat(aiGateway.audio(trace, "audio-1")).containsExactly(1);

    assertThat(aiRequestRepository.findAll())
        .noneMatch(request -> request.getRequestId().equals(trace.requestId()));
  }

  @Test
  @DisplayName("부른 쪽의 트랜잭션이 취소돼도 호출 기록은 남는다")
  void record_survivesCallerRollback() {
    given(aiWorkerClient.judge(trace, null, null, List.of(), "hi"))
        .willReturn(new JudgeResult("hi", false, null, null, USAGE));
    TransactionTemplate rollingBack = new TransactionTemplate(transactionManager);
    rollingBack.setPropagationBehavior(TransactionDefinition.PROPAGATION_REQUIRES_NEW);

    rollingBack.executeWithoutResult(
        status -> {
          aiGateway.judge(trace, null, null, List.of(), "hi");
          status.setRollbackOnly();
        });

    assertThat(request(AiRequestKind.JUDGE).getStatus()).isEqualTo(AiRequestStatus.OK);
  }

  @Test
  @DisplayName("어드민이 실행한 발음 만들기처럼 사용자가 없는 호출도 사용자 없이 기록한다")
  void callWithoutUser_isRecordedWithoutUser() {
    WorkerTrace adminTrace = new WorkerTrace(UUID.randomUUID(), null, null);
    given(aiWorkerClient.synthesize(adminTrace, "sofa", "af_heart", 1.0))
        .willReturn(new byte[] {1});

    aiGateway.synthesize(adminTrace, "sofa", "af_heart", 1.0);

    AiRequest request =
        aiRequestRepository
            .findByRequestIdAndKind(adminTrace.requestId(), AiRequestKind.TTS)
            .orElseThrow();
    assertThat(request.getStatus()).isEqualTo(AiRequestStatus.OK);
    assertThat(request.getUserId()).isNull();
  }

  private static Arguments call(
      String name,
      AiRequestKind kind,
      BiFunction<AiWorkerClient, WorkerTrace, Object> worker,
      BiFunction<AiGateway, WorkerTrace, Object> gateway,
      Object result) {
    return Arguments.of(name, kind, worker, gateway, result);
  }

  private static WorkerFeedback completedFeedback() {
    return new WorkerFeedback(
        "completed",
        null,
        new WorkerFeedback.Result(
            null, List.of(), List.of(), null, List.of(), List.of(), List.of()),
        "{}",
        USAGE);
  }

  private AiRequest request(AiRequestKind kind) {
    return aiRequestRepository.findByRequestIdAndKind(trace.requestId(), kind).orElseThrow();
  }
}
