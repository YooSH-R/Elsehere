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
import java.util.Map;
import java.util.UUID;
import java.util.stream.Collectors;

/**
 * 저장된 대화를 피드백 계약 0.3.0 입력으로 바꾼다. 변환 기준은 docs/api/feedback-integration.md의 "입력 변환"이다.
 *
 * <p>사용자 말은 원문 그대로 보낸다. 생성 번역으로 바꾸지 않고, 무례로 확정된 말만 blocked로 보낸다.
 */
final class FeedbackInputs {

  static final String SCHEMA_VERSION = "0.3.0";
  // 모델·프롬프트·검증 정책을 바꾸면 올린다. 같은 대화라도 버전이 다르면 다른 결과로 본다.
  static final String ANALYSIS_CONFIG_VERSION = "elsehere-feedback-v2";

  // 학습자 수준 설명. 워커 힌트·추천 단어(ai/worker/app/prompts.py LEVEL_LEARNER)와 같은 말을 쓴다.
  private static final Map<Integer, String> LEVEL_LEARNER =
      Map.of(
          1, "very simple (CEFR A1-A2), short",
          2, "simple and natural (CEFR A2-B1)",
          3, "natural, can use a common idiom (CEFR B1-B2)");

  private FeedbackInputs() {}

  /** 설정 버전은 "level-N", 어휘 프로필은 모델이 피드백 길이와 표현 수준을 맞추도록 수준 설명까지 담는다. */
  static FeedbackInput.Difficulty difficulty(Integer level) {
    String version = "level-" + level;
    String learner =
        level == null
            ? LEVEL_LEARNER.get(1)
            : LEVEL_LEARNER.getOrDefault(level, LEVEL_LEARNER.get(1));
    return new FeedbackInput.Difficulty(version, version + ": " + learner, null, "on_request");
  }

  static FeedbackInput from(Conversation conversation, Npc npc, List<Turn> turns, UUID requestId) {
    FeedbackInput.Difficulty difficulty = difficulty(conversation.getLevelApplied());
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
            : quest != null ? quest.situation() + " " + quest.direction() + goals(quest) : null;
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
                + " · "
                + (situation != null ? situation : freeTalk(conversation)),
            "en-US",
            "ko-KR",
            conversation.getStatus() == ConversationStatus.ENDED_BY_NPC ? "npc_end" : "user_end",
            null),
        inputTurns,
        List.of());
  }

  // 퀘스트가 없는 대화의 상황. 근무 시간이 아닌 NPC와의 대화는 응대 상황으로 평가하지 않도록 일상 대화라고 알린다.
  private static String freeTalk(Conversation conversation) {
    return conversation.isNpcOffDuty() ? "근무가 아닐 때의 일상 대화" : "자유 대화";
  }

  // 이 단계에서 학습자가 할 일. 모델이 장면에 맞는 표현만 권하게 한다.
  private static String goals(FirstDayStage quest) {
    return " · 할 일: "
        + quest.goals().stream()
            .map(goal -> goal.title() + "(" + goal.description() + ")")
            .collect(Collectors.joining(", "));
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
