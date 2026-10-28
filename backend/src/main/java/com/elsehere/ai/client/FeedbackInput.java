package com.elsehere.ai.client;

import java.util.List;

/**
 * 대화 종료 피드백(/v1/feedback) 입력. ai/feedback 계약 0.3.0(ai/feedback/schemas/feedback.input.schema.json)을
 * 따른다.
 *
 * <p>식별자는 영문·숫자로 시작하는 문자열이어야 한다. DB ID는 문자열로 바꿔 보내고 결과를 받을 때 되돌린다. null 칸도 계약상 필수라 그대로 보낸다.
 *
 * @param conversationRevision 확정 대화 스냅샷 버전
 * @param analysisConfigVersion 모델·프롬프트·검증 정책 조합을 식별하는 버전
 */
public record FeedbackInput(
    String schemaVersion,
    String requestId,
    Scope scope,
    int conversationRevision,
    String analysisConfigVersion,
    Context context,
    List<Turn> turns,
    List<Object> assistanceEvents) {

  public record Scope(String userId, String npcId, String conversationId) {}

  /**
   * @param mode tutorial, free_chat, ambient, quest
   * @param endReason user_end 또는 npc_end
   */
  public record Context(
      String sceneId,
      String mode,
      String sceneDescription,
      String targetLanguage,
      String explanationLanguage,
      String endReason,
      String tutorialGoal) {}

  /**
   * @param speaker user, npc, system
   * @param inputType voice, text, generated, system
   * @param textStatus final, needs_review, failed, blocked, system_notice. 무례로 확정된 사용자 말은 blocked
   * @param originalTranscript 음성 입력의 전사 원문. 그 밖에는 null
   */
  public record Turn(
      String turnId,
      int sequence,
      String speaker,
      String inputType,
      String textStatus,
      String originalTranscript,
      String finalText,
      Difficulty difficulty,
      List<String> assistanceIds) {}

  /**
   * @param hintPolicy on_request 또는 tutorial_guided
   */
  public record Difficulty(
      String settingsVersion,
      String vocabularyProfile,
      Double speechRateMultiplier,
      String hintPolicy) {}
}
