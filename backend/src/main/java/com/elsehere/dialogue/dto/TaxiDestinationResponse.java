package com.elsehere.dialogue.dto;

/**
 * 택시 기사가 이 턴에 알아들은 목적지. Unity가 택시를 그 장소로 몰고 도착하면 대화를 마친다.
 *
 * @param placeCode 갈 마을 장소 코드(CAFE, BAKERY, PARK 등)
 */
public record TaxiDestinationResponse(String placeCode) {}
