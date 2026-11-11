package com.elsehere.vocab.service;

import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.GlossaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.feedback.entity.FeedbackReviews;
import com.elsehere.feedback.repository.ConversationFeedbackRepository;
import com.elsehere.feedback.repository.TurnFeedbackRepository;
import com.elsehere.user.entity.User;
import com.elsehere.vocab.dto.VocabItemResponse;
import com.elsehere.vocab.dto.VocabItemResponse.SourceNpc;
import com.elsehere.vocab.dto.VocabListResponse;
import com.elsehere.vocab.dto.VocabSaveResponse;
import com.elsehere.vocab.entity.UserVocab;
import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.entity.VocabularySource;
import com.elsehere.vocab.repository.UserVocabRepository;
import com.elsehere.vocab.repository.VocabularyRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.PlaceGroup;
import com.elsehere.world.repository.NpcRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneId;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Optional;
import java.util.Set;
import java.util.function.Supplier;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Limit;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.json.JsonMapper;

/** 단어장(VOCAB-01~04). 저장할 때 AI나 외부 번역 API를 호출하지 않는다. */
@Service
@RequiredArgsConstructor
@Transactional
public class VocabService {

  /** 저장 시각을 보여주는 시간대. MVP는 한국 시간으로 고정한다. */
  private static final ZoneId DISPLAY_ZONE = ClockConfig.SERVICE_ZONE;

  /** 대화에서 처음 저장하는 단어의 언어. MVP는 영어만 배우므로 기본 언어 어휘로 둔다. */
  private static final String CONVERSATION_LANGUAGE = User.DEFAULT_LANGUAGE;

  private static final TypeReference<List<GlossaryResponse>> GLOSSARY = new TypeReference<>() {};

  /** 한 페이지 최대 개수. 지난 대화 목록(CONV-09)과 같다. */
  private static final int MAX_SIZE = 50;

  private final VocabularyRepository vocabularyRepository;
  private final UserVocabRepository userVocabRepository;
  private final ConversationLookup conversationLookup;
  private final ConversationRepository conversationRepository;
  private final ConversationFeedbackRepository conversationFeedbackRepository;
  private final TurnRepository turnRepository;
  private final TurnFeedbackRepository turnFeedbackRepository;
  private final NpcRepository npcRepository;
  private final JsonMapper jsonMapper;
  private final Clock clock;

  /** 단어장 목록(VOCAB-01). 최근 저장순이다. */
  @Transactional(readOnly = true)
  public VocabListResponse getVocab(
      Long userId, boolean includeMemorized, String cursor, int size) {
    if (size < 1 || size > MAX_SIZE) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    // 다음 페이지가 있는지 알기 위해 한 개를 더 가져온다.
    Limit limit = Limit.of(size + 1);
    List<UserVocab> fetched =
        cursor == null
            ? userVocabRepository.findFirstPage(userId, includeMemorized, limit)
            : findPageAfterCursor(userId, includeMemorized, cursor, limit);
    boolean hasNext = fetched.size() > size;
    List<UserVocab> page = hasNext ? fetched.subList(0, size) : fetched;
    String nextCursor = hasNext ? String.valueOf(page.getLast().getId()) : null;

    // 외운 단어도 보기를 끄면 목록과 개수 모두에서 외운 단어를 뺀다.
    long count =
        includeMemorized
            ? userVocabRepository.countByUserId(userId)
            : userVocabRepository.countByUserIdAndMemorizedAtIsNull(userId);
    return new VocabListResponse(new VocabListResponse.Counts(count), toItems(page), nextCursor);
  }

  /** 커서는 이전 페이지 마지막 항목의 ID다. 그 항목보다 먼저 저장한 항목부터 가져온다. */
  private List<UserVocab> findPageAfterCursor(
      Long userId, boolean includeMemorized, String cursor, Limit limit) {
    UserVocab last = findCursorEntry(userId, cursor);
    return userVocabRepository.findPageAfter(
        userId, includeMemorized, last.getSavedAt(), last.getId(), limit);
  }

