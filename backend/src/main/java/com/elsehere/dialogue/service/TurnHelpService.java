package com.elsehere.dialogue.service;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.MeaningResult;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.conversation.dto.GlossaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.dto.HintResponse;
import com.elsehere.dialogue.dto.MeaningResponse;
import com.elsehere.dialogue.dto.SuggestWordsResponse;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.mission.service.MissionDialogueService;
import com.elsehere.review.service.ReviewCardService;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Objects;
import java.util.Optional;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
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
@RequiredArgsConstructor
public class TurnHelpService {

  private static final TypeReference<List<GlossaryResponse>> GLOSSARY = new TypeReference<>() {};

  private final TurnRepository turnRepository;
  private final ConversationLookup conversationLookup;
  private final ReviewCardService reviewCardService;
  private final DialogueContexts dialogueContexts;
  private final MissionDialogueService missionDialogueService;
  private final ConversationResponses conversationResponses;
  private final AiGateway aiGateway;
  private final TransactionTemplate transactionTemplate;
  private final JsonMapper jsonMapper;
  private final Clock clock;

  public MeaningResponse meaning(Long userId, Long turnId, UUID requestId) {
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
    if (target.hintText() != null) {
      return new HintResponse(turnId, target.hintText());
    }

    WorkerTrace trace = new WorkerTrace(requestId, userId, target.conversationId());
    String hintText =
        aiGateway
            .hint(trace, target.npc(), target.history(), target.goal(), target.level())
            .firstSentence();
    Instant openedAt = clock.instant();
    transactionTemplate.executeWithoutResult(
        status ->
            turnRepository.findById(turnId).ifPresent(turn -> turn.openHint(hintText, openedAt)));
    return new HintResponse(turnId, hintText);
  }

  /** 추천 단어는 대답 응답(CONV-01·03)을 기다리게 하지 않도록 NPC 대사를 받은 뒤 따로 만든다. */
  public SuggestWordsResponse suggestWords(Long userId, Long turnId, UUID requestId) {
    HintTarget target = transactionTemplate.execute(status -> hintTarget(userId, turnId));
    if (target.suggestWords() != null) {
      return new SuggestWordsResponse(
          turnId, conversationResponses.readWords(target.suggestWords()));
    }

    WorkerTrace trace = new WorkerTrace(requestId, userId, target.conversationId());
    List<String> words =
        aiGateway
            .suggest(trace, target.npc(), target.history(), target.goal(), target.level())
            .words();
    String wordsJson = conversationResponses.writeWords(words);
    transactionTemplate.executeWithoutResult(
        status ->
            turnRepository.findById(turnId).ifPresent(turn -> turn.attachSuggestWords(wordsJson)));
    return new SuggestWordsResponse(turnId, words);
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
    if (conversation.isNpcOffDuty()) {
      return "";
    }
    Optional<MissionDialogueService.Context> active =
        missionDialogueService.context(userId, npcCode);
    if (active.isEmpty()) {
      return "";
    }
    MissionDialogueService.Context mission = active.get();
    Integer conversationStage =
        mission.townStory() ? conversation.getTownStoryStage() : conversation.getFirstDayStage();
    FirstDayMissionService.Context detail = mission.detail();
    if (!Objects.equals(conversationStage, detail.stage()) || !detail.hasGoals()) {
      return "";
    }
    return detail.state().intents().stream()
        .filter(intent -> detail.remainingGoals().contains(intent.id()))
        .map(intent -> intent.desc())
        .findFirst()
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
      String suggestWords,
      Long conversationId,
      int level,
      WorkerNpc npc,
      List<WorkerLine> history,
      String goal) {}
}
