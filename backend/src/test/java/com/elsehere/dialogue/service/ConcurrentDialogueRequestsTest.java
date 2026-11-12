package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.reply;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.times;

import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.Callable;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicInteger;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.test.annotation.DirtiesContext;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.transaction.support.TransactionTemplate;

@DialogueServiceTest
@Transactional(propagation = Propagation.NOT_SUPPORTED)
@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_CLASS)
class ConcurrentDialogueRequestsTest {

  @Autowired private ConversationService conversations;
  @Autowired private TurnService turns;
  @Autowired private AiGateway ai;
  @Autowired private EntityManager entityManager;
  @Autowired private TransactionTemplate transactions;

  @ParameterizedTest
  @ValueSource(booleans = {true, false})
  void repeatedRequestWhileAiIsRunning_generatesOnlyOnce(boolean starting) throws Exception {
    Long userId =
        transactions.execute(
            status ->
                UserTestData.insertUser(
                    entityManager,
                    AuthProvider.GOOGLE,
                    "concurrent-" + starting,
                    "Concurrent" + starting));
    CountDownLatch aiStarted = new CountDownLatch(1);
    CountDownLatch finishAi = new CountDownLatch(1);
    AtomicInteger replies = new AtomicInteger();
    given(ai.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willAnswer(
            invocation -> {
              if (replies.incrementAndGet() == 1) {
                aiStarted.countDown();
                assertThat(finishAi.await(5, TimeUnit.SECONDS)).isTrue();
              }
              return reply("Hello there!", null);
            });
    given(ai.judge(any(), any(), any(), any(), anyString()))
        .willReturn(new JudgeResult("Hi", false, null, null, List.of()));
    given(ai.guard(any(), any(), any(), anyString()))
        .willReturn(new GuardResult(new GuardResult.Guard(false, null), List.of()));
    UUID requestId = UUID.randomUUID();
    Callable<?> request;
    if (starting) {
      StartConversationRequest body =
          new StartConversationRequest(requestId, "CAFE_STAFF", InitiatedBy.USER);
      request = () -> conversations.start(userId, body);
    } else {
      Long conversationId =
          conversations
              .start(
                  userId,
                  new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC))
              .conversationId();
      TurnRequest body = new TurnRequest(requestId, InputType.TEXT, "Hi", null);
      request = () -> turns.send(userId, conversationId, body);
    }

    try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
      var first = executor.submit(request);
      try {
        assertThat(aiStarted.await(5, TimeUnit.SECONDS)).isTrue();
        var second = executor.submit(request);
        try {
          second.get(1, TimeUnit.SECONDS);
        } catch (TimeoutException expected) {
          // 중복 요청은 먼저 시작한 AI 작업의 저장이 끝날 때까지 기다릴 수 있다.
        } finally {
          finishAi.countDown();
        }
        assertThat(second.get(5, TimeUnit.SECONDS))
            .usingRecursiveComparison()
            .isEqualTo(first.get(5, TimeUnit.SECONDS));
        assertThat(replies.get()).isEqualTo(1);
        if (!starting) {
          then(ai).should(times(1)).judge(any(), any(), any(), any(), anyString());
          then(ai).should(times(1)).guard(any(), any(), any(), anyString());
        }
      } finally {
        finishAi.countDown();
      }
    }
  }
}
