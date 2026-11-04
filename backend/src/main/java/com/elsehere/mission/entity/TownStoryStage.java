package com.elsehere.mission.entity;

import java.util.List;

public enum TownStoryStage {
  DINER("DINER_STAFF", List.of("order_breakfast", "explain_spill")),
  LAUNDRY("LAUNDROMAT_STAFF", List.of("ask_laundry", "ask_cost")),
  BANK("BANK_STAFF", List.of("ask_atm", "ask_withdrawal")),
  WASH("LAUNDROMAT_STAFF", List.of("request_wash")),
  HAIRCUT("BARBER_STAFF", List.of("ask_haircut", "describe_style")),
  FLOWERS("FLOWER_SHOP_STAFF", List.of("choose_flower", "ask_wrapping")),
  THANK_BARBER("BARBER_STAFF", List.of("offer_flower", "thank_barber")),
  BAKERY("BAKERY_STAFF", List.of("choose_snack")),
  THANK_LAUNDRY("LAUNDROMAT_STAFF", List.of("collect_laundry", "thank_laundromat")),
  HOME_STORE("HOME_STORE_STAFF", List.of("ask_lamp", "ask_shipping")),
  ARCADE("ARCADE_STAFF", List.of("ask_game")),
  POST_OFFICE("POST_OFFICE_STAFF", List.of("ask_parcel", "ask_price"));

  private final String npcCode;
  private final List<String> goalIds;

  TownStoryStage(String npcCode, List<String> goalIds) {
    this.npcCode = npcCode;
    this.goalIds = goalIds;
  }

  public String npcCode() {
    return npcCode;
  }

  public List<String> goalIds() {
    return goalIds;
  }

  public String instruction() {
    return switch (this) {
      case DINER ->
          "The traveler orders breakfast and spills a drink on their clothes. Help them find the laundry when they ask.";
      case LAUNDRY ->
          "The traveler spilled a drink and wants to know how to wash their clothes and the laundry price. Explain only when asked.";
      case BANK ->
          "The traveler needs cash for the laundry. Explain where the ATM is and how to withdraw without asking for a PIN or password.";
      case WASH ->
          "The traveler has returned after asking the bank about cash. Discuss starting the wash; do not claim an actual payment occurred.";
      case HAIRCUT ->
          "While their laundry is washing, the traveler asks for a haircut. Ask what style they would like.";
      case FLOWERS ->
          "The traveler liked their haircut and wants a flower to thank the barber. Offer available flowers and paper wrapping when asked.";
      case THANK_BARBER ->
          "The traveler returns after choosing a flower to thank you for the haircut. Respond warmly to their thanks.";
      case BAKERY ->
          "The traveler wants a snack to thank the laundry owner. Help them choose from the bakery's listed items.";
      case THANK_LAUNDRY ->
          "The traveler returns for their laundry and offers thanks and a snack. Acknowledge their kindness.";
      case HOME_STORE ->
          "The traveler is considering a lamp as a travel gift and asks about shipping. Do not promise delivery; suggest asking the post office.";
      case ARCADE ->
          "The traveler stops by the arcade on the way to the post office and asks how the claw game works. Do not promise a prize.";
      case POST_OFFICE ->
          "The traveler asks how to send a parcel and what it costs. Explain the listed price without claiming a payment or shipment took place.";
    };
  }

  public String title() {
    return switch (this) {
      case DINER -> "다이너에서 아침 식사";
      case LAUNDRY -> "빨래방에서 세탁 문의";
      case BANK -> "은행에서 현금 인출 문의";
      case WASH -> "빨래방에서 세탁 맡기기";
      case HAIRCUT -> "이발소에서 머리 손질";
      case FLOWERS -> "꽃집에서 감사 선물 고르기";
      case THANK_BARBER -> "이발소에 감사 전하기";
      case BAKERY -> "빵집에서 간식 고르기";
      case THANK_LAUNDRY -> "빨래방에 감사 전하기";
      case HOME_STORE -> "생활용품점에서 램프 구경";
      case ARCADE -> "오락실에서 쉬어 가기";
      case POST_OFFICE -> "우체국에서 소포 문의";
    };
  }

