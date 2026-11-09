package com.elsehere.auth.controller;

import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.entity.ConsentType;
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

  /** 버전 없는 주소는 지금 버전 문서로 보낸다. 게임 화면은 이 주소로 연결해, 문서 버전이 바뀌어도 게임을 고치지 않는다. */
  @GetMapping("/{document:terms|privacy|voice|ai-training}")
  public String currentDocument(@PathVariable String document) {
    return "redirect:"
        + consentCatalog
            .currentDocumentUrl(document)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  @GetMapping("/{document:terms|privacy|voice|ai-training}/{version}")
  public String document(@PathVariable String document, @PathVariable String version, Model model) {
    if (!consentCatalog.hasDocument("/" + document + "/" + version)) {
      throw new BusinessException(ErrorCode.NOT_FOUND);
    }
    model.addAttribute("version", version);
    // 문서마다 버전이 다를 수 있어, 문서끼리의 링크는 보는 문서의 버전이 아니라 각 문서의 지금 주소를 쓴다.
    model.addAttribute("termsUrl", consentCatalog.currentTerm(ConsentType.TERMS).url());
    model.addAttribute("privacyUrl", consentCatalog.currentTerm(ConsentType.PRIVACY).url());
    model.addAttribute("voiceUrl", consentCatalog.currentTerm(ConsentType.VOICE).url());
    model.addAttribute(
        "aiTrainingUrl", consentCatalog.currentTerm(ConsentType.TRAINING_TEXT).url());
    return "legal/" + document;
  }
}
