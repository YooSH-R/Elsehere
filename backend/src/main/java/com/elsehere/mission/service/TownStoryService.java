package com.elsehere.mission.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.Quest;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.entity.TownStoryStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@Transactional
public class TownStoryService {
  private final TownStoryRepository stories;
  private final FirstDayMissionRepository firstDay;

  public TownStoryService(TownStoryRepository stories, FirstDayMissionRepository firstDay) {
    this.stories = stories;
    this.firstDay = firstDay;
  }

  public TownStory start(Long userId) {
    if (firstDay.findById(userId).filter(mission -> mission.isCompleted()).isEmpty()) {
      throw new BusinessException(ErrorCode.MISSION_NOT_READY);
    }
    return stories
        .findById(userId)
        .orElseGet(
            () -> {
              firstDay
                  .findForUpdate(userId)
                  .orElseThrow(() -> new BusinessException(ErrorCode.MISSION_NOT_READY));
              return stories
                  .findById(userId)
                  .orElseGet(() -> stories.save(TownStory.start(userId)));
            });
  }

  public void accept(Long userId, int stage, String npcCode, JudgeResult.Intent intent) {
    if (intent == null || intent.error() != null || intent.ids() == null) {
      return;
    }
    stories.findForUpdate(userId).ifPresent(story -> story.accept(stage, npcCode, intent.ids()));
  }

  public Optional<FirstDayMissionService.Context> context(Long userId, String npcCode) {
    return stories
        .findById(userId)
        .filter(story -> !story.isCompleted())
        .map(
            story -> {
              TownStoryStage current = story.current();
              if (!current.npcCode().equals(npcCode)) {
                return new FirstDayMissionService.Context(
                    story.getStage(),
                    null,
                    "Stay in your own role and help the visitor if asked.",
                    List.of());
              }
              List<String> remaining = new ArrayList<>();
              for (int index = 0; index < current.goalIds().size(); index++) {
                if (!story.hasGoal(index)) {
                  remaining.add(current.goalIds().get(index));
                }
              }
              WorkerState state =
                  new WorkerState(
                      "town_story_" + story.getStage(),
                      current.instruction()
                          + " Recognize only the learner's own current request, not a quoted example or hypothetical.",
                      current.goalIds().stream()
                          .map(id -> new WorkerState.Intent(id, TownStoryStage.goalDescription(id)))
                          .toList());
              return new FirstDayMissionService.Context(
                  story.getStage(), state, current.instruction(), remaining);
            });
  }

  public FirstDayMissionResponse status(Long userId) {
    TownStory story = stories.findById(userId).orElse(null);
    boolean started = story != null;
    int current = started ? story.getStage() : 0;
    List<FirstDayMissionResponse.Step> steps = new ArrayList<>();
    for (TownStoryStage stage : TownStoryStage.values()) {
      int index = stage.ordinal();
      List<FirstDayMissionResponse.Goal> goals = new ArrayList<>();
      for (int goal = 0; goal < stage.goalIds().size(); goal++) {
        String id = stage.goalIds().get(goal);
        boolean done = started && (index < current || (index == current && story.hasGoal(goal)));
        goals.add(new FirstDayMissionResponse.Goal(id, TownStoryStage.goalTitle(id), done));
      }
      String stepStatus =
          !started || index > current ? "UPCOMING" : index < current ? "DONE" : "CURRENT";
      steps.add(
          new FirstDayMissionResponse.Step(
              index, stage.title(), "TOWN", stage.npcCode(), stepStatus, goals));
    }
    boolean completed = started && story.isCompleted();
    FirstDayMission firstDayMission = firstDay.findById(userId).orElse(null);
    String scene =
        firstDayMission == null || firstDayMission.getScene() == null
            ? "TOWN"
            : firstDayMission.getScene();
    String situation =
        !started
            ? "마을에서 새로운 하루를 시작해 볼까요?"
            : completed
                ? "마을에서의 하루를 마쳤어요."
                : !"TOWN".equals(scene) ? "마을로 나가 다음 이야기를 이어 가세요." : story.current().situation();
    // 첫날 예산에 끝낸 퀘스트의 보상을 더한다.
    int balance =
        (firstDayMission == null ? 20 : firstDayMission.getBalance())
            + Quest.earned(firstDayMission != null && firstDayMission.isCompleted(), current);
    return new FirstDayMissionResponse(
        started,
        current,
        completed,
        scene,
        false,
        false,
        "CLEAR",
        balance,
        situation,
        !started
            ? "대화 연습이에요. 실제 결제·물건 전달·외형 변경은 없어요."
            : completed
                ? "마을의 이웃들과 자유롭게 대화해 보세요."
                : !"TOWN".equals(scene) ? "마을로 나가 다음 목표를 확인하세요." : "직원에게 이야기해 보세요.",
        steps);
  }
}
