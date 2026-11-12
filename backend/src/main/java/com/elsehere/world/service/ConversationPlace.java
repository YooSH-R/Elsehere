package com.elsehere.world.service;

import java.util.Map;
import java.util.Optional;

/**
 * 대화가 열린 장소 → NPC가 지금 있는 곳 사실. 말을 거는 플레이어가 바로 옆에 있으므로 Unity는 플레이어가 있는 장소 코드를 보낸다.
 *
 * <p>NPC는 일과에 따라 다른 매장에 들르거나 길을 걷는다. 인격의 장면(persona.scene)은 일터만 말하므로, 이 사실이 없으면 빨래방에 들어온 식당 주인이 아직
 * 길이나 식당에 있는 것처럼 말한다. 이름은 워커의 마을 안내(ai/worker/app/npc_prompt.py TOWN_GUIDE)와 같게 둔다.
 */
public final class ConversationPlace {

  // Unity 장소 코드(PlaceScenes·매장 실내 ShopInterior.PlaceCode, Data/World/PlaceHours.json) → 그곳에 있는 모습
  private static final Map<String, String> WHEREABOUTS =
      Map.ofEntries(
          Map.entry("TOWN", "outside on the main street of Maple Town"),
          Map.entry("HOME", "inside the learner's home"),
          Map.entry("CAFE", "inside Corner Cafe"),
          Map.entry("SHOP", "inside the drugstore"),
          Map.entry("HOTEL", "inside the hotel (Maple House)"),
          Map.entry("POLICE_STATION", "inside the police station"),
          Map.entry("POST_OFFICE", "inside the post office"),
          Map.entry("BANK", "inside Town Bank"),
          Map.entry("DINER", "inside Maple Burger"),
          Map.entry("BAKERY", "inside Maple Bakery"),
          Map.entry("LAUNDROMAT", "inside Suds Laundromat"),
          Map.entry("FLOWER_SHOP", "inside Bloom Flowers (the flower shop)"),
          Map.entry("BARBER", "inside Main St. Barber"),
          Map.entry(
              "ARCADE",
              "inside the Little Arcade building (Little Arcade, with Paper & Leaf Books upstairs)"),
          Map.entry(
              "HOME_STORE",
              "inside the Home & Living building (Home & Living, with Leaf Tea Room upstairs)"),
          Map.entry("TAXI", "inside the yellow taxi"));

  private static final Map<String, String> WORKPLACE_CODES =
      Map.ofEntries(
          Map.entry("거리", "TOWN"),
          Map.entry("카페", "CAFE"),
          Map.entry("드럭스토어", "SHOP"),
          Map.entry("호텔", "HOTEL"),
          Map.entry("경찰서", "POLICE_STATION"),
          Map.entry("우체국", "POST_OFFICE"),
          Map.entry("은행", "BANK"),
          Map.entry("햄버거 가게", "DINER"),
          Map.entry("빵집", "BAKERY"),
          Map.entry("빨래방", "LAUNDROMAT"),
          Map.entry("꽃집", "FLOWER_SHOP"),
          Map.entry("이발소", "BARBER"),
          Map.entry("오락실", "ARCADE"),
          Map.entry("생활용품점", "HOME_STORE"),
          Map.entry("택시", "TAXI"));

  private ConversationPlace() {}

  /** 지금 있는 곳 사실. 장소가 없거나 모르는 코드면 지어내지 않도록 비운다. */
  public static Optional<String> describe(String placeCode) {
    return Optional.ofNullable(placeCode == null ? null : WHEREABOUTS.get(placeCode))
        .map(
            where ->
                locationSentence(where)
                    + " You are here in person with the person talking to you, so talk about your surroundings and give directions from here. If this is not your workplace, you are only visiting: do not act as if you were at your workplace or still out on the street.");
  }

  /** 저장된 대화 시작 상황에서 당시의 장소를 복원한다. */
  public static Optional<String> recordedCode(String situation) {
    if (situation == null) {
      return Optional.empty();
    }
    return WHEREABOUTS.entrySet().stream()
        .filter(place -> situation.contains(locationSentence(place.getValue())))
        .map(Map.Entry::getKey)
        .findFirst();
  }

  /** 실제 장소 기록이 없는 이전 대화에서 사용하는 NPC 근무지 코드. */
  public static Optional<String> workplaceCode(String placeName) {
    return Optional.ofNullable(placeName).map(WORKPLACE_CODES::get);
  }

  private static String locationSentence(String where) {
    return "Where you are right now: %s.".formatted(where);
  }
}
