package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.reply;
import static com.elsehere.conversation.DialogueTestData.usage;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReplyChunk;
import com.elsehere.ai.client.NpcReplyListener;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;

// 발화 전송(CONV-03)을 스트리밍으로 받을 때. 판정·대답·조각 음성은 Mock으로 정한다.
@DialogueServiceTest
class TurnServiceStreamingTest {

  private static final String SAID = "Hi there";
  private static final byte[] CHUNK_WAV = {82, 73, 70, 70, 1};
  private static final byte[] FULL_WAV = {82, 73, 70, 70, 2};

  @Autowired private TurnService turnService;

  @Autowired private ConversationService conversationService;

  @Autowired private AiGateway aiGateway;

  @Autowired private TurnRepository turnRepository;

  @Autowired private EntityManager entityManager;

  private final List<String> received = new ArrayList<>();
  private final TurnEvents events =
      new TurnEvents() {
        @Override
        public void delta(String text) {
          received.add("delta:" + text);
        }

        @Override
        public void audio(int seq, String text, byte[] wav) {
          received.add("audio:" + seq + ":" + text + ":" + (wav == null ? "none" : wav.length));
        }
      };

  private Long userId;
  private Long conversationId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    conversationId =
        conversationService
            .start(
                userId,
                new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC))
            .conversationId();
    given(aiGateway.judge(any(), any(), any(), any(), eq(SAID)))
        .willReturn(new JudgeResult(SAID, false, null, null, List.of(usage("intent"))));
    given(aiGateway.guard(any(), any(), any(), eq(SAID)))
        .willReturn(new GuardResult(new GuardResult.Guard(false, null), List.of(usage("guard"))));
    given(aiGateway.audio(any(), eq("full-1"))).willReturn(FULL_WAV);
  }

  @Test
  @DisplayName("대답 글자 조각과 문장 조각 음성을 받는 대로 넘기고, 끝나면 턴을 저장해 REST와 같은 결과를 돌려준다")
  void send_withEvents_relaysDeltasAndChunkAudioThenSavesTheTurn() {
    repliesWith(
        listener -> {
          listener.onDelta("Hello!");
          listener.onChunk(new NpcReplyChunk(0, "Hello!", "chunk-0"));
          listener.onDelta(" Nice day.");
          listener.onChunk(new NpcReplyChunk(1, "Nice day.", "chunk-1"));
        });
    given(aiGateway.audio(any(), eq("chunk-0"))).willReturn(CHUNK_WAV);
    given(aiGateway.audio(any(), eq("chunk-1"))).willReturn(CHUNK_WAV);
    UUID requestId = UUID.randomUUID();

    TurnResponse response = turnService.send(userId, conversationId, request(requestId), events);

    assertThat(received)
        .containsExactly(
            "delta:Hello!", "audio:0:Hello!:5", "delta: Nice day.", "audio:1:Nice day.:5");
    assertThat(response.npcTurn().text()).isEqualTo("Hello! Nice day.");
    assertThat(turnRepository.findByRequestId(requestId)).isPresent();
  }

  @Test
  @DisplayName("합성이 늦거나 실패해 음성이 없는 조각은 음성 없이 넘기고 워커에 받으러 가지 않는다")
  void send_withEvents_chunkWithoutAudio_relaysTextOnly() {
    repliesWith(listener -> listener.onChunk(new NpcReplyChunk(0, "Hello!", null)));

    turnService.send(userId, conversationId, request(UUID.randomUUID()), events);

    assertThat(received).containsExactly("audio:0:Hello!:none");
    then(aiGateway).should(never()).audio(any(), eq(null));
  }

  @Test
  @DisplayName("조각 음성을 받지 못하면 그 조각만 음성 없이 넘기고 대답은 끝까지 이어 간다")
  void send_withEvents_chunkDownloadFails_relaysTextOnlyAndContinues() {
    repliesWith(
        listener -> {
          listener.onChunk(new NpcReplyChunk(0, "Hello!", "chunk-0"));
          listener.onDelta(" Nice day.");
        });
    given(aiGateway.audio(any(), eq("chunk-0")))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    TurnResponse response =
        turnService.send(userId, conversationId, request(UUID.randomUUID()), events);

    assertThat(received).containsExactly("audio:0:Hello!:none", "delta: Nice day.");
    assertThat(response.npcTurn().text()).isEqualTo("Hello! Nice day.");
  }

  @Test
  @DisplayName("같은 요청 ID로 다시 보내면 조각 없이 저장된 결과만 돌려주고 AI를 다시 부르지 않는다")
  void send_withEvents_sameRequestId_replaysWithoutEvents() {
    repliesWith(listener -> listener.onDelta("Hello! Nice day."));
    UUID requestId = UUID.randomUUID();
    TurnResponse first = turnService.send(userId, conversationId, request(requestId), events);
    received.clear();

    TurnResponse again = turnService.send(userId, conversationId, request(requestId), events);

    assertThat(received).isEmpty();
    assertThat(again.npcTurn().turnId()).isEqualTo(first.npcTurn().turnId());
    then(aiGateway)
        .should()
        .reply(any(), any(), any(), any(), eq(SAID), any(), anyInt(), any(), any());
  }

  private TurnRequest request(UUID requestId) {
    return new TurnRequest(requestId, InputType.TEXT, SAID, null);
  }

  private void repliesWith(java.util.function.Consumer<NpcReplyListener> stream) {
    given(aiGateway.reply(any(), any(), any(), any(), eq(SAID), any(), anyInt(), any(), any()))
        .willAnswer(
            invocation -> {
              stream.accept(invocation.getArgument(8));
              return reply("Hello! Nice day.", "full-1");
            });
  }
}
