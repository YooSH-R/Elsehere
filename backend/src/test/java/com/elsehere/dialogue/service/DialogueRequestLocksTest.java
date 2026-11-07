package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class DialogueRequestLocksTest {

  private final DialogueRequestLocks locks = new DialogueRequestLocks();

  @Test
  void failure_allowsRetryFromAnotherThread() throws Exception {
    UUID requestId = UUID.randomUUID();
    assertThatThrownBy(
            () ->
                locks.execute(
                    7L,
                    requestId,
                    () -> {
                      throw new IllegalStateException("AI failed");
                    }))
        .isInstanceOf(IllegalStateException.class);

    try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
      assertThat(
              executor
                  .submit(() -> locks.execute(7L, requestId, () -> "retried"))
                  .get(2, TimeUnit.SECONDS))
          .isEqualTo("retried");
    }
  }

  @ParameterizedTest
  @ValueSource(booleans = {true, false})
  void differentUserOrRequest_doesNotWait(boolean differentUser) throws Exception {
    UUID requestId = UUID.randomUUID();
    CountDownLatch started = new CountDownLatch(1);
    CountDownLatch release = new CountDownLatch(1);
    try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
      var waiting =
          executor.submit(
              () ->
                  locks.execute(
                      7L,
                      requestId,
                      () -> {
                        started.countDown();
                        try {
                          assertThat(release.await(5, TimeUnit.SECONDS)).isTrue();
                        } catch (InterruptedException exception) {
                          Thread.currentThread().interrupt();
                          throw new IllegalStateException(exception);
                        }
                        return "first";
                      }));
      try {
        assertThat(started.await(2, TimeUnit.SECONDS)).isTrue();
        Long userId = differentUser ? 8L : 7L;
        UUID independentId = differentUser ? requestId : UUID.randomUUID();
        assertThat(
                executor
                    .submit(() -> locks.execute(userId, independentId, () -> "independent"))
                    .get(2, TimeUnit.SECONDS))
            .isEqualTo("independent");
      } finally {
        release.countDown();
      }
      assertThat(waiting.get(2, TimeUnit.SECONDS)).isEqualTo("first");
    }
  }
}
