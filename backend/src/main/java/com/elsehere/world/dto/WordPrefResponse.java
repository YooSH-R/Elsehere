package com.elsehere.world.dto;

/**
 * WORLD-03 단어 카드 자동 표시 설정 결과.
 *
 * @param wordKey Unity 공용 단어 키(vocabularies.word_key)
 * @param hoverHidden 자동 표시를 껐는지. 끈 단어는 그 단어를 쓰는 모든 장소에서 호버로 카드를 띄우지 않고 클릭해야 보인다.
 */
public record WordPrefResponse(String wordKey, boolean hoverHidden) {}
