package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.usage;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.times;

import com.elsehere.ai.client.MeaningResult;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.dialogue.dto.MeaningResponse;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.transaction.support.TransactionTemplate;

/** 대답 직후 미리 만드는 뜻 보기와 학습자의 [뜻 보기]가 실제로 서로 다른 트랜잭션에서 겹치는 경우. */
@DialogueServiceTest
@Transactional(propagation = Propagation.NOT_SUPPORTED)
class MeaningConcurrencyTest {

  private static final String NPC_LINE = "Great! For here or to go?";
  private static final MeaningResult MEANING =
      new MeaningResult(
          "좋아요! 매장에서 드실 건가요, 가져가실 건가요?",
          List.of(new MeaningResult.Glossary("to go", "포장")),
          List.of(usage("translate")));

  @Autowired private TurnHelpService turnHelpService;
  @Autowired private AiGateway aiGateway;
  @Autowired private EntityManager entityManager;
  @Autowired private TransactionTemplate transactions;

  @Test
  @DisplayName("같은 대사의 뜻을 만드는 중에 다시 요청하면 AI를 한 번 더 부르지 않고 그 결과를 기다린다")
  void meaning_requestedWhileGenerating_waitsForSameResult() throws Exception {
    Seed seed =
        transactions.execute(
            status -> {
              String suffix = UUID.randomUUID().toString().substring(0, 8);
              Long owner =
                  UserTestData.insertUser(
                      entityManager,
                      AuthProvider.GOOGLE,
                      "meaning-concurrent-" + suffix,
                      "Meaning-" + suffix);
              var conversation = conversation(entityManager, owner, "CAFE_STAFF");
              var turn = npcTurn(entityManager, conversation, 1, NPC_LINE);
              return new Seed(owner, conversation.getId(), turn.getId());
            });
    CountDownLatch generating = new CountDownLatch(1);
    CountDownLatch release = new CountDownLatch(1);
    given(aiGateway.meaning(any(), any(), anyString(), anyInt()))
        .willAnswer(
            invocation -> {
              generating.countDown();
              assertThat(release.await(10, TimeUnit.SECONDS)).isTrue();
              return MEANING;
            });

    try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
      var prepared =
          executor.submit(
              () -> turnHelpService.meaning(seed.owner(), seed.turn(), UUID.randomUUID()));
      assertThat(generating.await(10, TimeUnit.SECONDS)).isTrue();
      var opened =
          executor.submit(
              () -> turnHelpService.meaning(seed.owner(), seed.turn(), UUID.randomUUID()));
      // 두 번째 요청이 잠금 앞에서 기다리기 시작할 시간을 준다.
      Thread.sleep(200);
      release.countDown();

      MeaningResponse first = prepared.get(30, TimeUnit.SECONDS);
      MeaningResponse second = opened.get(30, TimeUnit.SECONDS);
      assertThat(second).isEqualTo(first);
      then(aiGateway).should(times(1)).meaning(any(), any(), anyString(), anyInt());
    } finally {
      transactions.executeWithoutResult(
          status -> {
            entityManager
                .createNativeQuery("delete from turns where conversation_id = ?")
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

  private record Seed(Long owner, Long conversation, Long turn) {}
}
