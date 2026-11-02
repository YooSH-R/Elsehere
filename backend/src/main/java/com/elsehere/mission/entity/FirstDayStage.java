package com.elsehere.mission.entity;

import java.util.List;

/** 첫날 연계 미션 콘텐츠. 의도는 예문 일치가 아닌 대화 맥락의 의미로 판정한다. 날씨는 단계별로 정해 두고 장소와 무관하게 보낸다. */
public enum FirstDayStage {
  EARLY_ARRIVAL(
      "호텔에 도착했어요",
      "HOTEL",
      "HOTEL_STAFF",
      "CLEAR",
      "체크인까지 시간이 남았어요. 큰 캐리어는 호텔에 맡기고 잠시 쉬러 가려 해요.",
      "호텔 직원에게 요청하세요.",
      "It is too early to check in: the room is not ready yet. Do not check the learner in, give a room number or room key, or ask for ID, a credit card or payment. If they ask to check in, tell them early check-in is not possible today. When the learner asks, you can confirm the reservation by name, store the large suitcase until the room is ready and give directions to Corner Cafe, where they can wait. Do not offer any of these before the learner asks. The learner keeps a small blue shoulder bag.",
      goal("confirm_reservation", "예약 확인", "Confirm or identify an existing hotel reservation."),
      goal("store_suitcase", "캐리어 맡기기", "Ask to store the large suitcase until check-in."),
      goal("ask_cafe", "카페 위치 묻기", "Ask for a nearby cafe or directions to it.")),
  CAFE_BREAK(
      "카페에서 잠깐 쉬어요",
      "CAFE",
      "CAFE_STAFF",
      "CLEAR",
      "호텔에서 안내받은 카페에 도착했어요. 음료를 마시며 체크인 시간을 기다려요.",
      "직원에게 주문하고, 대화를 마친 뒤 의자에 앉아 쉬어 가세요.",
      "Offer drinks and ask one follow-up about size, temperature or milk. The learner may choose any drink. They need to dine in, then explicitly rest after ending the conversation. Do not mention a lost bag yet.",
      goal("order_drink", "음료 주문", "Order a drink, any drink is valid."),
      goal(
          "set_options",
          "음료 옵션 답하기",
          "Answer about size, temperature, milk or another drink option, including no preference."),
      goal("dine_in", "매장 이용 말하기", "Say the learner will have the drink here, not take away.")),
  MISSING_BAG(
      "작은 가방이 없어요",
      "HOTEL",
      "HOTEL_STAFF",
      "CLEAR",
      "호텔로 돌아와 여권을 꺼내려는데 작은 가방이 없어요. 카페 의자에 두고 온 것 같아요.",
      "호텔 직원에게 사정을 말하세요.",
      "The large suitcase is safely stored at this hotel. The learner left their separate small shoulder bag at the cafe. They need to go back for it before checking in. Do not claim the hotel has the small bag. Do not bring up the missing bag before the learner explains it.",
      goal(
          "report_left_bag",
          "분실 상황 설명",
          "Explain the small bag is missing or was left at the cafe."),
      goal(
          "return_later",
          "다녀오겠다고 말하기",
          "Say they will return after looking for or retrieving the bag.")),
  FIND_BAG(
      "카페에서 가방을 찾아요",
      "CAFE",
      "CAFE_STAFF",
      "CLOUDY",
      "카페 직원에게 분실물을 물어보세요. 작은 파란색 가방에는 빨간 열쇠고리가 달려 있어요.",
      "카페 직원에게 가방을 찾아 달라고 하세요. 가방 안에는 여권 지갑과 책이 있어요.",
      "You found a small blue shoulder bag with a red keyring. It contains a passport wallet and a book. Do not mention or offer the found bag before the learner asks about a lost bag. Ask for appearance and contents, one question at a time. Return it only after the learner describes matching appearance and contents and claims ownership. Do not invent other contents.",
      goal(
          "ask_lost_bag", "분실물 문의", "Ask whether a lost bag was found or request help finding it."),
      goal(
          "describe_bag",
          "색·모양 설명",
          "Describe this bag matching blue color AND shoulder bag type or red keyring. Wrong attributes do not count."),
      goal(
          "describe_contents",
          "내용물 설명",
          "Identify the passport or passport wallet AND book inside the bag. Wrong contents do not count."),
      goal(
          "confirm_ownership",
          "본인 물건 확인",
          "Explicitly confirm the described bag is theirs or ask to collect their identified bag.")),
  RAINY_STREET(
      "갑자기 비가 내려요",
      "TOWN",
      "TOWN_PASSERBY",
      "RAIN",
      "가방을 찾았어요! 호텔로 돌아가는 길에 비가 내리기 시작했어요.",
      "행인 Ben에게 물어보세요.",
      "It has started raining. The learner recovered the small bag. The drugstore sells umbrellas for 8 coins; the learner has 20 quest coins. Give directions to the store marked DRUGSTORE, not the cafe or hotel. Do not sell an umbrella yourself. Do not suggest umbrellas or the drugstore before the learner asks.",
      goal(
          "ask_umbrella_shop",
          "우산 판매처 묻기",
          "Ask where to buy an umbrella or for a store selling one."),
      goal(
          "ask_directions",
          "드럭스토어 가는 길 묻기",
          "Ask or confirm directions or the location of the drugstore.")),
  BUY_UMBRELLA(
      "우산을 사요",
      "SHOP",
      "SHOP_STAFF",
      "RAIN",
      "우산을 파는 드럭스토어예요. 여행용 예산은 20코인, 우산은 8코인이에요.",
      "점원에게 우산을 사세요.",
      "Sell one umbrella for exactly 8 quest coins when the learner asks for one. Balance before purchase is 20. Do not offer an umbrella or say its price before the learner asks. Ask how they want to pay (cash/card both accepted) only after the learner decides to buy. Purchase completes only when price, purchase intent and payment choice are all covered. Never change the price or charge twice.",
      goal("ask_price", "가격 묻기", "Ask or confirm the umbrella price."),
      goal("buy_umbrella", "구매 의사 말하기", "Explicitly decide to buy an umbrella."),
      goal(
          "choose_payment",
          "결제 방법 말하기",
          "Choose cash, card or another payment method offered by the clerk.")),
  CHECK_IN(
      "이제 체크인해요",
      "HOTEL",
      "HOTEL_STAFF",
      "RAIN",
      "여권이 든 가방도 찾고 우산도 샀어요. 호텔에서 오늘의 여정을 마무리해요.",
      "호텔 직원에게 요청하세요.",
      "Check-in time has arrived. The learner recovered the small bag and passport, bought an umbrella and has 12 quest coins left. The large suitcase is stored at this hotel. When the learner asks, confirm the reservation, check them in and return the suitcase. Do not start check-in, confirm the reservation or bring up the bag or suitcase before the learner does. Never ask them to fetch the bag again.",
      goal("found_bag", "가방 찾았다고 말하기", "Say they found or got back their small bag."),
      goal("request_checkin", "체크인 요청", "Ask to check in now."),
      goal(
          "show_reservation",
          "예약 정보 확인",
          "Identify or confirm their reservation, name or booking."),
      goal("collect_suitcase", "캐리어 찾기", "Ask to collect the stored large suitcase."));

