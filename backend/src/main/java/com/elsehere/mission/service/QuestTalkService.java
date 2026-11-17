package com.elsehere.mission.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.mission.entity.PracticeScenario;
import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

/**
 * 대화의 퀘스트 대화 목표({@link QuestTalk})를 고르는 창구. 대화 쪽(대화 시작·발화·도움말)은 퀘스트 종류를 몰라도 여기서 목표를 받고, 판정이 알아본 목표도
 * 여기로 넘긴다.
 *
 * <ul>
 *   <li>복습 대화: 퀘스트 대화 목표가 없다.
 *   <li>장소 연습 대화: 학습자가 고른 상황의 목표({@link PlacePracticeService}).
 *   <li>그 밖의 대화: 진행 중인 퀘스트의 목표. 마을 이야기를 하는 중이면 그 목표({@link TownStoryService}), 아니면 첫날 미션의 목표({@link
 *       FirstDayMissionService}).
 * </ul>
 */
@Service
@RequiredArgsConstructor
public class QuestTalkService {

  private final FirstDayMissionService firstDay;
  private final TownStoryService town;
  private final PlacePracticeService practice;
  private final ShowcaseQuestService showcase;

  /**
   * 대화를 시작할 때의 대화 목표.
   *
   * @param review 복습 대화인지
   * @param scenario 장소 연습으로 고른 상황. 장소 연습이 아니면 null
   */
  public Optional<SelectedQuest> forStart(
      Long userId, String npcCode, boolean review, PracticeScenario scenario) {
    if (review) {
      return Optional.empty();
    }
    if (scenario != null) {
      return Optional.of(new SelectedQuest(Source.PRACTICE, practice.context(scenario, List.of())));
    }
    return activeQuest(userId, npcCode);
  }

  /**
   * [다시 연습] 대화의 목표. 시연 상황을 다시 연습하면 그 단계의 조건 중 이 대화에서 아직 말하지 않은 것을 판정하고, 그 밖의 저장한 상황은 판정할 목표가 없다.
   *
   * @param scenarioKey 복습 카드에 저장한 상황 키
   * @param completedGoalIds 이 연습 대화에서 이미 말한 조건
   */
  public Optional<SelectedQuest> forReview(String scenarioKey, List<String> completedGoalIds) {
    return ShowcaseReviewGoals.talk(scenarioKey, completedGoalIds)
        .map(talk -> new SelectedQuest(Source.REVIEW, talk));
  }

  /** 발화할 때의 대화 목표. 장소 연습은 그 대화에서 이미 이룬 목표를 뺀다. */
  public Optional<SelectedQuest> forTurn(Long userId, String npcCode, Conversation conversation) {
    if (conversation.isReview()) {
      return Optional.empty();
    }
    if (conversation.isPractice()) {
      return practice.context(conversation).map(talk -> new SelectedQuest(Source.PRACTICE, talk));
    }
    return activeQuest(userId, npcCode);
  }

  /** 판정이 알아본 목표를 그 대화 목표를 낸 쪽에 넘긴다. 무례가 아닌 턴을 저장하는 트랜잭션에서 잠근 대화로 부른다. */
  public void accept(
      Long userId,
      String npcCode,
      Conversation conversation,
      SelectedQuest quest,
      JudgeResult.Intent intent) {
    switch (quest.source()) {
      case PRACTICE -> practice.accept(conversation, intent);
      case TOWN_STORY -> town.accept(userId, quest.talk().stage(), npcCode, intent);
      case FIRST_DAY -> firstDay.accept(userId, quest.talk().stage(), npcCode, intent);
      case SHOWCASE -> showcase.accept(userId, quest.talk().stage(), npcCode, intent);
      case REVIEW -> recordReviewGoals(conversation, quest.talk(), intent);
    }
  }

