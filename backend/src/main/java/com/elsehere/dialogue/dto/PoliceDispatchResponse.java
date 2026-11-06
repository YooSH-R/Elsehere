package com.elsehere.dialogue.dto;

/**
 * 경찰서 창구에서 신고를 받아 순찰차가 출동할 곳. 신고를 받은 경찰관이 대화를 마치고 직접 순찰차로 간다.
 *
 * @param placeCode 출동할 마을 장소 코드(CAFE, BAKERY, PARK 등). null이면 본길을 순찰한다
 */
public record PoliceDispatchResponse(String placeCode) {}
