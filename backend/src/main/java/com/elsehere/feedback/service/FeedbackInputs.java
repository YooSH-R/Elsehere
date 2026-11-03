package com.elsehere.feedback.service;

import com.elsehere.ai.client.FeedbackInput;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.entity.TownStoryStage;
import com.elsehere.world.entity.Npc;
import java.util.List;
import java.util.UUID;

/**
 * 저장된 대화를 피드백 계약 0.3.0 입력으로 바꾼다. 변환 기준은 docs/api/feedback-integration.md의 "입력 변환"이다.
 *
 * <p>사용자 말은 원문 그대로 보낸다. 생성 번역으로 바꾸지 않고, 무례로 확정된 말만 blocked로 보낸다.
 */
final class FeedbackInputs {

  static final String SCHEMA_VERSION = "0.3.0";
  // 모델·프롬프트·검증 정책을 바꾸면 올린다. 같은 대화라도 버전이 다르면 다른 결과로 본다.
  static final String ANALYSIS_CONFIG_VERSION = "elsehere-feedback-v1";

  private FeedbackInputs() {}

  static FeedbackInput from(Conversation conversation, Npc npc, List<Turn> turns, UUID requestId) {
    String level = "level-" + conversation.getLevelApplied();
    FeedbackInput.Difficulty difficulty =
        new FeedbackInput.Difficulty(level, level, null, "on_request");
    List<FeedbackInput.Turn> inputTurns =
        turns.stream().map(turn -> toInputTurn(turn, difficulty)).toList();
    Integer missionStage = conversation.getFirstDayStage();
    FirstDayStage quest =
        missionStage != null && missionStage >= 0 && missionStage < FirstDayStage.values().length
            ? FirstDayStage.values()[missionStage]
            : null;
    Integer townStage = conversation.getTownStoryStage();
    TownStoryStage townQuest =
        townStage != null && townStage >= 0 && townStage < TownStoryStage.values().length
            ? TownStoryStage.values()[townStage]
            : null;
    String situation =
        townQuest != null
            ? townQuest.situation()
            : quest != null ? quest.situation() + " " + quest.direction() : null;
    return new FeedbackInput(
        SCHEMA_VERSION,
        requestId.toString(),
        new FeedbackInput.Scope(
            String.valueOf(conversation.getUserId()),
            npc.getCode(),
            String.valueOf(conversation.getId())),
        // 턴을 더하면 바뀌는 대화 스냅샷 버전. 전사 수정 기능이 생기면 수정 횟수도 반영한다.
        turns.isEmpty() ? 0 : turns.get(turns.size() - 1).getSeq(),
        ANALYSIS_CONFIG_VERSION,
        new FeedbackInput.Context(
            npc.getScene().getCode(),
            situation == null ? "free_chat" : "quest",
            npc.getScene().getName()
                + " · "
                + npc.getRoleLabel()
                + " "
                + npc.getName()
                + (situation == null ? " · 자유 대화" : " · " + situation),
            "en-US",
            "ko-KR",
            conversation.getStatus() == ConversationStatus.ENDED_BY_NPC ? "npc_end" : "user_end",
            null),
        inputTurns,
        List.of());
  }

  private static FeedbackInput.Turn toInputTurn(Turn turn, FeedbackInput.Difficulty difficulty) {
    boolean user = turn.getSpeaker() == Speaker.USER;
    boolean voice = turn.getInputType() == InputType.VOICE;
    return new FeedbackInput.Turn(
        String.valueOf(turn.getId()),
        turn.getSeq(),
        user ? "user" : "npc",
        user ? (voice ? "voice" : "text") : "generated",
        user && Boolean.TRUE.equals(turn.getGuardAbusive()) ? "blocked" : "final",
        user && voice ? turn.getSttRawText() : null,
        turn.getText(),
        difficulty,
        List.of());
  }
}
