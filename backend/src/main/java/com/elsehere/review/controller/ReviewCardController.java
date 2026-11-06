package com.elsehere.review.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.review.dto.ReviewCardListResponse;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.service.ReviewCardService;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/** 사용자가 저장한 상황 복습 카드. */
@RestController
@RequiredArgsConstructor
public class ReviewCardController {
  private final ReviewCardService reviewCardService;

  public record CreateRequest(@NotNull @Positive Long conversationId) {}

  @PostMapping("/api/v1/review-cards")
  @ResponseStatus(HttpStatus.CREATED)
  public ReviewCardResponse create(
      @AuthenticationPrincipal LoginUser loginUser, @Valid @RequestBody CreateRequest request) {
    return reviewCardService.create(loginUser.userId(), request.conversationId());
  }

  @GetMapping("/api/v1/review-cards")
  public ReviewCardListResponse list(
      @AuthenticationPrincipal LoginUser loginUser,
      @RequestParam(required = false) String sceneCode,
      @RequestParam(required = false) String cursor,
      @RequestParam(defaultValue = "20") int size) {
    return reviewCardService.list(loginUser.userId(), sceneCode, cursor, size);
  }

  @GetMapping("/api/v1/review-cards/{cardId}")
  public ReviewCardResponse detail(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable Long cardId) {
    return reviewCardService.detail(loginUser.userId(), cardId);
  }

  @DeleteMapping("/api/v1/review-cards/{cardId}")
  @ResponseStatus(HttpStatus.NO_CONTENT)
  public void delete(@AuthenticationPrincipal LoginUser loginUser, @PathVariable Long cardId) {
    reviewCardService.delete(loginUser.userId(), cardId);
  }
}
