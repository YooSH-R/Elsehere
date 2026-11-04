package com.elsehere.ai.client;

import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * NPC가 곧 건네기로 한 것(/v1/npc/follow-up의 follow_up).
 *
 * @param item 대화에 나온 영어 이름
 * @param itemKo 화면에 보일 짧은 한국어 이름
 * @param waitKind 준비에 걸리는 정도(워커의 wait). QUICK·SHORT·LONG
 */
public record WorkerFollowUp(String item, String itemKo, @JsonProperty("wait") String waitKind) {}
