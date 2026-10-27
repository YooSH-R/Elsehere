package com.elsehere.world.dto;

/**
 * 장소의 사물에 붙은 공용 단어 카드. 같은 단어를 쓰는 사물은 모두 이 카드를 보여준다.
 *
 * @param wordKey Unity 공용 단어 키(vocabularies.word_key)
 * @param hasPronunciation 미리 만든 발음 음성이 있는지. 없으면 클라이언트가 발음 듣기 버튼을 숨긴다.
 * @param saved 로그인한 사용자가 이 단어를 단어장에 저장했는지
 * @param hoverHidden 로그인한 사용자가 이 단어 카드의 자동 표시를 껐는지
 */
public record WordCardResponse(
    String wordKey,
    ObjectVocabResponse vocab,
    boolean hasPronunciation,
    boolean saved,
    boolean hoverHidden) {}
