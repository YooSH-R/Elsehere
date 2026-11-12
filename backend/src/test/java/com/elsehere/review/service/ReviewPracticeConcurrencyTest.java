package com.elsehere.review.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.verifyNoInteractions;

import com.elsehere.ai.service.AiGateway;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.service.ConversationService;
import com.elsehere.dialogue.service.DialogueServiceTest;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.context.annotation.Import;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.transaction.support.TransactionTemplate;

/** 서로 다른 트랜잭션의 종료 재시도도 결과와 횟수를 한 번만 기록한다. */
@DialogueServiceTest
@Import(ReviewCardService.class)
@Transactional(propagation = Propagation.NOT_SUPPORTED)
class ReviewPracticeConcurrencyTest {
  @Autowired private ReviewCardService service;
  @Autowired private ConversationService conversations;
  @Autowired private EntityManager entityManager;
  @Autowired private TransactionTemplate transactions;
  @Autowired private AiGateway aiGateway;

  @Test
  void concurrentEndRequestsRecordOneAttemptWithoutCallingAi() throws Exception {
    Seed seed =
        transactions.execute(
            status -> {
              String suffix = UUID.randomUUID().toString().substring(0, 8);
              Long owner =
                  UserTestData.insertUser(
                      entityManager,
                      AuthProvider.GOOGLE,
                      "review-end-concurrent-" + suffix,
                      "Review-" + suffix);
              var source = conversation(entityManager, owner, "CAFE_STAFF");
              source.endByUser(Instant.parse("2026-10-27T01:05:00Z"));
              var feedback = ConversationFeedback.of(source.getId());
              feedback.complete(
                  "음료 주문",
                  """
          {"situationExpressions":[
            {"expression":"Can I get that to go?","meaningKo":"포장해 주세요."},
            {"expression":"Could I get less ice?","meaningKo":"얼음을 줄여 주세요."}
          ]}
          """,
                  "fixture",
                  null);
              entityManager.persist(feedback);
              var card = service.create(owner, source.getId());
              var review =
                  Conversation.start(
                      owner, card.npcId(), InitiatedBy.USER, 1, source.getStartedAt());
              review.attachReviewCard(card.id());
              entityManager.persist(review);
              entityManager.persist(
                  Turn.user(
                      review,
                      1,
                      UUID.randomUUID(),
                      InputType.TEXT,
                      "Can I get that to go?",
                      null,
                      null,
                      false,
                      null,
                      null));
              return new Seed(owner, source.getId(), review.getId(), card.id());
            });

    try {
      try (var executor = Executors.newFixedThreadPool(2)) {
        var start = new CountDownLatch(1);
        var first =
            executor.submit(
                () -> {
                  start.await();
                  return conversations.end(
                      seed.owner(),
                      seed.review(),
                      new EndConversationRequest(UUID.randomUUID(), false));
                });
        var second =
            executor.submit(
                () -> {
                  start.await();
                  return conversations.end(
                      seed.owner(),
                      seed.review(),
                      new EndConversationRequest(UUID.randomUUID(), false));
                });
        start.countDown();

        var firstResult = first.get(30, TimeUnit.SECONDS).reviewResult();
        var secondResult = second.get(30, TimeUnit.SECONDS).reviewResult();
        assertThat(firstResult).isNotNull().isEqualTo(secondResult);
        assertThat(firstResult.practiceCount()).isEqualTo(1);
        assertThat(service.detail(seed.owner(), seed.card()).practiceCount()).isEqualTo(1);
        verifyNoInteractions(aiGateway);
      }
    } finally {
      transactions.executeWithoutResult(
          status -> {
            entityManager
                .createNativeQuery("delete from review_cards where user_id = ?")
                .setParameter(1, seed.owner())
                .executeUpdate();
            entityManager
                .createNativeQuery("delete from conversation_feedback where conversation_id = ?")
                .setParameter(1, seed.conversation())
                .executeUpdate();
            entityManager
                .createNativeQuery("delete from turns where conversation_id = ?")
                .setParameter(1, seed.review())
                .executeUpdate();
            entityManager
                .createNativeQuery("delete from conversations where user_id = ?")
                .setParameter(1, seed.owner())
                .executeUpdate();
            entityManager
                .createNativeQuery("delete from users where id = ?")
                .setParameter(1, seed.owner())
                .executeUpdate();
          });
    }
  }

  private record Seed(Long owner, Long conversation, Long review, Long card) {}
}
