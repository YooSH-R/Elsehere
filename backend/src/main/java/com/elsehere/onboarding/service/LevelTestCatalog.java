package com.elsehere.onboarding.service;

import java.util.List;
import java.util.Optional;
import org.springframework.stereotype.Component;

/**
 * 레벨 테스트 문항(ONB-03). 테스트 방식이 정해지지 않아 MVP는 en-US 예시 선택형 문항만 서버 설정으로 둔다.
 *
 * <p>문항 원본 테이블이 없으므로 문항을 바꾸면 이 목록을 고친다. 이미 저장된 응답은 {@code itemCode}로 문항을 가리키므로 코드는 바꾸지 않는다.
 */
@Component
public class LevelTestCatalog {

  private static final String FILL_IN_BLANK = "빈칸에 알맞은 말을 고르세요.";

  private static final List<LevelTestItem> ITEMS =
      List.of(
          new LevelTestItem(
              "EN_L1_001",
              LevelTestItemType.CHOICE,
              FILL_IN_BLANK,
              "Can I ______ a coffee, please?",
              List.of("have", "has", "having", "had"),
              "have"),
          new LevelTestItem(
              "EN_L1_002",
              LevelTestItemType.CHOICE,
              FILL_IN_BLANK,
              "Where ______ the bus stop?",
              List.of("is", "are", "am", "be"),
              "is"),
          new LevelTestItem(
              "EN_L1_003",
              LevelTestItemType.CHOICE,
              "알맞은 대답을 고르세요.",
              "How are you today?",
              List.of("I'm good, thanks.", "I'm 20 years old.", "It's on Monday.", "Yes, I do."),
              "I'm good, thanks."),
          new LevelTestItem(
              "EN_L1_004",
              LevelTestItemType.CHOICE,
              FILL_IN_BLANK,
              "I ______ to the cafe yesterday.",
              List.of("go", "went", "going", "goes"),
              "went"),
          new LevelTestItem(
              "EN_L1_005",
              LevelTestItemType.CHOICE,
              "단어의 뜻을 고르세요.",
              "receipt",
              List.of("영수증", "요리법", "예약", "거스름돈"),
              "영수증"));

  public List<LevelTestItem> items() {
    return ITEMS;
  }

  public Optional<LevelTestItem> findItem(String itemCode) {
    return ITEMS.stream().filter(item -> item.itemCode().equals(itemCode)).findFirst();
  }
}
