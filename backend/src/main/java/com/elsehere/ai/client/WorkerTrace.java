package com.elsehere.ai.client;

import java.util.UUID;

/**
 * 워커 로그와 Spring 기록을 잇는 추적 정보. 워커 요청의 {@code X-Request-Id}, {@code X-User-Id}, {@code X-Conv-Id} 헤더가
 * 된다. 인증이나 소유권 검사를 대신하지 않는다.
 *
 * @param conversationId 대화와 관계없는 호출이면 null
 */
public record WorkerTrace(UUID requestId, Long userId, Long conversationId) {}
