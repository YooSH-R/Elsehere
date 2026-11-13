package com.elsehere.review.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyMap;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.timeout;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.MeaningResult;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.dto.ReviewTarget;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.transaction.support.TransactionCallback;
import org.springframework.transaction.support.TransactionTemplate;

// [다시 연습] 카드의 표현에 뜻이 없으면 '뜻 보기'와 같은 번역으로 한 번만 채워 저장한다. 테스트에서는 모델을 부르지 않는다.
class ReviewMeaningsTest {
  private final ReviewCardService cards = mock(ReviewCardService.class);
  private final ConversationLookup lookup = mock(ConversationLookup.class);
  private final NpcRepository npcs = mock(NpcRepository.class);
  private final WorkerNpcs workerNpcs = mock(WorkerNpcs.class);
  private final AiGateway ai = mock(AiGateway.class);
  private final TransactionTemplate transactions = mock(TransactionTemplate.class);
  private final ReviewMeanings meanings =
      new ReviewMeanings(cards, lookup, npcs, workerNpcs, ai, transactions);

  @BeforeEach
  void setUp() {
    when(transactions.execute(any()))
        .thenAnswer(call -> call.<TransactionCallback<?>>getArgument(0).doInTransaction(null));
    Conversation source = Conversation.start(8L, 3L, InitiatedBy.USER, 1, Instant.EPOCH);
    when(lookup.ownedConversation(8L, 2L)).thenReturn(source);
    Npc npc = mock(Npc.class);
    when(npcs.findById(3L)).thenReturn(Optional.of(npc));
    when(workerNpcs.of(npc)).thenReturn(mock(WorkerNpc.class));
  }

  @Test
  void onlyRecommendedSentencesWithoutAMeaningAreTranslatedAndSaved() {
    var card =
        card(
            new ReviewTarget(
                "I'd like an iced coffee, please.",
                "I'd like로 시작하면 부드러워요.",
                null,
                null,
                null,
                "I want coffee"),
            new ReviewTarget("Could I get less ice?", "얼음 조금만 주세요.", null, null, null),
            new ReviewTarget("To take away, please.", null, "지난 대화에서 추천받은 표현이에요.", null, null));
    when(ai.meaning(any(), any(), eq("To take away, please."), anyInt()))
        .thenReturn(new MeaningResult("포장해 주세요.", List.of(), List.of()));
    var saved = card(new ReviewTarget("To take away, please.", "포장해 주세요.", null, null, null));
    when(cards.recordMeanings(8L, 1L, Map.of("To take away, please.", "포장해 주세요.")))
        .thenReturn(saved);

    assertThat(meanings.fill(8L, card)).isSameAs(saved);
    verify(ai, times(1)).meaning(any(), any(), anyString(), anyInt());
  }

  @Test
  void aFailedTranslationKeepsTheCardAsItWas() {
    var card = card(new ReviewTarget("To take away, please.", null, null, null, null));
    when(ai.meaning(any(), any(), anyString(), anyInt()))
        .thenThrow(new IllegalStateException("worker down"));

    assertThat(meanings.fill(8L, card)).isSameAs(card);
    verify(cards, never()).recordMeanings(any(), any(), anyMap());
  }

  // 번역이 늦으면 카드를 붙잡지 않고 넘기고, 늦게 온 뜻은 도착하면 저장해 다음부터 보인다.
  @Test
  void aSlowTranslationDoesNotHoldTheCardAndIsSavedWhenItArrives() {
    var shortWait =
        new ReviewMeanings(
            cards, lookup, npcs, workerNpcs, ai, transactions, Duration.ofMillis(50));
    var card = card(new ReviewTarget("To take away, please.", null, null, null, null));
    CountDownLatch arrive = new CountDownLatch(1);
    when(ai.meaning(any(), any(), anyString(), anyInt()))
        .thenAnswer(
            call -> {
              arrive.await(5, TimeUnit.SECONDS);
              return new MeaningResult("포장해 주세요.", List.of(), List.of());
            });

    assertThat(shortWait.fill(8L, card)).isSameAs(card);
    verify(cards, never()).recordMeanings(any(), any(), anyMap());

    arrive.countDown();
    verify(cards, timeout(2000))
        .recordMeanings(8L, 1L, Map.of("To take away, please.", "포장해 주세요."));
  }

  @Test
  void aCardWithEveryMeaningDoesNotCallTheModel() {
    var card = card(new ReviewTarget("Could I get less ice?", "얼음 조금만 주세요.", null, null, null));

    assertThat(meanings.fill(8L, card)).isSameAs(card);
    verifyNoInteractions(ai);
  }

  private static ReviewCardResponse card(ReviewTarget... targets) {
    return new ReviewCardResponse(
        1L,
        2L,
        "TOWN",
        3L,
        new NpcSummaryResponse("DINER_STAFF", "Rosa", "햄버거 가게 직원"),
        "햄버거를 주문했어요.",
        List.of(targets),
        0,
        null,
        List.of(),
        null);
  }
}
