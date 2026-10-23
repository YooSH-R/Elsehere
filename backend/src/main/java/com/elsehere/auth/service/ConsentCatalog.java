package com.elsehere.auth.service;

import com.elsehere.auth.dto.ConsentAgreement;
import com.elsehere.user.entity.ConsentType;
import java.util.List;
import org.springframework.stereotype.Component;

/**
 * 가입할 때 받는 약관과 현재 버전. 약관 문서를 바꾸면 여기의 버전과 주소를 함께 바꾼다.
 *
 * <p>음성 처리 동의(VOICE)의 필수 여부는 정해지지 않아 선택으로 둔다.
 */
@Component
public class ConsentCatalog {

  private static final List<ConsentTerm> CURRENT_TERMS =
      List.of(
          new ConsentTerm(ConsentType.TERMS, "2026-10", true, "서비스 이용약관", "/terms/2026-10"),
          new ConsentTerm(ConsentType.PRIVACY, "2026-10", true, "개인정보 처리방침", "/privacy/2026-10"),
          new ConsentTerm(ConsentType.AGE14, "2026-10", true, "만 14세 이상입니다", null),
          new ConsentTerm(ConsentType.VOICE, "2026-10", false, "음성 처리 동의", "/voice/2026-10"));

  public List<ConsentTerm> currentTerms() {
    return CURRENT_TERMS;
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
