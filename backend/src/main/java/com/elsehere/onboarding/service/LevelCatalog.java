package com.elsehere.onboarding.service;

import java.util.List;
import org.springframework.stereotype.Component;

/**
 * NPC 대화 난이도 단계(ONB-08·09). MVP는 1단계만 고를 수 있다.
 *
 * <p>단계 수와 설명은 정해지지 않아, 정해지면 이 목록을 바꾼다.
 */
@Component
public class LevelCatalog {

  private static final List<ConversationLevel> LEVELS =
      List.of(
          new ConversationLevel(1, true, "천천히, 쉬운 단어와 짧은 문장으로 말해요."),
          new ConversationLevel(2, false, null),
          new ConversationLevel(3, false, null));

  public List<ConversationLevel> levels() {
    return LEVELS;
  }

  /** 목록에 있고 지금 고를 수 있는 단계인지. */
  public boolean isSelectable(int level) {
    return LEVELS.stream().anyMatch(each -> each.available() && each.level() == level);
  }
}
