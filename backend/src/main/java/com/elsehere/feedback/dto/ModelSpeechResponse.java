package com.elsehere.feedback.dto;

/**
 * 상세 평가 항목의 모범 음성(FB-04).
 *
 * @param url 모범 음성을 받는 주소
 */
public record ModelSpeechResponse(String url) {

  public static ModelSpeechResponse of(Long turnFeedbackId) {
    return new ModelSpeechResponse("/api/v1/turn-feedback/" + turnFeedbackId + "/model-speech");
  }
}
