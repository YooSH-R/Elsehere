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
      goal(
          "confirm_reservation",
          "예약 확인(예약자 이름)",
          "Confirm an existing hotel reservation by giving the name it is under. Saying only that they have a reservation, without a name, does not count.",
          "예약자 이름: {name}"),
      goal(
          "store_suitcase",
          "캐리어 맡기기",
          "Ask to store the large suitcase until check-in.",
          "체크인할 때까지 큰 캐리어를 맡아 달라고 하기"),
      goal(
          "ask_cafe",
          "카페 위치 묻기",
          "Ask for a nearby cafe or directions to it.",
          "근처 카페가 어디 있는지 묻기")),
  CAFE_BREAK(
      "카페에서 잠깐 쉬어요",
      "CAFE",
      "CAFE_STAFF",
      "CLEAR",
      "호텔에서 안내받은 카페에 도착했어요. 음료를 마시며 체크인 시간을 기다려요.",
      "직원에게 주문하고, 대화를 마친 뒤 의자에 앉아 쉬어 가세요.",
      "Ask what they would like without listing or recommending drinks; after they order, ask one follow-up about size, temperature or milk. Every drink costs 4 coins. The learner may choose any drink. They need to dine in, then explicitly rest after ending the conversation. Do not mention a lost bag yet.",
      goal(
          "order_drink",
          "음료 주문(음료 이름)",
          "Order a specific drink by name, such as a latte or orange juice; any drink is valid. Asking for just 'a drink' or 'something' does not count.",
          "마실 음료 이름 (예: latte, orange juice)"),
      goal(
          "set_options",
          "크기·온도 등 옵션 고르기",
          "Choose a specific size, temperature, milk or another drink option. Saying only that they have no preference does not count.",
          "크기나 온도 하나 (예: large, iced)"),
      goal(
          "dine_in",
          "매장 이용 말하기",
          "Say the learner will have the drink here, not take away.",
          "여기서 마시겠다고 말하기 (for here)")),
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
          "분실 설명(어떤 가방을 어디에)",
          "Explain that their bag is missing AND they think they left it at the cafe. Combine details from separate learner turns; 'my bag' or 'it' may refer to the bag already established in their conversation, without repeating its size or a fixed sentence. Losing something without a location anywhere in the learner's statements does not count.",
          "작은 가방이 없고, 카페에 두고 온 것 같다고 말하기"),
      goal(
          "return_later",
          "다녀오겠다고 말하기",
          "Say they will return after looking for or retrieving the bag.",
          "카페에 가서 찾아오겠다고 말하기")),
  FIND_BAG(
      "카페에서 가방을 찾아요",
      "CAFE",
      "CAFE_STAFF",
      "CLOUDY",
      "카페 직원에게 분실물을 물어보세요. 작은 파란색 가방에는 빨간 열쇠고리가 달려 있어요.",
      "카페 직원에게 가방을 찾아 달라고 하세요. 가방 안에는 여권 지갑과 책이 있어요.",
      "You found a small blue shoulder bag with a red keyring. It contains a passport wallet and a book. Do not mention or offer the found bag before the learner asks about a lost bag. Ask for appearance and contents, one question at a time. Return it only after the learner describes matching appearance and contents and claims ownership. Do not invent other contents.",
      goal(
          "ask_lost_bag",
          "분실물 문의",
          "Ask whether a lost bag was found or request help finding it.",
          "잃어버린 가방이 들어왔는지 묻기"),
      goal(
          "describe_bag",
          "색·모양 설명",
          "Describe this bag matching blue color AND shoulder bag type or red keyring. Wrong attributes do not count.",
          "파란색 숄더백, 빨간 열쇠고리"),
      goal(
          "describe_contents",
          "내용물 설명",
          "Identify the passport or passport wallet AND book inside the bag. Wrong contents do not count.",
          "여권 지갑과 책"),
      goal(
          "confirm_ownership",
          "본인 물건 확인",
          "Explicitly confirm the described bag is theirs or ask to collect their identified bag.",
          "그 가방이 내 것이라고 말하기")),
  RAINY_STREET(
      "갑자기 비가 내려요",
      "TOWN",
      "TOWN_PASSERBY",
      "RAIN",
      "가방을 찾았어요! 호텔로 돌아가는 길에 비가 내리기 시작했어요.",
      "행인 Ben에게 물어보세요.",
      "It has started raining. The learner recovered the small bag. The drugstore sells umbrellas for 8 coins. Give directions to the store marked DRUGSTORE, not the cafe or hotel. Do not sell an umbrella yourself. Do not suggest umbrellas or the drugstore before the learner asks.",
      goal(
          "ask_umbrella_shop",
          "우산 판매처 묻기",
          "Ask where to buy an umbrella or for a store selling one.",
          "우산을 어디서 살 수 있는지 묻기"),
      goal(
          "ask_directions",
          "드럭스토어 가는 길 묻기",
          "Ask or confirm directions or the location of the drugstore.",
          "드럭스토어 가는 길")),
  BUY_UMBRELLA(
      "우산을 사요",
      "SHOP",
      "SHOP_STAFF",
      "RAIN",
      "우산을 파는 드럭스토어예요. 우산은 8코인이에요.",
      "점원에게 우산을 사세요.",
      "Sell one umbrella for exactly 8 quest coins when the learner asks for one. Do not offer an umbrella or say its price before the learner asks. Ask how they want to pay (cash/card both accepted) only after the learner decides to buy. Purchase completes only when price, purchase intent and payment choice are all covered. Never change the price or charge twice.",
      goal("ask_price", "가격 묻기", "Ask or confirm the umbrella price.", "우산 가격"),
      goal("buy_umbrella", "구매 의사 말하기", "Explicitly decide to buy an umbrella.", "우산을 사겠다고 말하기"),
      goal(
          "choose_payment",
          "결제 방법 말하기",
          "Choose cash, card or another payment method offered by the clerk.",
          "현금 또는 카드")),
  CHECK_IN(
      "이제 체크인해요",
      "HOTEL",
      "HOTEL_STAFF",
      "RAIN",
      "여권이 든 가방도 찾고 우산도 샀어요. 호텔에서 체크인을 마무리해요.",
      "호텔 직원에게 요청하세요.",
      "Check-in time has arrived. The learner recovered the small bag and passport and bought an umbrella. The large suitcase is stored at this hotel. When the learner asks, confirm the reservation, check them in and return the suitcase. Do not start check-in, confirm the reservation or bring up the bag or suitcase before the learner does. Never ask them to fetch the bag again.",
      goal(
          "found_bag",
          "가방 찾았다고 말하기",
          "Say they found or got back their small bag.",
          "가방을 찾았다고 말하기"),
      goal("request_checkin", "체크인 요청", "Ask to check in now.", "지금 체크인하고 싶다고 말하기"),
      goal(
          "show_reservation",
          "예약 정보 확인",
          "Identify or confirm their reservation, name or booking.",
          "예약자 이름: {name}"),
      goal(
          "collect_suitcase",
          "캐리어 찾기",
          "Ask to collect the stored large suitcase.",
          "맡긴 큰 캐리어를 찾겠다고 말하기"));

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

  private static Goal goal(String id, String title, String description, String hint) {
    return new Goal(id, title, description, hint);
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

  /** 이 단계에서 값을 치르는 목표. 값을 치르지 않는 단계는 null이다. */
  public Payment payment() {
    return switch (this) {
      case CAFE_BREAK -> new Payment("order_drink", 4, "카페 음료");
      case BUY_UMBRELLA -> new Payment("choose_payment", 8, "우산");
      default -> null;
    };
  }

  public List<String> intentIds() {
    return goals.stream().map(Goal::id).toList();
  }

  /**
   * 단계 목표.
   *
   * @param hint 이 목표를 이루려면 말해야 할 정보. 퀘스트 카드 목표 아래에 보인다. {name}은 예약자 이름이다
   */
  public record Goal(String id, String title, String description, String hint) {}
}
