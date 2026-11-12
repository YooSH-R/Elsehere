package com.elsehere.onboarding.dto;

import com.fasterxml.jackson.annotation.JsonInclude;
import java.util.List;

/**
 * ONB-03 문항. 듣기 문항의 정답·대사와 말하기 문항의 판정 기준은 담지 않는다. 듣기 음성은 문항 음성(ONB-10)으로 받는다.
 *
 * @param question 듣기는 화면의 물음, 말하기는 상황 카드 문장
 * @param choices 듣기 문항의 보기 4개. 말하기 문항은 빈 목록
 * @param speaker 듣기 문항의 말한 사람. 말하기 문항은 담지 않는다
 * @param cues 말하기 문항의 말할 정보. 듣기 문항은 빈 목록
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public record LevelTestItemResponse(
    String itemCode,
    Type type,
    String instruction,
    String question,
    List<String> choices,
    String speaker,
    List<String> cues) {

  /** 레벨 테스트 문항 형식. */
  public enum Type {
    /** NPC 대사를 듣고 한국어 보기 4개 중 뜻을 고른다. */
    LISTEN,
    /** 상황 카드를 보고 한두 문장으로 말하거나 쓴다. */
    SPEAK
  }
}
