package com.elsehere.conversation.dto;

import java.time.Instant;

/**
 * NPC 턴 음성.
 *
 * @param url 다시 듣기(CONV-06) 주소. 음성이 없으면 null
 */
public record SpeechResponse(SpeechStatus status, String url) {

  /** 방금 만든 턴처럼 보관 정책으로 지울 일이 없는 턴의 음성. */
  public static SpeechResponse of(Long turnId, String npcAudioKey) {
    if (npcAudioKey == null) {
      return new SpeechResponse(SpeechStatus.FAILED, null);
    }
    return new SpeechResponse(SpeechStatus.READY, "/api/v1/turns/" + turnId + "/speech");
  }

  /**
   * @param npcAudioDeletedAt 보관 정책으로 재생을 차단한 시각. 삭제 대기를 포함하며, 처음 생성에 실패했으면 null
   */
  public static SpeechResponse of(Long turnId, String npcAudioKey, Instant npcAudioDeletedAt) {
    if (npcAudioDeletedAt != null) {
      return new SpeechResponse(SpeechStatus.EXPIRED, null);
    }
    return of(turnId, npcAudioKey);
  }
}