  public String situation() {
    return switch (this) {
      case DINER -> "아침을 먹다가 옷에 음료를 쏟았어요. 다이너 직원 Rosa에게 세탁할 곳을 물어봐요.";
      case LAUNDRY -> "옷을 세탁하려고 빨래방에 왔어요. Gus에게 방법과 가격을 물어봐요.";
      case BANK -> "세탁할 현금을 준비하려고 은행에 왔어요. Henry에게 ATM과 인출 방법을 물어봐요.";
      case WASH -> "현금 인출 방법을 알아봤어요. 빨래방의 Gus에게 세탁을 요청해요.";
      case HAIRCUT -> "세탁을 기다리는 동안 이발소에 들렀어요. Tony에게 원하는 머리를 설명해요.";
      case FLOWERS -> "마음에 드는 머리를 해 준 Tony에게 고마운 마음을 전할 꽃을 Lily와 골라요.";
      case THANK_BARBER -> "꽃을 골랐어요. 이발소의 Tony에게 선물하며 고마움을 전해요.";
      case BAKERY -> "빨래를 도와준 Gus에게 전할 간식을 빵집의 Grace와 골라요.";
      case THANK_LAUNDRY -> "빨래방에 돌아왔어요. 세탁한 옷을 찾으며 Gus에게 감사를 전해요.";
      case HOME_STORE -> "여행을 기억할 선물로 램프를 구경해요. Nina에게 배송에 관해 물어봐요.";
      case ARCADE -> "우체국에 가는 길에 오락실에 들렀어요. Max에게 게임 방법을 물어봐요.";
      case POST_OFFICE -> "마을에서의 하루를 마무리하며 Linda에게 소포 보내는 방법을 물어봐요.";
    };
  }

  public static String goalTitle(String id) {
    return switch (id) {
      case "order_breakfast" -> "아침 식사 주문";
      case "explain_spill" -> "음료를 쏟은 상황 설명";
      case "ask_laundry" -> "세탁 방법 묻기";
      case "ask_cost" -> "세탁 가격 묻기";
      case "ask_atm" -> "ATM 위치 묻기";
      case "ask_withdrawal" -> "현금 인출 방법 묻기";
      case "request_wash" -> "세탁 요청";
      case "ask_haircut" -> "이발 요청";
      case "describe_style" -> "원하는 스타일 설명";
      case "choose_flower" -> "선물할 꽃 고르기";
      case "ask_wrapping" -> "꽃 포장 요청";
      case "offer_flower" -> "꽃을 선물한다고 말하기";
      case "thank_barber" -> "Tony에게 감사하기";
      case "choose_snack" -> "감사 선물로 간식 고르기";
      case "collect_laundry" -> "세탁물 찾기 요청";
      case "thank_laundromat" -> "Gus에게 감사하기";
      case "ask_lamp" -> "램프에 관해 묻기";
      case "ask_shipping" -> "배송 가능 여부 묻기";
      case "ask_game" -> "게임 방법 묻기";
      case "ask_parcel" -> "소포 보내는 방법 묻기";
      case "ask_price" -> "소포 가격 묻기";
      default -> throw new IllegalArgumentException("Unknown town story goal: " + id);
    };
  }

  public static String goalDescription(String id) {
    return switch (id) {
      case "order_breakfast" -> "The learner orders breakfast or a breakfast item from the diner.";
      case "explain_spill" -> "The learner explains that a drink spilled on their clothes.";
      case "ask_laundry" -> "The learner asks how to wash their stained clothes at the laundromat.";
      case "ask_cost" -> "The learner asks or confirms the price of the wash.";
      case "ask_atm" -> "The learner asks where the ATM is.";
      case "ask_withdrawal" -> "The learner asks how to withdraw cash.";
      case "request_wash" -> "The learner asks to wash their clothes or start a wash.";
      case "ask_haircut" -> "The learner asks for a haircut.";
      case "describe_style" -> "The learner describes how they want their hair cut.";
      case "choose_flower" -> "The learner asks for an available flower as a thank-you gift.";
      case "ask_wrapping" -> "The learner asks for the flower to be wrapped.";
      case "offer_flower" -> "The learner offers the barber a flower as a gift.";
      case "thank_barber" -> "The learner thanks the barber for the haircut.";
      case "choose_snack" -> "The learner asks for a bakery item as a thank-you gift.";
      case "collect_laundry" -> "The learner asks to collect their laundry.";
      case "thank_laundromat" -> "The learner thanks the laundry owner for their help.";
      case "ask_lamp" -> "The learner asks about a lamp as a gift.";
      case "ask_shipping" -> "The learner asks if the lamp can be packaged or shipped.";
      case "ask_game" -> "The learner asks how to play the claw machine.";
      case "ask_parcel" -> "The learner asks how to send a parcel.";
      case "ask_price" -> "The learner asks or confirms the parcel delivery price.";
      default -> throw new IllegalArgumentException("Unknown town story goal: " + id);
    };
  }
}
