package com.elsehere.ai.client;

/**
 * NPC 대답(/v1/npc/reply)의 상대인 학습자. 워커는 캐릭터 기본형에 맞는 호칭만 쓰고, 닉네임은 영어 이름일 때만 부른다.
 *
 * @param nickname 학습자 닉네임
 * @param avatarType 캐릭터 기본형(F·M). 모르면 null이며 워커는 성별을 짐작한 호칭을 쓰지 않는다
 */
public record WorkerLearner(String nickname, String avatarType, double speechSpeed) {}
