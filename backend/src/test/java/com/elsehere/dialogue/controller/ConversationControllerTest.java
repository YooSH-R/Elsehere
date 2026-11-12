package com.elsehere.dialogue.controller;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.dialogue.dto.ConversationDetailResponse;
import com.elsehere.dialogue.dto.ConversationListResponse;
import com.elsehere.dialogue.dto.ConversationStartResponse;
import com.elsehere.dialogue.dto.ConversationStateResponse;
import com.elsehere.dialogue.dto.ConversationSummaryResponse;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.EndConversationResponse;
import com.elsehere.dialogue.dto.NpcTurnResponse;
import com.elsehere.dialogue.dto.SpeechResponse;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.dialogue.dto.UserTurnResponse;
import com.elsehere.dialogue.service.ConversationHistoryService;
import com.elsehere.dialogue.service.ConversationService;
import com.elsehere.dialogue.service.TurnEvents;
import com.elsehere.dialogue.service.TurnService;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.followup.dto.FollowUpResponse;
import com.elsehere.world.entity.PlaceGroup;
import java.nio.charset.StandardCharsets;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.mockito.ArgumentCaptor;
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
  private static final OffsetDateTime TIME_LIMIT_AT =
      OffsetDateTime.parse("2026-11-05T14:05:00+09:00");
  private static final OffsetDateTime GRACE_UNTIL =
      OffsetDateTime.parse("2026-11-05T14:05:30+09:00");
  private static final String TURN =
      "{\"requestId\": \"" + REQUEST_ID + "\", \"inputType\": \"TEXT\", \"text\": \"A latte\"}";
  private static final String START =
      "{\"requestId\": \""
          + REQUEST_ID
          + "\", \"npcCode\": \"CAFE_STAFF\", \"initiatedBy\": \"USER\"}";
  private static final NpcSummaryResponse NPC =
      new NpcSummaryResponse("CAFE_STAFF", "Mia", "카페 직원");

  @Autowired private MockMvc mockMvc;

  @MockitoBean private ConversationService conversationService;

  @MockitoBean private TurnService turnService;

  @MockitoBean private ConversationHistoryService conversationHistoryService;

  @Test
  void startReviewUsesAuthenticatedOwnerAndReturnsMode() throws Exception {
    given(conversationService.startReview(1L, 42L, REQUEST_ID, 1.1))
        .willReturn(
            new ConversationStartResponse(
                77L, ConversationStatus.ACTIVE, 1, NPC, List.of(), null, null, null, 42L));
    mockMvc
        .perform(
            post("/api/v1/review-cards/42/conversations")
                .with(authentication(loggedIn(1L)))
                .header("X-Requested-With", "XMLHttpRequest")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"requestId\":\"" + REQUEST_ID + "\"}"))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.conversationId").value(77))
        .andExpect(jsonPath("$.reviewCardId").value(42));
  }

  @Test
  @DisplayName("복습 대화 시작에 실은 NPC 말 빠르기를 서비스에 넘긴다")
  void startReview_withSpeechSpeed_passesSpeechSpeed() throws Exception {
    mockMvc.perform(
        request(
            post("/api/v1/review-cards/42/conversations"),
            "{\"requestId\": \"" + REQUEST_ID + "\", \"speechSpeed\": 1.3}"));

    then(conversationService).should().startReview(1L, 42L, REQUEST_ID, 1.3);
  }

  @Test
  void startPracticeUsesAuthenticatedOwnerAndReturnsScenario() throws Exception {
    given(conversationService.startPractice(1L, "DINER_TABLE", REQUEST_ID, 1.1))
        .willReturn(
            new ConversationStartResponse(
                77L,
                ConversationStatus.ACTIVE,
                1,
                NPC,
                List.of(),
                null,
                null,
                null,
                null,
                "DINER_TABLE"));
    mockMvc
        .perform(
            post("/api/v1/practice-scenarios/DINER_TABLE/conversations")
                .with(authentication(loggedIn(1L)))
                .header("X-Requested-With", "XMLHttpRequest")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"requestId\":\"" + REQUEST_ID + "\"}"))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.conversationId").value(77))
        .andExpect(jsonPath("$.practiceScenarioId").value("DINER_TABLE"));
  }

  @Test
  @DisplayName("장소 연습 대화 시작에 실은 NPC 말 빠르기를 서비스에 넘긴다")
  void startPractice_withSpeechSpeed_passesSpeechSpeed() throws Exception {
    mockMvc.perform(
        request(
            post("/api/v1/practice-scenarios/DINER_TABLE/conversations"),
            "{\"requestId\": \"" + REQUEST_ID + "\", \"speechSpeed\": 1.4}"));

    then(conversationService).should().startPractice(1L, "DINER_TABLE", REQUEST_ID, 1.4);
  }

  @Test
  @DisplayName("GET /api/v1/conversations는 NPC·커서·크기를 넘겨 지난 대화 목록 한 페이지를 돌려준다")
  void list_respondsPage() throws Exception {
    ConversationSummaryResponse item =
        new ConversationSummaryResponse(
            58L,
            NPC,
            "CAFE",
            "카페",
            PlaceGroup.FOOD,
            ConversationStatus.ENDED_BY_USER,
            ConversationEndReason.USER_ENDED,
            OffsetDateTime.parse("2026-10-21T14:00:00+09:00"),
            OffsetDateTime.parse("2026-10-21T14:05:00+09:00"),
            4,
            FeedbackStatus.DONE,
            false,
            0);
    given(conversationHistoryService.list(1L, "CAFE_STAFF", null, null, "60", 10, false))
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
        .andExpect(jsonPath("$.items[0].placeName").value("카페"))
        .andExpect(jsonPath("$.items[0].placeGroup").value("FOOD"))
        .andExpect(jsonPath("$.items[0].startedAt").value("2026-10-21T14:00:00+09:00"))
        .andExpect(jsonPath("$.items[0].userTurnCount").value(4))
        .andExpect(jsonPath("$.items[0].feedbackStatus").value("DONE"))
        .andExpect(jsonPath("$.items[0].practiceCount").value(0))
        .andExpect(jsonPath("$.nextCursor").value("58"));
  }

  @Test
  @DisplayName("GET /api/v1/conversations에 조건이 없으면 전체 NPC의 첫 페이지를 20개씩 요청한다")
  void list_withoutParams_usesDefaults() throws Exception {
    given(conversationHistoryService.list(1L, null, null, null, null, 20, false))
        .willReturn(new ConversationListResponse(List.of(), null));

    mockMvc
        .perform(get("/api/v1/conversations").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.items").isEmpty())
        .andExpect(jsonPath("$.nextCursor").doesNotExist());
  }

  @Test
  @DisplayName("대화 노트 요청은 완료된 피드백 필터·장소와 기존 조회 조건을 함께 전달한다")
  void list_feedbackOnly_passesCompletedFeedbackFilter() throws Exception {
    given(conversationHistoryService.list(1L, "CAFE_STAFF", "CAFE", null, "60", 10, true))
        .willReturn(new ConversationListResponse(List.of(), "58"));

    mockMvc
        .perform(
            get("/api/v1/conversations")
                .param("npcCode", "CAFE_STAFF")
                .param("cursor", "60")
                .param("size", "10")
                .param("feedbackOnly", "true")
                .param("sceneCode", "CAFE")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.items").isEmpty())
        .andExpect(jsonPath("$.nextCursor").value("58"));
  }

  @Test
  @DisplayName("대화 노트의 장소 묶음 탭은 placeGroup으로 넘긴다")
  void list_placeGroup_passesPlaceGroupFilter() throws Exception {
    given(conversationHistoryService.list(1L, null, null, "SERVICES", null, 20, true))
        .willReturn(new ConversationListResponse(List.of(), null));

    mockMvc
        .perform(
            get("/api/v1/conversations")
                .param("feedbackOnly", "true")
                .param("placeGroup", "SERVICES")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.items").isEmpty());
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
                new ConversationStateResponse(
                    ConversationStatus.ACTIVE,
                    null,
                    0,
                    3,
                    12,
                    TIME_LIMIT_AT,
                    GRACE_UNTIL,
                    false,
                    0),
                7L));

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
        .andExpect(jsonPath("$.conversation.affinity").value(12))
        .andExpect(jsonPath("$.conversation.timeLimitAt").value("2026-11-05T14:05:00+09:00"))
        .andExpect(jsonPath("$.conversation.graceUntil").value("2026-11-05T14:05:30+09:00"))
        .andExpect(jsonPath("$.conversation.closing").value(false))
        .andExpect(jsonPath("$.conversation.finalTurnsRemaining").value(0))
        .andExpect(jsonPath("$.deliveredFollowUpId").value(7));
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
  @DisplayName("대화 시작에 npcOffDuty가 true면 근무가 아닌 NPC와의 대화로 서비스에 넘긴다")
  void start_npcOffDuty_passesOffDuty() throws Exception {
    given(conversationService.start(eq(1L), any())).willReturn(startedConversation());

    mockMvc
        .perform(
            request(
                post("/api/v1/conversations"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"npcCode\": \"CAFE_STAFF\","
                    + " \"initiatedBy\": \"USER\", \"npcOffDuty\": true}"))
        .andExpect(status().isCreated());

    then(conversationService)
        .should()
        .start(1L, new StartConversationRequest(REQUEST_ID, "CAFE_STAFF", InitiatedBy.USER, true));
  }

  @ParameterizedTest
  @ValueSource(strings = {"", ", \"npcOffDuty\": null"})
  @DisplayName("npcOffDuty가 없거나 null이면 근무 중인 NPC와의 대화로 넘긴다")
  void start_withoutNpcOffDuty_passesOnDuty(String npcOffDutyField) throws Exception {
    given(conversationService.start(eq(1L), any())).willReturn(startedConversation());

    mockMvc
        .perform(
            request(
                post("/api/v1/conversations"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"npcCode\": \"CAFE_STAFF\","
                    + " \"initiatedBy\": \"USER\""
                    + npcOffDutyField
                    + "}"))
        .andExpect(status().isCreated());

    then(conversationService)
        .should()
        .start(1L, new StartConversationRequest(REQUEST_ID, "CAFE_STAFF", InitiatedBy.USER, false));
  }

  @Test
  @DisplayName("대화 시작에 설정창에서 고른 NPC 말 빠르기를 실어 보내면 서비스에 그대로 넘긴다")
  void start_withSpeechSpeed_passesSpeechSpeed() throws Exception {
    given(conversationService.start(eq(1L), any())).willReturn(startedConversation());

    mockMvc
        .perform(
            request(
                post("/api/v1/conversations"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"npcCode\": \"CAFE_STAFF\","
                    + " \"initiatedBy\": \"USER\", \"speechSpeed\": 1.4}"))
        .andExpect(status().isCreated());

    ArgumentCaptor<StartConversationRequest> request =
        ArgumentCaptor.forClass(StartConversationRequest.class);
    then(conversationService).should().start(eq(1L), request.capture());
    assertThat(request.getValue().speechSpeed()).isEqualTo(1.4);
  }

  @Test
  @DisplayName("NPC 말 빠르기를 보내지 않는 예전 클라이언트는 기본 1.1배로 넘긴다")
  void start_withoutSpeechSpeed_passesDefaultSpeechSpeed() throws Exception {
    given(conversationService.start(eq(1L), any())).willReturn(startedConversation());

    mockMvc
        .perform(
            request(
                post("/api/v1/conversations"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"npcCode\": \"CAFE_STAFF\", \"initiatedBy\": \"USER\"}"))
        .andExpect(status().isCreated());

    ArgumentCaptor<StartConversationRequest> request =
        ArgumentCaptor.forClass(StartConversationRequest.class);
    then(conversationService).should().start(eq(1L), request.capture());
    assertThat(request.getValue().speechSpeed()).isEqualTo(1.1);
  }

  @ParameterizedTest
  @ValueSource(doubles = {0.7, 1.6})
  @DisplayName("NPC 말 빠르기가 0.8~1.5배 밖이면 대화 시작·발화 전송·복습·장소 연습 시작 모두 400 VALIDATION_FAILED다")
  void speechSpeedOutsideSettingRange_isRejected(double speechSpeed) throws Exception {
    mockMvc
        .perform(
            request(
                post("/api/v1/conversations"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"npcCode\": \"CAFE_STAFF\", \"initiatedBy\": \"USER\","
                    + " \"speechSpeed\": "
                    + speechSpeed
                    + "}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
    mockMvc
        .perform(
            request(
                post("/api/v1/conversations/5/turns"),
                "{\"requestId\": \""
                    + REQUEST_ID
                    + "\", \"inputType\": \"TEXT\", \"text\": \"Hi\", \"speechSpeed\": "
                    + speechSpeed
                    + "}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
    mockMvc
        .perform(
            request(
                post("/api/v1/review-cards/42/conversations"),
                "{\"requestId\": \"" + REQUEST_ID + "\", \"speechSpeed\": " + speechSpeed + "}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
    mockMvc
        .perform(
            request(
                post("/api/v1/practice-scenarios/DINER_TABLE/conversations"),
                "{\"requestId\": \"" + REQUEST_ID + "\", \"speechSpeed\": " + speechSpeed + "}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("발화 전송에 실은 NPC 말 빠르기를 서비스에 넘긴다")
  void sendTurn_withSpeechSpeed_passesSpeechSpeed() throws Exception {
    mockMvc.perform(
        request(
            post("/api/v1/conversations/5/turns"),
            "{\"requestId\": \""
                + REQUEST_ID
                + "\", \"inputType\": \"TEXT\", \"text\": \"Hi\", \"speechSpeed\": 0.9}"));

    ArgumentCaptor<TurnRequest> request = ArgumentCaptor.forClass(TurnRequest.class);
    then(turnService).should().send(eq(1L), eq(5L), request.capture());
    assertThat(request.getValue().speechSpeed()).isEqualTo(0.9);
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
  @DisplayName(
      "POST /api/v1/conversations에 Accept: text/event-stream이면 첫 인사 글자·조각 음성을 보내고 마지막에 REST와 같은 본문을 conversation으로 보낸다")
  void start_streamAccept_sendsDeltaAudioThenConversation() throws Exception {
    given(conversationService.start(eq(1L), any(), any()))
        .willAnswer(
            invocation -> {
              TurnEvents events = invocation.getArgument(2);
              events.delta("Hi");
              events.audio(0, "Hi!", new byte[] {82, 73, 70, 70});
              return startedConversation();
            });

    String body =
        mockMvc
            .perform(
                request(post("/api/v1/conversations"), START)
                    .header("Accept", "text/event-stream, application/json"))
            .andExpect(status().isOk())
            .andExpect(content().contentTypeCompatibleWith(MediaType.TEXT_EVENT_STREAM))
            .andExpect(header().string("X-Accel-Buffering", "no"))
            .andReturn()
            .getResponse()
            .getContentAsString(StandardCharsets.UTF_8);

    assertThat(body)
        .startsWith(
            "event: delta\ndata: {\"text\":\"Hi\"}\n\n"
                + "event: audio\ndata: {\"seq\":0,\"text\":\"Hi!\",\"wav\":\"UklGRg==\"}\n\n"
                + "event: conversation\ndata: {\"conversationId\":5,");
    assertThat(body).contains("\"text\":\"Hi!\"").endsWith("}\n\n");
  }

  @Test
  @DisplayName("대화 시작 스트림이어도 첫 인사를 보내기 전에 난 오류는 지금과 같은 HTTP 상태와 오류 본문으로 응답한다")
  void start_streamAccept_errorBeforeEvents_respondsHttpError() throws Exception {
    given(conversationService.start(eq(1L), any(), any()))
        .willThrow(
            new BusinessException(
                ErrorCode.CONVERSATION_ALREADY_ACTIVE, Map.of("activeConversationId", 3L)));

    mockMvc
        .perform(
            request(post("/api/v1/conversations"), START)
                .header("Accept", "text/event-stream, application/json"))
        .andExpect(status().isConflict())
        .andExpect(jsonPath("$.error.code").value("CONVERSATION_ALREADY_ACTIVE"))
        .andExpect(jsonPath("$.error.details.activeConversationId").value(3));
  }

  @Test
  @DisplayName("첫 인사를 보내기 시작한 뒤 난 오류는 HTTP 상태와 공통 오류 본문을 error 이벤트로 보내고 스트림을 끝낸다")
  void start_streamAccept_errorAfterEvents_sendsErrorEvent() throws Exception {
    given(conversationService.start(eq(1L), any(), any()))
        .willAnswer(
            invocation -> {
              TurnEvents events = invocation.getArgument(2);
              events.delta("Hi");
              throw new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE);
            });

    String body =
        mockMvc
            .perform(
                request(post("/api/v1/conversations"), START)
                    .header("Accept", "text/event-stream, application/json"))
            .andExpect(status().isOk())
            .andReturn()
            .getResponse()
            .getContentAsString(StandardCharsets.UTF_8);

    assertThat(body)
        .isEqualTo(
            "event: delta\ndata: {\"text\":\"Hi\"}\n\n"
                + "event: error\ndata: {\"status\":503,\"error\":{\"code\":\"AI_TEMPORARILY_UNAVAILABLE\","
                + "\"message\":\""
                + ErrorCode.AI_TEMPORARILY_UNAVAILABLE.getMessage()
                + "\"}}\n\n");
  }

  @Test
  @DisplayName("POST /turns는 음성 입력의 전사 원문을 넘기고 사용자·NPC 턴과 대화 상태를 돌려준다")
  void sendTurn_respondsTurns() throws Exception {
    given(
            turnService.send(
                1L, 5L, new TurnRequest(REQUEST_ID, InputType.VOICE, "A latte", "a latte")))
        .willReturn(
            new TurnResponse(
                new UserTurnResponse(11L, 2, Speaker.USER, "A latte", false, null, List.of()),
                npcTurn(12L, 3, "Sure!"),
                new ConversationStateResponse(
                    ConversationStatus.ACTIVE,
                    null,
                    0,
                    3,
                    -4,
                    TIME_LIMIT_AT,
                    GRACE_UNTIL,
                    false,
                    0),
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
        .andExpect(jsonPath("$.conversation.affinity").value(-4))
        .andExpect(jsonPath("$.conversation.timeLimitAt").value("2026-11-05T14:05:00+09:00"));
  }

  @ParameterizedTest
  @ValueSource(strings = {"application/json", "*/*", ""})
  @DisplayName("POST /turns는 스트림을 요청하지 않으면(Accept 없음·JSON·모두) 지금처럼 JSON 한 번으로 응답한다")
  void sendTurn_withoutStreamAccept_respondsJson(String accept) throws Exception {
    given(turnService.send(any(), any(), any())).willReturn(turnResponse());
    MockHttpServletRequestBuilder builder = request(post("/api/v1/conversations/5/turns"), TURN);
    if (!accept.isEmpty()) {
      builder.header("Accept", accept);
    }

    mockMvc
        .perform(builder)
        .andExpect(status().isOk())
        .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_JSON))
        .andExpect(jsonPath("$.npcTurn.text").value("Sure!"));
  }

  @Test
  @DisplayName(
      "POST /turns에 Accept: text/event-stream이면 대사 글자·조각 음성(base64 WAV)을 보내고 마지막에 REST와 같은 본문을 turn으로 보낸다")
  void sendTurn_streamAccept_sendsDeltaAudioThenTurn() throws Exception {
    given(turnService.send(eq(1L), eq(5L), any(), any()))
        .willAnswer(
            invocation -> {
              TurnEvents events = invocation.getArgument(3);
              events.delta("Sure");
              events.audio(0, "Sure!", new byte[] {82, 73, 70, 70});
              events.audio(1, "Coming up.", null);
              return turnResponse();
            });

    String body =
        mockMvc
            .perform(
                request(post("/api/v1/conversations/5/turns"), TURN)
                    .header("Accept", "text/event-stream, application/json"))
            .andExpect(status().isOk())
            .andExpect(content().contentTypeCompatibleWith(MediaType.TEXT_EVENT_STREAM))
            .andExpect(header().string("X-Accel-Buffering", "no"))
            .andExpect(header().string("Cache-Control", "no-cache"))
            .andReturn()
            .getResponse()
            .getContentAsString(StandardCharsets.UTF_8);

    assertThat(body)
        .startsWith(
            "event: delta\ndata: {\"text\":\"Sure\"}\n\n"
                + "event: audio\ndata: {\"seq\":0,\"text\":\"Sure!\",\"wav\":\"UklGRg==\"}\n\n"
                + "event: audio\ndata: {\"seq\":1,\"text\":\"Coming up.\",\"wav\":null}\n\n"
                + "event: turn\ndata: {\"userTurn\":");
    assertThat(body).contains("\"text\":\"Sure!\"").endsWith("}\n\n");
  }

  @Test
  @DisplayName("스트림 요청이어도 대사를 보내기 전에 난 오류는 지금과 같은 HTTP 상태와 오류 본문으로 응답한다")
  void sendTurn_streamAccept_errorBeforeEvents_respondsHttpError() throws Exception {
    given(turnService.send(eq(1L), eq(5L), any(), any()))
        .willThrow(new BusinessException(ErrorCode.CONVERSATION_NOT_ACTIVE));

    mockMvc
        .perform(
            request(post("/api/v1/conversations/5/turns"), TURN)
                .header("Accept", "text/event-stream, application/json"))
        .andExpect(status().isConflict())
        .andExpect(jsonPath("$.error.code").value("CONVERSATION_NOT_ACTIVE"));
  }

  @Test
  @DisplayName("대사를 보내기 시작한 뒤 난 오류는 HTTP 상태와 공통 오류 본문을 error 이벤트로 보내고 스트림을 끝낸다")
  void sendTurn_streamAccept_errorAfterEvents_sendsErrorEvent() throws Exception {
    given(turnService.send(eq(1L), eq(5L), any(), any()))
        .willAnswer(
            invocation -> {
              TurnEvents events = invocation.getArgument(3);
              events.delta("Sure");
              throw new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE);
            });

    String body =
        mockMvc
            .perform(
                request(post("/api/v1/conversations/5/turns"), TURN)
                    .header("Accept", "text/event-stream, application/json"))
            .andExpect(status().isOk())
            .andReturn()
            .getResponse()
            .getContentAsString(StandardCharsets.UTF_8);

    assertThat(body)
        .isEqualTo(
            "event: delta\ndata: {\"text\":\"Sure\"}\n\n"
                + "event: error\ndata: {\"status\":503,\"error\":{\"code\":\"AI_TEMPORARILY_UNAVAILABLE\","
                + "\"message\":\""
                + ErrorCode.AI_TEMPORARILY_UNAVAILABLE.getMessage()
                + "\"}}\n\n");
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
                new FeedbackResponse(
                    FeedbackStatus.FAILED, null, null, null, "AI_TIMEOUT", List.of(), List.of()),
                List.of(),
                new FollowUpResponse(
                    7L,
                    NPC,
                    "CAFE",
                    "iced latte",
                    "아이스 라테",
                    OffsetDateTime.parse("2026-11-04T14:05:45+09:00"),
                    45,
                    false)));

    mockMvc
        .perform(
            request(
                post("/api/v1/conversations/5/end"),
                "{\"requestId\": \"" + REQUEST_ID + "\", \"skipFeedback\": false}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.status").value("ENDED_BY_USER"))
        .andExpect(jsonPath("$.userTurnCount").value(2))
        .andExpect(jsonPath("$.feedback.status").value("FAILED"))
        .andExpect(jsonPath("$.feedback.errorCode").value("AI_TIMEOUT"))
        .andExpect(jsonPath("$.followUp.followUpId").value(7))
        .andExpect(jsonPath("$.followUp.itemKo").value("아이스 라테"))
        .andExpect(jsonPath("$.followUp.remainingSeconds").value(45))
        .andExpect(jsonPath("$.followUp.ready").value(false));
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

  private static ConversationStartResponse startedConversation() {
    return new ConversationStartResponse(
        5L,
        ConversationStatus.ACTIVE,
        1,
        NPC,
        List.of(),
        npcTurn(10L, 1, "Hi!"),
        new ConversationStateResponse(
            ConversationStatus.ACTIVE, null, 0, 3, 0, TIME_LIMIT_AT, GRACE_UNTIL, false, 0),
        null);
  }

  private static TurnResponse turnResponse() {
    return new TurnResponse(
        new UserTurnResponse(11L, 2, Speaker.USER, "A latte", false, null, List.of()),
        npcTurn(12L, 3, "Sure!"),
        new ConversationStateResponse(
            ConversationStatus.ACTIVE, null, 0, 3, 0, TIME_LIMIT_AT, GRACE_UNTIL, false, 0),
        List.of());
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
