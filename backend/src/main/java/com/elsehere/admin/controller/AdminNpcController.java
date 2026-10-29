package com.elsehere.admin.controller;

import com.elsehere.admin.dto.PersonaRequest;
import com.elsehere.admin.dto.PersonaResponse;
import com.elsehere.admin.service.AdminNpcService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** 운영자 어드민의 NPC 관리 API. 관리자 권한은 보안 설정(SecurityConfig)이 확인한다. */
@RestController
@RequiredArgsConstructor
public class AdminNpcController {

  private final AdminNpcService adminNpcService;

  /** NPC 페르소나를 고친다. 저장한 값은 다음 대화부터 AI 워커에 보낸다. */
  @PutMapping("/api/v1/admin/npcs/{npcCode}/persona")
  public PersonaResponse updatePersona(
      @PathVariable String npcCode, @Valid @RequestBody PersonaRequest request) {
    return adminNpcService.updatePersona(npcCode, request);
  }
}
