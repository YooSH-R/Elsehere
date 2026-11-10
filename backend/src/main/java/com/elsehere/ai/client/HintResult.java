package com.elsehere.ai.client;

import java.util.List;

/**
 * 힌트(/v1/hint) 결과.
 *
 * @param sayThis 학습자가 다음에 할 만한 문장(최대 2개). 첫 문장이 가장 쉽다
 * @param keywords 문장에 쓴 단어·구(최대 3개)
 */
public record HintResult(List<Phrase> sayThis, List<Phrase> keywords, List<WorkerUsage> usage) {

  /** 추천 문장이 없으면 null이다. */
  public String firstSentence() {
    return sayThis == null || sayThis.isEmpty() ? null : sayThis.get(0).en();
  }

  public String firstMeaningKo() {
    return sayThis == null || sayThis.isEmpty() ? null : sayThis.get(0).ko();
  }

  public record Phrase(String en, String ko) {}
}
