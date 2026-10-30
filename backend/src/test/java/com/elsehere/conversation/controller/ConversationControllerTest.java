package com.elsehere.conversation.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.ConversationDetailResponse;
import com.elsehere.conversation.dto.ConversationListResponse;
import com.elsehere.conversation.dto.ConversationStartResponse;
import com.elsehere.conversation.dto.ConversationStateResponse;
import com.elsehere.conversation.dto.ConversationSummaryResponse;
import com.elsehere.conversation.dto.EndConversationRequest;
import com.elsehere.conversation.dto.EndConversationResponse;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.dto.NpcTurnResponse;
import com.elsehere.conversation.dto.SpeechResponse;
import com.elsehere.conversation.dto.StartConversationRequest;
import com.elsehere.conversation.dto.TurnRequest;
import com.elsehere.conversation.dto.TurnResponse;
import com.elsehere.conversation.dto.UserTurnResponse;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.service.ConversationHistoryService;
import com.elsehere.conversation.service.ConversationService;
import com.elsehere.conversation.service.TurnService;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.feedback.entity.FeedbackStatus;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 대화 규칙은 서비스 테스트에서 DB와 함께 검증하고, 여기서는 요청·응답 연결과 입력 검증만 본다.
@SecuredWebMvcTest(controllers = ConversationController.class)
class ConversationControllerTest {

  private static final UUID REQUEST_ID = UUID.fromString("00000000-0000-0000-0000-000000000001");
  private static final NpcSummaryResponse NPC =
      new NpcSummaryResponse("CAFE_STAFF", "Mia", "카페 직원");

  @Autowired private MockMvc mockMvc;

  @MockitoBean private ConversationService conversationService;

  @MockitoBean private TurnService turnService;

  @MockitoBean private ConversationHistoryService conversationHistoryService;

