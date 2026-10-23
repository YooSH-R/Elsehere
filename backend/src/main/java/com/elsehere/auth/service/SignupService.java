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

    User user =
        userRepository.save(
            User.signUp(
                pendingSignup.provider(),
                pendingSignup.providerUid(),
                pendingSignup.email(),
                request.nickname(),
                request.avatarType()));
    saveAgreedConsents(user, request);
    return UserResponse.from(user);
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

  private void saveAgreedConsents(User user, SignupRequest request) {
    Instant agreedAt = Instant.now(clock);
    consentCatalog
        .findAgreedTerms(request.consents())
        .forEach(
            term ->
                userConsentRepository.save(
                    UserConsent.agree(user, term.consentType(), term.version(), agreedAt)));
  }
}
