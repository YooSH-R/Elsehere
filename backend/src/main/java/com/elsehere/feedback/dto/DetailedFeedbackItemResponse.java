package com.elsehere.feedback.dto;

/**
 * 사용자 발화 하나의 상세 평가(화면 17).
 *
 * @param betterText 더 자연스러운 표현. 적절한 발화라 고칠 표현이 없으면 null
 * @param idiom 회화 표현 추천. 없으면 null
 * @param hasModelSpeech 모범 음성(FB-04)을 만들어 두었는지
 */
public record DetailedFeedbackItemResponse(
    Long turnFeedbackId,
    Long turnId,
    String userText,
    String betterText,
    String reason,
    IdiomResponse idiom,
    boolean hasModelSpeech) {}