  @Test
  @DisplayName("GET /api/v1/conversations는 NPC·커서·크기를 넘겨 지난 대화 목록 한 페이지를 돌려준다")
  void list_respondsPage() throws Exception {
    ConversationSummaryResponse item =
        new ConversationSummaryResponse(
            58L,
            NPC,
            "CAFE",
            ConversationStatus.ENDED_BY_USER,
            ConversationEndReason.USER_ENDED,
            OffsetDateTime.parse("2026-10-21T14:00:00+09:00"),
            OffsetDateTime.parse("2026-10-21T14:05:00+09:00"),
            4,
            FeedbackStatus.DONE);
    given(conversationHistoryService.list(1L, "CAFE_STAFF", "60", 10))
        .willReturn(new ConversationListResponse(List.of(item), "58"));

    mockMvc
        .perform(
            get("/api/v1/conversations")
                .param("npcCode", "CAFE_STAFF")
                .param("cursor", "60")
                .param("size", "10")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.items[0].conversationId").value(58))
        .andExpect(jsonPath("$.items[0].npc.npcCode").value("CAFE_STAFF"))
        .andExpect(jsonPath("$.items[0].startedAt").value("2026-10-21T14:00:00+09:00"))
        .andExpect(jsonPath("$.items[0].userTurnCount").value(4))
        .andExpect(jsonPath("$.items[0].feedbackStatus").value("DONE"))
        .andExpect(jsonPath("$.nextCursor").value("58"));
  }

  @Test
  @DisplayName("GET /api/v1/conversations에 조건이 없으면 전체 NPC의 첫 페이지를 20개씩 요청한다")
  void list_withoutParams_usesDefaults() throws Exception {
    given(conversationHistoryService.list(1L, null, null, 20))
        .willReturn(new ConversationListResponse(List.of(), null));

    mockMvc
        .perform(get("/api/v1/conversations").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.items").isEmpty())
        .andExpect(jsonPath("$.nextCursor").doesNotExist());
  }

  @Test
  @DisplayName("POST /api/v1/conversations는 201과 첫 NPC 턴·음성 주소를 돌려준다")
  void start_respondsCreated() throws Exception {
    given(
            conversationService.start(
                1L, new StartConversationRequest(REQUEST_ID, "CAFE_STAFF", InitiatedBy.NPC)))
        .willReturn(
            new ConversationStartResponse(
                5L,
                ConversationStatus.ACTIVE,
                1,
                NPC,
                List.of(),
                npcTurn(10L, 1, "Hi!"),
                new ConversationStateResponse(ConversationStatus.ACTIVE, null, 0, 3, 12)));

    mockMvc
        .perform(
            request(
                post("/api/v1/conversations"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"npcCode\": \"CAFE_STAFF\","
                    + " \"initiatedBy\": \"NPC\"}"))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.conversationId").value(5))
        .andExpect(jsonPath("$.npc.name").value("Mia"))
        .andExpect(jsonPath("$.openingTurn.text").value("Hi!"))
        .andExpect(jsonPath("$.openingTurn.speech.status").value("READY"))
        .andExpect(jsonPath("$.openingTurn.speech.url").value("/api/v1/turns/10/speech"))
        .andExpect(jsonPath("$.conversation.rudeCount").value(0))
        .andExpect(jsonPath("$.conversation.rudeLimit").value(3))
        .andExpect(jsonPath("$.conversation.affinity").value(12));
  }

  @Test
  @DisplayName("대화 시작에 initiatedBy가 없으면 400 VALIDATION_FAILED다")
  void start_withoutInitiatedBy_isRejected() throws Exception {
    mockMvc
        .perform(
            request(
                post("/api/v1/conversations"),
                "{\"requestId\": \"" + REQUEST_ID + "\", \"npcCode\": \"CAFE_STAFF\"}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("진행 중인 대화가 있으면 409 CONVERSATION_ALREADY_ACTIVE와 진행 중인 대화 ID를 돌려준다")
  void start_whileActive_respondsConflict() throws Exception {
    given(conversationService.start(eq(1L), any()))
        .willThrow(
            new BusinessException(
                ErrorCode.CONVERSATION_ALREADY_ACTIVE, Map.of("activeConversationId", 3L)));

    mockMvc
        .perform(
            request(
                post("/api/v1/conversations"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"npcCode\": \"CAFE_STAFF\","
                    + " \"initiatedBy\": \"USER\"}"))
        .andExpect(status().isConflict())
        .andExpect(jsonPath("$.error.code").value("CONVERSATION_ALREADY_ACTIVE"))
        .andExpect(jsonPath("$.error.details.activeConversationId").value(3));
  }

  @Test
  @DisplayName("POST /turns는 음성 입력의 전사 원문을 넘기고 사용자·NPC 턴과 대화 상태를 돌려준다")
  void sendTurn_respondsTurns() throws Exception {
    given(
            turnService.send(
                1L, 5L, new TurnRequest(REQUEST_ID, InputType.VOICE, "A latte", "a latte")))
        .willReturn(
            new TurnResponse(
                new UserTurnResponse(11L, 2, Speaker.USER, "A latte", false, null),
                npcTurn(12L, 3, "Sure!"),
                new ConversationStateResponse(ConversationStatus.ACTIVE, null, 0, 3, -4),
                List.of()));

    mockMvc
        .perform(
            request(
                post("/api/v1/conversations/5/turns"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"inputType\": \"VOICE\","
                    + " \"text\": \"A latte\", \"sttRawText\": \"a latte\"}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userTurn.text").value("A latte"))
        .andExpect(jsonPath("$.npcTurn.seq").value(3))
        .andExpect(jsonPath("$.conversation.status").value("ACTIVE"))
        .andExpect(jsonPath("$.conversation.rudeLimit").value(3))
        .andExpect(jsonPath("$.conversation.affinity").value(-4));
  }

  @Test
  @DisplayName("빈 문장이나 500자를 넘는 문장은 400 VALIDATION_FAILED다")
  void sendTurn_invalidText_isRejected() throws Exception {
    mockMvc
        .perform(
            request(
                post("/api/v1/conversations/5/turns"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"inputType\": \"TEXT\", \"text\": \" \"}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
    mockMvc
        .perform(
            request(
                post("/api/v1/conversations/5/turns"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"inputType\": \"TEXT\", \"text\": \""
                    + "a".repeat(501)
                    + "\"}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("POST /end는 종료 상태와 짧은 피드백을 돌려준다")
  void end_respondsFeedback() throws Exception {
    given(conversationService.end(1L, 5L, new EndConversationRequest(REQUEST_ID, false)))
        .willReturn(
            new EndConversationResponse(
                5L,
                ConversationStatus.ENDED_BY_USER,
                ConversationEndReason.USER_ENDED,
                null,
                NPC,
                "CAFE",
                2,
                new FeedbackResponse(FeedbackStatus.FAILED, null, null, null, "AI_TIMEOUT"),
                List.of()));

    mockMvc
        .perform(
            request(
                post("/api/v1/conversations/5/end"),
                "{\"requestId\": \"" + REQUEST_ID + "\", \"skipFeedback\": false}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.status").value("ENDED_BY_USER"))
        .andExpect(jsonPath("$.userTurnCount").value(2))
        .andExpect(jsonPath("$.feedback.status").value("FAILED"))
        .andExpect(jsonPath("$.feedback.errorCode").value("AI_TIMEOUT"));
  }

  @Test
  @DisplayName("GET /active는 진행 중인 대화가 없으면 204, 있으면 200과 대화를 돌려준다")
  void active_respondsNoContentOrConversation() throws Exception {
    given(conversationService.active(1L)).willReturn(Optional.empty());
    mockMvc
        .perform(get("/api/v1/conversations/active").with(authentication(loggedIn(1L))))
        .andExpect(status().isNoContent());

    given(conversationService.active(1L))
        .willReturn(
            Optional.of(
                new ConversationDetailResponse(
                    5L,
                    ConversationStatus.ACTIVE,
                    null,
                    1,
                    NPC,
                    "CAFE",
                    null,
                    null,
                    0,
                    3,
                    List.of())));
    mockMvc
        .perform(get("/api/v1/conversations/active").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.conversationId").value(5));
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/conversations/active"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  private static NpcTurnResponse npcTurn(Long turnId, int seq, String text) {
    return new NpcTurnResponse(
        turnId, seq, Speaker.NPC, text, null, SpeechResponse.of(turnId, "a.wav"));
  }

  private static MockHttpServletRequestBuilder request(
      MockHttpServletRequestBuilder builder, String body) {
    return builder
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
        .contentType(MediaType.APPLICATION_JSON)
        .content(body)
        .with(authentication(loggedIn(1L)));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