  /** 숫자가 아니거나, 없거나, 다른 사용자의 항목을 가리키는 커서는 잘못된 요청이다. */
  private UserVocab findCursorEntry(Long userId, String cursor) {
    long userVocabId;
    try {
      userVocabId = Long.parseLong(cursor);
    } catch (NumberFormatException exception) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED, exception);
    }
    return userVocabRepository
        .findByIdAndUserId(userVocabId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.VALIDATION_FAILED));
  }

  /** 메모·외웠어요 표시를 바꾼다(VOCAB-03). null인 값은 보내지 않은 것으로 보고 그대로 둔다. */
  public VocabItemResponse updateVocab(
      Long userId, Long userVocabId, String memo, Boolean memorized) {
    UserVocab userVocab = findOwnEntry(userId, userVocabId);
    if (memo != null) {
      userVocab.changeMemo(memo);
    }
    if (memorized != null) {
      userVocab.changeMemorized(memorized, Instant.now(clock));
    }
    return toItem(userVocab);
  }

  /** 내 단어장에서 지운다(VOCAB-04). 공용 어휘(vocabularies)는 다른 사용자도 쓰므로 남긴다. */
  public void deleteVocab(Long userId, Long userVocabId) {
    userVocabRepository.delete(findOwnEntry(userId, userVocabId));
  }

  /** 내 단어장 항목. 다른 사용자의 항목은 존재 여부를 드러내지 않도록 없는 것과 같이 NOT_FOUND로 응답한다. */
  private UserVocab findOwnEntry(Long userId, Long userVocabId) {
    return userVocabRepository
        .findByIdAndUserId(userVocabId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  /** 사물 카드의 단어를 저장한다(VOCAB-02 OBJECT). 문장은 사물 카드 예문이다. */
  public VocabSaveResponse saveObjectWord(Long userId, String wordKey) {
    Vocabulary vocabulary =
        vocabularyRepository
            .findByWordKey(wordKey)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    return saveUnlessSaved(
        userId, vocabulary, () -> UserVocab.saveFromObject(userId, vocabulary, Instant.now(clock)));
  }

  /**
   * NPC 대사 뜻 보기(CONV-04)의 단어 풀이에서 단어를 저장한다(VOCAB-02 NPC_LINE). 뜻은 단어 풀이의 뜻, 문장은 NPC 대사 전문과 대사 뜻이다.
   */
  public VocabSaveResponse saveNpcLineWord(Long userId, Long turnId, String text) {
    Turn line = conversationLookup.ownedNpcTurn(userId, turnId);
    // 뜻 보기를 하지 않은 대사는 단어 풀이가 없어 저장할 단어를 확인할 수 없다.
    if (line.getGlossary() == null) {
      throw new BusinessException(ErrorCode.VOCAB_SOURCE_MISMATCH);
    }
    GlossaryResponse word =
        SourceWords.find(
                jsonMapper.readValue(line.getGlossary(), GLOSSARY), GlossaryResponse::text, text)
            .orElseThrow(() -> new BusinessException(ErrorCode.VOCAB_SOURCE_MISMATCH));
    Vocabulary vocabulary = findOrCreateWord(word.text(), word.meaningKo(), VocabularySource.NPC);
    return saveUnlessSaved(
        userId,
        vocabulary,
        () ->
            UserVocab.saveFromNpcLine(
                userId,
                vocabulary,
                line.getId(),
                line.getConversation().getId(),
                line.getText(),
                line.getMeaningKo(),
                Instant.now(clock)));
  }

  /**
   * 대화 피드백의 단어장 후보, "이 상황에서 자주 써요" 표현, 문장 교정의 고친 문장에서 단어·숙어·문장을 저장한다(VOCAB-02 FEEDBACK). 후보와 표현은 그
   * 뜻과 학습 예문·예문 뜻을, 고친 문장은 고친 이유를 뜻으로 저장하고 예문은 비운다.
   *
   * <p>다른 사용자의 대화는 존재 여부를 드러내지 않도록 NOT_FOUND로 응답한다.
   */
  public VocabSaveResponse saveFeedbackWord(Long userId, Long conversationId, String text) {
    Conversation conversation = conversationLookup.ownedConversation(userId, conversationId);
    // 피드백을 만들지 못했거나 후보를 넣기 전에 만든 피드백은 저장할 단어를 확인할 수 없다.
    List<FeedbackReviews.VocabCandidate> candidates =
        new ArrayList<>(
            conversationFeedbackRepository
                .findById(conversation.getId())
                .map(ConversationFeedback::getReviews)
                .map(reviews -> jsonMapper.readValue(reviews, FeedbackReviews.class))
                .map(VocabService::feedbackEntries)
                .orElse(List.of()));
    // 같은 말이 후보에도 있으면 뜻과 예문이 있는 후보를 먼저 쓴다.
    candidates.addAll(correctedSentences(conversation));
    FeedbackReviews.VocabCandidate word =
        SourceWords.find(candidates, FeedbackReviews.VocabCandidate::expression, text)
            .orElseThrow(() -> new BusinessException(ErrorCode.VOCAB_SOURCE_MISMATCH));
    Vocabulary vocabulary =
        findOrCreateWord(word.expression(), word.meaningKo(), VocabularySource.FEEDBACK);
    return saveUnlessSaved(
        userId,
        vocabulary,
        () ->
            UserVocab.saveFromFeedback(
                userId,
                vocabulary,
                conversation.getId(),
                word.exampleEn(),
                word.exampleKo(),
                Instant.now(clock)));
  }

  // 저장할 수 있는 말: 단어장 후보와 "이 상황에서 자주 써요" 숙어·문장. 이 필드를 넣기 전의 피드백은 null이다.
  private static List<FeedbackReviews.VocabCandidate> feedbackEntries(FeedbackReviews reviews) {
    List<FeedbackReviews.VocabCandidate> entries = new ArrayList<>();
    if (reviews.vocabCandidates() != null) {
      entries.addAll(reviews.vocabCandidates());
    }
    if (reviews.situationExpressions() != null) {
      reviews.situationExpressions().stream()
          .map(
              item ->
                  new FeedbackReviews.VocabCandidate(
                      item.expression(), item.meaningKo(), item.exampleEn(), item.exampleKo()))
          .forEach(entries::add);
    }
    return entries;
  }

  // 문장 교정(FB-03)의 고친 문장. 한국어로 말한 발화의 영어 문장도 여기 있다. 학습 예문이 없으므로 고친 이유를 뜻으로 둔다.
  private List<FeedbackReviews.VocabCandidate> correctedSentences(Conversation conversation) {
    List<Long> userTurnIds =
        turnRepository.findAllByConversationIdOrderBySeqAsc(conversation.getId()).stream()
            .filter(turn -> turn.getSpeaker() == Speaker.USER)
            .map(Turn::getId)
            .toList();
    return turnFeedbackRepository.findAllByTurnIdInOrderByTurnIdAsc(userTurnIds).stream()
        .filter(feedback -> feedback.getBetterText() != null)
        .map(
            feedback ->
                new FeedbackReviews.VocabCandidate(
                    feedback.getBetterText(), feedback.getReason(), null, null))
        .toList();
  }

  /** 이미 저장한 단어면 새로 만들지 않고 처음 저장한 항목(문장 포함)을 그대로 돌려준다. */
  private VocabSaveResponse saveUnlessSaved(
      Long userId, Vocabulary vocabulary, Supplier<UserVocab> newEntry) {
    Optional<UserVocab> existing =
        userVocabRepository.findByUserIdAndVocabularyId(userId, vocabulary.getId());
    if (existing.isPresent()) {
      return new VocabSaveResponse(toItem(existing.get()), false);
    }
    return new VocabSaveResponse(toItem(userVocabRepository.save(newEntry.get())), true);
  }

  /** 사전에 있는 단어면 기존 뜻을 그대로 쓰고, 없으면 대화에서 받은 뜻으로 새로 만든다. */
  private Vocabulary findOrCreateWord(String text, String meaningKo, VocabularySource source) {
    return vocabularyRepository
        .findFirstByLanguageCodeAndTextIgnoreCaseOrderByIdAsc(CONVERSATION_LANGUAGE, text)
        .orElseGet(
            () ->
                vocabularyRepository.save(
                    Vocabulary.newWord(CONVERSATION_LANGUAGE, text, meaningKo, source)));
  }

  private VocabItemResponse toItem(UserVocab userVocab) {
    return toItems(List.of(userVocab)).getFirst();
  }

  /** 대화에서 저장한 단어는 출처 대화의 NPC 이름과 일하는 곳을 함께 보여 준다. 한 페이지의 대화와 NPC는 한 번에 읽는다. */
  private List<VocabItemResponse> toItems(List<UserVocab> entries) {
    Map<Long, SourceNpc> npcs = sourceNpcsByConversation(entries);
    return entries.stream()
        .map(
            entry -> {
              // 사물에서 저장한 단어는 출처 대화가 없다. 빈 Map.of()는 null 키 조회도 막으므로 먼저 거른다.
              Long conversationId = entry.getSourceConversationId();
              SourceNpc npc = conversationId == null ? SourceNpc.NONE : npcs.get(conversationId);
              return VocabItemResponse.from(entry, npc, DISPLAY_ZONE);
            })
        .toList();
  }

  private Map<Long, SourceNpc> sourceNpcsByConversation(List<UserVocab> entries) {
    Set<Long> conversationIds =
        entries.stream()
            .map(UserVocab::getSourceConversationId)
            .filter(Objects::nonNull)
            .collect(Collectors.toSet());
    if (conversationIds.isEmpty()) {
      return Map.of();
    }
    List<Conversation> conversations = conversationRepository.findAllById(conversationIds);
    Map<Long, Npc> npcsById =
        npcRepository
            .findAllById(
                conversations.stream().map(Conversation::getNpcId).collect(Collectors.toSet()))
            .stream()
            .collect(Collectors.toMap(Npc::getId, npc -> npc));
    return conversations.stream()
        .collect(
            Collectors.toMap(
                Conversation::getId,
                conversation -> sourceNpc(conversation, npcsById.get(conversation.getNpcId()))));
  }

  /** 근무 시간 밖에 만난 NPC와 나눈 말은 일하는 곳의 표현이 아니라 일상 대화라 일하는 곳을 붙이지 않는다. */
  private static SourceNpc sourceNpc(Conversation conversation, Npc npc) {
    if (conversation.isNpcOffDuty()) {
      return new SourceNpc(npc.getName(), null, PlaceGroup.DAILY);
    }
    return new SourceNpc(npc.getName(), npc.getPlaceName(), npc.getPlaceGroup());
  }
}
