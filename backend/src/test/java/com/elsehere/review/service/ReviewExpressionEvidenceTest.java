package com.elsehere.review.service;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class ReviewExpressionEvidenceTest {
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
