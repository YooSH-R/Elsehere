package com.elsehere.review.dto;

/**
 * [다시 연습]에서 말할 조건. 시연 상황처럼 판정할 조건이 있는 상황만 있다.
 *
 * @param hint 조건 이름 아래에 작게 보일, 무엇을 말하면 되는지
 */
public record ReviewGoal(String id, String title, String hint) {}
