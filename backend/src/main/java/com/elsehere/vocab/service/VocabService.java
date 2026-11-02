package com.elsehere.vocab.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.GlossaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.feedback.dto.FeedbackReviews;
import com.elsehere.feedback.dto.VocabCandidateResponse;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.feedback.repository.ConversationFeedbackRepository;
import com.elsehere.user.entity.User;
import com.elsehere.vocab.dto.VocabItemResponse;
import com.elsehere.vocab.dto.VocabListResponse;
import com.elsehere.vocab.dto.VocabSaveResponse;
import com.elsehere.vocab.entity.UserVocab;
import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.entity.VocabularySource;
import com.elsehere.vocab.repository.UserVocabRepository;
import com.elsehere.vocab.repository.VocabularyRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneId;
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
  private static final ZoneId DISPLAY_ZONE = ZoneId.of("Asia/Seoul");

  /** 대화에서 처음 저장하는 단어의 언어. MVP는 영어만 배우므로 기본 언어 어휘로 둔다. */
  private static final String CONVERSATION_LANGUAGE = User.DEFAULT_LANGUAGE;

  private static final TypeReference<List<GlossaryResponse>> GLOSSARY = new TypeReference<>() {};

  /** 한 페이지 최대 개수. 지난 대화 목록(CONV-09)과 같다. */
  private static final int MAX_SIZE = 50;

  private final VocabularyRepository vocabularyRepository;
  private final UserVocabRepository userVocabRepository;
  private final TurnRepository turnRepository;
  private final ConversationRepository conversationRepository;
  private final ConversationFeedbackRepository conversationFeedbackRepository;
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
    Turn line = findOwnNpcLine(userId, turnId);
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
   * 대화 피드백의 단어장 후보에서 단어를 저장한다(VOCAB-02 FEEDBACK). 뜻은 후보의 뜻, 문장은 후보의 학습 예문과 예문 뜻이다.
   *
   * <p>다른 사용자의 대화는 존재 여부를 드러내지 않도록 NOT_FOUND로 응답한다.
   */
  public VocabSaveResponse saveFeedbackWord(Long userId, Long conversationId, String text) {
    Conversation conversation =
        conversationRepository
            .findByIdAndUserId(conversationId, userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    // 피드백을 만들지 못했거나 후보를 넣기 전에 만든 피드백은 저장할 단어를 확인할 수 없다.
    List<VocabCandidateResponse> candidates =
        conversationFeedbackRepository
            .findById(conversation.getId())
            .map(ConversationFeedback::getReviews)
            .map(reviews -> jsonMapper.readValue(reviews, FeedbackReviews.class))
            .map(FeedbackReviews::vocabCandidates)
            .orElse(List.of());
    VocabCandidateResponse word =
        SourceWords.find(candidates, VocabCandidateResponse::expression, text)
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

  /** 다른 사용자의 턴과 내가 말한 턴은 존재 여부를 드러내지 않도록 NOT_FOUND로 응답한다(CONV-04와 같다). */
  private Turn findOwnNpcLine(Long userId, Long turnId) {
    return turnRepository
        .findById(turnId)
        .filter(turn -> turn.getSpeaker() == Speaker.NPC)
        .filter(turn -> turn.getConversation().getUserId().equals(userId))
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
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

  /** 대화에서 저장한 단어는 출처 대화의 NPC 이름을 함께 보여 준다. 한 페이지의 NPC는 한 번에 읽는다. */
  private List<VocabItemResponse> toItems(List<UserVocab> entries) {
    Map<Long, String> npcNames = npcNamesByConversation(entries);
    return entries.stream()
        .map(
            entry -> {
              // 사물에서 저장한 단어는 출처 대화가 없다. 빈 Map.of()는 null 키 조회도 막으므로 먼저 거른다.
              Long conversationId = entry.getSourceConversationId();
              String npcName = conversationId == null ? null : npcNames.get(conversationId);
              return VocabItemResponse.from(entry, npcName, DISPLAY_ZONE);
            })
        .toList();
  }

  private Map<Long, String> npcNamesByConversation(List<UserVocab> entries) {
    Set<Long> conversationIds =
        entries.stream()
            .map(UserVocab::getSourceConversationId)
            .filter(Objects::nonNull)
            .collect(Collectors.toSet());
    if (conversationIds.isEmpty()) {
      return Map.of();
    }
    Map<Long, Long> npcIds =
        conversationRepository.findAllById(conversationIds).stream()
            .collect(Collectors.toMap(Conversation::getId, Conversation::getNpcId));
    Map<Long, String> names =
        npcRepository.findAllById(Set.copyOf(npcIds.values())).stream()
            .collect(Collectors.toMap(Npc::getId, Npc::getName));
    return npcIds.entrySet().stream()
        .collect(Collectors.toMap(Map.Entry::getKey, entry -> names.get(entry.getValue())));
  }
}
