package com.elsehere.review.service;

import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.service.TurnRecommendations;
import com.elsehere.feedback.entity.FeedbackReviews;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.review.dto.ReviewTarget;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.regex.Pattern;
import tools.jackson.databind.json.JsonMapper;

/** 이미 저장된 피드백의 고친 문장과 표현을 복습 카드로 옮긴다. */
final class ReviewTargets {
  private ReviewTargets() {}

  private static final int MAX_CORRECTIONS = 3;
  private static final int MAX_EXPRESSIONS = 3;
  private static final int MAX_TARGETS = 5;
  private static final Pattern HANGUL = Pattern.compile("[가-힣]");
  // 시연 주문 조건 차례(버거 → 재료 → 사이드·음료 → 매장·포장)와 그 조건의 표현에 들어가는 말. 소문자·구두점을 지운 표현에 맞춘다.
  private static final List<Pattern> SHOWCASE_ORDER_CONDITIONS =
      List.of(
          Pattern.compile("\\b(?:cheese)?burgers?\\b"),
          Pattern.compile(
              "\\b(?:onions?|tomato(?:es)?|pickles?|lettuce|without|leave out|leave off|hold the)\\b"),
          Pattern.compile("\\b(?:fries|coke|cola|soda|lemonade|drinks?|sides?)\\b"),
          Pattern.compile(
              "\\b(?:to go|take away|takeaway|take it away|takeout|take out|for here|eat here|eat in|dine in)\\b"));

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

  /** 기본 목표의 개수 제한과 별개로, 대화 중 추천한 문장을 누락 없이 보존한다. */
  static List<ReviewTarget> includeRecommendations(
      List<ReviewTarget> targets, List<Turn> sourceTurns, JsonMapper mapper) {
    var selected = new LinkedHashMap<String, ReviewTarget>();
    for (ReviewTarget target : forPractice(targets)) {
      selected.putIfAbsent(TurnRecommendations.key(target.expression()), target);
    }
    // 추천 문장마다 어느 발화에서 나왔는지. 한 발화의 추천은 그 발화의 다른 말이라 같은 뜻이다.
    var recommendedIn = new HashMap<String, Integer>();
    for (int turn = 0; turn < sourceTurns.size(); turn++) {
      for (String sentence : TurnRecommendations.sentences(sourceTurns.get(turn), mapper)) {
        if (learnerCanSay(sentence, null)) {
          String key = TurnRecommendations.key(sentence);
          recommendedIn.putIfAbsent(key, turn);
          selected.putIfAbsent(
              key, new ReviewTarget(sentence, null, "지난 대화에서 같은 뜻으로 추천받은 표현이에요.", null, null));
        }
      }
    }
    List<ReviewTarget> included = List.copyOf(selected.values());
    return isShowcaseOrder(sourceTurns)
        ? inShowcaseOrderConditionOrder(
            oneExpressionPerMeaning(shownInConversation(included, recommendedIn), recommendedIn))
        : included;
  }

  private static boolean isShowcaseOrder(List<Turn> sourceTurns) {
    return sourceTurns.stream()
        .anyMatch(
            turn ->
                Integer.valueOf(ShowcaseStage.ORDER.ordinal())
                    .equals(turn.getConversation().getShowcaseStage()));
  }

  /** 시연 주문 복습에서는 대화 중에 뜬 추천 문장과 고친 문장만 둔다. 대화가 끝난 뒤 피드백에서 처음 본 상황 표현은 넣지 않는다. */
  private static List<ReviewTarget> shownInConversation(
      List<ReviewTarget> targets, Map<String, Integer> recommendedIn) {
    return targets.stream()
        .filter(
            target ->
                target.originalText() != null
                    || recommendedIn.containsKey(TurnRecommendations.key(target.expression())))
        .toList();
  }

  /**
   * 시연 주문 복습에서는 같은 뜻의 표현을 하나만 둔다. 주문 조건 하나는 한 가지를 말하므로 조건마다 하나만 남기고, 고친 문장을 그 발화의 추천보다 먼저 남긴다. 어느
   * 조건에도 맞지 않는 표현은 고친 문장은 그대로 두고, 추천은 발화마다 하나만 남긴다.
   */
  private static List<ReviewTarget> oneExpressionPerMeaning(
      List<ReviewTarget> targets, Map<String, Integer> recommendedIn) {
    var kept = new ArrayList<ReviewTarget>();
    for (int condition = 0; condition < SHOWCASE_ORDER_CONDITIONS.size(); condition++) {
      int wanted = condition;
      targets.stream()
          .filter(target -> showcaseOrderCondition(target) == wanted)
          .min(Comparator.comparing((ReviewTarget target) -> target.originalText() == null))
          .ifPresent(kept::add);
    }
    var turnsKept = new HashSet<Integer>();
    for (ReviewTarget target : targets) {
      if (showcaseOrderCondition(target) < SHOWCASE_ORDER_CONDITIONS.size()) {
        continue;
      }
      Integer turn = recommendedIn.get(TurnRecommendations.key(target.expression()));
      if (target.originalText() != null || turnsKept.add(turn)) {
        kept.add(target);
      }
    }
    // 원래 차례를 지키도록 처음 목록 순서로 되돌린다.
    return targets.stream().filter(kept::contains).toList();
  }

  /**
   * 시연 주문 복습에서는 이번에 써 볼 표현을 주문 조건 차례로 둔다. 고친 문장 구역은 지금처럼 앞에 두고, 각 구역 안에서 조건 차례로 둔다. 같은 조건 안에서는 원래
   * 차례를 지키고, 어느 조건에도 맞지 않는 표현은 뒤에 둔다. 문장을 새로 만들지 않는다.
   */
  private static List<ReviewTarget> inShowcaseOrderConditionOrder(List<ReviewTarget> targets) {
    return targets.stream()
        .sorted(
            Comparator.comparing((ReviewTarget target) -> target.originalText() == null)
                .thenComparing(ReviewTargets::showcaseOrderCondition))
        .toList();
  }

  private static int showcaseOrderCondition(ReviewTarget target) {
    String words = TurnRecommendations.key(target.expression());
    for (int i = 0; i < SHOWCASE_ORDER_CONDITIONS.size(); i++) {
      if (SHOWCASE_ORDER_CONDITIONS.get(i).matcher(words).find()) {
        return i;
      }
    }
    return SHOWCASE_ORDER_CONDITIONS.size();
  }
}
