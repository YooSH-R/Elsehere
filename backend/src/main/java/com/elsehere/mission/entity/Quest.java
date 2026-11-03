package com.elsehere.mission.entity;

/**
 * 퀘스트 목록의 퀘스트. 첫날 연계 미션 하나와, 마을 이야기 12단계를 화면에서 네 장으로 나눈 퀘스트다. 장은 따로 저장하지 않고 마을 이야기 단계 범위로 정한다. 앞
 * 퀘스트를 끝내야 다음이 열린다.
 *
 * <p>보상 코인은 끝낸 퀘스트로 계산해 첫날 예산에 더한다. 따로 지급 기록을 남기지 않으므로 같은 보상을 두 번 받지 않는다.
 */
public enum Quest {
  CHECK_IN("호텔 체크인과 잃어버린 가방", "체크인하고, 잃어버린 가방을 찾고, 비 오는 날 우산을 사요.", 10, -1, -1),
  SPILLED_MORNING("옷에 쏟은 아침", "다이너에서 옷에 음료를 쏟고 빨래방과 은행을 오가요.", 6, 0, 4),
  HAIRCUT_AND_FLOWERS("새 머리와 꽃 선물", "기다리는 동안 머리를 자르고 고마운 마음을 꽃으로 전해요.", 6, 4, 7),
  THANK_YOU("고마운 마음 전하기", "빵집에서 간식을 골라 빨래방 주인에게 고마움을 전해요.", 4, 7, 9),
  TOWN_ERRANDS("마을 구경과 소포", "생활용품점과 오락실을 구경하고 우체국에서 소포를 보내요.", 6, 9, 12);

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
