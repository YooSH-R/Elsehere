package com.elsehere.mission.service;

import com.elsehere.ai.client.JudgeResult;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

/**
 * 진행 중인 퀘스트의 대화 목표({@link QuestTalk})를 고른다. 마을 이야기를 하는 중이면 그 목표를, 아니면 첫날 미션의 목표를 준다. 판정이 알아본 목표도 고른
 * 퀘스트로 넘긴다.
 */
@Service
@RequiredArgsConstructor
public class MissionDialogueService {
  private final FirstDayMissionService firstDay;
  private final TownStoryService town;

  public Optional<Context> context(Long userId, String npcCode) {
    Optional<QuestTalk> activeTown = town.context(userId, npcCode);
    if (activeTown.isPresent()) {
      return activeTown.map(detail -> new Context(true, detail));
    }
    return firstDay.context(userId, npcCode).map(detail -> new Context(false, detail));
  }

  /**
   * @param townStory 마을 이야기의 목표인지. 아니면 첫날 미션의 목표다
   */
  public record Context(boolean townStory, QuestTalk detail) {}

  public void accept(Long userId, String npcCode, Context context, JudgeResult.Intent intent) {
    if (context.townStory()) {
      town.accept(userId, context.detail().stage(), npcCode, intent);
    } else {
      firstDay.accept(userId, context.detail().stage(), npcCode, intent);
    }
  }
}
