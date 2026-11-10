package com.elsehere.review.service;

import java.util.Locale;
import java.util.regex.Pattern;

/** 의미 채점이 아닌, 실제 발화에 보이는 같은 표현의 보수적인 근거 찾기. */
final class ReviewExpressionEvidence {
  private static final Pattern NEGATION = Pattern.compile("\\b(no|not|never|without|cannot)\\b");
  private static final Pattern QUOTED =
      Pattern.compile(
          "\"[^\"]*\"|“[^”]*”|‘[^’]*’|(?<!\\p{L})'(?:[^'\\r\\n]|(?<=\\p{L})'(?=\\p{L}))+'(?!\\p{L})");

  private ReviewExpressionEvidence() {}

  static boolean matches(String expression, String text) {
    String target = normalize(expression);
    if (target.isBlank() || text == null) {
      return false;
    }
    String unquoted = QUOTED.matcher(text).replaceAll(" ");
    // 목표 자체가 여러 문장이거나 부정형인 경우도 똑같이 말했으면 사용 근거다.
    if (target.equals(normalize(unquoted))) {
      return true;
    }
    for (String sentence : unquoted.split("[.!?;]")) {
      String words = " " + normalize(sentence) + " ";
      String phrase = " " + target + " ";
      int found = words.indexOf(phrase);
      String prefix = found >= 0 ? words.substring(0, found) : "";
      // “No, I'm good.”의 No는 앞선 제안에 대한 응답이지 good의 부정이 아니다.
      boolean negated = NEGATION.matcher(prefix.replaceFirst("^\\s*no\\s*$", " ")).find();
      if (found >= 0 && !negated) {
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
        .replaceAll("\\b(you|we|they)'re\\b", "$1 are")
        .replaceAll("\\b(it|that)'s\\b", "$1 is")
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
