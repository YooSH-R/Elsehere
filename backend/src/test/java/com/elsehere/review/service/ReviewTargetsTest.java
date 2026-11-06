package com.elsehere.review.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.feedback.entity.FeedbackReviews.SituationExpression;
import java.util.Arrays;
import java.util.List;
import org.junit.jupiter.api.Test;

class ReviewTargetsTest {
  @Test
  void selectKeepsThreeDistinctExpressionsAndTheirOriginalLearningContext() {
    var first = expression("Can I get that to go?", "포장해 주실 수 있나요?");
    var second = expression("Could I get less ice?", "얼음을 조금만 넣어 주세요.");
    var third = expression("Have a good one!", "좋은 하루 보내세요!");

    var selected =
        ReviewTargets.select(
            List.of(
                first, second, first, third, expression("Can I pay by card?", "카드로 결제해도 될까요?")));

    assertThat(selected)
        .extracting(target -> target.expression())
        .containsExactly(first.expression(), second.expression(), third.expression());
    assertThat(selected.getFirst().usageKo()).isEqualTo(first.usageKo());
    assertThat(selected.getFirst().exampleEn()).isEqualTo(first.exampleEn());
    assertThat(selected.getFirst().exampleKo()).isEqualTo(first.exampleKo());
  }

  @Test
  void missingOrInvalidExpressionsDoNotBecomeTargets() {
    assertThat(ReviewTargets.select(null)).isEmpty();
    var selected =
        ReviewTargets.select(
            Arrays.asList(
                null,
                expression("", "뜻"),
                expression("hello", " "),
                expression("Can I get that to go?", "포장해 주세요."),
                expression(" CAN I  GET THAT TO GO? ", "같은 표현"),
                expression("Could I get less ice?", "얼음을 줄여 주세요.")));
    assertThat(selected)
        .extracting(target -> target.expression())
        .containsExactly("Can I get that to go?", "Could I get less ice?");
  }

  private static SituationExpression expression(String text, String meaning) {
    return new SituationExpression(text, meaning, "주문할 때 써요.", text, meaning);
  }
}
