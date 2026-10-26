package com.elsehere.onboarding.dto;

import com.elsehere.onboarding.service.ConversationLevel;
import com.fasterxml.jackson.annotation.JsonInclude;
import java.util.List;

/**
 * ONB-08 언어별 학습 설정과 고를 수 있는 난이도.
 *
 * @param recommendedLevel 레벨 테스트 추천 난이도. 테스트를 마치지 않았으면 {@code null}이다.
 * @param selectedLevel 사용자가 고른 난이도. 난이도 설정(ONB-09) 전에는 {@code null}이다.
 * @param levelTest 마지막 레벨 테스트. 기록이 없으면 응답에 담지 않는다.
 */
public record LanguageProfileResponse(
    String languageCode,
    Integer recommendedLevel,
    Integer selectedLevel,
    @JsonInclude(JsonInclude.Include.NON_NULL) LevelTestSummary levelTest,
    List<ConversationLevel> levels) {}
