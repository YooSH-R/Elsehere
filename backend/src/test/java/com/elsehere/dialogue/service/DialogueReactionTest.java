package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

class DialogueReactionTest {
  @ParameterizedTest
  @CsvSource({"0,rude_warning", "1,rude_repeated", "2,rude_final"})
  void confirmedRudenessUsesTheCurrentOccurrence(int previous, String expected) {
    assertThat(DialogueReaction.select(true, previous, List.of("COMPLIMENT"))).isEqualTo(expected);
  }

  @Test
  void ordinaryReplyDoesNotBecomeAngryOnlyBecauseEarlierTurnsWereRude() {
    assertThat(DialogueReaction.select(false, 2, List.of("SMALL_TALK"))).isNull();
  }

  @Test
  void confirmedComplimentIsDifferentFromOrdinaryThanks() {
    assertThat(DialogueReaction.select(false, 0, List.of("COMPLIMENT"))).isEqualTo("compliment");
    assertThat(DialogueReaction.select(false, 0, List.of("GREETING"))).isNull();
  }

  @Test
  void apologyAfterRudenessTakesPriorityOverPraiseInTheSameMessage() {
    assertThat(DialogueReaction.select(false, 2, List.of("APOLOGY", "COMPLIMENT")))
        .isEqualTo("apology_after_rude");
    assertThat(DialogueReaction.select(false, 0, List.of("APOLOGY"))).isNull();
  }
}
