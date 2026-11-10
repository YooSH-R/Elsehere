package com.elsehere.ai.client;

import java.util.List;

/**
 * 레벨 테스트 말하기 판정(/v1/level-test/speaking) 결과.
 *
 * @param results 보낸 문항마다 통과 여부. 보낸 순서와 같다
 * @param feedbackKo 결과 화면에 보여 줄 한국어 한 줄 피드백
 */
public record LevelTestSpeakingResult(
    List<ItemResult> results, String feedbackKo, List<WorkerUsage> usage) {

  public record ItemResult(String itemCode, boolean passed) {}
}
