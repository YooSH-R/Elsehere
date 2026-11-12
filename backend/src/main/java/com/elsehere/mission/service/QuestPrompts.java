package com.elsehere.mission.service;

/** 첫날 미션·마을 이야기·장소 연습의 대화 지시({@link QuestTalk})에 같이 붙이는 문장. */
final class QuestPrompts {

  private QuestPrompts() {}

  /** 의도 판정에 보내는 목표 인정 기준. 말을 바꾸거나 짧게 답해도 뜻이 분명하면 인정하고, 빠진 세부가 있는 목표만 남긴다. */
  static final String QUEST_EVIDENCE =
      " Accept clear paraphrases, indirect requests and short answers understood from the partner's question. Do not require exact wording or perfect grammar."
          + " Check each objective independently. Combine explicit details from earlier learner turns in this conversation when the latest line contributes; they need not repeat them."
          + " Never credit NPC lines, quoted examples, negatives or hypotheticals. A bare mention does not count. If a required detail is still missing or ambiguous, leave only that objective pending.";

  /**
   * 퀘스트 목표는 학습자가 먼저 말해야 영어로 말할 거리가 생긴다. 요청을 꺼낸 뒤에는 그 요청에 빠진 정보만 확인한다.
   *
   * <p>한 목표에 답하면서 남은 목표의 답(예: 늦은 체크아웃을 물었는데 추가 요금까지)을 같이 말하면 학습자가 그 목표를 말할 이유가 사라지므로, 이번 턴에 물은 것만
   * 답하게 한다.
   */
  static final String LEARNER_LEADS =
      " Never be the first to bring up the learner's remaining tasks, the things they need or what happened to them; wait for the learner to raise them in their own words."
          + " Answer only what the learner said or asked in this turn. Keep back any price, fee, time, how-to or other fact that a still-pending objective is for the learner to ask about, and do not do or offer what a pending objective is for the learner to request, until the learner asks."
          + " After the learner raises a mission request, acknowledge what is clear and ask one short question about only the missing required detail in that request."
          + " Use details the learner already gave or clearly selected. If the request is complete, answer it without another question. Do not ask for known details, introduce an unrelated pending goal, or correct grammar in the reply."
          + " In ordinary small talk, react to their topic and share brief details fitting your personality and current situation. If they seem unsure what to say, offer a small observation or an easy question in your own voice without naming undisclosed mission tasks.";

  /** 지금 단계 담당이 아닌 NPC에게 보내는 안내. 지금 할 일을 물으면 모른다고 하지 않고 담당 NPC와 장소를 알려 주게 한다. 이름을 찾지 못하면 장소만 알린다. */
  static String sendToOwner(String place, String owner) {
    String whom = owner == null ? "" : " and talk to " + owner;
    return "The learner's next stop is "
        + place
        + (owner == null ? "" : ", where they should talk to " + owner)
        + ". If the learner asks about their current task, where to go or who can help, tell them to go to "
        + place
        + whom
        + " instead of saying you do not know.";
  }
}
