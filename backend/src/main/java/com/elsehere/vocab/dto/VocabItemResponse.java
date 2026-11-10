package com.elsehere.vocab.dto;

import com.elsehere.vocab.entity.UserVocab;
import com.elsehere.vocab.entity.UserVocabSourceType;
import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.entity.VocabularyType;
import com.elsehere.world.entity.PlaceGroup;
import java.time.OffsetDateTime;
import java.time.ZoneId;

/**
 * VOCAB-01 단어장 항목.
 *
 * @param vocab 단어·숙어와 그 뜻(공용 어휘). 숙어는 type이 IDIOM이다.
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

  /**
   * 저장한 곳.
   *
   * @param npcName 출처 대화의 NPC 이름. 사물에서 저장한 단어는 null이다.
   * @param placeName 출처 대화 NPC가 일하는 곳(카페, 우체국). 사물에서 저장했거나 근무 시간 밖 대화면 null이다.
   * @param placeGroup 단어장에서 대화 표현을 나누는 장소 묶음. 근무 시간 밖 대화는 DAILY, 사물에서 저장한 단어는 null이다.
   */
  public record Source(
      UserVocabSourceType sourceType,
      String npcName,
      Long conversationId,
      String placeName,
      PlaceGroup placeGroup) {}

  /**
   * 출처 대화의 NPC. 사물에서 저장한 단어는 {@link #NONE}이다.
   *
   * @param placeName 일하는 곳. 근무 시간 밖 대화면 null이다.
   * @param placeGroup 장소 묶음. 근무 시간 밖 대화는 DAILY다.
   */
  public record SourceNpc(String name, String placeName, PlaceGroup placeGroup) {

    public static final SourceNpc NONE = new SourceNpc(null, null, null);
  }

  /** 저장 시각은 {@code zone} 기준으로 보여준다. */
  public static VocabItemResponse from(UserVocab userVocab, SourceNpc npc, ZoneId zone) {
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
        new Source(
            userVocab.getSourceType(),
            npc.name(),
            userVocab.getSourceConversationId(),
            npc.placeName(),
            npc.placeGroup()));
  }
}
