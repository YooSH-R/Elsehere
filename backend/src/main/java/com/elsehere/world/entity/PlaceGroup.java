package com.elsehere.world.entity;

/** 단어장에서 대화 표현을 나누는 장소 묶음. NPC가 일하는 곳의 용도로 정한다. */
public enum PlaceGroup {
  /** 카페, 식당, 빵집 */
  FOOD,
  /** 드럭스토어, 꽃집, 생활용품점, 오락실 */
  SHOPPING,
  /** 은행, 우체국, 빨래방, 이발소, 호텔, 택시 */
  SERVICES,
  /** 경찰서 */
  SAFETY,
  /** 거리에서 만난 사람, 근무 시간 밖에 만난 NPC */
  DAILY
}
