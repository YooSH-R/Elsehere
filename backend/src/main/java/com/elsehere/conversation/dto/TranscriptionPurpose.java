package com.elsehere.conversation.dto;

/** 음성 인식 용도. 자유 대화 발화는 TURN, 튜토리얼 대본 대사는 TUTORIAL, 레벨 테스트 말하기 답은 LEVEL_TEST다. */
public enum TranscriptionPurpose {
  TURN,
  TUTORIAL,
  LEVEL_TEST
}
