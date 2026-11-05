package com.elsehere.dialogue.service;

import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.locks.ReentrantLock;
import java.util.function.Supplier;
import org.springframework.stereotype.Component;

/** 같은 요청이 저장되기 전에 다시 들어와도 AI를 중복 호출하지 않도록 직렬화한다. */
@Component
class DialogueRequestLocks {

  private final ConcurrentHashMap<RequestKey, PendingRequest> pending = new ConcurrentHashMap<>();

  <T> T execute(Long userId, UUID requestId, Supplier<T> action) {
    RequestKey key = new RequestKey(userId, requestId);
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

  private static final class PendingRequest {
    private final ReentrantLock lock = new ReentrantLock();
    private int callers;
  }
}
