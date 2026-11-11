package com.elsehere.onboarding.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.onboarding.dto.LevelTestAnswerRequest;
import com.elsehere.onboarding.dto.LevelTestLanguageRequest;
import com.elsehere.onboarding.dto.LevelTestResultResponse;
import com.elsehere.onboarding.dto.LevelTestStartResponse;
import com.elsehere.onboarding.service.LevelTestCompletionService;
import com.elsehere.onboarding.service.LevelTestService;
import com.elsehere.onboarding.service.LevelTestSpeechService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/** 레벨 테스트(ONB-03~07, ONB-10). 듣기 4·말하기 2 문항으로 초급·중급 중 추천 난이도를 정한다. */
@RestController
@RequestMapping("/api/v1/level-tests")
@RequiredArgsConstructor
public class LevelTestController {

  private static final MediaType AUDIO_WAV = MediaType.parseMediaType("audio/wav");

  private final LevelTestService levelTestService;
  private final LevelTestCompletionService levelTestCompletionService;
  private final LevelTestSpeechService levelTestSpeechService;

  @PostMapping
  @ResponseStatus(HttpStatus.CREATED)
  public LevelTestStartResponse start(
      @AuthenticationPrincipal LoginUser loginUser,
      @Valid @RequestBody LevelTestLanguageRequest request) {
    return levelTestService.start(loginUser.userId(), request.languageCode());
  }

  @PutMapping("/{testId}/answers/{itemCode}")
  @ResponseStatus(HttpStatus.NO_CONTENT)
  public void answer(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long testId,
      @PathVariable String itemCode,
      @Valid @RequestBody LevelTestAnswerRequest request) {
    levelTestService.answer(loginUser.userId(), testId, itemCode, request.answer());
  }

  /** [건너뛰기]는 테스트를 시작하기 전에 누르므로 테스트 ID 없이 언어만 받는다. */
  @PostMapping("/skip")
  @ResponseStatus(HttpStatus.CREATED)
  public LevelTestResultResponse skip(
      @AuthenticationPrincipal LoginUser loginUser,
      @Valid @RequestBody LevelTestLanguageRequest request) {
    return levelTestService.skip(loginUser.userId(), request.languageCode());
  }

  @PostMapping("/{testId}/complete")
  public LevelTestResultResponse complete(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable Long testId) {
    return levelTestCompletionService.complete(loginUser.userId(), testId);
  }

  @PostMapping("/{testId}/quit")
  public LevelTestResultResponse quit(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable Long testId) {
    return levelTestService.quit(loginUser.userId(), testId);
  }

  /** 듣기 문항 음성(ONB-10). 자막 없이 이 음성만 들려준다. */
  @GetMapping("/items/{itemCode}/speech")
  public ResponseEntity<byte[]> speech(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable String itemCode) {
    return ResponseEntity.ok()
        .contentType(AUDIO_WAV)
        .body(levelTestSpeechService.speech(loginUser.userId(), itemCode));
  }
}
