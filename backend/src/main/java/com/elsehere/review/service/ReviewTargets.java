package com.elsehere.review.service;

import com.elsehere.feedback.entity.FeedbackReviews;
import com.elsehere.review.dto.ReviewTarget;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;

/** 이미 저장된 피드백의 표현을 복습 카드로 옮긴다. */
final class ReviewTargets {
  private ReviewTargets() {}

  static List<ReviewTarget> select(List<FeedbackReviews.SituationExpression> expressions) {
    if (expressions == null) {
      return List.of();
    }
    var selected = new LinkedHashMap<String, ReviewTarget>();
    for (var expression : expressions) {
      if (expression == null
          || expression.expression() == null
          || expression.meaningKo() == null
          || expression.expression().isBlank()
          || expression.meaningKo().isBlank()
          || !learnerCanSay(expression.expression(), expression.usageKo())) {
        continue;
      }
      String text = expression.expression().strip();
      String key = text.replaceAll("\\s+", " ").toLowerCase(Locale.ROOT);
      selected.putIfAbsent(
          key,
          new ReviewTarget(
              text,
              expression.meaningKo().strip(),
              expression.usageKo(),
              expression.exampleEn(),
              expression.exampleKo()));
      if (selected.size() == 3) {
        break;
      }
    }
    return List.copyOf(selected.values());
  }

  /** 화자 표시가 없던 기존 카드의 명백한 직원 전용 문장을 복습 목표에서 제외한다. */
  static boolean learnerCanSay(String expression, String usage) {
    String words = ReviewExpressionEvidence.canonicalExpression(expression);
    if (words.equals("you re all set")
        || words.equals("you are all set")
        || words.equals("what can i get you")
        || words.equals("anything else")
        || words.equals("how can i help you")
        || words.startsWith("enjoy your stay")) {
      return false;
    }
    return usage == null
        || !(usage.contains("직원이 사용하는")
            || usage.contains("NPC가 사용하는")
            || usage.contains("점원이 사용하는")
            || usage.contains("직원 입장"));
  }

  static List<ReviewTarget> forPractice(List<ReviewTarget> targets) {
    return targets.stream()
        .filter(target -> learnerCanSay(target.expression(), target.usageKo()))
        .toList();
  }
}
