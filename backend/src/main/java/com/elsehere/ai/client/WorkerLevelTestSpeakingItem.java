package com.elsehere.ai.client;

import java.util.List;

/**
 * 레벨 테스트 말하기 판정(/v1/level-test/speaking)에 보내는 문항 하나와 학습자의 답.
 *
 * @param situation 화면에 보여 준 상황(한국어)
 * @param cues 화면에 보여 준 말할 정보(한국어)
 * @param criteria 통과 기준(영어). 화면에는 보여 주지 않는다
 * @param answer 학습자가 말해 받아 적은 말이나 직접 쓴 말
 */
public record WorkerLevelTestSpeakingItem(
    String itemCode, String situation, List<String> cues, String criteria, String answer) {}
