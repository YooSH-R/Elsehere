package com.elsehere.user.service;

import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.auth.service.ConsentTerm;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.service.TrainingRecordingService;
import com.elsehere.user.dto.TrainingConsentResponse;
import com.elsehere.user.dto.TrainingConsentsResponse;
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
 * AI 개선(학습·평가)에 대화 기록·녹음 음성을 쓰는 선택 동의를 조회하고 켜고 끈다(USER-02·03).
 *
 * <p>게스트는 동의할 수 없다. 녹음 음성 동의를 끄면 그동안 저장한 녹음을 바로 지운다. 대화 기록은 서비스에 필요하므로 지우지 않고 학습용 내보내기에서만
 * 뺀다(docs/ai-training-data.md).
 */
@Service
@RequiredArgsConstructor
@Transactional
public class TrainingConsentService {

  private final UserRepository userRepository;
  private final UserConsentRepository userConsentRepository;
  private final ConsentCatalog consentCatalog;
  private final TrainingRecordingService trainingRecordingService;
  private final Clock clock;

  @Transactional(readOnly = true)
  public TrainingConsentsResponse findAll(Long userId) {
    return new TrainingConsentsResponse(
        consentCatalog.trainingTerms().stream()
            .map(term -> toResponse(term, hasAgreed(userId, term)))
            .toList());
  }

  public TrainingConsentResponse change(Long userId, ConsentType consentType, boolean agreed) {
    if (!consentType.isTraining()) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (user.getProvider() == AuthProvider.GUEST) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }

    ConsentTerm term = consentCatalog.currentTerm(consentType);
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

  private List<UserConsent> activeConsents(Long userId, ConsentType consentType) {
    return userConsentRepository.findByUserIdAndConsentTypeAndWithdrawnAtIsNull(
        userId, consentType);
  }

  private static TrainingConsentResponse toResponse(ConsentTerm term, boolean agreed) {
    return new TrainingConsentResponse(
        term.consentType(), term.version(), term.title(), term.url(), agreed);
  }
}