  // 판정이 알아본 조건 중 이 연습의 조건만, 말한 순서대로 그 연습 대화에 더한다.
  private static void recordReviewGoals(
      Conversation conversation, QuestTalk talk, JudgeResult.Intent intent) {
    if (intent == null || intent.error() != null || intent.ids() == null) {
      return;
    }
    talk.goals()
        .ifPresent(
            goals -> {
              List<String> allowed =
                  goals.state().intents().stream().map(WorkerState.Intent::id).toList();
              List<String> said = new ArrayList<>(conversation.getReviewCompletedGoals());
              intent.ids().stream()
                  .filter(allowed::contains)
                  .filter(id -> !said.contains(id))
                  .forEach(said::add);
              conversation.recordReviewGoals(said);
            });
  }

  // 마을 이야기를 하는 중이면 그 목표, 아니면 첫날 미션의 목표. 두 퀘스트는 앞 퀘스트를 끝내야 다음이 열려 동시에 진행되지 않는다.
  private Optional<SelectedQuest> activeQuest(Long userId, String npcCode) {
    Optional<QuestTalk> demo = showcase.context(userId, npcCode);
    // 시연 서버에서는 시연 퀘스트만 대화 목표를 낸다. 시작 전에는 진행 중이던 다른 퀘스트도 목표를 내지 않는다.
    if (demo.isPresent() || showcase.isShowcaseServer()) {
      return demo.map(talk -> new SelectedQuest(Source.SHOWCASE, talk));
    }
    Optional<QuestTalk> activeTown = town.context(userId, npcCode);
    if (activeTown.isPresent()) {
      return activeTown.map(talk -> new SelectedQuest(Source.TOWN_STORY, talk));
    }
    return firstDay.context(userId, npcCode).map(talk -> new SelectedQuest(Source.FIRST_DAY, talk));
  }

  /** 대화 목표를 낸 쪽. */
  public enum Source {
    FIRST_DAY,
    TOWN_STORY,
    SHOWCASE,
    PRACTICE,
    REVIEW
  }

  /** 고른 대화 목표와 그 목표를 낸 쪽. */
  public record SelectedQuest(Source source, QuestTalk talk) {

    /**
     * 대화를 시작할 때 퀘스트 단계와 남은 목표를 대화에 남긴다. 판정할 목표가 있는 퀘스트 대화(그 단계의 대상 NPC)만 남긴다. 장소 연습은 상황을 따로 남긴다.
     */
    public void recordStage(Conversation conversation) {
      if (source == Source.SHOWCASE) {
        // 택시도 시작 상황을 남겨 한 발화 대화의 피드백과 후속 처리에 사용한다.
        conversation.attachShowcaseStage(talk.stage());
      }
      if (!(talk instanceof QuestTalk.Goals goals)) {
        return;
      }
      switch (source) {
        case SHOWCASE -> {
          conversation.recordShowcaseGoals(goals.remainingGoals());
        }
        case TOWN_STORY -> {
          conversation.attachTownStoryStage(goals.stage());
          conversation.recordTownStoryGoals(goals.remainingGoals());
        }
        case FIRST_DAY -> {
          conversation.attachFirstDayStage(goals.stage());
          conversation.recordFirstDayGoals(goals.remainingGoals());
        }
        case PRACTICE -> {
          // 장소 연습은 대화를 만들 때 상황(attachPracticeScenario)을 남긴다.
        }
        case REVIEW -> {}
      }
    }

    /** 이 대화를 시작한 단계의 목표인지. 대화 중에 퀘스트 단계가 넘어갔으면 아니다. 장소 연습은 단계가 없어 늘 그렇다. */
    public boolean startedAtThisStage(Conversation conversation) {
      return switch (source) {
        case PRACTICE, REVIEW -> true;
        case TOWN_STORY -> Objects.equals(conversation.getTownStoryStage(), talk.stage());
        case FIRST_DAY -> Objects.equals(conversation.getFirstDayStage(), talk.stage());
        case SHOWCASE -> Objects.equals(conversation.getShowcaseStage(), talk.stage());
      };
    }
  }
}
