package com.elsehere.dialogue.dto;

/**
 * NPC 턴 음성 상태. FAILED면 자막을 보여 주고 CONV-07로 다시 만든다. EXPIRED는 보관 정책(기간 만료·용량 정리)으로 지운 음성이며 다시 만들지 않는다.
 */
public enum SpeechStatus {
  READY,
  FAILED,
  EXPIRED
}
