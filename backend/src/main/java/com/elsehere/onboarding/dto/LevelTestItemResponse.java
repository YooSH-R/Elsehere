package com.elsehere.onboarding.dto;

import com.elsehere.onboarding.service.LevelTestItem;
import com.elsehere.onboarding.service.LevelTestItemType;
import java.util.List;

/** ONB-03 문항. 정답은 담지 않는다. */
public record LevelTestItemResponse(
    String itemCode,
    LevelTestItemType type,
    String instruction,
    String question,
    List<String> choices) {

  public static LevelTestItemResponse from(LevelTestItem item) {
    return new LevelTestItemResponse(
        item.itemCode(), item.type(), item.instruction(), item.question(), item.choices());
  }
}
