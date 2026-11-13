package com.elsehere.review.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.review.dto.ReviewExpressionUsage;
import com.elsehere.review.dto.ReviewPracticeProgressResponse;
import com.elsehere.review.dto.ReviewPracticeResultResponse;
import com.elsehere.review.dto.ReviewTarget;
import com.elsehere.review.dto.ReviewUtterance;
import com.elsehere.review.entity.ReviewCard;
import com.elsehere.review.repository.ReviewCardRepository;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Limit;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.databind.json.JsonMapper;

/** 대화 행 잠금을 가진 종료 트랜잭션에서 한 번만 결과와 연습 횟수를 확정한다. */
@Service
@RequiredArgsConstructor
public class ReviewPracticeResults {
  private final ReviewCardRepository cardRepository;
  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final JsonMapper jsonMapper;

  @Transactional(readOnly = true)
  public ReviewPracticeProgressResponse progress(Long userId, Long cardId, Long conversationId) {
    ReviewCard card =
        cardRepository
            .findByIdAndUserId(cardId, userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    Conversation conversation =
        conversationRepository
            .findByIdAndUserId(conversationId, userId)
            .filter(found -> cardId.equals(found.getReviewCardId()))
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    List<ReviewExpressionUsage> expressions = expressions(card, utterances(conversationId));
    return new ReviewPracticeProgressResponse(
        conversationId,
        cardId,
        usedExpressions(expressions).size(),
        expressions,
        conversation.getReviewCompletedGoals());
  }

  @Transactional(propagation = Propagation.MANDATORY)
  public ReviewPracticeResultResponse record(Long userId, Conversation conversation) {
    if (conversation.getReviewResult() != null) {
      return read(conversation.getReviewResult());
    }
    var owned = cardRepository.findOwnedForUpdate(conversation.getReviewCardId(), userId);
    // NPC가 먼저 끝낸 뒤 카드를 삭제했다면 종료 자체는 성공시키고 삭제된 카드를 다시 만들지 않는다.
    if (owned.isEmpty()) {
      return null;
    }
    var card = owned.get();
    List<ReviewUtterance> utterances = utterances(conversation.getId());
    List<ReviewExpressionUsage> expressions = expressions(card, utterances);
    List<String> used = usedExpressions(expressions);
    card.recordPractice(conversation.getEndedAt(), jsonMapper.writeValueAsString(used));
    var result =
        new ReviewPracticeResultResponse(
            conversation.getId(),
            card.getId(),
            used.size() >= 2,
            used.size(),
            card.getPracticeCount(),
            conversation.getEndedAt(),
            expressions,
            utterances);
    conversation.recordReviewResult(jsonMapper.writeValueAsString(result));
    return result;
  }

  private List<ReviewUtterance> utterances(Long conversationId) {
    return turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId).stream()
        .filter(
            turn ->
                turn.getSpeaker() == Speaker.USER && !Boolean.TRUE.equals(turn.getGuardAbusive()))
        .map(turn -> new ReviewUtterance(turn.getId(), turn.getText()))
        .toList();
  }

  private List<ReviewExpressionUsage> expressions(
      ReviewCard card, List<ReviewUtterance> utterances) {
    return ReviewTargets.includeRecommendations(
            Arrays.asList(jsonMapper.readValue(card.getTargets(), ReviewTarget[].class)),
            turnRepository.findAllByConversationIdOrderBySeqAsc(card.getSourceConversationId()),
            jsonMapper)
        .stream()
        .map(
            target ->
                new ReviewExpressionUsage(
                    target.expression(),
                    utterances.stream()
                        .filter(
                            turn ->
                                ReviewExpressionEvidence.matches(target.expression(), turn.text()))
                        .toList()))
        .toList();
  }

  private List<String> usedExpressions(List<ReviewExpressionUsage> expressions) {
    var distinctUsed =
        expressions.stream()
            .filter(item -> !item.evidence().isEmpty())
            .map(ReviewExpressionUsage::expression)
            .collect(
                Collectors.toMap(
                    ReviewExpressionEvidence::canonicalExpression,
                    expression -> expression,
                    (first, duplicate) -> first,
                    LinkedHashMap::new));
    return List.copyOf(distinctUsed.values());
  }

  @Transactional(readOnly = true)
  public ReviewPracticeResultResponse latest(Long userId, Long cardId) {
    requireCard(userId, cardId);
    return conversationRepository.findReviewResults(userId, cardId, Limit.of(1)).stream()
        .findFirst()
        .map(conversation -> read(conversation.getReviewResult()))
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  @Transactional(readOnly = true)
  public ReviewPracticeResultResponse detail(Long userId, Long cardId, Long conversationId) {
    requireCard(userId, cardId);
    return conversationRepository
        .findByIdAndUserId(conversationId, userId)
        .filter(conversation -> cardId.equals(conversation.getReviewCardId()))
        .filter(conversation -> conversation.getReviewResult() != null)
        .map(conversation -> read(conversation.getReviewResult()))
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  private void requireCard(Long userId, Long cardId) {
    if (cardRepository.findByIdAndUserId(cardId, userId).isEmpty()) {
      throw new BusinessException(ErrorCode.NOT_FOUND);
    }
  }

  private ReviewPracticeResultResponse read(String json) {
    return jsonMapper.readValue(json, ReviewPracticeResultResponse.class);
  }
}
