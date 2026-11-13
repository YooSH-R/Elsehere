package com.elsehere.ai.client;

import java.util.List;

/**
 * 학습자 말의 영어 표현(/v1/expressions) 결과. NPC 대답이 기다리는 의도 판정(/v1/judge)의 출력을 줄이려고 판정에서 떼어 냈다.
 *
 * @param translation 한국어가 섞인 말이면 영어 번역, 영어에 문법 오류가 있으면 교정문. 보여 줄 것이 없으면 null
 * @param betterExpressions 표현 카드에 접어 둘 같은 뜻의 다른 표현(최대 3개). 고친 문장·한국어가 섞인 말에도 준다. 없으면 빈 목록
 */
public record ExpressionsResult(
    Translation translation, List<String> betterExpressions, List<WorkerUsage> usage) {

  public ExpressionsResult {
    betterExpressions = betterExpressions == null ? List.of() : List.copyOf(betterExpressions);
  }

  /** 번역이 실패했으면 {@code english}가 null이고 {@code error}에 이유가 있다. */
  public record Translation(String english, String error) {}

  public String english() {
    return translation == null ? null : translation.english();
  }
}
