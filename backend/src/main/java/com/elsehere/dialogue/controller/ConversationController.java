package com.elsehere.dialogue.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.error.ErrorResponse;
import com.elsehere.dialogue.dto.ConversationDetailResponse;
import com.elsehere.dialogue.dto.ConversationListResponse;
import com.elsehere.dialogue.dto.ConversationStartResponse;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.EndConversationResponse;
import com.elsehere.dialogue.dto.NpcSpeechSpeed;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.dialogue.service.ConversationHistoryService;
import com.elsehere.dialogue.service.ConversationService;
import com.elsehere.dialogue.service.TurnService;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.validation.Valid;
import jakarta.validation.constraints.DecimalMax;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.NotNull;
import java.util.UUID;
import java.util.function.Supplier;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;
import tools.jackson.databind.json.JsonMapper;

/**
 * NPC 자유 대화: 시작(CONV-01), 발화(CONV-03), 종료(CONV-08), 지난 대화 목록(CONV-09), 상세(CONV-10), 진행 중인
 * 대화(CONV-11). 상황 복습과 장소 연습(PRC-02)의 대화 시작도 받는다.
 */
@Slf4j
@RestController
@RequiredArgsConstructor
public class ConversationController {

  private final ConversationService conversationService;
  private final TurnService turnService;
  private final ConversationHistoryService conversationHistoryService;
  private final JsonMapper jsonMapper;

  public record StartReviewRequest(
      @NotNull UUID requestId,
      @DecimalMin(NpcSpeechSpeed.MIN) @DecimalMax(NpcSpeechSpeed.MAX) Double speechSpeed) {

    public StartReviewRequest {
      speechSpeed = NpcSpeechSpeed.orDefault(speechSpeed);
    }
  }

  @PostMapping("/api/v1/review-cards/{cardId}/conversations")
  @ResponseStatus(HttpStatus.CREATED)
  public ConversationStartResponse startReview(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long cardId,
      @Valid @RequestBody StartReviewRequest request) {
    return conversationService.startReview(
        loginUser.userId(), cardId, request.requestId(), request.speechSpeed());
  }

  public record StartPracticeRequest(
      @NotNull UUID requestId,
      @DecimalMin(NpcSpeechSpeed.MIN) @DecimalMax(NpcSpeechSpeed.MAX) Double speechSpeed) {

    public StartPracticeRequest {
      speechSpeed = NpcSpeechSpeed.orDefault(speechSpeed);
    }
  }

  /** 장소 연습 시작(PRC-02). 고른 상황의 가게 직원과 대화를 시작한다. */
  @PostMapping("/api/v1/practice-scenarios/{scenarioId}/conversations")
  @ResponseStatus(HttpStatus.CREATED)
  public ConversationStartResponse startPractice(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable String scenarioId,
      @Valid @RequestBody StartPracticeRequest request) {
    return conversationService.startPractice(
        loginUser.userId(), scenarioId, request.requestId(), request.speechSpeed());
  }

  @PostMapping("/api/v1/conversations")
  @ResponseStatus(HttpStatus.CREATED)
  public ConversationStartResponse start(
      @AuthenticationPrincipal LoginUser loginUser,
      @Valid @RequestBody StartConversationRequest request) {
    return conversationService.start(loginUser.userId(), request);
  }

  /**
   * 대화 시작(CONV-01)을 스트리밍으로 받는다. Accept에 text/event-stream이 있으면 이 메서드가 받는다. AI가 만드는 첫 인사의 글자와 문장 조각
   * 음성을 만들어지는 대로 보내고, 대화를 저장하면 REST 응답과 같은 본문을 마지막에 보낸다.
   */
  @PostMapping(path = "/api/v1/conversations", produces = MediaType.TEXT_EVENT_STREAM_VALUE)
  public void startStream(
      @AuthenticationPrincipal LoginUser loginUser,
      @Valid @RequestBody StartConversationRequest request,
      HttpServletResponse response) {
    TurnEventStream stream = TurnEventStream.forOpening(response, jsonMapper);
    ConversationStartResponse started =
        streamed(stream, () -> conversationService.start(loginUser.userId(), request, stream));
    if (started != null) {
      stream.conversation(started);
    }
  }

