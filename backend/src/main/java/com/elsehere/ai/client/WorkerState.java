package com.elsehere.ai.client;

import com.fasterxml.jackson.annotation.JsonProperty;
import java.util.List;

/**
 * 의도 판정(/v1/judge)에 보내는 지금 대화 상태. 상태를 바꿀지는 판정 결과를 받아 Spring이 정한다.
 *
 * @param intents 이 상태에서 학습자가 할 수 있는 일
 * @param slots 의도와 함께 고를 값. 없으면 {@link Slots#NONE}
 */
public record WorkerState(
    String id,
    String desc,
    List<Intent> intents,
    @JsonProperty("accumulate_history") boolean accumulateHistory,
    Slots slots) {

  public WorkerState(String id, String desc, List<Intent> intents) {
    this(id, desc, intents, false, Slots.NONE);
  }

  public WorkerState(String id, String desc, List<Intent> intents, boolean accumulateHistory) {
    this(id, desc, intents, accumulateHistory, Slots.NONE);
  }

  public WorkerState(String id, String desc, List<Intent> intents, Slots slots) {
    this(id, desc, intents, false, slots);
  }

  public record Intent(String id, String desc) {}

  /** 의도와 함께 고를 값. 판정 결과의 {@code intent.slots}에 같은 이름으로 돌아온다. */
  public record Slots(Option option) {
    public static final Slots NONE = new Slots(null);
  }

  /**
   * 정해진 값 중 하나를 고르는 칸. 학습자가 말하지 않았으면 판정은 {@code "none"}을 돌려준다.
   *
   * @param slot 결과에 들어갈 칸 이름
   * @param labels 고를 수 있는 값
   * @param prompt 무엇을 말했을 때 채우는지
   */
  public record Option(String slot, List<String> labels, String prompt) {}
}
