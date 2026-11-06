package com.elsehere.review.service;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class ReviewExpressionEvidenceTest {
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
