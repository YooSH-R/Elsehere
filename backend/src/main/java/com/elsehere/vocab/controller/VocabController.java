package com.elsehere.vocab.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.vocab.dto.VocabItemResponse;
import com.elsehere.vocab.dto.VocabListResponse;
import com.elsehere.vocab.dto.VocabSaveRequest;
import com.elsehere.vocab.dto.VocabSaveResponse;
import com.elsehere.vocab.dto.VocabUpdateRequest;
import com.elsehere.vocab.entity.UserVocabSourceType;
import com.elsehere.vocab.service.VocabService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/** 단어장 목록(VOCAB-01), 단어 저장(VOCAB-02), 메모·외웠어요(VOCAB-03), 삭제(VOCAB-04). */
@RestController
@RequiredArgsConstructor
public class VocabController {

  private final VocabService vocabService;

  @GetMapping("/api/v1/vocab")
  public VocabListResponse getVocab(
      @AuthenticationPrincipal LoginUser loginUser,
      @RequestParam(defaultValue = "false") boolean includeMemorized,
      @RequestParam(required = false) String cursor,
      @RequestParam(defaultValue = "20") int size) {
    return vocabService.getVocab(loginUser.userId(), includeMemorized, cursor, size);
  }

  /** 새로 저장하면 201, 이미 저장한 단어면 처음 저장한 항목을 200으로 돌려준다. */
  @PostMapping("/api/v1/vocab")
  public ResponseEntity<VocabSaveResponse> saveWord(
      @AuthenticationPrincipal LoginUser loginUser, @Valid @RequestBody VocabSaveRequest request) {
    // NPC 대사·피드백 출처는 단어 풀이·단어장 후보로 단어를 확인해야 해서, 그 연동 전까지 저장하지 않는다.
    if (request.sourceType() != UserVocabSourceType.OBJECT) {
      throw new BusinessException(ErrorCode.VOCAB_SOURCE_MISMATCH);
    }
    // 단어 키는 사물 저장에서만 쓰는 값이라 어노테이션 대신 여기서 확인한다.
    if (request.wordKey() == null || request.wordKey().isBlank()) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    VocabSaveResponse response = vocabService.saveObjectWord(loginUser.userId(), request.wordKey());
    return ResponseEntity.status(response.created() ? HttpStatus.CREATED : HttpStatus.OK)
        .body(response);
  }

  @PatchMapping("/api/v1/vocab/{userVocabId}")
  public VocabItemResponse updateVocab(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable Long userVocabId,
      @Valid @RequestBody VocabUpdateRequest request) {
    return vocabService.updateVocab(
        loginUser.userId(), userVocabId, request.memo(), request.memorized());
  }

  @DeleteMapping("/api/v1/vocab/{userVocabId}")
  @ResponseStatus(HttpStatus.NO_CONTENT)
  public void deleteVocab(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable Long userVocabId) {
    vocabService.deleteVocab(loginUser.userId(), userVocabId);
  }
}
