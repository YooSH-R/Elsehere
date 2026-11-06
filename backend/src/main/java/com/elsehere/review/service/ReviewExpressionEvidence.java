package com.elsehere.review.service;

import java.util.Locale;
import java.util.regex.Pattern;

/** 의미 채점이 아닌, 실제 발화에 보이는 같은 표현의 보수적인 근거 찾기. */
final class ReviewExpressionEvidence {
  private static final Pattern NEGATION = Pattern.compile("\\b(no|not|never|without|cannot)\\b");

  private ReviewExpressionEvidence() {}

  static boolean matches(String expression, String text) {
    String target = normalize(expression);
    if (target.isBlank() || text == null) {
      return false;
    }
    for (String sentence : text.split("[.!?;]")) {
      String words = " " + normalize(sentence) + " ";
      String phrase = " " + target + " ";
      int found = words.indexOf(phrase);
      if (found >= 0 && !NEGATION.matcher(words.substring(0, found)).find()) {
        return true;
      }
    }
    return false;
  }

  static String canonicalExpression(String text) {
    return normalize(text);
  }

  private static String normalize(String text) {
    if (text == null) {
      return "";
    }
    return text.toLowerCase(Locale.ROOT)
        .replace('\u2019', '\'')
        .replaceAll("\\bi'm\\b", "i am")
        .replaceAll("\\bi'd\\b", "i would")
        .replaceAll("\\bi'll\\b", "i will")
        .replaceAll("\\bi've\\b", "i have")
        .replaceAll("\\bcan't\\b", "can not")
        .replaceAll("\\bwon't\\b", "will not")
        .replaceAll("\\bdon't\\b", "do not")
        .replaceAll("\\bdoesn't\\b", "does not")
        .replaceAll("\\bdidn't\\b", "did not")
        .replaceAll("n't\\b", " not")
        .replaceAll("\\bplease\\b", " ")
        .replaceAll("\\b(?:can|could) (i|you)\\b", "can $1")
        .replaceAll("[^\\p{L}\\p{N}]+", " ")
        .strip()
        .replaceAll("\\s+", " ");
  }
}
