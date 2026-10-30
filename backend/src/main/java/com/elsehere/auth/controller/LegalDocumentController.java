package com.elsehere.auth.controller;

import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;

/**
 * 가입 화면(AUTH-03)의 약관 문서. 로그인 전에 열므로 로그인을 묻지 않는다.
 *
 * <p>지금 받는 버전({@link ConsentCatalog})의 주소만 열고, 본문은 templates/legal에 둔다. 문구를 바꾸면 ConsentCatalog의 버전도
 * 올린다.
 */
@Controller
@RequiredArgsConstructor
public class LegalDocumentController {

  private final ConsentCatalog consentCatalog;

  @GetMapping("/{document:terms|privacy|voice|ai-training}/{version}")
  public String document(@PathVariable String document, @PathVariable String version, Model model) {
    if (!consentCatalog.hasDocument("/" + document + "/" + version)) {
      throw new BusinessException(ErrorCode.NOT_FOUND);
    }
    model.addAttribute("version", version);
    return "legal/" + document;
  }
}
