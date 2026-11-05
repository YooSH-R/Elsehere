package com.elsehere.mission.entity;

/**
 * 퀘스트 목록의 퀘스트. 첫날 연계 미션 하나와, 마을 이야기 12단계를 둘로 나눈 연계 퀘스트 두 개다. 마을 퀘스트는 마을 이야기 단계 범위로 정하고, 이야기는 퀘스트
 * 하나를 끝내면 멈췄다가 다음 퀘스트를 시작해야 이어진다({@link TownStory#isPaused()}). 앞 퀘스트를 끝내야 다음이 열린다.
 *
 * <p>보상 코인은 끝낸 퀘스트로 계산해 코인에 더한다. 따로 지급 기록을 남기지 않으므로 같은 보상을 두 번 받지 않는다.
 */
public enum Quest {
  CHECK_IN("호텔 체크인과 잃어버린 가방", "체크인하고, 잃어버린 가방을 찾고, 비 오는 날 우산을 사요.", 10, -1, -1),
  MORNING_AND_HAIRCUT(
      "옷에 쏟은 아침과 새 머리", "식당에서 옷에 음료를 쏟고, 빨래를 맡긴 사이 머리를 잘라 고마운 마음을 꽃으로 전해요.", 15, 0, 7),
  THANKS_AND_PARCEL("고마운 마음과 선물 소포", "빨래방 주인에게 간식으로 고마움을 전하고, 고향에 선물을 소포로 부쳐요.", 10, 7, 12);

  private final String title;
  private final String description;
  private final int reward;
  private final int firstStoryStage;
  private final int endStoryStage;

  Quest(String title, String description, int reward, int firstStoryStage, int endStoryStage) {
    this.title = title;
    this.description = description;
    this.reward = reward;
    this.firstStoryStage = firstStoryStage;
    this.endStoryStage = endStoryStage;
  }

  public String title() {
    return title;
  }

  public String description() {
    return description;
  }

  public int reward() {
    return reward;
  }

  public boolean isTownStory() {
    return firstStoryStage >= 0;
  }

  /** 이 장의 첫 마을 이야기 단계 */
  public int firstStoryStage() {
    return firstStoryStage;
  }

  /** 이 장 다음 장의 첫 단계. 마을 이야기 단계가 여기까지 오면 이 장을 끝낸 것이다. */
  public int endStoryStage() {
    return endStoryStage;
  }

  /** 마을 이야기 단계가 속한 퀘스트. 이야기를 모두 끝냈으면 마지막 퀘스트다. */
  public static Quest townQuestAt(int storyStage) {
    Quest last = null;
    for (Quest quest : values()) {
      if (quest.isTownStory()) {
        last = quest;
        if (storyStage < quest.endStoryStage) {
          return quest;
        }
      }
    }
    return last;
  }

  /** 마을 이야기가 이 단계에 이르면 퀘스트 하나를 끝내고 다음 퀘스트의 첫 단계에 선 것이다. 이야기의 처음과 끝은 아니다. */
  public static boolean startsNextQuest(int storyStage) {
    for (Quest quest : values()) {
      if (quest.isTownStory() && quest.firstStoryStage > 0 && quest.firstStoryStage == storyStage) {
        return true;
      }
    }
    return false;
  }

  /** 끝낸 퀘스트의 보상 합계. 마을 이야기를 시작하지 않았으면 storyStage는 0이다. */
  public static int earned(boolean firstDayCompleted, int storyStage) {
    int sum = firstDayCompleted ? CHECK_IN.reward : 0;
    for (Quest quest : values()) {
      if (quest.isTownStory() && storyStage >= quest.endStoryStage) {
        sum += quest.reward;
      }
    }
    return sum;
  }
}
