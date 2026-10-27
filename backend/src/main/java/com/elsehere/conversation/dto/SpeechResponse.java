package com.elsehere.conversation.dto;

/**
 * NPC 턴 음성.
 *
 * @param url 다시 듣기(CONV-06) 주소. 음성이 없으면 null
 */
public record SpeechResponse(SpeechStatus status, String url) {

  public static SpeechResponse of(Long turnId, String npcAudioKey) {
    if (npcAudioKey == null) {
      return new SpeechResponse(SpeechStatus.FAILED, null);
    }
    return new SpeechResponse(SpeechStatus.READY, "/api/v1/turns/" + turnId + "/speech");
  }
}
