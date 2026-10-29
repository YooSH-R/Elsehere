package com.elsehere.onboarding.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.onboarding.dto.LevelTestAnswerRequest;
import com.elsehere.onboarding.dto.LevelTestLanguageRequest;
import com.elsehere.onboarding.dto.LevelTestResultResponse;
import com.elsehere.onboarding.dto.LevelTestStartResponse;
import com.elsehere.onboarding.service.LevelTestService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/** 레벨 테스트(ONB-03~07). MVP는 화면 04에서 [건너뛰기]만 쓰고, 테스트 진행은 프론트에서 막아 둔다. */
@RestController
@RequestMapping("/api/v1/level-tests")
@RequiredArgsConstructor
public class LevelTestController {

  private final LevelTestService levelTestService;

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
    return levelTestService.complete(loginUser.userId(), testId);
  }

  @PostMapping("/{testId}/quit")
  public LevelTestResultResponse quit(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable Long testId) {
    return levelTestService.quit(loginUser.userId(), testId);
  }
}
