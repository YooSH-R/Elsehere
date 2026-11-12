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

  /** 이 단계에서 값을 치르는 목표. 값을 치르지 않는 단계는 null이다. */
  public Payment payment() {
    return switch (this) {
      case DINER -> new Payment("order_breakfast", 7, "아침 식사");
      case WASH -> new Payment("request_wash", 6, "세탁");
      case HAIRCUT -> new Payment("describe_style", 15, "이발");
      case FLOWERS -> new Payment("choose_flower", 5, "꽃");
      case BAKERY -> new Payment("choose_snack", 4, "간식");
      case ARCADE -> new Payment("ask_game", 2, "인형뽑기");
      case POST_OFFICE -> new Payment("ask_price", 5, "소포");
      default -> null;
    };
  }

  public String instruction() {
    return switch (this) {
      case DINER ->
          "The traveler orders breakfast and spills a drink on their clothes. Help them find the laundry when they ask. Any breakfast with a drink costs 7 coins.";
      case LAUNDRY ->
          "This is a self-service laundromat. The traveler spilled a drink and wants to know how to operate the machines and what they cost. Gus explains the machines, not a drop-off service. Explain only when asked and use only the prices and times in the Situation.";
      case BANK ->
          "The traveler needs cash for the laundry. Give ATM directions when they seek the ATM, and explain withdrawals when asked. If they ask how to withdraw but give no amount, ask how much. Never ask for a PIN or password or require a physical withdrawal.";
      case WASH ->
          "This is a self-service laundromat. The traveler has returned after asking the bank about cash and will operate the machines themselves. Do not take the learner's clothes or offer a drop-off laundry service. Help them start a washer or choose wash, dry or both when asked. Use only the machine prices and times in the Situation; do not claim a payment occurred.";
      case HAIRCUT ->
          "While their laundry is washing, the traveler asks for a haircut. Ask what style they would like. A haircut costs 15 coins.";
      case FLOWERS ->
          "The traveler liked their haircut and wants a flower to thank the barber. Offer available flowers and paper wrapping when asked. A wrapped flower costs 5 coins.";
      case THANK_BARBER ->
          "The traveler returns after choosing a flower to thank you for the haircut. Respond warmly to their thanks.";
      case BAKERY ->
          "The traveler wants a snack to thank Gus for explaining the laundry machines. Help them choose from the bakery's listed items. A snack costs 4 coins.";
      case THANK_LAUNDRY ->
          "This is a self-service laundromat. The traveler returns to remove their own clothes from a machine, then offers thanks and a snack for your guidance. Explain how to check the cycle and remove the clothes when asked. Do not claim to have washed, dried or held their clothes, and do not invent a completed cycle. Acknowledge their kindness.";
      case HOME_STORE ->
          "The traveler is considering a lamp as a travel gift and asks about shipping. Do not promise delivery; suggest asking the post office.";
      case ARCADE ->
          "The traveler stops by the arcade on the way to the post office and asks how the claw game works. Do not promise a prize. One game costs 2 coins.";
      case POST_OFFICE ->
          "The traveler asks how to send a parcel and what it costs. Sending a parcel costs 5 coins; do not claim a shipment took place.";
    };
  }

  public String title() {
    return switch (this) {
      case DINER -> "햄버거 가게에서 아침 식사";
      case LAUNDRY -> "빨래방에서 세탁 문의";
      case BANK -> "은행에서 현금 인출 문의";
      case WASH -> "셀프빨래방에서 세탁기 사용하기";
      case HAIRCUT -> "이발소에서 머리 손질";
      case FLOWERS -> "꽃집에서 감사 선물 고르기";
      case THANK_BARBER -> "이발소에 감사 전하기";
      case BAKERY -> "빵집에서 간식 고르기";
      case THANK_LAUNDRY -> "빨래방에서 빨래 꺼내고 감사 전하기";
      case HOME_STORE -> "생활용품점에서 램프 구경";
      case ARCADE -> "오락실에서 쉬어 가기";
      case POST_OFFICE -> "우체국에서 소포 문의";
    };
  }

  public String situation() {
    return switch (this) {
      case DINER -> "아침을 먹다가 옷에 음료를 쏟았어요. 햄버거 가게 직원 Rosa에게 세탁할 곳을 물어봐요.";
      case LAUNDRY -> "셀프빨래방에 왔어요. Gus에게 세탁기 사용 방법과 요금을 물어봐요.";
      case BANK -> "Henry에게 ATM 위치와 현금 인출 방법을 묻고, 얼마를 뽑을지 말해요.";
      case WASH -> "빨래방에 돌아왔어요. 직접 세탁할 수 있도록 Gus에게 세탁기 사용 방법을 확인해요.";
      case HAIRCUT -> "세탁을 기다리는 동안 이발소에 들렀어요. Tony에게 원하는 머리를 설명해요.";
      case FLOWERS -> "마음에 드는 머리를 해 준 Tony에게 고마운 마음을 전할 꽃을 Lily와 골라요.";
      case THANK_BARBER -> "꽃을 골랐어요. 이발소의 Tony에게 선물하며 고마움을 전해요.";
      case BAKERY -> "빨래를 도와준 Gus에게 전할 간식을 빵집의 Grace와 골라요.";
      case THANK_LAUNDRY -> "빨래방에 돌아왔어요. 빨래 꺼내는 방법을 확인하고 기계 사용을 도와준 Gus에게 감사를 전해요.";
      case HOME_STORE -> "여행을 기억할 선물로 램프를 구경해요. Nina에게 배송에 관해 물어봐요.";
      case ARCADE -> "우체국에 가는 길에 오락실에 들렀어요. Max에게 게임 방법을 물어봐요.";
      case POST_OFFICE -> "Linda에게 소포를 보낼 곳을 말하고 배송비를 물어봐요.";
    };
  }

  public static String goalTitle(String id) {
    return switch (id) {
      case "order_breakfast" -> "아침 주문";
      case "explain_spill" -> "옷에 쏟은 일 설명";
      case "ask_laundry" -> "세탁 방법 묻기";
      case "ask_cost" -> "세탁 가격 묻기";
      case "ask_atm" -> "ATM 위치 묻기";
      case "ask_withdrawal" -> "현금 인출 방법 묻기";
      case "request_wash" -> "세탁기 사용 방법 확인";
      case "ask_haircut" -> "이발 요청";
      case "describe_style" -> "원하는 머리 모양 설명";
      case "choose_flower" -> "선물할 꽃 고르기";
      case "ask_wrapping" -> "꽃 포장 요청";
      case "offer_flower" -> "꽃을 선물한다고 말하기";
      case "thank_barber" -> "Tony에게 감사하기";
      case "choose_snack" -> "간식 고르기";
      case "collect_laundry" -> "빨래 꺼내기 확인";
      case "thank_laundromat" -> "Gus에게 감사하기";
      case "ask_lamp" -> "램프에 대해 묻기";
      case "ask_shipping" -> "포장 또는 배송 가능 여부 묻기";
      case "ask_game" -> "게임 방법 묻기";
      case "ask_parcel" -> "소포 보내기";
      case "ask_price" -> "소포 가격 묻기";
      default -> throw new IllegalArgumentException("Unknown town story goal: " + id);
    };
  }

  /** 이 목표를 이루려면 말해야 할 정보. 퀘스트 카드 목표 아래에 보인다. */
  public static String goalHint(String id) {
    return switch (id) {
      case "order_breakfast" -> "음식과 음료를 함께";
      case "explain_spill" -> "무엇을 어디에 쏟았는지 설명하기";
      case "ask_laundry" -> "옷을 어떻게 빨 수 있는지 묻기";
      case "ask_cost" -> "세탁 가격";
      case "ask_atm" -> "ATM이 어디 있는지 묻기";
      case "ask_withdrawal" -> "현금 뽑는 방법과 금액";
      case "request_wash" -> "직접 세탁기를 쓰겠다고 말하거나 시작 방법 묻기";
      case "ask_haircut" -> "머리를 자르고 싶다고 말하기";
      case "describe_style" -> "길이와 부위";
      case "choose_flower" -> "꽃을 선물하려는 마음과 받을 사람 또는 이유를 자유롭게 말해요.";
      case "ask_wrapping" -> "꽃을 포장해 달라고 하기";
      case "offer_flower" -> "꽃을 선물로 드린다고 말하기";
      case "thank_barber" -> "무엇이 고마운지 말하기";
      case "choose_snack" -> "어떤 간식을 몇 개 살지, 누구에게 줄지 말해 보세요.";
      case "collect_laundry" -> "빨래를 꺼내도 되는지 또는 어떻게 꺼내는지 묻거나 직접 꺼낸다고 말하기";
      case "thank_laundromat" -> "기계 사용을 도와준 것에 감사하기";
      case "ask_lamp" -> "램프와 선물 받을 사람";
      case "ask_shipping" -> "램프를 포장하거나 보낼 수 있는지 묻기";
      case "ask_game" -> "인형뽑기 하는 방법 묻기 (완료 시 2코인 차감)";
      case "ask_parcel" -> "소포를 보낼 곳";
      case "ask_price" -> "배송 가격 묻기 또는 확인하기 (완료 시 5코인 차감)";
      default -> throw new IllegalArgumentException("Unknown town story goal: " + id);
    };
  }

  public static String goalDescription(String id) {
    return switch (id) {
      case "order_breakfast" ->
          "The learner orders both a breakfast food AND a drink from the diner. Ordering only one of them does not count.";
      case "explain_spill" ->
          "The learner explains what they spilled AND where on their clothes it went, such as coffee on their shirt. Saying only that there was an accident does not count.";
      case "ask_laundry" -> "The learner asks how to wash their stained clothes at the laundromat.";
      case "ask_cost" -> "The learner asks or confirms the price of the wash.";
      case "ask_atm" ->
          "The learner asks where the ATM is or clearly seeks its location, such as 'I'm looking for an ATM.' Accept imperfect wording like 'I'm finding an ATM' when it clearly requests help finding one. Merely mentioning an ATM or reporting that they already found it does not count.";
      case "ask_withdrawal" ->
          "The learner asks how to withdraw cash AND says how much they want to take out.";
      case "request_wash" ->
          "The learner asks to use or start a washing machine, asks how to start it, or confirms wash or both in response to the attendant's wash/dry/both question (for example, 'Both, please.'). Use the preceding question to understand short answers. No pickup or return time is required. A refusal, an unrelated reply, or asking only about the price does not count.";
      case "ask_haircut" -> "The learner asks for a haircut.";
      case "describe_style" ->
          "The learner describes the cut with both length (shorter, a little off, how many centimeters) AND which part, such as the sides, back or top. A vague request like 'something nice' does not count.";
      case "choose_flower" ->
          "The learner expresses an intention to buy or give flowers AND identifies a recipient OR a reason for the gift. The flower variety is optional. Accept natural paraphrases, including a thank-you gift for their barber, without requiring the hint's exact wording. Resolve pronouns using explicit facts from previous learner turns; the current utterance must add relevant information. A gift without any flower purchase/giving context is insufficient. Do not credit NPC-supplied facts, quoted examples, negatives or hypotheticals. Wrapping remains a separate objective.";
      case "ask_wrapping" -> "The learner asks for the flower to be wrapped.";
      case "offer_flower" -> "The learner offers the barber a flower as a gift.";
      case "thank_barber" ->
          "The learner thanks the barber AND says what they are thankful for, such as the haircut.";
      case "choose_snack" ->
          "The learner asks for a specific bakery item and states both the quantity and the recipient.";
      case "collect_laundry" ->
          "The learner asks whether or how to remove their own clothes from the washer or dryer, checks whether the cycle is finished, or says they are taking their clothes out. Staff do not hold or collect the clothes for them. An unrelated question or a refusal does not count.";
      case "thank_laundromat" ->
          "The learner thanks Gus and says what they appreciated, such as his explanation or help with the machines. The reason can appear in an earlier learner turn in this conversation; a bare 'thank you' with no learner-provided reason does not count. Do not require thanks for a washing or drop-off service.";
      case "ask_lamp" ->
          "The learner asks about a lamp AND says who the gift is for, such as their family back home.";
      case "ask_shipping" -> "The learner asks if the lamp can be packaged or shipped.";
      case "ask_game" -> "The learner asks how to play the claw machine.";
      case "ask_parcel" ->
          "The learner asks to send a parcel AND says where it is going, such as a city or country.";
      case "ask_price" -> "The learner asks or confirms the parcel delivery price.";
      default -> throw new IllegalArgumentException("Unknown town story goal: " + id);
    };
  }
}
