package com.elsehere.vocab.dto;

import com.elsehere.vocab.entity.UserVocab;
import com.elsehere.vocab.entity.UserVocabSourceType;
import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.entity.VocabularyType;
import java.time.OffsetDateTime;
import java.time.ZoneId;

/**
 * VOCAB-01 단어장 항목.
 *
 * @param vocab 단어와 단어 하나의 뜻(공용 어휘)
 * @param context 단어를 저장한 문장과 문장 뜻. 저장한 문장이 없으면 null이다.
 * @param memorized 외웠어요로 표시했는지
 * @param source 저장한 곳. 사물에서 저장한 단어는 NPC·대화가 없다.
 */
public record VocabItemResponse(
    Long userVocabId,
    Word vocab,
    Context context,
    String memo,
    boolean memorized,
    OffsetDateTime savedAt,
    Source source) {

  public record Word(Long vocabId, VocabularyType type, String text, String meaningKo) {}

  public record Context(String text, String meaningKo) {}

  public record Source(UserVocabSourceType sourceType, String npcName, Long conversationId) {}

  /** 저장 시각은 {@code zone} 기준으로 보여준다. */
  public static VocabItemResponse from(UserVocab userVocab, ZoneId zone) {
    Vocabulary vocabulary = userVocab.getVocabulary();
    Context context =
        userVocab.getContextText() == null
            ? null
            : new Context(userVocab.getContextText(), userVocab.getContextMeaningKo());
    return new VocabItemResponse(
        userVocab.getId(),
        new Word(
            vocabulary.getId(),
            vocabulary.getType(),
            vocabulary.getText(),
            vocabulary.getMeaningKo()),
        context,
        userVocab.getMemo(),
        userVocab.getMemorizedAt() != null,
        userVocab.getSavedAt().atZone(zone).toOffsetDateTime(),
        // 대화에서 저장한 단어의 NPC 이름은 대화 기능이 생긴 뒤 채운다.
        new Source(userVocab.getSourceType(), null, userVocab.getSourceConversationId()));
  }
}
