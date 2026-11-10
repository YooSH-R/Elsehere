package com.elsehere.onboarding.service;

import java.util.List;

/**
 * 레벨 테스트 문항 하나. 듣기 문항의 정답·대사와 말하기 문항의 판정 기준은 채점에만 쓰고 클라이언트에 보내지 않는다.
 *
 * @param itemCode 응답 저장(level_test_answers.item_code)에 쓰는 문항 ID
 * @param question 듣기는 화면의 물음, 말하기는 상황 카드 문장
 * @param choices 듣기 문항의 한국어 보기 4개. 말하기 문항은 빈 목록
 * @param answer 듣기 문항의 정답 보기. 말하기 문항은 null
 * @param speaker 듣기 문항 화면에 보여 줄 말한 사람("Gus · 빨래방 주인")
 * @param npcCode 듣기 음성을 만들 목소리의 NPC
 * @param script 듣기 음성으로 들려줄 영어 대사. 화면에는 보여 주지 않는다(자막 없음)
 * @param cues 말하기 문항 화면에 보여 줄 말할 정보
 * @param criteria 말하기 답을 AI가 판정할 통과 기준(영어)
 */
public record LevelTestItem(
    String itemCode,
    LevelTestItemType type,
    String instruction,
    String question,
    List<String> choices,
    String answer,
    String speaker,
    String npcCode,
    String script,
    List<String> cues,
    String criteria) {

  static LevelTestItem listening(
      String itemCode,
      String speaker,
      String npcCode,
      String script,
      List<String> choices,
      String answer) {
    return new LevelTestItem(
        itemCode,
        LevelTestItemType.LISTEN,
        "잘 듣고 무슨 뜻인지 고르세요.",
        "무슨 뜻일까요?",
        choices,
        answer,
        speaker,
        npcCode,
        script,
        List.of(),
        null);
  }

  static LevelTestItem speaking(
      String itemCode, String situation, List<String> cues, String criteria) {
    return new LevelTestItem(
        itemCode,
        LevelTestItemType.SPEAK,
        "상황에 맞게 한두 문장으로 말해 보세요.",
        situation,
        List.of(),
        null,
        null,
        null,
        null,
        cues,
        criteria);
  }

  public boolean isListening() {
    return type == LevelTestItemType.LISTEN;
  }

  public boolean isSpeaking() {
    return type == LevelTestItemType.SPEAK;
  }
}
