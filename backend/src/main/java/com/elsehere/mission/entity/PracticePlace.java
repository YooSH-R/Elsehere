package com.elsehere.mission.entity;

/** 장소 연습을 하는 가게와 그 가게의 직원 NPC. 선언 순서가 장소 연습 목록(PRC-01)의 순서다. */
public enum PracticePlace {
  HOTEL("호텔", "HOTEL_STAFF"),
  CAFE("카페", "CAFE_STAFF"),
  DINER("햄버거 가게", "DINER_STAFF"),
  LAUNDRY("빨래방", "LAUNDROMAT_STAFF"),
  BANK("은행", "BANK_STAFF"),
  BARBER("이발소", "BARBER_STAFF"),
  FLORIST("꽃집", "FLOWER_SHOP_STAFF"),
  BAKERY("빵집", "BAKERY_STAFF"),
  DRUGSTORE("드럭스토어", "SHOP_STAFF"),
  HOME_STORE("생활용품점", "HOME_STORE_STAFF"),
  ARCADE("오락실", "ARCADE_STAFF"),
  POST_OFFICE("우체국", "POST_OFFICE_STAFF");

  private final String title;
  private final String npcCode;

  PracticePlace(String title, String npcCode) {
    this.title = title;
    this.npcCode = npcCode;
  }

  /** 목록에 보일 장소 이름. */
  public String title() {
    return title;
  }

  public String npcCode() {
    return npcCode;
  }
}
