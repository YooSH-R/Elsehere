package com.elsehere.ai.dto;

import com.fasterxml.jackson.annotation.JsonInclude;

/**
 * AI-01 응답. BOOT-01의 {@code aiStatus}도 같은 형식이다.
 *
 * <p>한도 정책(API 명세 1.4)이 정해지기 전이라 {@code quota}와 {@code resumeAt}은 담지 않는다.
 *
 * @param remainingConversations 오늘 더 시작할 수 있는 NPC 대화 수.
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public record AiStatusResponse(
    AiStatus status, Capabilities capabilities, Integer remainingConversations) {

  /** 기능별로 지금 새 AI 요청을 보낼 수 있는지. 클라이언트는 이 값으로 AI가 필요한 버튼을 켜고 끈다. */
  public record Capabilities(
      boolean canStartConversation, boolean canUseSpeech, boolean canGenerateFeedback) {}
}
