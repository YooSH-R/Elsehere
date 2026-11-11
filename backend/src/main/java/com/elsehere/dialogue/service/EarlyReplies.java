package com.elsehere.dialogue.service;

import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.NpcReplyListener;
import com.elsehere.common.logging.LogContext;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;
import java.util.function.Function;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * 판정을 기다리지 않고 NPC 대답을 먼저 시작한다({@link EarlyReply}). 버린 대답만큼 AI 사용량이 늘므로 설정 {@code
 * elsehere.dialogue.early-reply}(환경 변수 {@code NPC_EARLY_REPLY})로 끌 수 있다.
 */
@Component
class EarlyReplies {

  private static final Executor CALLS = Executors.newVirtualThreadPerTaskExecutor();

  private final boolean enabled;

  EarlyReplies(@Value("${elsehere.dialogue.early-reply:true}") boolean enabled) {
    this.enabled = enabled;
  }

  boolean enabled() {
    return enabled;
  }

  /**
   * @param assumed 무례·대화 사건이 없다고 보고 만든 대답 요청
   * @param reply 받은 조각을 넘길 곳을 받아 대답을 끝까지 받는 호출
   */
  EarlyReply start(ReplyRequest assumed, Function<NpcReplyListener, NpcReply> reply) {
    return EarlyReply.start(assumed, reply, LogContext.propagating(CALLS));
  }
}
