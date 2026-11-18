package com.elsehere.review.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.feedback.entity.FeedbackReviews;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.feedback.repository.ConversationFeedbackRepository;
import com.elsehere.feedback.repository.TurnFeedbackRepository;
import com.elsehere.mission.service.ShowcaseReviewGoals;
import com.elsehere.review.dto.ReviewCardListResponse;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.dto.ReviewGoal;
import com.elsehere.review.dto.ReviewScenario;
import com.elsehere.review.dto.ReviewTarget;
import com.elsehere.review.entity.ReviewCard;
import com.elsehere.review.repository.ReviewCardRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Limit;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.databind.json.JsonMapper;

/** 원본 피드백으로 복습 상황을 만들고 저장된 내용만 읽는다. */
@Service
@RequiredArgsConstructor
public class ReviewCardService {
  private final ConversationLookup conversationLookup;
  private final ConversationRepository conversationRepository;
  private final ConversationFeedbackRepository feedbackRepository;
  private final ReviewCardRepository reviewCardRepository;
  private final TurnRepository turnRepository;
  private final TurnFeedbackRepository turnFeedbackRepository;
  private final NpcRepository npcRepository;
  private final JsonMapper jsonMapper;

  @Transactional(readOnly = true)
  public ReviewCardResponse detail(Long userId, Long cardId) {
    ReviewCard card = owned(userId, cardId);
    Npc npc =
        npcRepository
            .findById(card.getNpcId())
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    return response(card, npc);
  }

  @Transactional
  public void delete(Long userId, Long cardId) {
    ReviewCard card = lockOwned(userId, cardId);
    if (conversationRepository.existsByReviewCardIdAndStatus(cardId, ConversationStatus.ACTIVE)) {
      throw new BusinessException(ErrorCode.REVIEW_IN_PROGRESS);
    }
    reviewCardRepository.delete(card);
  }

  @Transactional(propagation = org.springframework.transaction.annotation.Propagation.MANDATORY)
  public void lockForStart(Long userId, Long cardId) {
    lockOwned(userId, cardId);
  }

