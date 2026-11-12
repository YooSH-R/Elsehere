package com.elsehere.dialogue.service;

import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.locks.ReentrantLock;
import java.util.function.Supplier;
import org.springframework.stereotype.Component;

/** 같은 요청이 저장되기 전에 다시 들어와도 AI를 중복 호출하지 않도록 직렬화한다. */
@Component
class DialogueRequestLocks {

  private final ConcurrentHashMap<Object, PendingRequest> pending = new ConcurrentHashMap<>();

  <T> T execute(Long userId, UUID requestId, Supplier<T> action) {
    return execute(new RequestKey(userId, requestId), action);
  }

  /** 한 NPC 대사의 뜻 보기(CONV-04). 대답 직후 미리 만드는 요청과 학습자의 [뜻 보기]가 겹쳐도 AI를 한 번만 부른다. */
  <T> T executeMeaning(Long turnId, Supplier<T> action) {
    return execute(new MeaningKey(turnId), action);
  }

  private <T> T execute(Object key, Supplier<T> action) {
    PendingRequest request =
        pending.compute(
            key,
            (ignored, current) -> {
              PendingRequest entry = current == null ? new PendingRequest() : current;
              entry.callers++;
              return entry;
            });
    request.lock.lock();
    try {
      return action.get();
    } finally {
      request.lock.unlock();
      // 대기자가 잡고 있는 잠금이 제거되어 같은 키에 잠금이 둘 생기지 않도록 참조 수도 compute 안에서 관리한다.
      pending.compute(
          key,
          (ignored, current) -> {
            current.callers--;
            if (current.callers == 0) {
              return null;
            }
            return current;
          });
    }
  }

  private record RequestKey(Long userId, UUID requestId) {}

  private record MeaningKey(Long turnId) {}

  private static final class PendingRequest {
    private final ReentrantLock lock = new ReentrantLock();
    private int callers;
  }
}
