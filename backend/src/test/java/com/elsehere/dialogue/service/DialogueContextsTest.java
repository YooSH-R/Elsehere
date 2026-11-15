package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.ai.client.WorkerLine;
import com.elsehere.conversation.DialogueTestData;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.List;
import java.util.stream.IntStream;
import org.hibernate.SessionFactory;
import org.hibernate.stat.Statistics;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import org.springframework.beans.factory.annotation.Autowired;

@DialogueDbTest
class DialogueContextsTest {

  @Autowired private DialogueContexts contexts;
  @Autowired private EntityManager entityManager;

  @ParameterizedTest
  @CsvSource({"2147483647,21,60", "45,6,45"})
  void history_loadsOnlyTheLast40TurnsInOrder(int until, int first, int last) {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "history-user");
    Conversation conversation =
        DialogueTestData.conversation(entityManager, userId, "TOWN_PASSERBY");
    Long conversationId = conversation.getId();
    for (int seq = 1; seq <= 60; seq++) {
      DialogueTestData.textTurn(entityManager, conversation, seq, "line " + seq);
    }
    entityManager.flush();
    entityManager.clear();
    Statistics statistics =
        entityManager.getEntityManagerFactory().unwrap(SessionFactory.class).getStatistics();
    statistics.setStatisticsEnabled(true);
    statistics.clear();
    try {
      List<WorkerLine> history =
          until == Integer.MAX_VALUE
              ? contexts.history(conversationId)
              : contexts.historyUntil(conversationId, until);

      assertThat(history)
          .extracting(WorkerLine::text)
          .containsExactlyElementsOf(
              IntStream.rangeClosed(first, last).mapToObj(seq -> "line " + seq).toList());
      assertThat(statistics.getEntityLoadCount()).isEqualTo(40);
    } finally {
      statistics.setStatisticsEnabled(false);
    }
  }
}
