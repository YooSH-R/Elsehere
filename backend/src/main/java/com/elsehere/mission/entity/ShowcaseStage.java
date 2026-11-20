package com.elsehere.mission.entity;

import java.util.List;

public enum ShowcaseStage {
  HOTEL(
      "호텔에서 저녁 나들이 준비",
      "HOTEL",
      "HOTEL_STAFF",
      "비가 와요. 호텔 직원에게 택시를 잡아 달라고 부탁해 보세요.",
      "호텔 직원에게 요청하세요.",
      "You are the hotel receptionist, not a food seller. Greet briefly and wait for the learner to ask for help. When they ask you to get or call a taxi, tell them a taxi is waiting right outside the hotel and lend them an umbrella because it is raining. If they ask where Maple Burger is instead, say it is down the street, then tell them a taxi is waiting right outside and lend them an umbrella. Do not ask where they are going; the driver will ask. They do not need to promise anything; do not ask for a promise, supply their answer or place a food order.",
      List.of(
          // 대본은 택시 부탁이지만, 길을 물어도 단계가 막히지 않도록 같은 목표로 인정한다.
          new Goal(
              "ask_for_taxi",
              "택시 부탁하기",
              "Asks the receptionist to get, call or find a taxi, for example because it is raining. Asking where the burger shop or Maple Burger is, or how to get there, also counts.",
              "택시를 잡아 달라고 부탁해 보세요."))),
  TAXI(
      "택시로 햄버거 가게 가기",
      "TOWN",
      "TAXI_DRIVER",
      "호텔 앞 택시에 타고 Maple Burger로 가 달라고 말해 보세요.",
      "택시 기사에게 요청하세요.",
      "You are the taxi driver. The passenger is already sitting in your taxi. Open with 'Where would you like to go?' Use short, beginner-friendly English. Do not use 'Hop in' or other boarding idioms. If boarding must be mentioned later, say 'Please get in.' Ask where the passenger wants to go, then use the normal taxi destination rules. Maple Burger is the burger shop labelled Maple Diner in the destination list. Never offer coffee, food or hotel services, and do not say you have arrived before the vehicle reaches the destination.",
      List.of(
          new Goal(
              "arrive_burger_shop",
              "햄버거 가게 앞에서 내리기",
              "The taxi actually arrives at the burger shop and the passenger gets out.",
              "기사에게 목적지를 말하고 도착할 때까지 기다리세요."))),
  ORDER(
      "햄버거와 사이드 주문",
      "TOWN",
      "DINER_STAFF",
      "치즈버거에서 양파를 빼고, 감자튀김과 콜라를 주문해 보세요. 여기서 먹을지 포장할지도 말해 주세요.",
      "햄버거 가게 직원에게 주문하세요.",
      "You work at Maple Burger. This is a new order, not the breakfast-spill or laundry story. Greet briefly. If you remember what this regular usually gets, you may ask in the greeting whether they want their usual; otherwise let the learner choose their food. If they want something different, take the new order without pushing the usual again, and never assume an order they have not chosen. Listen for the burger, an ingredient preference, sides and a drink, and whether they will eat here or take it away. Preserve every requested removal. After each learner answer, ask about the next missing choice in this order: burger, ingredient preference, side and drink, eating here or taking away. Ask one short question at a time; do not give the learner their answer. When asking about dining, say 'Are you eating here?' or 'Will you be eating in?' Never ask 'For here or to go?' and never supply the learner's 'to go' answer. The complete meal costs 7 coins with no surcharge. Both for here and to go are valid. Once all choices are made, confirm briefly and hand over the meal now; there is no preparation timer, seat wait, spill or laundromat errand in this demonstration.",
      List.of(
          new Goal(
              "order_burger",
              "치즈버거 주문하기",
              "Orders a burger, including a cheeseburger or another listed burger.",
              "한 개를 주문해 보세요."),
          new Goal(
              "customize_burger",
              "재료 조건 말하기",
              "States an ingredient preference such as no onions, without onions, leaving something out or explicitly keeping the usual ingredients.",
              "치즈버거의 양파를 빼 달라고 말해 보세요."),
          new Goal(
              "add_sides_drink",
              "사이드와 음료 주문하기",
              "Orders a side and drink, for example fries and cola, or clearly declines one while choosing the other.",
              "감자튀김과 콜라를 함께 주문해 보세요."),
          // 이 설명은 추천 문장을 만들 때도 쓰인다. 시연 대본이 포장이라 포장을 예로 들어 'To go, please.'가 추천되게 한다.
          new Goal(
              "choose_dining",
              "매장 이용 여부 말하기",
              "Chooses to take the order away, for example 'To go, please.' Choosing to eat here instead and full sentences are equally valid.",
              "매장에서 먹을지 포장할지 알려 주세요.")));

  public static final String QUEST_ID = "SHOWCASE_RAINY_EVENING";
  private final String title;
  private final String scene;
  private final String npcCode;
  private final String situation;
  private final String direction;
  private final String instruction;
  private final List<Goal> goals;

  ShowcaseStage(
      String title,
      String scene,
      String npcCode,
      String situation,
      String direction,
      String instruction,
      List<Goal> goals) {
    this.title = title;
    this.scene = scene;
    this.npcCode = npcCode;
    this.situation = situation;
    this.direction = direction;
    this.instruction = instruction;
    this.goals = goals;
  }

  public String title() {
    return title;
  }

  public String scene() {
    return scene;
  }

  public String npcCode() {
    return npcCode;
  }

  public String situation() {
    return situation;
  }

  /** 왼쪽 위 카드의 짧은 요청 한 줄(누구에게 무엇을). 다른 이야기 퀘스트의 단계 안내와 같은 모양이다. */
  public String direction() {
    return direction;
  }

  public String instruction() {
    return instruction
        + " Use beginner-friendly English and one or two short sentences, at most 22 words per reply."
        + " Ask only one question at a time and leave room for the learner to speak; do not narrate future quest steps.";
  }

  public List<Goal> goals() {
    return goals;
  }

  public List<String> goalIds() {
    return goals.stream().map(Goal::id).toList();
  }

  public record Goal(String id, String title, String description, String hint) {}
}
