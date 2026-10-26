package com.elsehere.onboarding.service;

import java.util.List;

/**
 * 레벨 테스트 문항 하나. 정답({@code answer})은 채점에만 쓰고 클라이언트에 보내지 않는다.
 *
 * @param itemCode 응답 저장(level_test_answers.item_code)에 쓰는 문항 ID
 */
public record LevelTestItem(
    String itemCode,
    LevelTestItemType type,
    String instruction,
    String question,
    List<String> choices,
    String answer) {}
