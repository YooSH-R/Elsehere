package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.reply;
import static com.elsehere.conversation.DialogueTestData.usage;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;

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
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.test.context.TestPropertySource;

// 판정을 기다리지 않고 대답을 먼저 시작하는 것을 설정으로 껐을 때(비용이 늘면 끈다).
@DialogueServiceTest
@TestPropertySource(properties = "elsehere.dialogue.early-reply=false")
class TurnServiceEarlyReplyOffTest {

  private static final String SAID = "Hi there";

  @Autowired private TurnService turnService;

  @Autowired private ConversationService conversationService;

  @Autowired private AiGateway aiGateway;

  @Autowired private EntityManager entityManager;

  private final List<String> received = new ArrayList<>();
  private final TurnEvents events =
      new TurnEvents() {
        @Override
        public void delta(String text) {
          received.add("delta:" + text);
        }

        @Override
        public void audio(int seq, String text, byte[] wav) {}
      };

  @Test
  @DisplayName("설정을 끄면 목표 없는 대화도 판정이 끝난 뒤에 대답을 시작한다")
  void send_freeTalk_startsTheReplyAfterJudging() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long conversationId =
        conversationService
            .start(
                userId,
                new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC))
            .conversationId();
    CountDownLatch replyStarted = new CountDownLatch(1);
    AtomicBoolean startedBeforeJudged = new AtomicBoolean();
    given(aiGateway.judge(any(), any(), any(), any(), eq(SAID)))
        .willAnswer(
            invocation -> {
              startedBeforeJudged.set(replyStarted.await(500, TimeUnit.MILLISECONDS));
              received.add("judged");
              return new JudgeResult(SAID, false, null, null, List.of(usage("intent")));
            });
    given(aiGateway.guard(any(), any(), any(), eq(SAID)))
        .willReturn(new GuardResult(new GuardResult.Guard(false, null), List.of(usage("guard"))));
    given(aiGateway.reply(any(), any(), any(), any(), eq(SAID), any(), anyInt(), any(), any()))
        .willAnswer(
            invocation -> {
              replyStarted.countDown();
              received.add("replied");
              return reply("Hello! Nice day.", null);
            });

    turnService.send(
        userId,
        conversationId,
        new TurnRequest(UUID.randomUUID(), InputType.TEXT, SAID, null),
        events);

    assertThat(startedBeforeJudged).isFalse();
    assertThat(received).containsExactly("judged", "replied");
  }
}
