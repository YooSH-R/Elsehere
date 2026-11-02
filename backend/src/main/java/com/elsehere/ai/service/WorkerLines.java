package com.elsehere.ai.service;

import com.elsehere.ai.client.WorkerLine;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.json.JsonMapper;

/** 대화 턴을 AI 워커에 보낼 앞선 대화 줄로 바꾼다. 대답·힌트와 기억 카드 갱신이 함께 쓴다. */
@Component
@RequiredArgsConstructor
public class WorkerLines {

  private static final TypeReference<List<String>> EVENTS = new TypeReference<>() {};

  private final JsonMapper jsonMapper;

  /**
   * 최근 {@code limit}줄만 보낸다. 무례로 확정된 말만 rude로 보내고(판정 실패는 false), 학습자 줄에는 의도 판정이 고른 대화 사건을 함께 보낸다.
   * 워커는 무례 원문을 표식으로 바꾸고, 사건은 NPC의 대화 중 기분 계산에 쓴다.
   *
   * @param turns 대화 순서(seq)대로 정렬한 턴
   */
  public List<WorkerLine> recent(List<Turn> turns, int limit) {
    return turns.subList(Math.max(0, turns.size() - limit), turns.size()).stream()
        .map(
            turn ->
                turn.getSpeaker() == Speaker.NPC
                    ? WorkerLine.npc(turn.getText())
                    : WorkerLine.learner(
                        turn.getText(),
                        turn.getTranslationEn(),
                        turn.isRude(),
                        turn.getEvents() == null
                            ? null
                            : jsonMapper.readValue(turn.getEvents(), EVENTS)))
        .toList();
  }
}
