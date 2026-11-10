package com.elsehere.review.service;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class ReviewExpressionEvidenceTest {
  @Test
  void recognizesFarewellContractionsInTheLastSpokenSentence() {
    assertThat(
            ReviewExpressionEvidence.matches(
                "You're a lifesaver!", "Thank you. You are a lifesaver!"))
        .isTrue();
    assertThat(ReviewExpressionEvidence.matches("That's the one!", "Yes, that is the one."))
        .isTrue();
    assertThat(ReviewExpressionEvidence.matches("It's on me.", "It is on me.")).isTrue();
    assertThat(ReviewExpressionEvidence.matches("You're a lifesaver!", "You are not a lifesaver."))
        .isFalse();
    assertThat(
            ReviewExpressionEvidence.matches(
                "You're a lifesaver!", "The example is \"You are a lifesaver.\""))
        .isFalse();
  }

  @Test
  void exactMultiSentenceTargetsAndPoliteRefusalsAreRecognized() {
    assertThat(ReviewExpressionEvidence.matches("No, thanks. I'm good.", "No, thanks. I'm good."))
        .isTrue();
    assertThat(ReviewExpressionEvidence.matches("I'm good.", "No, I'm good, thanks.")).isTrue();
    assertThat(
            ReviewExpressionEvidence.matches("It's pouring out there!", "it's pouring out there"))
        .isTrue();
  }

  @Test
  void quotedExamplesAreNotEvidenceButUnquotedContractionsStillAre() {
    assertThat(
            ReviewExpressionEvidence.matches(
                "Can I get that to go?", "The example is \"Could I get that to go?\"."))
        .isFalse();
    assertThat(
            ReviewExpressionEvidence.matches(
                "I'd like a coffee.", "The example is 'I'd like a coffee'."))
        .isFalse();
    assertThat(
            ReviewExpressionEvidence.matches(
                "Can I get that to go?", "Lily said “Can I get that to go?”"))
        .isFalse();
    assertThat(ReviewExpressionEvidence.matches("I'd like a coffee.", "I'd like a coffee."))
        .isTrue();
  }

  @Test
  void findsConservativeRequestAndContractionVariantsInActualUtterances() {
    assertThat(
            ReviewExpressionEvidence.matches(
                "Can I get that to go?", "Could I please get that to go?"))
        .isTrue();
    assertThat(
            ReviewExpressionEvidence.matches(
                "I'd like a coffee.", "I would like a coffee, please."))
        .isTrue();
  }

  @Test
  void doesNotTurnNegationOrDifferentWordsIntoUseEvidence() {
    assertThat(ReviewExpressionEvidence.matches("I'm good.", "I am not good.")).isFalse();
    assertThat(
            ReviewExpressionEvidence.matches(
                "Could you hold my luggage?", "Don't hold my luggage."))
        .isFalse();
    assertThat(
            ReviewExpressionEvidence.matches("Can I get that to go?", "Can I get that to goatee?"))
        .isFalse();
    assertThat(ReviewExpressionEvidence.matches("I'd like a coffee.", "I had a coffee.")).isFalse();
  }
}
