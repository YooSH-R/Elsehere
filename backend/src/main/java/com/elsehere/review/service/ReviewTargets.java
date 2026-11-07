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
          || expression.meaningKo().isBlank()) {
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
}
