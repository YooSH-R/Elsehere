package com.elsehere.onboarding.service;

import java.util.List;
import java.util.Optional;
import org.springframework.stereotype.Component;

/**
 * 레벨 테스트 문항(ONB-03). en-US 듣기 4문항과 말하기 2문항이고, 결과로 초급(1)과 중급(2)을 가른다.
 *
 * <p>지금 퀘스트가 초급 기준이라, 문항은 그보다 위(B1)를 알아듣고 말할 수 있는지를 본다. 문법 문항은 두지 않는다. 듣기는 중급 NPC가 실제로 말하는 길이(두 문장,
 * 20단어 안팎)에 정보 2~3개와 일상 관용구를 담고, 오답 보기는 그 정보 하나만 바꾸어 대충 들으면 틀리게 했다. 정답 보기 자리는 문항마다 다르다. 말하기는 퀘스트와
 * 같은 과제를 한두 문장으로 해내는지 본다.
 *
 * <p>문항 원본 테이블이 없으므로 문항을 바꾸면 이 목록을 고친다. 이미 저장된 응답은 {@code itemCode}로 문항을 가리키므로 코드는 바꾸지 않는다.
 */
@Component
public class LevelTestCatalog {

  private static final List<LevelTestItem> ITEMS =
      List.of(
          LevelTestItem.listening(
              "EN_LT_L1",
              "Jake · 호텔 직원",
              "HOTEL_STAFF",
              "Check-in's not till three, but you're welcome to leave your suitcase with us and"
                  + " come back later.",
              List.of(
                  "3시까지 체크아웃해야 하니 캐리어를 챙기라고 한다",
                  "체크인은 3시부터이고, 캐리어를 맡기고 나중에 와도 된다",
                  "3시 전에 체크인할 수 있으니 캐리어를 방에 두라고 한다",
                  "캐리어는 3개까지만 맡길 수 있다"),
              "체크인은 3시부터이고, 캐리어를 맡기고 나중에 와도 된다"),
          LevelTestItem.listening(
              "EN_LT_L2",
              "Gus · 빨래방 주인",
              "LAUNDROMAT_STAFF",
              "The machine on the left's out of order, so use the one by the window. And it only"
                  + " takes quarters.",
              List.of(
                  "왼쪽 기계는 고장이라 창가 기계를 쓰고, 25센트 동전만 된다",
                  "창가 기계는 고장이라 왼쪽 기계를 쓰고, 동전은 안 받는다",
                  "왼쪽 기계를 쓰되, 세탁물은 4분의 1만 넣으라고 한다",
                  "창가 기계는 25분 뒤에 쓸 수 있다"),
              "왼쪽 기계는 고장이라 창가 기계를 쓰고, 25센트 동전만 된다"),
          LevelTestItem.listening(
              "EN_LT_L3",
              "Rosa · 식당 직원",
              "DINER_STAFF",
              "Oh no, you're soaked! Breakfast's on the house today. The laundromat's two doors"
                  + " down if you wanna rinse that out.",
              List.of(
                  "아침은 집에서 먹으라고 하고, 빨래방은 두 블록 떨어져 있다고 한다",
                  "젖은 옷을 말려 주는 대신 아침값을 더 받겠다고 한다",
                  "오늘은 일찍 닫으니 빨래방 옆 식당으로 가라고 한다",
                  "흠뻑 젖었다며 오늘 아침값은 받지 않겠다고 하고, 두 집 건너 빨래방에서 헹구라고 한다"),
              "흠뻑 젖었다며 오늘 아침값은 받지 않겠다고 하고, 두 집 건너 빨래방에서 헹구라고 한다"),
          LevelTestItem.listening(
              "EN_LT_L4",
              "Linda · 우체국 직원",
              "POST_OFFICE_STAFF",
              "If you need it there by Friday, you'd better go with priority. Standard might not"
                  + " make it till next week.",
              List.of(
                  "빠른우편은 금요일에만 보낼 수 있다",
                  "일반 우편이 더 빨라서 다음 주 전에 도착한다",
                  "금요일까지 도착해야 하면 빠른우편으로 보내야 하고, 일반 우편은 다음 주에나 도착할 수 있다",
                  "금요일이 지나서 다시 오면 먼저 처리해 주겠다"),
              "금요일까지 도착해야 하면 빠른우편으로 보내야 하고, 일반 우편은 다음 주에나 도착할 수 있다"),
          LevelTestItem.speaking(
              "EN_LT_S1",
              "카페에 가방을 두고 왔어요. 가방이 어떻게 생겼는지 말하고 찾아 달라고 하세요.",
              List.of("파란 숄더백", "빨간 열쇠고리", "안에 여권 지갑과 책"),
              "Says they left or lost a bag at the cafe and describes it with all three details"
                  + " (a blue shoulder bag, a red keyring, a passport wallet and a book inside), in"
                  + " English sentences rather than a list of words."),
          LevelTestItem.speaking(
              "EN_LT_S2",
              "어제 빨래방에 맡긴 셔츠에 커피 얼룩이 그대로 남았어요. 무슨 일인지 말하고 어떻게 해 달라고 정중히 부탁하세요.",
              List.of("커피 얼룩이 그대로 남음", "다시 빨아 달라고 부탁"),
              "Explains in English sentences that the coffee stain is still on the shirt they"
                  + " picked up, and politely asks for something to be done about it (for example,"
                  + " washing it again)."));

  public List<LevelTestItem> items() {
    return ITEMS;
  }

  public Optional<LevelTestItem> findItem(String itemCode) {
    return ITEMS.stream().filter(item -> item.itemCode().equals(itemCode)).findFirst();
  }
}
