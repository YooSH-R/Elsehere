package com.elsehere.dialogue.service;

import java.util.List;

/** 이미 끝난 무례·대화 사건 판정으로 이번 대답의 표정 반응을 정한다. */
final class DialogueReaction {
  private DialogueReaction() {}

  static String select(boolean rude, int previousRudeCount, List<String> events) {
    if (rude) {
      if (previousRudeCount >= 2) {
        return "rude_final";
      }
      return previousRudeCount == 1 ? "rude_repeated" : "rude_warning";
    }
    if (events == null) {
      return null;
    }
    if (previousRudeCount > 0 && events.contains("APOLOGY")) {
      return "apology_after_rude";
    }
    if (events.contains("COMPLIMENT")) {
      return "compliment";
    }
    return null;
  }
}
