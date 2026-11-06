package com.elsehere.review.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.verifyNoInteractions;

import com.elsehere.ai.client.AiGateway;
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

/** 2개 요청이 실제로 서로 다른 트랜잭션에서 같은 원본을 저장하는 경우. */
@DialogueServiceTest
@Import(ReviewCardService.class)
@Transactional(propagation = Propagation.NOT_SUPPORTED)
class ReviewCardConcurrencyTest {
  @Autowired private ReviewCardService service;
  @Autowired private EntityManager entityManager;
  @Autowired private TransactionTemplate transactions;
  @Autowired private AiGateway aiGateway;

  @Test
  void concurrentSavesReturnOneCardWithoutCallingAi() throws Exception {
    Seed seed =
        transactions.execute(
            status -> {
              String suffix = UUID.randomUUID().toString().substring(0, 8);
              Long owner =
                  UserTestData.insertUser(
                      entityManager,
                      AuthProvider.GOOGLE,
                      "review-concurrent-" + suffix,
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
              return new Seed(owner, source.getId());
            });

    try {
      try (var executor = Executors.newFixedThreadPool(2)) {
        var start = new CountDownLatch(1);
        var first =
            executor.submit(
                () -> {
                  start.await();
                  return service.create(seed.owner(), seed.conversation());
                });
        var second =
            executor.submit(
                () -> {
                  start.await();
                  return service.create(seed.owner(), seed.conversation());
                });
        start.countDown();

        assertThat(first.get(30, TimeUnit.SECONDS).id())
            .isEqualTo(second.get(30, TimeUnit.SECONDS).id());
        assertThat(service.list(seed.owner(), null, null, 20).items()).hasSize(1);
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
                .createNativeQuery("delete from conversations where id = ?")
                .setParameter(1, seed.conversation())
                .executeUpdate();
            entityManager
                .createNativeQuery("delete from users where id = ?")
                .setParameter(1, seed.owner())
                .executeUpdate();
          });
    }
  }

  private record Seed(Long owner, Long conversation) {}
}
