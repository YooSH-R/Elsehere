package com.elsehere.onboarding.service;

import java.util.List;
import org.springframework.stereotype.Component;

/**
 * NPC 대화 난이도 단계(ONB-08·09). 초급·중급·고급 3단계이고 MVP는 초급(1단계)만 고를 수 있다.
 *
 * <p>중급·고급의 설명과 단계별 차이는 정해지지 않아, 정해지면 이 목록을 바꾼다.
 */
@Component
public class LevelCatalog {

  private static final List<ConversationLevel> LEVELS =
      List.of(
          new ConversationLevel(1, "초급", true, "천천히, 쉬운 단어와 짧은 문장으로 말해요."),
          new ConversationLevel(2, "중급", false, null),
          new ConversationLevel(3, "고급", false, null));

  public List<ConversationLevel> levels() {
    return LEVELS;
  }

  /** 목록에 있고 지금 고를 수 있는 단계인지. */
  public boolean isSelectable(int level) {
    return LEVELS.stream().anyMatch(each -> each.available() && each.level() == level);
  }
}
