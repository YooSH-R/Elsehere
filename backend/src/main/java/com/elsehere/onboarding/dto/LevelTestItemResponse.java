package com.elsehere.onboarding.dto;

import com.elsehere.onboarding.service.LevelTestItem;
import com.elsehere.onboarding.service.LevelTestItemType;
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
    LevelTestItemType type,
    String instruction,
    String question,
    List<String> choices,
    String speaker,
    List<String> cues) {

  public static LevelTestItemResponse from(LevelTestItem item) {
    return new LevelTestItemResponse(
        item.itemCode(),
        item.type(),
        item.instruction(),
        item.question(),
        item.choices(),
        item.speaker(),
        item.cues());
  }
}
