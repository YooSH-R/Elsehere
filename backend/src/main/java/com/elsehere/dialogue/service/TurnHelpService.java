package com.elsehere.dialogue.service;

import com.elsehere.ai.client.MeaningResult;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.GlossaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.dto.HintResponse;
import com.elsehere.dialogue.dto.MeaningResponse;
import com.elsehere.dialogue.dto.SuggestWordsResponse;
import com.elsehere.mission.service.QuestTalk;
import com.elsehere.mission.service.QuestTalkService;
import com.elsehere.review.service.ReviewCardService;
import java.nio.charset.StandardCharsets;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.json.JsonMapper;

/**
 * NPC 대사 뜻 보기(CONV-04), 힌트(CONV-05), 추천 단어(CONV-12). 한 번 만든 결과는 턴에 저장해 두고 다시 요청하면 AI를 부르지 않는다.
 *
 * <p>AI를 기다리는 동안 DB 트랜잭션을 잡지 않도록 조회 → AI 호출 → 저장을 나눈다.
 */
@Service
@Slf4j
@RequiredArgsConstructor
public class TurnHelpService {

  private static final TypeReference<List<GlossaryResponse>> GLOSSARY = new TypeReference<>() {};

  private final TurnRepository turnRepository;
  private final ConversationLookup conversationLookup;
  private final ReviewCardService reviewCardService;
  private final DialogueContexts dialogueContexts;
  private final QuestTalkService questTalks;
  private final ConversationResponses conversationResponses;
  private final AiGateway aiGateway;
  private final DialogueRequestLocks requestLocks;
  private final TransactionTemplate transactionTemplate;
  private final JsonMapper jsonMapper;
  private final Clock clock;

  /**
   * NPC 턴이 저장되면 대답 직후 {@link MeaningPreparation}이 미리 부르고, 학습자의 [뜻 보기]는 저장된 뜻을 받는다. 미리 만드는 중에 누르면 그
   * 결과를 기다린다.
   */
  public MeaningResponse meaning(Long userId, Long turnId, UUID requestId) {
    return requestLocks.executeMeaning(turnId, () -> meaningOnce(userId, turnId, requestId));
  }

  private MeaningResponse meaningOnce(Long userId, Long turnId, UUID requestId) {
    NpcLine line = transactionTemplate.execute(status -> npcLine(userId, turnId));
    if (line.meaningKo() != null) {
      return new MeaningResponse(
          turnId, line.meaningKo(), jsonMapper.readValue(line.glossary(), GLOSSARY));
    }

    WorkerTrace trace = new WorkerTrace(requestId, userId, line.conversationId());
    MeaningResult result = aiGateway.meaning(trace, line.npc(), line.text(), line.level());
    List<GlossaryResponse> glossary =
        result.glossary() == null
            ? List.of()
            : result.glossary().stream()
                .map(item -> new GlossaryResponse(item.text(), item.meaningKo()))
                .toList();
    String glossaryJson = jsonMapper.writeValueAsString(glossary);
    transactionTemplate.executeWithoutResult(
        status ->
            turnRepository
                .findById(turnId)
                .ifPresent(turn -> turn.attachMeaning(result.meaningKo(), glossaryJson)));
    return new MeaningResponse(turnId, result.meaningKo(), glossary);
  }

  /** 힌트는 워커가 주는 추천 문장 중 가장 쉬운 첫 문장을 쓴다. */
  public HintResponse hint(Long userId, Long turnId, UUID requestId) {
    HintTarget target = transactionTemplate.execute(status -> hintTarget(userId, turnId));
    HintResponse hint = prepareHint(userId, turnId, requestId, target);
    Instant openedAt = clock.instant();
    transactionTemplate.executeWithoutResult(
        status ->
            turnRepository
                .findById(turnId)
                .ifPresent(turn -> turn.openHint(hint.hintText(), hint.hintMeaningKo(), openedAt)));
    return hint;
  }

  private HintResponse prepareHint(Long userId, Long turnId, UUID requestId, HintTarget target) {
    if (target.hintText() != null) {
      return new HintResponse(turnId, target.hintText(), target.hintMeaningKo());
    }

    WorkerTrace trace = new WorkerTrace(requestId, userId, target.conversationId());
    var result =
        aiGateway.hint(trace, target.npc(), target.history(), target.goal(), target.level());
    if (result == null || result.firstSentence() == null) {
      throw new BusinessException(ErrorCode.AI_RESPONSE_INVALID);
    }
    String hintText = result.firstSentence();
    String meaningKo = result.firstMeaningKo();
    transactionTemplate.executeWithoutResult(
        status ->
            turnRepository.findById(turnId).ifPresent(turn -> turn.cacheHint(hintText, meaningKo)));
    return new HintResponse(turnId, hintText, meaningKo);
  }

