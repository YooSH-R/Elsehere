package com.elsehere.mission.entity;

/**
 * 퀘스트에서 물건을 사거나 값을 치르는 목표. 이 목표를 마치는 순간 값만큼 코인이 줄어든다.
 *
 * @param goalId 값을 치르는 목표
 * @param coins 값(코인). NPC가 말하는 값과 같다
 * @param item 산 것. 코인 칩 아래에 "우산 −8코인"처럼 보인다
 */
public record Payment(String goalId, int coins, String item) {}
