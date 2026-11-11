package com.elsehere.dialogue.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.world.service.PoliceOfficers;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;

/**
 * 경찰서 창구의 신고 접수(2026-11-05 결정). 정해진 단어가 아니라 의도 판정이 문맥으로 출동할 일과 장소를 고르고, 출동할 일이면 그 경찰관이 직접 순찰차로 간다고
 * 말하며 대화를 마친다. Unity는 턴 응답의 출동 장소로 순찰차를 보낸다.
 *
 * <ul>
 *   <li>신고와 장소가 나오면 그 장소로, 본길에서 생긴 일이거나 장소를 모른다고 하면 본길 순찰로 출동한다.
 *   <li>신고했지만 장소를 아직 말하지 않았으면 출동하지 않고 어디서 생긴 일인지 묻는다.
 *   <li>그 밖의 말에는 출동하지 않고, 경찰관이 순찰차가 나간다고 말하지 않게 한다.
 * </ul>
 *
 * <p>상태 문구는 평가 도구(ai/tools/police_report_eval.py)와 같게 둔다. 한쪽을 고치면 같이 고친다.
 */
final class PoliceDesk {

  private static final String REPORT = "report_incident";
  private static final String NO_PLACE = "no_place";
  private static final String PLACE = "place";
  private static final String MAIN_STREET = "the main street";

  // 판정이 고르는 장소 이름표 → Unity 순찰차 목적지 코드. 마을(TownScene)의 장소 전체이며, 2층 매장은 같은 건물 앞에 선다.
  private static final Map<String, String> PLACES = new LinkedHashMap<>();

  static {
    PLACES.put("Corner Cafe", "CAFE");
    PLACES.put("the hotel", "HOTEL");
    PLACES.put("the drugstore", "SHOP");
    PLACES.put("the learner's home", "HOME");
    PLACES.put("the post office", "POST_OFFICE");
    PLACES.put("Maple Diner", "DINER");
    PLACES.put("Town Bank", "BANK");
    PLACES.put("Suds Laundromat", "LAUNDROMAT");
    PLACES.put("Bloom Flowers (flower shop)", "FLOWER_SHOP");
    PLACES.put("Main St. Barber (barber shop)", "BARBER");
    PLACES.put("Maple Bakery", "BAKERY");
    PLACES.put("the park with the fountain", "PARK");
    PLACES.put("Little Arcade (claw machines) or Paper & Leaf Books above it", "ARCADE");
    PLACES.put("Home & Living (furniture store) or Leaf Tea Room above it", "HOME_STORE");
    PLACES.put("the ice cream truck plaza", "ICE_CREAM_PLAZA");
  }

  static final WorkerState STATE =
      new WorkerState(
          "police_desk",
          "You are at the front desk of the town police station. People come to tell you about"
              + " trouble in town. Maple Burger is the burger shop labelled Maple Diner in"
              + " the destination list.",
          List.of(
              new WorkerState.Intent(
                  REPORT,
                  "Tells you about trouble happening now or just now that the police should go and"
                      + " check, even without words like 'stolen' or 'police': something taken, lost"
                      + " or missing (a thing, a pet or a person), an accident, a fight, someone hurt"
                      + " or in danger, a break-in, a fire, someone acting strangely or scaring"
                      + " people, or any other trouble in town. Also an answer that tells you more"
                      + " about the trouble they reported, such as where it happened."),
              new WorkerState.Intent(
                  NO_PLACE,
                  "After telling you about the trouble, says they do not know, did not see or cannot"
                      + " remember where it happened."),
              new WorkerState.Intent(
                  "chat",
                  "Anything else: greetings, small talk, questions about how things work or what to"
                      + " do 'if' something happens, asking for directions because they are lost,"
                      + " trouble that is already over or solved, stories, movies and jokes.")),
          new WorkerState.Slots(
              new WorkerState.Option(PLACE, placeLabels(), "where the trouble is or happened")));

  private PoliceDesk() {}

  /** 이 NPC가 경찰서 창구에서 신고를 받는 경찰관인지 */
  static boolean serves(String npcCode) {
    return PoliceOfficers.includes(npcCode);
  }

  /** 이 턴의 판정으로 출동할지. 출동하면 장소 코드를, 본길 순찰이면 장소 코드 null을 담는다. */
  static Optional<Dispatch> dispatch(JudgeResult judge) {
    JudgeResult.Intent intent = judge.intent();
    if (intent == null || intent.error() != null || intent.ids() == null) {
      return Optional.empty();
    }
    if (intent.ids().contains(NO_PLACE)) {
      return Optional.of(Dispatch.PATROL);
    }
    if (!intent.ids().contains(REPORT)) {
      return Optional.empty();
    }
    Object place = intent.slots().get(PLACE);
    if (MAIN_STREET.equals(place)) {
      return Optional.of(Dispatch.PATROL);
    }
    String code = "Maple Burger".equals(place) ? "DINER" : PLACES.get(place);
    return Optional.ofNullable(code)
        .map(destinationCode -> new Dispatch(destinationCode, (String) place));
  }

  /** 이번 대답에서 경찰관이 할 일 */
  static String instruction(JudgeResult judge, Optional<Dispatch> dispatch) {
    if (dispatch.isPresent()) {
      String where =
          dispatch.get().placeCode() == null
              ? "along the main street to look around"
              : "to " + dispatch.get().placeLabel();
      return "They reported trouble. Tell them you will drive out "
          + where
          + " yourself in the patrol car to handle it, and say goodbye. Do not promise an exact"
          + " departure time: the patrol car may be finishing another call first.";
    }
    JudgeResult.Intent intent = judge.intent();
    if (intent != null
        && intent.error() == null
        && intent.ids() != null
        && intent.ids().contains(REPORT)) {
      return "They are reporting trouble but have not said where. Ask where it happened in a few"
          + " simple words. Do not say a patrol car is going out yet.";
    }
    return "Do not say a patrol car is going out or that you are heading out: nothing has been"
        + " reported to go out for. If they want to report something, ask what happened and where.";
  }

  private static List<String> placeLabels() {
    List<String> labels = new ArrayList<>(PLACES.keySet());
    labels.add(MAIN_STREET);
    return List.copyOf(labels);
  }

  /**
   * 순찰차 출동.
   *
   * @param placeCode Unity 순찰차 목적지 코드. null이면 본길을 순찰한다
   * @param placeLabel 판정이 고른 장소 이름표. 순찰이면 null
   */
  record Dispatch(String placeCode, String placeLabel) {
    static final Dispatch PATROL = new Dispatch(null, null);
  }
}
