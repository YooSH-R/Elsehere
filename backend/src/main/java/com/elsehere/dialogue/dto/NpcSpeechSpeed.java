package com.elsehere.dialogue.dto;

/**
 * NPC 대화 음성 빠르기(Kokoro speed 배율). 학습자가 마이페이지 슬라이더로 고르고 기기에 저장해 대화 시작·발화 전송·음성 다시 만들기 요청마다 보낸다. 범위는
 * AI 워커 {@code Learner.speech_speed}와 같다.
 */
public final class NpcSpeechSpeed {

  public static final String MIN = "0.8";
  public static final String MAX = "1.5";

  /** 값을 보내지 않는 예전 클라이언트에 쓰는 빠르기. 슬라이더 기본값과 같다. */
  public static final double DEFAULT = 1.1;

  private NpcSpeechSpeed() {}

  public static double orDefault(Double speechSpeed) {
    return speechSpeed == null ? DEFAULT : speechSpeed;
  }
}
