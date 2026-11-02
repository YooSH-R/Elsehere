package com.elsehere.dialogue.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.conversation.dto.TranscriptionPurpose;
import com.elsehere.dialogue.dto.SpeechRegenerateRequest;
import com.elsehere.dialogue.dto.SpeechResponse;
import com.elsehere.dialogue.dto.TranscriptionResponse;
import com.elsehere.dialogue.service.SpeechService;
import jakarta.validation.Valid;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RequestPart;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.multipart.MultipartFile;

/** 음성 인식(CONV-02)과 NPC 음성 다시 듣기(CONV-06)·다시 만들기(CONV-07). */
@RestController
@RequiredArgsConstructor
public class SpeechController {

  private static final MediaType AUDIO_WAV = MediaType.parseMediaType("audio/wav");

  private final SpeechService speechService;

  @PostMapping(
      value = "/api/v1/speech/transcriptions",
      consumes = MediaType.MULTIPART_FORM_DATA_VALUE)
  public TranscriptionResponse transcribe(
      @AuthenticationPrincipal LoginUser loginUser,
      @RequestParam UUID requestId,
      @RequestParam TranscriptionPurpose purpose,
      @RequestParam(required = false) Long conversationId,
      @RequestPart("audio") MultipartFile audio) {
    return speechService.transcribe(loginUser.userId(), requestId, purpose, conversationId, audio);
  }

  @GetMapping("/api/v1/turns/{turnId}/speech")
  public ResponseEntity<byte[]> npcSpeech(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable Long turnId) {
    return ResponseEntity.ok()
        .contentType(AUDIO_WAV)
        .body(speechService.npcSpeech(loginUser.userId(), turnId));
  }

  @PostMapping("/api/v1/turns/{turnId}/speech")
  public SpeechResponse regenerateSpeech(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long turnId,
      @Valid @RequestBody SpeechRegenerateRequest request) {
    return speechService.regenerateSpeech(loginUser.userId(), turnId, request.requestId());
  }
}
