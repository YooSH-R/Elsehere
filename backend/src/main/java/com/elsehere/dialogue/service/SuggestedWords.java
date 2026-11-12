package com.elsehere.dialogue.service;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Locale;

/** 워커가 보낸 추천 단어 후보에서 화면에 보일 단어를 고른다. CONV-12 응답과 CONV-10 대화 상세가 같은 규칙을 쓴다. */
final class SuggestedWords {

  /** 화면에 보이는 추천 단어 수. 워커는 거를 여유를 두고 후보를 더 보낸다. */
  private static final int SHOWN = 3;

  /** 추천 단어가 비거나 1개만 보이지 않게, 건너뛴 구를 다시 넣어서라도 지키는 수 */
  private static final int FEWEST = 2;

  private SuggestedWords() {}

  /**
   * 추천 문장에서 잘라 온 구는 건너뛰고 3개까지 고르되, 그러면 2개보다 적게 남을 때는 짧은 구부터 다시 넣는다. 고른 단어는 워커가 보낸 순서대로 돌려준다.
   *
   * @param sentence 같은 턴의 추천 문장. 없으면 앞에서부터 3개를 고른다
   */
  static List<String> pick(List<String> words, String sentence) {
    if (words == null) {
      return null;
    }
    List<String> picked =
        new ArrayList<>(
            words.stream().filter(word -> !cutFromSentence(word, sentence)).limit(SHOWN).toList());
    words.stream()
        .filter(word -> cutFromSentence(word, sentence))
        .sorted(Comparator.comparingInt(word -> english(word).split(" ").length))
        .limit(Math.max(0, FEWEST - picked.size()))
        .forEach(picked::add);
    return words.stream().filter(picked::contains).toList();
  }

  /**
   * 추천 문장에 그대로 들어 있는 여러 단어 구(have a good)는 문장을 잘라 온 말이다. 한 단어(bag)는 문장에 있어도 단어로 쓸 만해서 거르지 않는다. 단어
   * 칸은 "영어 — 뜻"이라 영어만 비교하고, 대소문자·문장 부호는 가리지 않는다.
   */
  private static boolean cutFromSentence(String word, String sentence) {
    String english = english(word);
    return sentence != null
        && english.contains(" ")
        && (" " + spoken(sentence) + " ").contains(" " + english + " ");
  }

  private static String english(String word) {
    return spoken(word.split(" — ", 2)[0]);
  }

  private static String spoken(String text) {
    return text.toLowerCase(Locale.ROOT)
        .replace('’', '\'')
        .replaceAll("[^a-z0-9' ]", " ")
        .trim()
        .replaceAll("\\s+", " ");
  }
}