  /** 추천 단어는 대답 응답(CONV-01·03)을 기다리게 하지 않도록 NPC 대사를 받은 뒤 따로 만든다. */
  public SuggestWordsResponse suggestWords(Long userId, Long turnId, UUID requestId) {
    HintTarget target = transactionTemplate.execute(status -> hintTarget(userId, turnId));
    List<String> words;
    if (target.suggestWords() != null) {
      words = conversationResponses.readWords(target.suggestWords());
    } else {
      WorkerTrace trace = new WorkerTrace(requestId, userId, target.conversationId());
      words =
          aiGateway
              .suggest(trace, target.npc(), target.history(), target.goal(), target.level())
              .words();
      String wordsJson = conversationResponses.writeWords(words);
      transactionTemplate.executeWithoutResult(
          status ->
              turnRepository
                  .findById(turnId)
                  .ifPresent(turn -> turn.attachSuggestWords(wordsJson)));
    }
    try {
      // suggest와 hint는 같은 HINT 종류로 기록되므로 호출 기록의 고유 키를 분리한다.
      UUID hintRequestId =
          UUID.nameUUIDFromBytes((requestId + ":hint").getBytes(StandardCharsets.UTF_8));
      HintResponse hint = prepareHint(userId, turnId, hintRequestId, target);
      return new SuggestWordsResponse(
          turnId,
          SuggestedWords.pick(words, hint.hintText()),
          hint.hintText(),
          hint.hintMeaningKo());
    } catch (BusinessException exception) {
      // 문장 생성만 실패해도 이미 받은 단어와 대화는 계속 쓸 수 있다.
      log.warn("hint_prefetch_failed turn_id={} error_code={}", turnId, exception.getErrorCode());
      return new SuggestWordsResponse(turnId, SuggestedWords.pick(words, null));
    }
  }

  private NpcLine npcLine(Long userId, Long turnId) {
    Turn turn = conversationLookup.ownedNpcTurn(userId, turnId);
    Conversation conversation = turn.getConversation();
    return new NpcLine(
        turn.getText(),
        turn.getMeaningKo(),
        turn.getGlossary(),
        conversation.getId(),
        conversation.getLevelApplied(),
        dialogueContexts.npcOf(conversation).workerNpc());
  }

  private HintTarget hintTarget(Long userId, Long turnId) {
    Turn turn = conversationLookup.ownedNpcTurn(userId, turnId);
    Conversation conversation = turn.getConversation();
    NpcContext npc = dialogueContexts.npcOf(conversation);
    return new HintTarget(
        turn.getHintText(),
        turn.getHintMeaningKo(),
        turn.getSuggestWords(),
        conversation.getId(),
        conversation.getLevelApplied(),
        npc.workerNpc(),
        dialogueContexts.historyUntil(conversation.getId(), turn.getSeq()),
        learningGoal(userId, conversation, npc.summary().npcCode()));
  }

  // 지난 대화나 근무 밖의 대화에 현재 퀘스트를 섞지 않는다. NPC만 아는 사실 대신 학습자에게 남은 첫 목표만 보낸다.
  private String learningGoal(Long userId, Conversation conversation, String npcCode) {
    if (conversation.isReview()) {
      var card = reviewCardService.detail(userId, conversation.getReviewCardId());
      return "Practice the original situation: "
          + card.situationSummary()
          + " Target expressions: "
          + card.targets().stream()
              .map(com.elsehere.review.dto.ReviewTarget::expression)
              .collect(java.util.stream.Collectors.joining("; "));
    }
    // 근무 밖의 대화에는 지금 퀘스트를 섞지 않는다. 장소 연습은 근무 중인 직원과만 한다.
    if (conversation.isNpcOffDuty()) {
      return "";
    }
    // 장소 연습은 고른 상황, 그 밖에는 이 대화를 시작한 퀘스트 단계에서 아직 이루지 않은 첫 목표다.
    return questTalks
        .forTurn(userId, npcCode, conversation)
        .filter(quest -> quest.startedAtThisStage(conversation))
        .flatMap(quest -> quest.talk().goals())
        .filter(QuestTalk::hasGoals)
        .flatMap(
            goals ->
                goals.state().intents().stream()
                    .filter(intent -> goals.remainingGoals().contains(intent.id()))
                    .map(intent -> intent.desc())
                    .findFirst())
        .orElse("");
  }

  /** AI 호출 전에 트랜잭션 안에서 꺼내 둔 NPC 턴 정보. */
  private record NpcLine(
      String text,
      String meaningKo,
      String glossary,
      Long conversationId,
      int level,
      WorkerNpc npc) {}

  /**
   * 힌트·추천 단어를 만들 때 쓸 NPC 턴 정보.
   *
   * @param suggestWords 저장해 둔 추천 단어 JSON 배열. 아직 만들지 않았으면 null
   * @param history 이 NPC 대사까지의 앞선 대화
   */
  private record HintTarget(
      String hintText,
      String hintMeaningKo,
      String suggestWords,
      Long conversationId,
      int level,
      WorkerNpc npc,
      List<WorkerLine> history,
      String goal) {}
}
