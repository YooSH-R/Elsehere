package com.elsehere.feedback.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.feedback.dto.FeedbackRequest;
import com.elsehere.feedback.dto.ModelSpeechResponse;
import com.elsehere.feedback.service.ModelSpeechService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** 상세 평가 항목의 모범 음성 만들기·받기(FB-04). */
@RestController
@RequiredArgsConstructor
public class ModelSpeechController {

  private static final MediaType AUDIO_WAV = MediaType.parseMediaType("audio/wav");

  private final ModelSpeechService modelSpeechService;

  @PostMapping("/api/v1/turn-feedback/{turnFeedbackId}/model-speech")
  public ModelSpeechResponse create(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long turnFeedbackId,
      @Valid @RequestBody FeedbackRequest request) {
    return modelSpeechService.create(loginUser.userId(), turnFeedbackId, request.requestId());
  }

  @GetMapping("/api/v1/turn-feedback/{turnFeedbackId}/model-speech")
  public ResponseEntity<byte[]> load(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable Long turnFeedbackId) {
    return ResponseEntity.ok()
        .contentType(AUDIO_WAV)
        .body(modelSpeechService.load(loginUser.userId(), turnFeedbackId));
  }
}
