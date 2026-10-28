package com.elsehere.world.controller;

import com.elsehere.world.service.PronunciationService;
import lombok.RequiredArgsConstructor;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RestController;

/** 사물 단어 발음 듣기(WORLD-04). 발음은 사용자마다 같으므로 로그인만 확인한다. */
@RestController
@RequiredArgsConstructor
public class PronunciationController {

  private static final MediaType AUDIO_WAV = MediaType.parseMediaType("audio/wav");

  private final PronunciationService pronunciationService;

  @GetMapping("/api/v1/vocab/{vocabId}/pronunciation")
  public ResponseEntity<byte[]> getPronunciation(@PathVariable Long vocabId) {
    return ResponseEntity.ok()
        .contentType(AUDIO_WAV)
        .body(pronunciationService.getPronunciation(vocabId));
  }
}
