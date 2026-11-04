package com.elsehere.mission.service;

import com.elsehere.ai.client.JudgeResult;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

@Service
@RequiredArgsConstructor
public class MissionDialogueService {
  private final FirstDayMissionService firstDay;
  private final TownStoryService town;

  public Optional<Context> context(Long userId, String npcCode) {
    Optional<FirstDayMissionService.Context> activeTown = town.context(userId, npcCode);
    if (activeTown.isPresent()) {
      return activeTown.map(detail -> new Context(true, detail));
    }
    return firstDay.context(userId, npcCode).map(detail -> new Context(false, detail));
  }

  public record Context(boolean townStory, FirstDayMissionService.Context detail) {}

  public void accept(Long userId, String npcCode, Context context, JudgeResult.Intent intent) {
    if (context.townStory()) {
      town.accept(userId, context.detail().stage(), npcCode, intent);
    } else {
      firstDay.accept(userId, context.detail().stage(), npcCode, intent);
    }
  }
}
