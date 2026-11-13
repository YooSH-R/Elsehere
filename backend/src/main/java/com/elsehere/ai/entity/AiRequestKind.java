package com.elsehere.ai.entity;

/** AI 워커 기능 단위. 한 턴에서 같은 요청 ID로 여러 기능을 부르므로 이것으로 구분한다. */
public enum AiRequestKind {
  STT,
  GUARD,
  JUDGE,
  NPC,
  TTS,
  HINT,
  MEANING,
  FEEDBACK,
  MEMORY,
  EXPRESSIONS
}
