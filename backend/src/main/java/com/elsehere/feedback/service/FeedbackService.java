package com.elsehere.feedback.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.FeedbackInput;
import com.elsehere.ai.client.WorkerFeedback;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.service.AiRequestRecorder;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.feedback.dto.ConversationFeedbackResponse;
import com.elsehere.feedback.dto.DetailedFeedbackItemResponse;
import com.elsehere.feedback.dto.DetailedFeedbackResponse;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.feedback.dto.FeedbackReviews;
import com.elsehere.feedback.dto.IdiomResponse;
import com.elsehere.feedback.dto.ReviewsResponse;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.feedback.entity.TurnFeedback;
import com.elsehere.feedback.repository.ConversationFeedbackRepository;
import com.elsehere.feedback.repository.TurnFeedbackRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import java.util.function.Consumer;
import java.util.function.Function;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;
import tools.jackson.databind.json.JsonMapper;

/**
 * 대화 종료 피드백. 종료(CONV-08) 때 짧은 피드백과 상세 평가를 한 번에 만들어 저장하고, 다시 보기(FB-01)·상세 평가(FB-03)는 저장된 결과만 돌려준다.
 * 생성에 실패하면 FB-02로 다시 만든다. 연결 기준: docs/api/feedback-integration.md
 *
 * <p>AI를 기다리는 동안 DB 트랜잭션을 잡지 않도록 조회 → AI 호출 → 저장을 나눈다. 사용자 AI 한도 예약은 아직 하지 않는다.
 */
@Service
@RequiredArgsConstructor
public class FeedbackService {

  // 평가할 사용자 발화가 없어 만들지 않은 경우의 이유. 워커의 insufficient_evidence 코드와 같다.
  private static final String NO_USABLE_USER_TURNS = "NO_USABLE_USER_TURNS";

  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final NpcRepository npcRepository;
  private final ConversationFeedbackRepository conversationFeedbackRepository;
  private final TurnFeedbackRepository turnFeedbackRepository;
  private final AiWorkerClient aiWorkerClient;
  private final AiRequestRecorder aiRequestRecorder;
  private final TransactionTemplate transactionTemplate;
  private final JsonMapper jsonMapper;

  /**
   * CONV-08에서 대화를 끝낸 뒤 부른다. 이미 만든 피드백이 있으면 다시 만들지 않는다. 만들지 못해도 대화 종료는 성공으로 두고 FAILED를 돌려준다.
   *
   * @param skip 사용자가 피드백을 건너뛰었는지
   */
  public FeedbackResponse feedbackOnEnd(
      Long userId, Long conversationId, UUID requestId, boolean skip) {
    Optional<FeedbackResponse> existing =
        transactionTemplate.execute(
            status ->
                conversationFeedbackRepository.findById(conversationId).map(this::toResponse));
    if (existing.isPresent()) {
      return existing.get();
    }
    return generate(userId, conversationId, requestId, skip);
  }

  /** FB-02 다시 만들기. 실패한 피드백만 다시 만들고, 그 밖에는 저장된 피드백을 돌려준다. */
  public FeedbackResponse regenerate(Long userId, Long conversationId, UUID requestId) {
    ConversationFeedback feedback =
        transactionTemplate.execute(
            status -> {
              ownedConversation(userId, conversationId);
              return conversationFeedbackRepository
                  .findById(conversationId)
                  .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
            });
    if (feedback.getStatus() != FeedbackStatus.FAILED) {
      return toResponse(feedback);
    }
    return generate(userId, conversationId, requestId, false);
  }

  /** FB-01 피드백 다시 보기. AI를 호출하지 않는다. */
  public ConversationFeedbackResponse feedback(Long userId, Long conversationId) {
    return transactionTemplate.execute(
        status -> {
          Conversation conversation = ownedConversation(userId, conversationId);
          Npc npc = npcOf(conversation);
          List<Turn> userTurns = userTurns(conversationId);
          return new ConversationFeedbackResponse(
              conversationId,
              new NpcSummaryResponse(npc.getCode(), npc.getName(), npc.getRoleLabel()),
              npc.getScene().getCode(),
              userTurns.size(),
              conversationFeedbackRepository
                  .findById(conversationId)
                  .map(this::toResponse)
                  .orElse(null),
              List.of(),
              detailedItems(userTurns));
        });
  }

  /** FB-03 상세 평가. 종료 때 만든 평가를 돌려주며 AI를 다시 호출하지 않는다. */
  public DetailedFeedbackResponse detailed(Long userId, Long conversationId) {
    return transactionTemplate.execute(
        status -> {
          ownedConversation(userId, conversationId);
          return new DetailedFeedbackResponse(
              conversationId, detailedItems(userTurns(conversationId)));
        });
  }

