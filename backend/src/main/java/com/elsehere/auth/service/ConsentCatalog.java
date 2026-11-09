package com.elsehere.auth.service;

import com.elsehere.auth.dto.ConsentAgreement;
import com.elsehere.user.entity.ConsentType;
import java.util.List;
import java.util.Optional;
import org.springframework.stereotype.Component;

/**
 * 가입할 때 받는 약관과 현재 버전. 약관 문서를 바꾸면 여기의 버전과 주소를 함께 바꾼다.
 *
 * <p>음성 처리 동의(VOICE)는 선택이며, 동의하지 않으면 음성 입력(CONV-02)을 쓸 수 없다. 음성 처리와 AI 개선 동의(TRAINING_*)는 가입 뒤에도
 * USER-03으로 켜고 끈다. 문구를 바꾸면 버전을 올리고, 옛 버전 동의는 동의하지 않은 것으로 본다(docs/ai-training-data.md).
 */
@Component
public class ConsentCatalog {

  private static final List<ConsentTerm> CURRENT_TERMS =
      List.of(
          new ConsentTerm(ConsentType.TERMS, "2026-10", true, "서비스 이용약관", "/terms/2026-10"),
          new ConsentTerm(
              ConsentType.PRIVACY, "2026-11-09", true, "개인정보 처리방침", "/privacy/2026-11-09"),
          new ConsentTerm(ConsentType.AGE14, "2026-10", true, "만 14세 이상입니다", null),
          new ConsentTerm(ConsentType.VOICE, "2026-10", false, "음성 처리 동의", "/voice/2026-10"),
          new ConsentTerm(
              ConsentType.TRAINING_TEXT,
              "2026-10",
              false,
              "AI 개선에 내 대화 기록 활용",
              "/ai-training/2026-10"),
          new ConsentTerm(
              ConsentType.TRAINING_VOICE,
              "2026-10",
              false,
              "AI 개선에 내 녹음 음성 활용",
              "/ai-training/2026-10"));

  public List<ConsentTerm> currentTerms() {
    return CURRENT_TERMS;
  }

  /** 선택 동의. 음성 처리, AI 개선 대화 기록, AI 개선 녹음 음성 순서다. 가입 뒤에도 켜고 끈다. */
  public List<ConsentTerm> optionalTerms() {
    return CURRENT_TERMS.stream().filter(term -> !term.required()).toList();
  }

  /** 문서 이름(terms·privacy·voice·ai-training)의 지금 버전 주소. 그런 문서가 없으면 비어 있다. */
  public Optional<String> currentDocumentUrl(String document) {
    String prefix = "/" + document + "/";
    return CURRENT_TERMS.stream()
        .map(ConsentTerm::url)
        .filter(url -> url != null && url.startsWith(prefix))
        .findFirst();
  }

  /** 지금 받는 약관의 문서 주소인지. 약관 페이지는 이 주소만 연다. */
  public boolean hasDocument(String url) {
    return CURRENT_TERMS.stream().anyMatch(term -> url.equals(term.url()));
  }

  public ConsentTerm currentTerm(ConsentType consentType) {
    return CURRENT_TERMS.stream()
        .filter(term -> term.consentType() == consentType)
        .findFirst()
        .orElseThrow();
  }

  /** 현재 버전에 동의한 약관을 돌려준다. 다른 버전에 동의했거나 없는 약관은 뺀다. */
  public List<ConsentTerm> findAgreedTerms(List<ConsentAgreement> agreements) {
    return CURRENT_TERMS.stream().filter(term -> isAgreed(term, agreements)).toList();
  }

  /** 필수 약관 중 현재 버전에 동의하지 않은 것을 돌려준다. 다른 버전에 동의했으면 동의하지 않은 것으로 본다. */
  public List<ConsentType> findMissingRequired(List<ConsentAgreement> agreements) {
    return CURRENT_TERMS.stream()
        .filter(ConsentTerm::required)
        .filter(term -> !isAgreed(term, agreements))
        .map(ConsentTerm::consentType)
        .toList();
  }

  private static boolean isAgreed(ConsentTerm term, List<ConsentAgreement> agreements) {
    return agreements.stream()
        .anyMatch(
            agreement ->
                agreement.consentType() == term.consentType()
                    && agreement.version().equals(term.version()));
  }
}
