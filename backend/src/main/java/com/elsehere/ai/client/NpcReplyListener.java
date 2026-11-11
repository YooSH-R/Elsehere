package com.elsehere.ai.client;

/**
 * NPC 대답 스트림의 글자 조각과 문장 조각 음성을 받는 대로 넘겨받는다. 대답을 Unity로 바로 흘려보낼 때 쓴다(docs/api/mvp-api.md CONV-03
 * 스트리밍).
 *
 * <p>워커 스트림을 읽는 스레드에서 차례로 부르므로, 오래 걸리는 일을 하면 다음 조각이 그만큼 늦게 온다.
 */
public interface NpcReplyListener {

  /** 조각을 받지 않는다. 대답을 끝까지 모아 한 번에 응답할 때 쓴다. */
  NpcReplyListener NONE =
      new NpcReplyListener() {
        @Override
        public void onDelta(String text) {}

        @Override
        public void onChunk(NpcReplyChunk chunk) {}
      };

  /** 대사 글자 조각. 이어 붙이면 대답 전문이 된다. */
  void onDelta(String text);

  /** 문장 조각 음성. seq 순서대로 온다. */
  void onChunk(NpcReplyChunk chunk);

  /**
   * 부른 쪽이 이 대답을 버렸는지. 판정을 기다리지 않고 먼저 시작했다가 판정 뒤에 버린 대답이면 true다. {@link AiGateway}는 버린 대답을 기록하지 않아
   * 같은 요청 ID로 다시 부른 대답의 기록을 덮지 않는다.
   */
  default boolean discarded() {
    return false;
  }
}
