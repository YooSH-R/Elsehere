package com.elsehere.vocab.service;

import java.util.List;
import java.util.Optional;
import java.util.function.Function;

/** 저장 출처(NPC 대사 단어 풀이, 피드백 단어장 후보)에서 저장할 단어 하나를 찾는다(VOCAB-02). */
final class SourceWords {

  private SourceWords() {}

  /**
   * 단어 하나만 저장하므로 두 단어 이상인 표현은 출처에 있어도 찾지 않는다.
   *
   * @param requested 클라이언트가 보낸 단어. 대소문자와 앞뒤 공백은 따지지 않는다.
   */
  static <T> Optional<T> find(List<T> entries, Function<T, String> textOf, String requested) {
    String word = requested.strip();
    if (word.isEmpty() || word.chars().anyMatch(Character::isWhitespace)) {
      return Optional.empty();
    }
    return entries.stream().filter(entry -> word.equalsIgnoreCase(textOf.apply(entry))).findFirst();
  }
}
