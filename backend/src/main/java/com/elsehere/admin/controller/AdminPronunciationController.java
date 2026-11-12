package com.elsehere.admin.controller;

import com.elsehere.admin.dto.PronunciationStatusResponse;
import com.elsehere.admin.service.AdminPronunciationService;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/** 사물 단어 발음(WORLD-04) 만들기. 화면 없이 관리 API로 실행한다(docs/admin.md). */
@RestController
@RequiredArgsConstructor
public class AdminPronunciationController {

  private final AdminPronunciationService adminPronunciationService;

  @GetMapping("/api/v1/admin/pronunciations")
  public PronunciationStatusResponse status() {
    return adminPronunciationService.status();
  }

  /** 발음 파일이 없는 사물 단어의 발음을 백그라운드에서 만든다. 진행 상황은 GET으로 본다. */
  @PostMapping("/api/v1/admin/pronunciations")
  @ResponseStatus(HttpStatus.ACCEPTED)
  public PronunciationStatusResponse start() {
    return adminPronunciationService.start();
  }
}
