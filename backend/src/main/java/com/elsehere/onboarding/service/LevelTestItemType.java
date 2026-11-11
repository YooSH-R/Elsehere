package com.elsehere.onboarding.service;

/** 레벨 테스트 문항 형식. */
public enum LevelTestItemType {
  /** NPC 대사를 듣고 한국어 보기 4개 중 뜻을 고른다. 서버가 정답과 비교해 채점한다. */
  LISTEN,
  /** 상황 카드를 보고 한두 문장으로 말하거나 쓴다. 완료할 때 AI가 통과 기준으로 판정한다. */
  SPEAK
}
