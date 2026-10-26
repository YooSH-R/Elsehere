package com.elsehere.onboarding.service;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class LevelTestCatalogTest {

  private final LevelTestCatalog catalog = new LevelTestCatalog();

  @Test
  @DisplayName("예시 문항 5개는 모두 보기에서 고르는 문항이고 정답이 보기 중 하나다")
  void items_areFiveChoiceItemsWithAnswerAmongChoices() {
    assertThat(catalog.items())
        .hasSize(5)
        .allSatisfy(
            item -> {
              assertThat(item.type()).isEqualTo(LevelTestItemType.CHOICE);
              assertThat(item.choices()).contains(item.answer());
            });
  }
}
