package com.elsehere.auth.service;

import com.elsehere.auth.dto.SignupRequest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.entity.User;
import com.elsehere.user.entity.UserConsent;
import com.elsehere.user.repository.UserConsentRepository;
import com.elsehere.user.repository.UserRepository;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Map;
import lombok.RequiredArgsConstructor;
import org.hibernate.exception.ConstraintViolationException;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 가입 대기 중인 소셜 로그인 계정으로 사용자를 만든다(AUTH-04). 사용자와 약관 동의는 한 트랜잭션으로 저장한다. */
@Service
@RequiredArgsConstructor
@Transactional
public class SignupService {

  private final UserRepository userRepository;
  private final UserConsentRepository userConsentRepository;
  private final ConsentCatalog consentCatalog;
  private final Clock clock;

  public UserResponse signUp(PendingSignup pendingSignup, SignupRequest request) {
    rejectIfAlreadySignedUp(pendingSignup);
    rejectIfRequiredConsentMissing(request);
    rejectIfNicknameTaken(request.nickname());

    User user = saveNewUser(pendingSignup, request);
    List<ConsentTerm> agreedTerms = saveAgreedConsents(user, request);
    boolean voiceConsent =
        agreedTerms.stream().anyMatch(term -> term.consentType() == ConsentType.VOICE);
    return UserResponse.from(user, voiceConsent);
  }

  private void rejectIfAlreadySignedUp(PendingSignup pendingSignup) {
    boolean alreadySignedUp =
        userRepository
            .findByProviderAndProviderUid(pendingSignup.provider(), pendingSignup.providerUid())
            .isPresent();
    if (alreadySignedUp) {
      throw new BusinessException(ErrorCode.ALREADY_SIGNED_UP);
    }
  }

  private void rejectIfRequiredConsentMissing(SignupRequest request) {
    List<ConsentType> missingConsents = consentCatalog.findMissingRequired(request.consents());
    if (!missingConsents.isEmpty()) {
      throw new BusinessException(
          ErrorCode.VALIDATION_FAILED, Map.of("missingConsents", missingConsents));
    }
  }

  private void rejectIfNicknameTaken(String nickname) {
    if (userRepository.existsByNickname(nickname)) {
      throw new BusinessException(ErrorCode.NICKNAME_TAKEN);
    }
  }

  /**
   * 사전 검사(rejectIfNicknameTaken)와 저장 사이에 다른 요청이 같은 닉네임으로 먼저 가입하면 DB 제약이 저장을 막는다. 그 경우도 중복 닉네임으로
   * 응답한다.
   */
  private User saveNewUser(PendingSignup pendingSignup, SignupRequest request) {
    try {
      return userRepository.save(
          User.signUp(
              pendingSignup.provider(),
              pendingSignup.providerUid(),
              pendingSignup.email(),
              request.nickname(),
              request.avatarType()));
    } catch (DataIntegrityViolationException exception) {
      if (violatesNicknameUnique(exception)) {
        throw new BusinessException(ErrorCode.NICKNAME_TAKEN, exception);
      }
      throw exception;
    }
  }

  private static boolean violatesNicknameUnique(DataIntegrityViolationException exception) {
    return exception.getCause() instanceof ConstraintViolationException violation
        && User.NICKNAME_UNIQUE_CONSTRAINT.equals(violation.getConstraintName());
  }

  private List<ConsentTerm> saveAgreedConsents(User user, SignupRequest request) {
    Instant agreedAt = Instant.now(clock);
    List<ConsentTerm> agreedTerms = consentCatalog.findAgreedTerms(request.consents());
    agreedTerms.forEach(
        term ->
            userConsentRepository.save(
                UserConsent.agree(user, term.consentType(), term.version(), agreedAt)));
    return agreedTerms;
  }
}