  private final String title;
  private final String scene;
  private final String npc;
  private final String weather;
  private final String situation;
  private final String direction;
  private final String instruction;
  private final List<Goal> goals;

  FirstDayStage(
      String title,
      String scene,
      String npc,
      String weather,
      String situation,
      String direction,
      String instruction,
      Goal... goals) {
    this.title = title;
    this.scene = scene;
    this.npc = npc;
    this.weather = weather;
    this.situation = situation;
    this.direction = direction;
    this.instruction = instruction;
    this.goals = List.of(goals);
  }

  private static Goal goal(String id, String title, String description) {
    return new Goal(id, title, description);
  }

  public String title() {
    return title;
  }

  public String scene() {
    return scene;
  }

  public String npc() {
    return npc;
  }

  /** 이 단계의 마을 날씨 이름. Unity 날씨 정의와 이름만 맞추면 되므로 enum으로 막지 않는다(새 날씨는 이 값만 바꾼다). */
  public String weather() {
    return weather;
  }

  public String situation() {
    return situation;
  }

  public String direction() {
    return direction;
  }

  public String instruction() {
    return instruction;
  }

  public List<Goal> goals() {
    return goals;
  }

  public List<String> intentIds() {
    return goals.stream().map(Goal::id).toList();
  }

  public record Goal(String id, String title, String description) {}
}