  private ReviewCard lockOwned(Long userId, Long cardId) {
    return reviewCardRepository
        .findOwnedForUpdate(cardId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  private ReviewCard owned(Long userId, Long cardId) {
    return reviewCardRepository
        .findByIdAndUserId(cardId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  @Transactional(readOnly = true)
  public ReviewCardListResponse list(Long userId, String sceneCode, String cursor, int limit) {
    Long afterId = cursorId(cursor);
    if (limit < 1
        || limit > 50
        || (sceneCode != null && (sceneCode.isBlank() || sceneCode.length() > 30))) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    List<ReviewCard> found =
        reviewCardRepository.findPage(userId, sceneCode, afterId, Limit.of(limit + 1));
    boolean hasNext = found.size() > limit;
    List<ReviewCard> page = found.stream().limit(limit).toList();
    Map<Long, Npc> npcs =
        npcRepository
            .findAllById(page.stream().map(ReviewCard::getNpcId).distinct().toList())
            .stream()
            .collect(Collectors.toMap(Npc::getId, Function.identity()));
    List<ReviewCardResponse> items =
        page.stream()
            .map(
                card -> {
                  Npc npc = npcs.get(card.getNpcId());
                  if (npc == null) {
                    throw new BusinessException(ErrorCode.NOT_FOUND);
                  }
                  return response(card, npc);
                })
            .toList();
    return new ReviewCardListResponse(items, hasNext ? page.getLast().getId().toString() : null);
  }

  private static Long cursorId(String cursor) {
    if (cursor == null) {
      return null;
    }
    try {
      long id = Long.parseLong(cursor);
      if (id > 0) {
        return id;
      }
    } catch (NumberFormatException ignored) {
      // 잘못되거나 범위를 넘는 커서는 공통 요청 오류로 응답한다.
    }
    throw new BusinessException(ErrorCode.VALIDATION_FAILED);
  }

  /**
   * 뜻이 비어 있던 표현에 채운 뜻을 카드에 저장한다. 대화 중 추천 문장도 뜻과 함께 카드 표현으로 남겨, 다음부터는 저장한 뜻을 그대로 읽고 모델을 다시 부르지 않는다.
   * 이미 뜻이 있는 표현은 바꾸지 않는다.
   */
  @Transactional
  public ReviewCardResponse recordMeanings(Long userId, Long cardId, Map<String, String> meanings) {
    ReviewCard card = owned(userId, cardId);
    Npc npc =
        npcRepository
            .findById(card.getNpcId())
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    List<ReviewTarget> targets =
        response(card, npc).targets().stream()
            .map(target -> withMeaning(target, meanings))
            .toList();
    card.replaceTargets(jsonMapper.writeValueAsString(targets));
    return response(card, npc);
  }

  private static ReviewTarget withMeaning(ReviewTarget target, Map<String, String> meanings) {
    String meaning = meanings.get(target.expression());
    if (meaning == null || (target.meaningKo() != null && !target.meaningKo().isBlank())) {
      return target;
    }
    return new ReviewTarget(
        target.expression(),
        meaning,
        target.usageKo(),
        target.exampleEn(),
        target.exampleKo(),
        target.originalText());
  }

  /** 같은 원본 대화에 대한 동시 저장을 원본 행 잠금으로 직렬화한다. */
  @Transactional
  public ReviewCardResponse create(Long userId, Long conversationId) {
    Conversation conversation =
        conversationLookup.ownedConversationForUpdate(userId, conversationId);
    var existing = reviewCardRepository.findByUserIdAndSourceConversationId(userId, conversationId);
    Npc npc =
        npcRepository
            .findById(conversation.getNpcId())
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (existing.isPresent()) {
      return response(existing.get(), npc);
    }
    var feedback =
        feedbackRepository
            .findById(conversationId)
            .filter(value -> value.getStatus() == FeedbackStatus.DONE)
            .orElseThrow(() -> new BusinessException(ErrorCode.REVIEW_NOT_READY));
    if (conversation.isActive() || feedback.getReviews() == null) {
      throw new BusinessException(ErrorCode.REVIEW_NOT_READY);
    }
    var savedReviews = jsonMapper.readValue(feedback.getReviews(), FeedbackReviews.class);
    var targets =
        ReviewTargets.includeRecommendations(
            ReviewTargets.select(corrections(conversationId), savedReviews.situationExpressions()),
            turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId),
            jsonMapper);
    if (targets.size() < 2) {
      throw new BusinessException(ErrorCode.REVIEW_NOT_READY);
    }
    String summary = feedback.getSummary();
    ReviewCard card =
        reviewCardRepository.save(
            ReviewCard.of(
                userId,
                conversationId,
                npc.getId(),
                npc.getScene().getCode(),
                summary == null ? "지난 대화의 표현을 연습해요." : summary,
                jsonMapper.writeValueAsString(targets)));
    card.captureScenario(
        jsonMapper.writeValueAsString(
            ReviewScenarios.capture(conversation, npc.getCode(), summary)));
    return response(card, npc);
  }

  // 문장 교정(FB-03)이 고쳐 준 학습자 발화. 무례로 막힌 말은 다시 연습하지 않는다.
  private List<ReviewTargets.Correction> corrections(Long conversationId) {
    Map<Long, String> spoken =
        turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId).stream()
            .filter(turn -> turn.getSpeaker() == Speaker.USER && !turn.isRude())
            .collect(Collectors.toMap(Turn::getId, Turn::getText));
    return turnFeedbackRepository.findAllByTurnIdInOrderByTurnIdAsc(spoken.keySet()).stream()
        .map(
            feedback ->
                new ReviewTargets.Correction(
                    spoken.get(feedback.getTurnId()),
                    feedback.getBetterText(),
                    feedback.getReason()))
        .toList();
  }

  private ReviewCardResponse response(ReviewCard card, Npc npc) {
    List<ReviewTarget> targets =
        ReviewTargets.includeRecommendations(
            Arrays.asList(jsonMapper.readValue(card.getTargets(), ReviewTarget[].class)),
            turnRepository.findAllByConversationIdOrderBySeqAsc(card.getSourceConversationId()),
            jsonMapper);
    List<String> used =
        Arrays.asList(jsonMapper.readValue(card.getUsedExpressions(), String[].class));
    return new ReviewCardResponse(
        card.getId(),
        card.getSourceConversationId(),
        card.getSceneCode(),
        card.getNpcId(),
        new NpcSummaryResponse(npc.getCode(), npc.getName(), npc.getRoleLabel()),
        card.getSituationSummary(),
        targets,
        card.getPracticeCount(),
        card.getLastPracticedAt(),
        used,
        withGoals(
            card.getScenario() != null
                ? jsonMapper.readValue(card.getScenario(), ReviewScenario.class)
                : ReviewScenarios.capture(
                    conversationRepository.findById(card.getSourceConversationId()).orElse(null),
                    npc.getCode(),
                    card.getSituationSummary())));
  }

  // 시연 상황처럼 판정할 조건이 있으면 카드에 조건을 함께 보인다. 저장한 상황에는 넣지 않고 읽을 때 붙인다.
  private static ReviewScenario withGoals(ReviewScenario scenario) {
    if (scenario == null) {
      return null;
    }
    return new ReviewScenario(
        scenario.key(),
        scenario.title(),
        scenario.situation(),
        scenario.learnerRole(),
        scenario.learnerNotes(),
        ShowcaseReviewGoals.goals(scenario.key()).stream()
            .map(goal -> new ReviewGoal(goal.id(), goal.title(), goal.hint()))
            .toList());
  }
}