  @PostMapping("/api/v1/conversations/{conversationId}/turns")
  public TurnResponse sendTurn(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long conversationId,
      @Valid @RequestBody TurnRequest request) {
    return turnService.send(loginUser.userId(), conversationId, request);
  }

  /**
   * 발화 전송(CONV-03)을 스트리밍으로 받는다. Accept에 text/event-stream이 있으면 이 메서드가 받는다. NPC 대사 글자와 문장 조각 음성을
   * 만들어지는 대로 보내고, 턴을 저장하면 REST 응답과 같은 본문을 마지막에 보낸다.
   *
   * <p>지금 REST처럼 이 요청 스레드에서 끝까지 처리한다. 같은 요청 ID의 잠금이 이 스레드에 묶여 있기 때문이다.
   */
  @PostMapping(
      path = "/api/v1/conversations/{conversationId}/turns",
      produces = MediaType.TEXT_EVENT_STREAM_VALUE)
  public void sendTurnStream(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long conversationId,
      @Valid @RequestBody TurnRequest request,
      HttpServletResponse response) {
    TurnEventStream stream = new TurnEventStream(response, jsonMapper);
    TurnResponse turn =
        streamed(
            stream, () -> turnService.send(loginUser.userId(), conversationId, request, stream));
    if (turn != null) {
      stream.turn(turn);
    }
  }

  // 대사를 보내기 전에 난 오류는 던져 공통 오류 처리기가 지금처럼 HTTP 오류로 응답하게 한다. 보내기 시작한 뒤에는 상태를 바꿀 수 없어 error
  // 이벤트로 알리고 null을 돌려준다.
  private <T> T streamed(TurnEventStream stream, Supplier<T> call) {
    try {
      return call.get();
    } catch (BusinessException exception) {
      if (!stream.isStarted()) {
        throw exception;
      }
      stream.error(
          exception.getErrorCode().getStatus().value(),
          ErrorResponse.of(
              exception.getErrorCode(), exception.getMessage(), exception.getDetails()));
      return null;
    } catch (RuntimeException exception) {
      if (!stream.isStarted()) {
        throw exception;
      }
      log.error("turn_stream_failed type={}", exception.getClass().getName(), exception);
      stream.error(
          ErrorCode.INTERNAL_ERROR.getStatus().value(),
          ErrorResponse.of(ErrorCode.INTERNAL_ERROR, null));
      return null;
    }
  }

  @PostMapping("/api/v1/conversations/{conversationId}/end")
  public EndConversationResponse end(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long conversationId,
      @Valid @RequestBody EndConversationRequest request) {
    return conversationService.end(loginUser.userId(), conversationId, request);
  }

  @GetMapping("/api/v1/conversations")
  public ConversationListResponse list(
      @AuthenticationPrincipal LoginUser loginUser,
      @RequestParam(required = false) String npcCode,
      @RequestParam(required = false) String cursor,
      @RequestParam(defaultValue = "20") int size,
      @RequestParam(defaultValue = "false") boolean feedbackOnly,
      @RequestParam(required = false) String sceneCode) {
    return conversationHistoryService.list(
        loginUser.userId(), npcCode, sceneCode, cursor, size, feedbackOnly);
  }

  @GetMapping("/api/v1/conversations/active")
  public ResponseEntity<ConversationDetailResponse> active(
      @AuthenticationPrincipal LoginUser loginUser) {
    return conversationService
        .active(loginUser.userId())
        .map(ResponseEntity::ok)
        .orElseGet(() -> ResponseEntity.noContent().build());
  }

  @GetMapping("/api/v1/conversations/{conversationId}")
  public ConversationDetailResponse detail(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable Long conversationId) {
    return conversationService.detail(loginUser.userId(), conversationId);
  }
}