  private FeedbackResponse generate(
      Long userId, Long conversationId, UUID requestId, boolean skip) {
    Snapshot snapshot =
        transactionTemplate.execute(status -> snapshot(userId, conversationId, requestId));
    if (skip) {
      return save(conversationId, feedback -> feedback.skip(null), List.of(), List.of());
    }
    if (snapshot.userTurnIds().isEmpty()) {
      return save(
          conversationId, feedback -> feedback.skip(NO_USABLE_USER_TURNS), List.of(), List.of());
    }

    WorkerTrace trace = new WorkerTrace(requestId, userId, conversationId);
    WorkerFeedback result;
    try {
      result = aiWorkerClient.feedback(trace, snapshot.input());
    } catch (BusinessException exception) {
      // 워커에 닿지 못했거나 요청을 거절당했다. 대화 종료는 그대로 두고 실패로 기록한다.
      String errorCode = exception.getErrorCode().name();
      return save(conversationId, feedback -> feedback.fail(errorCode, null), List.of(), List.of());
    }
    aiRequestRecorder.record(
        trace,
        AiRequestKind.FEEDBACK,
        result.usage(),
        result.completed() ? AiRequestStatus.OK : AiRequestStatus.FAILED);

    String model = FeedbackResults.firstModel(result.usage());
    if (result.completed()) {
      String reviews = jsonMapper.writeValueAsString(FeedbackResults.reviews(result.result()));
      String summary = FeedbackResults.summary(result.result());
      return save(
          conversationId,
          feedback -> feedback.complete(summary, reviews, model, result.resultJson()),
          snapshot.userTurnIds(),
          FeedbackResults.turnFeedbacks(result.result()));
    }
    if (result.insufficientEvidence()) {
      String reason = result.problemCode() != null ? result.problemCode() : NO_USABLE_USER_TURNS;
      return save(conversationId, feedback -> feedback.skip(reason), List.of(), List.of());
    }
    return save(
        conversationId,
        feedback -> feedback.fail(result.problemCode(), model),
        List.of(),
        List.of());
  }

  // 다시 만들면 이전 상세 평가를 지우고 새로 저장한다. 같은 turn_id가 유일해야 해서 삭제를 먼저 반영한다.
  private FeedbackResponse save(
      Long conversationId,
      Consumer<ConversationFeedback> update,
      List<Long> userTurnIds,
      List<TurnFeedback> turnFeedbacks) {
    return transactionTemplate.execute(
        status -> {
          ConversationFeedback feedback =
              conversationFeedbackRepository
                  .findById(conversationId)
                  .orElseGet(() -> ConversationFeedback.of(conversationId));
          update.accept(feedback);
          conversationFeedbackRepository.save(feedback);
          if (!userTurnIds.isEmpty()) {
            turnFeedbackRepository.deleteAllByTurnIdIn(userTurnIds);
            turnFeedbackRepository.flush();
            turnFeedbackRepository.saveAll(turnFeedbacks);
          }
          return toResponse(feedback);
        });
  }

  private Snapshot snapshot(Long userId, Long conversationId, UUID requestId) {
    Conversation conversation = ownedConversation(userId, conversationId);
    List<Turn> turns = turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId);
    List<Long> userTurnIds =
        turns.stream().filter(turn -> turn.getSpeaker() == Speaker.USER).map(Turn::getId).toList();
    FeedbackInput input = FeedbackInputs.from(conversation, npcOf(conversation), turns, requestId);
    return new Snapshot(input, userTurnIds);
  }

  private List<DetailedFeedbackItemResponse> detailedItems(List<Turn> userTurns) {
    Map<Long, Turn> turnsById =
        userTurns.stream().collect(Collectors.toMap(Turn::getId, Function.identity()));
    return turnFeedbackRepository.findAllByTurnIdInOrderByTurnIdAsc(turnsById.keySet()).stream()
        .map(
            feedback ->
                new DetailedFeedbackItemResponse(
                    feedback.getId(),
                    feedback.getTurnId(),
                    turnsById.get(feedback.getTurnId()).getText(),
                    feedback.getBetterText(),
                    feedback.getReason(),
                    feedback.getIdiom() == null
                        ? null
                        : new IdiomResponse(feedback.getIdiom(), feedback.getIdiomMeaningKo()),
                    feedback.getModelAudioKey() != null))
        .toList();
  }

  private List<Turn> userTurns(Long conversationId) {
    return turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId).stream()
        .filter(turn -> turn.getSpeaker() == Speaker.USER)
        .toList();
  }

  private FeedbackResponse toResponse(ConversationFeedback feedback) {
    FeedbackReviews reviews =
        feedback.getReviews() == null
            ? null
            : jsonMapper.readValue(feedback.getReviews(), FeedbackReviews.class);
    return new FeedbackResponse(
        feedback.getStatus(),
        feedback.getSummary(),
        reviews == null ? null : new ReviewsResponse(reviews.good(), reviews.improve()),
        reviews == null ? null : reviews.suggestion(),
        feedback.getErrorCode(),
        // 이 필드를 넣기 전에 저장한 피드백은 장면 표현이 없다.
        reviews == null || reviews.situationExpressions() == null
            ? List.of()
            : reviews.situationExpressions());
  }

  // 다른 사용자의 대화는 존재 여부를 드러내지 않도록 NOT_FOUND로 응답한다.
  private Conversation ownedConversation(Long userId, Long conversationId) {
    return conversationRepository
        .findByIdAndUserId(conversationId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  private Npc npcOf(Conversation conversation) {
    return npcRepository
        .findById(conversation.getNpcId())
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  private record Snapshot(FeedbackInput input, List<Long> userTurnIds) {}
}
