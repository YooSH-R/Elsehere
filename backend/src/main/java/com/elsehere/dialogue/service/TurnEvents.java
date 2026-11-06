package com.elsehere.dialogue.service;

/**
 * 발화 전송(CONV-03)을 스트리밍으로 받을 때 NPC 대답의 글자 조각과 문장 조각 음성을 받는 쪽. 턴을 저장한 결과는 {@link TurnService#send}가
 * 돌려준다.
 */
public interface TurnEvents {

  /** 대사 글자 조각. 이어 붙이면 대답 전문이 된다. */
  void delta(String text);

  /**
   * 문장 조각 음성. seq 순서대로 온다.
   *
   * @param wav 조각 음성(PCM16 WAV, 립싱크용 음소 청크 포함). 합성이 늦거나 실패했거나 받지 못했으면 null이며, 그 조각은 자막만 보여 준다
   */
  void audio(int seq, String text, byte[] wav);
}
