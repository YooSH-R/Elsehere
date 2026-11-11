package com.elsehere.dialogue.service;

import java.util.ArrayList;
import java.util.List;
import java.util.Objects;

/**
 * NPC 대답 요청에서 판정 결과로 정하는 부분. 판정을 기다리지 않고 먼저 시작한 대답({@link EarlyReply})을 쓸지 이것이 같은지로 가린다.
 *
 * @param text 학습자 말. 판정이 행동 묘사를 지운 말을 쓴다
 * @param instruction 이번 대답에서 할 일. 없으면 빈 문자열
 * @param reaction 서버가 확정한 무례·칭찬·사과 반응. 없으면 null
 */
record ReplyRequest(String text, String instruction, String reaction) {

  /** {@code other}와 다른 칸 이름(text·instruction·reaction). 먼저 시작한 대답을 버린 이유를 로그로 남길 때 쓴다. */
  List<String> changedFrom(ReplyRequest other) {
    List<String> changed = new ArrayList<>();
    if (!Objects.equals(text, other.text)) {
      changed.add("text");
    }
    if (!Objects.equals(instruction, other.instruction)) {
      changed.add("instruction");
    }
    if (!Objects.equals(reaction, other.reaction)) {
      changed.add("reaction");
    }
    return changed;
  }
}
