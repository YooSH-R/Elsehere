package com.elsehere.user.service;

import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.auth.service.ConsentTerm;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.service.TrainingRecordingService;
import com.elsehere.user.dto.ConsentResponse;
import com.elsehere.user.dto.ConsentsResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.entity.User;
import com.elsehere.user.entity.UserConsent;
import com.elsehere.user.repository.UserConsentRepository;
import com.elsehere.user.repository.UserRepository;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 선택 동의를 조회하고 켜고 끈다(USER-02·03). 음성 처리(VOICE)와 AI 개선(학습·평가)에 대화 기록·녹음 음성을 쓰는 동의다.
 *
 * <p>게스트는 음성 처리 동의만 바꿀 수 있고 AI 개선에는 동의할 수 없다. 녹음 음성 동의를 끄면 그동안 저장한 녹음을 바로 지운다. 대화 기록은 서비스에 필요하므로
 * 지우지 않고 학습용 내보내기에서만 뺀다(docs/ai-training-data.md).
 */
@Service
@RequiredArgsConstructor
@Transactional
public class OptionalConsentService {

  private final UserRepository userRepository;
  private final UserConsentRepository userConsentRepository;
  private final ConsentCatalog consentCatalog;
  private final TrainingRecordingService trainingRecordingService;
  private final Clock clock;

  /** 이 사용자가 바꿀 수 있는 선택 동의. 게스트에게는 음성 처리 동의만 준다. */
  @Transactional(readOnly = true)
  public ConsentsResponse findAll(Long userId) {
    User user = findUser(userId);
    return new ConsentsResponse(
        consentCatalog.optionalTerms().stream()
            .filter(term -> canChange(user, term))
            .map(term -> toResponse(term, hasAgreed(userId, term)))
            .toList());
  }

  public ConsentResponse change(Long userId, ConsentType consentType, boolean agreed) {
    User user = findUser(userId);
    ConsentTerm term = consentCatalog.currentTerm(consentType);
    if (!canChange(user, term)) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }

    if (agreed) {
      agree(user, term);
    } else {
      withdraw(userId, consentType);
    }
    return toResponse(term, agreed);
  }

  /** 지금 문구(version)에 동의했고 철회하지 않았는지. */
  @Transactional(readOnly = true)
  public boolean hasAgreed(Long userId, ConsentType consentType) {
    return hasAgreed(userId, consentCatalog.currentTerm(consentType));
  }

  private boolean hasAgreed(Long userId, ConsentTerm term) {
    return activeConsents(userId, term.consentType()).stream()
        .anyMatch(consent -> consent.getVersion().equals(term.version()));
  }

  private void agree(User user, ConsentTerm term) {
    if (hasAgreed(user.getId(), term)) {
      return;
    }
    // 옛 버전 동의는 철회로 정리하고 지금 버전으로 새로 남긴다.
    Instant now = Instant.now(clock);
    activeConsents(user.getId(), term.consentType()).forEach(consent -> consent.withdraw(now));
    userConsentRepository.save(UserConsent.agree(user, term.consentType(), term.version(), now));
  }

  private void withdraw(Long userId, ConsentType consentType) {
    Instant now = Instant.now(clock);
    activeConsents(userId, consentType).forEach(consent -> consent.withdraw(now));
    if (consentType == ConsentType.TRAINING_VOICE) {
      trainingRecordingService.deleteAllOf(userId);
    }
  }

  private User findUser(Long userId) {
    return userRepository
        .findById(userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  // 필수 약관은 가입할 때만 받는다. 게스트는 동의 기록을 학습에 쓰지 않으므로 AI 개선에는 동의할 수 없다.
  private static boolean canChange(User user, ConsentTerm term) {
    if (term.required()) {
      return false;
    }
    return user.getProvider() != AuthProvider.GUEST || !term.consentType().isTraining();
  }

  private List<UserConsent> activeConsents(Long userId, ConsentType consentType) {
    return userConsentRepository.findByUserIdAndConsentTypeAndWithdrawnAtIsNull(
        userId, consentType);
  }

  private static ConsentResponse toResponse(ConsentTerm term, boolean agreed) {
    return new ConsentResponse(
        term.consentType(), term.version(), term.title(), term.url(), agreed);
  }
}
