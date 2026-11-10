package com.elsehere.onboarding.service;

import java.util.List;
import org.springframework.stereotype.Component;

/**
 * NPC 대화 난이도 단계(ONB-08·09). 초급·중급·고급 3단계이고 초급·중급을 고를 수 있다. 고급은 "추후 제공"으로 잠가 둔다.
 *
 * <p>고른 단계는 학습 설정의 {@code selected_level}에 저장되고, 대화를 시작할 때 그 대화의 난이도({@code levelApplied})가 되어 NPC
 * 대답·힌트·뜻 보기·피드백에 쓰인다. 단계별 NPC 말투 규칙은 AI 워커의 학습 수준 규칙(app/npc_prompt.py)에 있다.
 */
@Component
public class LevelCatalog {

  private static final List<ConversationLevel> LEVELS =
      List.of(
          new ConversationLevel(1, "초급", true, "천천히, 쉬운 단어와 짧은 문장으로 말해요."),
          new ConversationLevel(2, "중급", true, "자연스러운 일상 영어로, 관용구를 섞어 조금 더 길게 말해요."),
          new ConversationLevel(3, "고급", false, null));

  public List<ConversationLevel> levels() {
    return LEVELS;
  }

  /** 목록에 있고 지금 고를 수 있는 단계인지. */
  public boolean isSelectable(int level) {
    return LEVELS.stream().anyMatch(each -> each.available() && each.level() == level);
  }
}
