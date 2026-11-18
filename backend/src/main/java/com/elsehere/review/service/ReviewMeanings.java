package com.elsehere.review.service;

import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.dto.ReviewTarget;
import com.elsehere.world.repository.NpcRepository;
import java.time.Duration;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * [다시 연습] 카드의 표현 중 뜻이 없는 문장(대화 중 추천 문장)에 '뜻 보기'와 같은 번역으로 뜻을 한 번만 채워 카드에 저장한다. 고친 문장은 뜻 대신 내가 한 말을
 * 보이므로 채우지 않는다.
 *
 * <p>문장마다 동시에 부르고 최대 {@link #WAIT}만 기다린다. 그 안에 온 뜻은 이번 카드에 보이고, 늦게 온 뜻은 도착하면 저장해 다음부터 보인다. 저장한 뜻이
 * 있으면 다시 부르지 않는다. 번역이 실패하면 뜻 없이 그대로 연습한다.
 */
@Slf4j
@Service
public class ReviewMeanings {
  static final Duration WAIT = Duration.ofSeconds(3);
  private static final Executor CALLS = Executors.newVirtualThreadPerTaskExecutor();
  private static final int DEFAULT_LEVEL = 2;

  private final ReviewCardService reviewCards;
  private final ConversationLookup conversationLookup;
  private final NpcRepository npcRepository;
  private final WorkerNpcs workerNpcs;
  private final AiGateway aiGateway;
  private final TransactionTemplate transactionTemplate;
  private final Duration wait;

  @Autowired
  public ReviewMeanings(
      ReviewCardService reviewCards,
      ConversationLookup conversationLookup,
      NpcRepository npcRepository,
      WorkerNpcs workerNpcs,
      AiGateway aiGateway,
      TransactionTemplate transactionTemplate) {
    this(
        reviewCards,
        conversationLookup,
        npcRepository,
        workerNpcs,
        aiGateway,
        transactionTemplate,
        WAIT);
  }

  ReviewMeanings(
      ReviewCardService reviewCards,
      ConversationLookup conversationLookup,
      NpcRepository npcRepository,
      WorkerNpcs workerNpcs,
      AiGateway aiGateway,
      TransactionTemplate transactionTemplate,
      Duration wait) {
    this.reviewCards = reviewCards;
    this.conversationLookup = conversationLookup;
    this.npcRepository = npcRepository;
    this.workerNpcs = workerNpcs;
    this.aiGateway = aiGateway;
    this.transactionTemplate = transactionTemplate;
    this.wait = wait;
  }

  public ReviewCardResponse fill(Long userId, ReviewCardResponse card) {
    List<String> missing =
        card.targets().stream()
            .filter(target -> target.originalText() == null || target.originalText().isBlank())
            .filter(target -> target.meaningKo() == null || target.meaningKo().isBlank())
            .map(ReviewTarget::expression)
            .distinct()
            .toList();
    if (missing.isEmpty()) {
      return card;
    }
    Speaker speaker;
    try {
      speaker = transactionTemplate.execute(status -> speaker(userId, card));
    } catch (RuntimeException exception) {
      log.warn("review_meanings_skipped card_id={} reason={}", card.id(), exception.toString());
      return card;
    }
    Map<String, CompletableFuture<String>> calls = new LinkedHashMap<>();
    for (String sentence : missing) {
      calls.put(
          sentence, CompletableFuture.supplyAsync(() -> meaning(userId, speaker, sentence), CALLS));
    }
    CompletableFuture<Void> all =
        CompletableFuture.allOf(calls.values().toArray(CompletableFuture[]::new));
    try {
      all.get(wait.toMillis(), TimeUnit.MILLISECONDS);
    } catch (TimeoutException late) {
      all.whenComplete((done, failure) -> save(userId, card.id(), arrived(calls)));
    } catch (ExecutionException failed) {
      log.warn("review_meanings_failed card_id={} reason={}", card.id(), failed.getCause());
    } catch (InterruptedException interrupted) {
      Thread.currentThread().interrupt();
    }
    Map<String, String> ready = arrived(calls);
    if (ready.isEmpty()) {
      return card;
    }
    ReviewCardResponse saved = save(userId, card.id(), ready);
    return saved == null ? card : saved;
  }

  // 번역에는 그 대화의 NPC 정보와 학습자 난이도만 쓴다. 게임 시각·근무 여부는 뜻에 영향이 없다.
  private Speaker speaker(Long userId, ReviewCardResponse card) {
    Conversation source = conversationLookup.ownedConversation(userId, card.sourceConversationId());
    var npc =
        npcRepository
            .findById(card.npcId())
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    Integer level = source.getLevelApplied();
    return new Speaker(source.getId(), workerNpcs.of(npc), level == null ? DEFAULT_LEVEL : level);
  }

  private String meaning(Long userId, Speaker speaker, String sentence) {
    var trace = new WorkerTrace(UUID.randomUUID(), userId, speaker.conversationId());
    return aiGateway.meaning(trace, speaker.npc(), sentence, speaker.level()).meaningKo();
  }

  private static Map<String, String> arrived(Map<String, CompletableFuture<String>> calls) {
    Map<String, String> ready = new LinkedHashMap<>();
    calls.forEach(
        (sentence, call) -> {
          if (call.isDone() && !call.isCompletedExceptionally()) {
            String meaning = call.getNow(null);
            if (meaning != null && !meaning.isBlank()) {
              ready.put(sentence, meaning.strip());
            }
          }
        });
    return ready;
  }

  private ReviewCardResponse save(Long userId, Long cardId, Map<String, String> meanings) {
    if (meanings.isEmpty()) {
      return null;
    }
    try {
      return reviewCards.recordMeanings(userId, cardId, meanings);
    } catch (RuntimeException exception) {
      log.warn("review_meanings_not_saved card_id={} reason={}", cardId, exception.toString());
      return null;
    }
  }

  private record Speaker(Long conversationId, WorkerNpc npc, int level) {}
}
