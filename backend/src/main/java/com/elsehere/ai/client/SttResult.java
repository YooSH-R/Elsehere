package com.elsehere.ai.client;

import java.util.List;

/**
 * 음성 인식(/v1/stt) 결과.
 *
 * @param text 전사한 말. 무음이면 빈 문자열
 * @param audioS 음성 길이(초)
 */
public record SttResult(String text, Double audioS, List<WorkerUsage> usage) {}
