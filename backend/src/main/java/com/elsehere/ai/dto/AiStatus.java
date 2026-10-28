package com.elsehere.ai.dto;

/**
 * AI 이용 상태(AI-01). 화면 HUD의 "AI 사용 가능" 표시와 화면 19의 사용량 소진 안내에 쓴다.
 *
 * <p>외부 AI 장애(AI_TEMPORARILY_UNAVAILABLE)와 운영 제한(AI_RESTRICTED)은 판단하는 기능이 생길 때 추가한다.
 */
public enum AiStatus {
  AI_AVAILABLE,
  AI_EXHAUSTED
}
