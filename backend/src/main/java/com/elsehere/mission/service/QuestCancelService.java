package com.elsehere.mission.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.Quest;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.repository.TownStoryRepository;
import com.elsehere.user.repository.UserRepository;
import java.util.Arrays;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 퀘스트 그만두기. 하던 퀘스트를 그 퀘스트의 처음으로 되돌려 목록에서 다시 시작하기를 기다리게 한다. 그 퀘스트 안에서 쓴 코인은 진행에서 계산하므로 함께 돌아오고, 끝낸
 * 퀘스트와 받은 보상은 그대로다. 새 퀘스트를 시작할 때 하던 퀘스트를 취소하는 규칙({@link ShowcaseQuestService#start})과 같은 방식이다.
 */
@Service
@RequiredArgsConstructor
@Transactional
public class QuestCancelService {
  private final UserRepository users;
  private final ConversationRepository conversations;
  private final FirstDayMissionRepository firstDay;
  private final TownStoryRepository stories;
  private final ShowcaseQuestService showcase;

  public void cancel(Long userId, String questId) {
    // 사용자 행을 잠가 대화 시작이나 퀘스트 시작과 겹치지 않게 한다. 대화 중에는 그 대화가 진행을 바꿀 수 있어 그만두지 않는다.
    users.findForUpdate(userId).orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (conversations.findFirstByUserIdAndStatus(userId, ConversationStatus.ACTIVE).isPresent()) {
      throw new BusinessException(ErrorCode.CONVERSATION_ALREADY_ACTIVE);
    }
    if (ShowcaseStage.QUEST_ID.equals(questId)) {
      showcase.cancel(userId);
      return;
    }
    Quest quest = questOf(questId);
    if (quest.isTownStory()) {
      cancelTownQuest(userId, quest);
    } else {
      cancelFirstDay(userId);
    }
  }

  // 첫날 퀘스트는 진행 행이 없는 것이 시작 전이다.
  private void cancelFirstDay(Long userId) {
    FirstDayMission mission =
        firstDay
            .findForUpdate(userId)
            .filter(running -> !running.isCompleted())
            .orElseThrow(() -> new BusinessException(ErrorCode.QUEST_NOT_IN_PROGRESS));
    firstDay.delete(mission);
  }

  // 마을 이야기는 퀘스트 하나를 끝내면 다음 퀘스트를 시작할 때까지 멈춘다. 멈춘 동안이나 다른 퀘스트를 하는 중이면 그만둘 것이 없다.
  private void cancelTownQuest(Long userId, Quest quest) {
    TownStory story =
        stories
            .findForUpdate(userId)
            .filter(running -> !running.isPaused() && !running.isCompleted())
            .filter(running -> Quest.townQuestAt(running.getStage()) == quest)
            .orElseThrow(() -> new BusinessException(ErrorCode.QUEST_NOT_IN_PROGRESS));
    story.cancelQuest();
  }

  private static Quest questOf(String questId) {
    return Arrays.stream(Quest.values())
        .filter(quest -> quest.name().equals(questId))
        .findFirst()
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }
}
