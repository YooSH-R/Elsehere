package com.elsehere.review.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.feedback.entity.FeedbackReviews.SituationExpression;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.review.dto.ReviewTarget;
import java.time.Instant;
import java.util.Arrays;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.json.JsonMapper;

class ReviewTargetsTest {
  @Test
  void staffLinesDoNotBecomeLearnerTargetsButSharedFarewellsRemain() {
    var selected =
        ReviewTargets.select(
            List.of(),
            List.of(
                expression("You're all set.", "이제 다 됐어요."),
                new SituationExpression(
                    "What can I get you?",
                    "무엇을 드릴까요?",
                    "직원이 주문을 받을 때 써요.",
                    "What can I get you?",
                    "무엇을 드릴까요?"),
                expression("I think I left my bag here.", "여기에 가방을 두고 간 것 같아요."),
                expression("Have a good one!", "좋은 하루 보내세요!")));
    assertThat(selected)
        .extracting(target -> target.expression())
        .containsExactly("I think I left my bag here.", "Have a good one!");
  }

  @Test
  void selectKeepsThreeDistinctExpressionsAndTheirOriginalLearningContext() {
    var first = expression("Can I get that to go?", "포장해 주실 수 있나요?");
    var second = expression("Could I get less ice?", "얼음을 조금만 넣어 주세요.");
    var third = expression("Have a good one!", "좋은 하루 보내세요!");

    var selected =
        ReviewTargets.select(
            List.of(),
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
    assertThat(ReviewTargets.select(List.of(), null)).isEmpty();
    var selected =
        ReviewTargets.select(
            List.of(),
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

  // 시연 주문을 다시 연습할 때는 이번에 써 볼 표현을 주문 조건 차례(버거 → 재료 → 사이드·음료 → 매장·포장)로 둔다.
  // 같은 조건 안에서는 원래 차례를 지키고, 어느 조건에도 맞지 않는 표현(감사 인사 등)은 뒤에 둔다.
  @Test
  void showcaseBurgerPracticeListsExpressionsInTheOrderConditionOrder() {
    Conversation source = sourceConversation();
    source.attachShowcaseStage(ShowcaseStage.ORDER.ordinal());
    var takeaway =
        new ReviewTarget("I'll take it to go, please.", "포장해 갈게요.", "포장을 부탁할 때", null, null);
    var sides =
        new ReviewTarget("Could I get a side of fries, too?", "감자튀김도 주세요.", null, null, null);
    var onions =
        new ReviewTarget("Could I get that without onions?", "양파 빼 주세요.", null, null, null);
    Turn order = recommendedTurn(source, 2, "one burger", "Can I get a cheeseburger, please?");
    Turn dining = recommendedTurn(source, 4, "to go please", "To take away, please.");
    Turn farewell = recommendedTurn(source, 6, "thanks", "Thank you for your help.");

    var selected =
        ReviewTargets.includeRecommendations(
            List.of(takeaway, sides, onions),
            List.of(order, dining, farewell),
            JsonMapper.builder().build());

    assertThat(selected)
        .extracting(ReviewTarget::expression)
        .containsExactly(
            "Can I get a cheeseburger, please?",
            "Could I get that without onions?",
            "Could I get a side of fries, too?",
            "I'll take it to go, please.",
            "To take away, please.",
            "Thank you for your help.");
  }

  @Test
  void normalPracticeKeepsItsOrderEvenWhenItHasTakeawayRecommendations() {
    Conversation source = sourceConversation();
    var burger = new ReviewTarget("Could I have a cheeseburger?", "치즈버거 주세요.", null, null, null);
    Turn dining = recommendedTurn(source, 4, "to go please", "I'll take it to go, please.");

    var selected =
        ReviewTargets.includeRecommendations(
            List.of(burger), List.of(dining), JsonMapper.builder().build());

    assertThat(selected)
        .extracting(ReviewTarget::expression)
        .containsExactly("Could I have a cheeseburger?", "I'll take it to go, please.");
  }

  private static Conversation sourceConversation() {
    return Conversation.start(1L, 1L, InitiatedBy.USER, 1, Instant.parse("2026-11-13T01:00:00Z"));
  }

  private static Turn recommendedTurn(
      Conversation source, int seq, String original, String... alternatives) {
    Turn turn =
        Turn.user(
            source,
            seq,
            UUID.randomUUID(),
            InputType.TEXT,
            original,
            null,
            null,
            false,
            null,
            null);
    turn.recordBetterExpressions(JsonMapper.builder().build().writeValueAsString(alternatives));
    return turn;
  }

  private static SituationExpression expression(String text, String meaning) {
    return new SituationExpression(text, meaning, "주문할 때 써요.", text, meaning);
  }
}
