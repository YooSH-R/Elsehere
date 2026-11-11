package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.NpcReplyChunk;
import com.elsehere.ai.client.NpcReplyListener;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class EarlyReplyTest {

  private static final ReplyRequest REQUEST = new ReplyRequest("Hi there", "", null);
  private static final NpcReply REPLY = new NpcReply("Hello! Nice day.", "full-1", null, List.of());

  private final List<String> received = new ArrayList<>();
  private final NpcReplyListener recording =
      new NpcReplyListener() {
        @Override
        public void onDelta(String text) {
          received.add("delta:" + text);
        }

        @Override
        public void onChunk(NpcReplyChunk chunk) {
          received.add("audio:" + chunk.seq());
        }
      };

  @Test
  @DisplayName("쓰기로 하기 전에 받은 조각은 쥐고 있다가, 쓰기로 하면 받은 순서대로 넘기고 대답을 돌려준다")
  void keep_releasesHeldPiecesInOrderAndReturnsTheReply() {
    EarlyReply early =
        EarlyReply.start(
            REQUEST,
            listener -> {
              listener.onDelta("Hello!");
              listener.onChunk(new NpcReplyChunk(0, "Hello!", "chunk-0"));
              listener.onDelta(" Nice day.");
              return REPLY;
            },
            Runnable::run);
    assertThat(received).isEmpty();

    NpcReply reply = early.keep(recording);

    assertThat(reply).isSameAs(REPLY);
    assertThat(received).containsExactly("delta:Hello!", "audio:0", "delta: Nice day.");
  }

  @Test
  @DisplayName("쓰기로 한 뒤에 오는 조각은 쥐지 않고 바로 넘긴다")
  void keep_passesLaterPiecesStraightThrough() {
    CountDownLatch firstPassed = new CountDownLatch(1);
    NpcReplyListener countingFirst =
        new NpcReplyListener() {
          @Override
          public void onDelta(String text) {
            recording.onDelta(text);
            firstPassed.countDown();
          }

          @Override
          public void onChunk(NpcReplyChunk chunk) {
            recording.onChunk(chunk);
          }
        };
    try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
      EarlyReply early =
          EarlyReply.start(
              REQUEST,
              listener -> {
                listener.onDelta("Hello!");
                awaitQuietly(firstPassed);
                listener.onDelta(" Nice day.");
                return REPLY;
              },
              executor);

      early.keep(countingFirst);
    }

    assertThat(received).containsExactly("delta:Hello!", "delta: Nice day.");
  }

  @Test
  @DisplayName("버리면 쥐고 있던 조각을 버리고, 다음 조각이 올 때 대답 스트림을 멈추며 버린 대답이라고 알린다")
  void discard_dropsHeldPiecesAndStopsAtTheNextPiece() {
    List<Runnable> tasks = new ArrayList<>();
    AtomicBoolean readOn = new AtomicBoolean();
    List<NpcReplyListener> given = new ArrayList<>();
    EarlyReply.start(
            REQUEST,
            listener -> {
              given.add(listener);
              listener.onDelta("Hello!");
              readOn.set(true);
              return REPLY;
            },
            tasks::add)
        .discard();

    tasks.forEach(Runnable::run);

    assertThat(readOn).isFalse();
    assertThat(given.getFirst().discarded()).isTrue();
  }

  @Test
  @DisplayName("대답 호출이 실패했으면 쓰기로 할 때 그 오류를 그대로 던진다")
  void keep_failedReply_throwsTheSameError() {
    BusinessException failure = new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE);
    EarlyReply early =
        EarlyReply.start(
            REQUEST,
            listener -> {
              throw failure;
            },
            Runnable::run);

    assertThatThrownBy(() -> early.keep(recording)).isSameAs(failure);
  }

  private static void awaitQuietly(CountDownLatch latch) {
    try {
      latch.await(2, TimeUnit.SECONDS);
    } catch (InterruptedException exception) {
      Thread.currentThread().interrupt();
    }
  }
}
