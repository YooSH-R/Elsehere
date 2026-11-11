package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.reply;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.NpcReplyChunk;
import com.elsehere.ai.client.NpcReplyListener;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.dialogue.dto.ConversationStartResponse;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.function.Consumer;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;

// 대화 시작(CONV-01)을 스트리밍으로 받을 때. 첫 인사와 조각 음성은 Mock으로 정한다.
@DialogueServiceTest
class ConversationServiceStreamingTest {

  private static final byte[] CHUNK_WAV = {82, 73, 70, 70, 1};
  private static final byte[] FULL_WAV = {82, 73, 70, 70, 2};

  @Autowired private ConversationService conversationService;

  @Autowired private ConversationRepository conversationRepository;

  @Autowired private AiGateway aiGateway;

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

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    given(aiGateway.audio(any(), eq("chunk-0"))).willReturn(CHUNK_WAV);
    given(aiGateway.audio(any(), eq("chunk-1"))).willReturn(CHUNK_WAV);
    given(aiGateway.audio(any(), eq("full-1"))).willReturn(FULL_WAV);
  }

  @Test
  @DisplayName("AI가 만드는 첫 인사의 글자 조각과 문장 조각 음성을 받는 대로 넘기고, 끝나면 대화를 저장해 REST와 같은 결과를 돌려준다")
  void start_withEvents_relaysGreetingDeltasAndChunkAudioThenSaves() {
    greetsWith(
        listener -> {
          listener.onDelta("Hi!");
          listener.onChunk(new NpcReplyChunk(0, "Hi!", "chunk-0"));
          listener.onDelta(" What can I get you?");
          listener.onChunk(new NpcReplyChunk(1, "What can I get you?", "chunk-1"));
        });
    UUID requestId = UUID.randomUUID();

    ConversationStartResponse response =
        conversationService.start(userId, staffRequest(requestId), events);

    assertThat(received)
        .containsExactly(
            "delta:Hi!",
            "audio:0:Hi!:5",
            "delta: What can I get you?",
            "audio:1:What can I get you?:5");
    assertThat(response.openingTurn().text()).isEqualTo("Hi! What can I get you?");
    assertThat(conversationRepository.findByRequestId(requestId)).isPresent();
  }

  @Test
  @DisplayName("말걸기 대사가 있는 행인이 먼저 말을 걸었으면 AI를 부르지 않으므로 조각 없이 그 대사로 시작한다")
  void start_withEvents_passerbyApproach_sendsNoChunks() {
    ConversationStartResponse response =
        conversationService.start(
            userId,
            new StartConversationRequest(UUID.randomUUID(), "TOWN_PASSERBY", InitiatedBy.NPC),
            events);

    assertThat(received).isEmpty();
    assertThat(response.openingTurn().text()).isNotBlank();
    then(aiGateway)
        .should(never())
        .reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any(), any());
  }

  @Test
  @DisplayName("같은 요청 ID로 다시 시작하면 AI를 다시 부르지 않고 조각 없이 처음 시작한 대화를 돌려준다")
  void start_withEvents_sameRequestId_returnsSavedConversationWithoutChunks() {
    greetsWith(listener -> listener.onDelta("Hi!"));
    UUID requestId = UUID.randomUUID();
    ConversationStartResponse first =
        conversationService.start(userId, staffRequest(requestId), events);
    received.clear();

    ConversationStartResponse again =
        conversationService.start(userId, staffRequest(requestId), events);

    assertThat(received).isEmpty();
    assertThat(again.conversationId()).isEqualTo(first.conversationId());
    then(aiGateway)
        .should(times(1))
        .reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any(), any());
  }

  private static StartConversationRequest staffRequest(UUID requestId) {
    return new StartConversationRequest(requestId, "CAFE_STAFF", InitiatedBy.USER);
  }

  private void greetsWith(Consumer<NpcReplyListener> stream) {
    given(
            aiGateway.reply(
                any(), any(), any(), any(), anyString(), anyString(), anyInt(), any(), any()))
        .willAnswer(
            invocation -> {
              stream.accept(invocation.getArgument(8));
              return reply("Hi! What can I get you?", "full-1");
            });
  }
}
