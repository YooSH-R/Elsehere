package com.elsehere.review.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.feedback.entity.FeedbackReviews;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.feedback.repository.ConversationFeedbackRepository;
import com.elsehere.review.dto.ReviewCardListResponse;
import com.elsehere.review.dto.ReviewCardResponse;
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
    var targets = ReviewTargets.select(savedReviews.situationExpressions());
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

  private ReviewCardResponse response(ReviewCard card, Npc npc) {
    List<ReviewTarget> targets =
        ReviewTargets.forPractice(
            Arrays.asList(jsonMapper.readValue(card.getTargets(), ReviewTarget[].class)));
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
        card.getScenario() != null
            ? jsonMapper.readValue(card.getScenario(), ReviewScenario.class)
            : ReviewScenarios.capture(
                conversationRepository.findById(card.getSourceConversationId()).orElse(null),
                npc.getCode(),
                card.getSituationSummary()));
  }
}
