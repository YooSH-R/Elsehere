package com.elsehere.world.dto;

/**
 * 사물 카드.
 *
 * @param objectKey Unity 씬의 사물 키(world_objects.unity_key)
 * @param hasPronunciation 미리 만든 발음 음성이 있는지. 없으면 클라이언트가 발음 듣기 버튼을 숨긴다.
 * @param saved 로그인한 사용자가 이 단어를 단어장에 저장했는지
 * @param hoverHidden 로그인한 사용자가 이 사물 카드의 자동 표시를 껐는지
 */
public record ObjectCardResponse(
    String objectKey,
    ObjectVocabResponse vocab,
    boolean hasPronunciation,
    boolean saved,
    boolean hoverHidden) {}
