package com.elsehere.ai.client;

import com.fasterxml.jackson.annotation.JsonProperty;
import java.util.List;

/**
 * 의도 판정(/v1/judge)에 보내는 지금 대화 상태. 상태를 바꿀지는 판정 결과를 받아 Spring이 정한다.
 *
 * @param intents 이 상태에서 학습자가 할 수 있는 일
 */
public record WorkerState(
    String id,
    String desc,
    List<Intent> intents,
    @JsonProperty("accumulate_history") boolean accumulateHistory) {

  public WorkerState(String id, String desc, List<Intent> intents) {
    this(id, desc, intents, false);
  }

  public record Intent(String id, String desc) {}
}
