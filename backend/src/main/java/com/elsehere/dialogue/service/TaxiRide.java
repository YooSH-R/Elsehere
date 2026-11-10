package com.elsehere.dialogue.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;

/**
 * 택시 뒷좌석에서 기사에게 목적지를 말하는 대화(2026-11-10 결정). 정해진 단어가 아니라 의도 판정이 최근 대화까지 보고 목적지를 고르므로 "거기 가 주세요"처럼
 * 앞에서 말한 장소를 가리켜도 알아듣는다. Unity는 턴 응답의 목적지로 택시를 몰고, 도착하면 대화를 마친다.
 *
 * <ul>
 *   <li>마을 장소로 가자고 하면 그 장소로 출발하고, 기사가 지금 그곳으로 간다고 말한다. 가는 도중에 다른 곳을 말하면 그곳으로 바꾼다.
 *   <li>가자고 했지만 마을 안의 어느 곳인지 모르면 출발하지 않고 어디로 갈지 묻는다.
 *   <li>그 밖의 말에는 출발하지 않고, 기사가 부탁받지 않은 곳으로 간다고 말하지 않게 한다.
 * </ul>
 *
 * <p>상태 문구는 평가 도구(ai/tools/taxi_destination_eval.py)와 같게 둔다. 한쪽을 고치면 같이 고친다.
 */
final class TaxiRide {

  private static final String DRIVER = "TAXI_DRIVER";
  private static final String GO_TO = "go_to";
  private static final String DESTINATION = "destination";
  private static final int RECENT_LINES = 6;

  private static final String HOME = "the learner's home";

  // 판정이 고르는 장소 이름표 → Unity 택시 목적지 코드. Unity TaxiRide의 목적지 목록(TaxiCabinBuilder.FillDestinations)과
  // 같다. 북쪽 빵집·공원·오락실·생활용품점·아이스크림 트럭은 택시가 가지 않는다(순찰차만 감).
  private static final Map<String, String> PLACES = new LinkedHashMap<>();

  static {
    PLACES.put("Corner Cafe", "CAFE");
    PLACES.put("the hotel", "HOTEL");
    PLACES.put("the drugstore", "SHOP");
    PLACES.put(HOME, "HOME");
    PLACES.put("the post office", "POST_OFFICE");
    PLACES.put("Maple Diner", "DINER");
    PLACES.put("Town Bank", "BANK");
    PLACES.put("Suds Laundromat", "LAUNDROMAT");
    PLACES.put("Bloom Flowers (flower shop)", "FLOWER_SHOP");
    PLACES.put("Main St. Barber (barber shop)", "BARBER");
    PLACES.put("the police station", "POLICE_STATION");
  }

  static final WorkerState STATE =
      new WorkerState(
          "taxi_ride",
          "You drive the taxi and the learner is your passenger in the back seat. You drive only to"
              + " places in this town. Maple Burger is the burger shop labelled Maple Diner in"
              + " the destination list.",
          List.of(
              new WorkerState.Intent(
                  GO_TO,
                  "Tells you a place to drive them to now, or agrees to go to a place, even in a few"
                      + " words, in Korean or as a plain order. Also changing to another place"
                      + " during the ride. Words like 'there', 'that place', 'yes' or 'the first"
                      + " one' mean the place just talked about in the conversation."),
              new WorkerState.Intent(
                  "chat",
                  "Anything else: greetings, small talk, asking where a place is, how long the ride"
                      + " takes or how much it costs, places they went before or do not want to go,"
                      + " and talk during the ride that does not ask for a new place.")),
          false,
          new WorkerState.Slots(
              new WorkerState.Option(
                  DESTINATION,
                  List.copyOf(PLACES.keySet()),
                  "the place in this town they want to be driven to now")),
          RECENT_LINES);

  private TaxiRide() {}

  /** 이 NPC가 택시를 모는 기사인지 */
  static boolean serves(String npcCode) {
    return DRIVER.equals(npcCode);
  }

  /** 이 턴의 판정으로 출발할 곳. 마을 장소로 가자고 하지 않았으면 비어 있다. */
  static Optional<Destination> destination(JudgeResult judge) {
    if (!asksToGo(judge)) {
      return Optional.empty();
    }
    Object place = judge.intent().slots().get(DESTINATION);
    String code = "Maple Burger".equals(place) ? "DINER" : PLACES.get(place);
    return Optional.ofNullable(code)
        .map(destinationCode -> new Destination(destinationCode, (String) place));
  }

  /** 이번 대답에서 기사가 할 일 */
  static String instruction(JudgeResult judge, Optional<Destination> destination) {
    if (destination.isPresent()) {
      return "They asked to go to "
          + spoken(destination.get().placeLabel())
          + ". Say in a few words that you are driving them there now. Do not ask where to go"
          + " and do not say you have arrived.";
    }
    if (asksToGo(judge)) {
      return "They want to go somewhere, but it is not a place you drive to, or you cannot tell"
          + " which place they mean. Ask where they want to go, and if it helps, name a few places"
          + " you can take them: "
          + String.join(", ", PLACES.keySet().stream().map(TaxiRide::spoken).toList())
          + ". Do not say you are driving there yet.";
    }
    return "Do not say you are driving them to a place they did not ask for in this conversation."
        + " If they have not told you where to go yet, ask where they would like to go when it"
        + " fits.";
  }

  // 기사가 승객에게 말할 장소 이름. 판정용 이름표의 "the learner's home"은 승객에게 "their home"이다.
  private static String spoken(String placeLabel) {
    return HOME.equals(placeLabel) ? "their home" : placeLabel;
  }

  private static boolean asksToGo(JudgeResult judge) {
    JudgeResult.Intent intent = judge.intent();
    return intent != null
        && intent.error() == null
        && intent.ids() != null
        && intent.ids().contains(GO_TO);
  }

  /**
   * 택시가 갈 곳.
   *
   * @param placeCode Unity 택시 목적지 코드
   * @param placeLabel 판정이 고른 장소 이름표
   */
  record Destination(String placeCode, String placeLabel) {}
}
