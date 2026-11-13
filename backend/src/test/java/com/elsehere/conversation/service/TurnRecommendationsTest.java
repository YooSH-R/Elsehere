package com.elsehere.conversation.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import tools.jackson.databind.json.JsonMapper;

class TurnRecommendationsTest {
  private final JsonMapper mapper = JsonMapper.builder().build();

  @ParameterizedTest
  @CsvSource(
      delimiter = '|',
      value = {
        "to go please|Could I get that to go?",
        "no tomato please|Could you leave out the tomato?",
        "two tickets|Could I have two tickets?",
        "aisle seat please|I'd prefer an aisle seat, please."
      })
  void preservesStoredRecommendationsWithoutDependingOnTheLearnersWords(
      String original, String recommendation) {
    Turn turn = user(original, null);
    turn.recordBetterExpressions(mapper.writeValueAsString(List.of(recommendation)));
    assertThat(TurnRecommendations.sentences(turn, mapper)).containsExactly(recommendation);
  }

  @Test
  void omitsOriginalAndDuplicatesButKeepsNamesQuantitiesAndNegationUnchanged() {
    Turn turn = user("no tomato please", false);
    turn.recordBetterExpressions(
        """
        ["No tomato, please.", "Could you leave out the tomato?",
         "COULD YOU LEAVE OUT THE TOMATO!", "I'd like two tickets for Kim, please."]
        """);
    assertThat(TurnRecommendations.sentences(turn, mapper))
        .containsExactly(
            "Could you leave out the tomato?", "I'd like two tickets for Kim, please.");
  }

  @Test
  void blockedAndNpcTurnsNeverBecomeLearnerPracticeTargets() {
    Turn blocked = user("a blocked utterance", true);
    blocked.recordBetterExpressions("[\"A generated sentence.\"]");
    Turn npc = Turn.npc(conversation(), 1, "Hello.", null, Instant.EPOCH);
    npc.recordBetterExpressions("[\"A generated sentence.\"]");
    assertThat(TurnRecommendations.sentences(blocked, mapper)).isEmpty();
    assertThat(TurnRecommendations.sentences(npc, mapper)).isEmpty();
  }

  @Test
  void malformedOrMissingOldRecommendationDataDoesNotBreakSavedFeedback() {
    Turn turn = user("Two tickets.", false);
    assertThat(TurnRecommendations.sentences(turn, mapper)).isEmpty();
    turn.recordBetterExpressions("not an array");
    assertThat(TurnRecommendations.sentences(turn, mapper)).isEmpty();
  }

  private Turn user(String text, Boolean blocked) {
    return Turn.user(
        conversation(), 1, new UUID(0, 1), InputType.TEXT, text, null, null, blocked, null, null);
  }

  private Conversation conversation() {
    return Conversation.start(1L, 1L, InitiatedBy.USER, 1, Instant.EPOCH);
  }
}
