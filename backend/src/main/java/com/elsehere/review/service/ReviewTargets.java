package com.elsehere.review.service;

import com.elsehere.feedback.entity.FeedbackReviews;
import com.elsehere.review.dto.ReviewTarget;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.regex.Pattern;

/** 이미 저장된 피드백의 고친 문장과 표현을 복습 카드로 옮긴다. */
final class ReviewTargets {
  private ReviewTargets() {}

  private static final int MAX_CORRECTIONS = 3;
  private static final int MAX_EXPRESSIONS = 3;
  private static final int MAX_TARGETS = 5;
  private static final Pattern HANGUL = Pattern.compile("[가-힣]");

  /** 학습자가 한 말과 피드백이 고쳐 준 문장. */
  record Correction(String originalText, String betterText, String reason) {}

  /**
   * 복습 목표. 학습자가 다시 말해 볼 고친 문장(한국어로 한 말 먼저, 세 개까지)을 앞에 두고 이 상황에서 자주 쓰는 표현으로 채운다. 모두 다섯 개까지다.
   *
   * @param corrections 대화 순서대로의 고친 문장
   */
  static List<ReviewTarget> select(
      List<Correction> corrections, List<FeedbackReviews.SituationExpression> expressions) {
    var selected = new LinkedHashMap<String, ReviewTarget>();
    corrections.stream()
        .filter(ReviewTargets::changesTheSentence)
        .sorted(Comparator.comparing(correction -> !spokenInKorean(correction)))
        .limit(MAX_CORRECTIONS)
        .forEach(
            correction ->
                selected.putIfAbsent(
                    key(correction.betterText()),
                    new ReviewTarget(
                        correction.betterText().strip(),
                        spokenInKorean(correction)
                            ? correction.originalText().strip()
                            : correction.reason(),
                        correction.reason(),
                        null,
                        null,
                        correction.originalText().strip())));
    int corrected = selected.size();
    for (var expression : nonNull(expressions)) {
      if (selected.size() == MAX_TARGETS || selected.size() - corrected == MAX_EXPRESSIONS) {
        break;
      }
      if (expression == null
          || expression.expression() == null
          || expression.meaningKo() == null
          || expression.expression().isBlank()
          || expression.meaningKo().isBlank()
          || !learnerCanSay(expression.expression(), expression.usageKo())) {
        continue;
      }
      selected.putIfAbsent(
          key(expression.expression()),
          new ReviewTarget(
              expression.expression().strip(),
              expression.meaningKo().strip(),
              expression.usageKo(),
              expression.exampleEn(),
              expression.exampleKo()));
    }
    return List.copyOf(selected.values());
  }

  // 고칠 것이 없는 문장은 고친 문장이 없거나, 대소문자·문장부호만 다르다.
  private static boolean changesTheSentence(Correction correction) {
    return correction.originalText() != null
        && correction.betterText() != null
        && !correction.betterText().isBlank()
        && !wordsOf(correction.betterText()).equals(wordsOf(correction.originalText()));
  }

  private static String wordsOf(String text) {
    return text.toLowerCase(Locale.ROOT).replaceAll("[^\\p{L}\\p{N}]+", " ").strip();
  }

  private static boolean spokenInKorean(Correction correction) {
    return HANGUL.matcher(correction.originalText()).find();
  }

  private static String key(String expression) {
    return expression.strip().replaceAll("\\s+", " ").toLowerCase(Locale.ROOT);
  }

  private static <T> List<T> nonNull(List<T> items) {
    return items == null ? List.of() : items;
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
