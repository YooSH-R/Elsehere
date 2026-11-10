package com.elsehere.dialogue.dto;

/** 학습자 말의 영어 번역 또는 문법 교정문. 화면의 "영어로는 이렇게 말해요" 카드에 쓴다. */
public record TranslationResponse(String english) {}
