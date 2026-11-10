package com.elsehere.onboarding.service;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class LevelTestCatalogTest {

  private final LevelTestCatalog catalog = new LevelTestCatalog();

  @Test
  @DisplayName("듣기 4문항은 말할 NPC·대사와 보기 4개 중 정답 하나를 갖는다")
  void listeningItems_haveSpeakerScriptAndAnswerAmongFourChoices() {
    assertThat(catalog.items())
        .filteredOn(item -> item.type() == LevelTestItemType.LISTEN)
        .hasSize(4)
        .allSatisfy(
            item -> {
              assertThat(item.npcCode()).isNotBlank();
              assertThat(item.script()).isNotBlank();
              assertThat(item.choices()).hasSize(4).contains(item.answer());
            });
  }

  @Test
  @DisplayName("듣기 정답의 보기 자리는 문항마다 달라 한 번호만 골라서는 다 맞힐 수 없다")
  void listeningAnswers_sitInDifferentPositions() {
    assertThat(
            catalog.items().stream()
                .filter(item -> item.type() == LevelTestItemType.LISTEN)
                .map(item -> item.choices().indexOf(item.answer()))
                .distinct()
                .count())
        .isGreaterThan(1);
  }

  @Test
  @DisplayName("말하기 2문항은 상황·말할 정보·AI 판정 기준을 갖는다")
  void speakingItems_haveSituationCuesAndCriteria() {
    assertThat(catalog.items())
        .filteredOn(item -> item.type() == LevelTestItemType.SPEAK)
        .hasSize(2)
        .allSatisfy(
            item -> {
              assertThat(item.question()).isNotBlank();
              assertThat(item.cues()).isNotEmpty();
              assertThat(item.criteria()).isNotBlank();
            });
  }
}
