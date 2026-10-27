package com.elsehere.ai.client;

/**
 * 워커가 모델을 한 번 부를 때마다 돌려주는 사용량. 크레딧 차감액이 아니라 비용 근거다.
 *
 * @param step 호출 역할(intent, translate, npc, suggest, stt 등)
 * @param model 실제로 쓴 모델. 대체 모델이면 그 이름
 * @param audioS 음성 인식만: 음성 길이(초). 토큰 대신 쓴다
 */
public record WorkerUsage(
    String step,
    String model,
    Integer inputTokens,
    Integer outputTokens,
    Double audioS,
    Double latencyS,
    Boolean fallback) {}
