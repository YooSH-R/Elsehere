package com.elsehere.dialogue.service;

import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.NpcReplyChunk;
import com.elsehere.ai.client.NpcReplyListener;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.Executor;
import java.util.function.Consumer;
import java.util.function.Function;

/**
 * 판정을 기다리지 않고 먼저 시작한 NPC 대답(CONV-03). 받은 조각은 쥐고 있다가, 판정 뒤에 정한 대답 요청이 {@link #request()}와 같으면 {@link
 * #keep}으로 넘기고 다르면 {@link #discard}로 버린다.
 *
 * <p>버린 대답은 다음 조각이 올 때 워커 스트림을 끊어 생성과 음성 합성을 멈추게 하고, AI 호출 기록을 남기지 않는다({@link
 * NpcReplyListener#discarded()}).
 */
final class EarlyReply {

  private final ReplyRequest request;
  private final Held held = new Held();
  private final CompletableFuture<NpcReply> call;

  private EarlyReply(
      ReplyRequest request, Function<NpcReplyListener, NpcReply> reply, Executor executor) {
    this.request = request;
    this.call = CompletableFuture.supplyAsync(() -> reply.apply(held), executor);
  }

  /**
   * @param reply 받은 조각을 넘길 곳을 받아 대답을 끝까지 받는 호출
   */
  static EarlyReply start(
      ReplyRequest request, Function<NpcReplyListener, NpcReply> reply, Executor executor) {
    return new EarlyReply(request, reply, executor);
  }

  /** 판정 전에 가정한 대답 요청. 무례·대화 사건이 없는 턴의 요청이다. */
  ReplyRequest request() {
    return request;
  }

  /** 쥐고 있던 조각부터 {@code listener}에 넘기고 대답이 끝나기를 기다린다. 대답이 실패했으면 그 오류를 그대로 던진다. */
  NpcReply keep(NpcReplyListener listener) {
    held.release(listener);
    try {
      return call.join();
    } catch (CompletionException exception) {
      if (exception.getCause() instanceof RuntimeException cause) {
        throw cause;
      }
      throw exception;
    }
  }

  /** 쥐고 있던 조각을 버리고, 다음 조각이 오면 대답 스트림을 끊는다. 이미 넘기기 시작한 대답이면 아무것도 하지 않는다. */
  void discard() {
    held.discard();
  }

  // 워커 스트림을 읽는 스레드와 판정을 기다린 스레드가 함께 쓰므로 조각 넘기기와 상태 바꾸기를 한 잠금으로 묶는다.
  private static final class Held implements NpcReplyListener {

    private final List<Consumer<NpcReplyListener>> pieces = new ArrayList<>();
    private NpcReplyListener target;
    private boolean discarded;

    @Override
    public synchronized void onDelta(String text) {
      pass(listener -> listener.onDelta(text));
    }

    @Override
    public synchronized void onChunk(NpcReplyChunk chunk) {
      pass(listener -> listener.onChunk(chunk));
    }

    @Override
    public synchronized boolean discarded() {
      return discarded;
    }

    synchronized void release(NpcReplyListener listener) {
      target = listener;
      pieces.forEach(piece -> piece.accept(listener));
      pieces.clear();
    }

    synchronized void discard() {
      if (target != null) {
        return;
      }
      discarded = true;
      pieces.clear();
    }

    private void pass(Consumer<NpcReplyListener> piece) {
      if (discarded) {
        throw new Discarded();
      }
      if (target == null) {
        pieces.add(piece);
      } else {
        piece.accept(target);
      }
    }
  }

  // 버린 대답의 스트림 읽기를 멈춘다. 워커 클라이언트가 스트림을 닫아 워커가 연결 끊김을 알게 된다.
  private static final class Discarded extends RuntimeException {

    Discarded() {
      super("early reply discarded", null, false, false);
    }
  }
}
