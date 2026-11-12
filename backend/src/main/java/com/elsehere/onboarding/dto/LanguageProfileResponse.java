package com.elsehere.onboarding.dto;

import com.fasterxml.jackson.annotation.JsonInclude;
import java.util.List;

/**
 * ONB-08 언어별 학습 설정과 고를 수 있는 난이도.
 *
 * @param recommendedLevel 레벨 테스트 추천 난이도. 테스트를 마치지 않았으면 {@code null}이다.
 * @param selectedLevel 사용자가 고른 난이도. 난이도 설정(ONB-09) 전에는 {@code null}이다.
 * @param levelTest 마지막 레벨 테스트. 기록이 없으면 응답에 담지 않는다.
 * @param levels 고를 수 있는 NPC 대화 난이도
 */
public record LanguageProfileResponse(
    String languageCode,
    Integer recommendedLevel,
    Integer selectedLevel,
    @JsonInclude(JsonInclude.Include.NON_NULL) LevelTestSummary levelTest,
    List<Level> levels) {

  /**
   * NPC 대화 난이도 한 단계.
   *
   * @param name 화면에 보여주는 단계 이름(초급·중급·고급)
   * @param available 지금 고를 수 있는지. {@code false}면 화면에 "추후 제공"으로 보인다.
   * @param description 단계 설명. 아직 제공하지 않는 단계는 {@code null}이다.
   */
  public record Level(int level, String name, boolean available, String description) {}
}
