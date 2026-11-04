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
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.hibernate.exception.ConstraintViolationException;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 가입 대기 중인 소셜 로그인 계정으로 사용자를 만든다(AUTH-04). 사용자와 약관 동의는 한 트랜잭션으로 저장한다. */
@Slf4j
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

    Optional<User> guest = findGuest(pendingSignup);
    User user =
        saveRejectingTakenNickname(
            guest
                .map(found -> convertGuest(found, pendingSignup, request))
                .orElseGet(() -> newUser(pendingSignup, request)));
    List<ConsentTerm> agreedTerms = applyConsents(user, request);
    // 이메일·닉네임은 개인정보라 남기지 않는다.
    if (guest.isPresent()) {
      log.info(
          "user_signed_up user_id={} provider={} from_guest={}",
          user.getId(),
          pendingSignup.provider(),
          true);
    } else {
      log.info("user_signed_up user_id={} provider={}", user.getId(), pendingSignup.provider());
    }
    boolean voiceConsent =
        agreedTerms.stream().anyMatch(term -> term.consentType() == ConsentType.VOICE);
    return UserResponse.from(user, voiceConsent);
  }

  /** 가입하면 회원으로 바뀔 게스트. 게스트로 로그인한 세션에서 Google 로그인하지 않았으면 비어 있다. */
  @Transactional(readOnly = true)
  public Optional<ConvertingGuest> findConvertingGuest(PendingSignup pendingSignup) {
    return findGuest(pendingSignup)
        .map(
            guest ->
                new ConvertingGuest(
                    guest.getAvatarType(),
                    hasAgreed(guest, consentCatalog.currentTerm(ConsentType.VOICE))));
  }

  // 다른 탭에서 먼저 가입해 이미 회원이 된 사용자는 바꾸지 않고 새 사용자를 만든다.
  private Optional<User> findGuest(PendingSignup pendingSignup) {
    if (pendingSignup.guestUserId() == null) {
      return Optional.empty();
    }
    return userRepository.findById(pendingSignup.guestUserId()).filter(User::isGuest);
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
  private User saveRejectingTakenNickname(User user) {
    try {
      return userRepository.saveAndFlush(user);
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

  private static User newUser(PendingSignup pendingSignup, SignupRequest request) {
    return User.signUp(
        pendingSignup.provider(),
        pendingSignup.providerUid(),
        pendingSignup.email(),
        request.nickname(),
        request.avatarType());
  }

  // 캐릭터는 게스트 때 고른 것을 그대로 쓰므로 요청의 캐릭터는 보지 않는다.
  private static User convertGuest(User guest, PendingSignup pendingSignup, SignupRequest request) {
    guest.becomeMember(
        pendingSignup.provider(),
        pendingSignup.providerUid(),
        pendingSignup.email(),
        request.nickname());
    return guest;
  }

  /**
   * 가입 화면에서 동의한 약관을 남기고 동의하지 않은 약관은 철회한다. 새 사용자에게는 동의 기록이 없고, 게스트에게는 게스트 때 한 음성 처리 동의만 있다. 이미 지금
   * 버전에 동의해 둔 약관은 다시 남기지 않는다.
   */
  private List<ConsentTerm> applyConsents(User user, SignupRequest request) {
    Instant now = Instant.now(clock);
    List<ConsentTerm> agreedTerms = consentCatalog.findAgreedTerms(request.consents());
    for (ConsentTerm term : consentCatalog.currentTerms()) {
      boolean agreed = agreedTerms.contains(term);
      if (agreed && hasAgreed(user, term)) {
        continue;
      }
      activeConsents(user, term.consentType()).forEach(consent -> consent.withdraw(now));
      if (agreed) {
        userConsentRepository.save(
            UserConsent.agree(user, term.consentType(), term.version(), now));
      }
    }
    return agreedTerms;
  }

  private boolean hasAgreed(User user, ConsentTerm term) {
    return activeConsents(user, term.consentType()).stream()
        .anyMatch(consent -> consent.getVersion().equals(term.version()));
  }

  private List<UserConsent> activeConsents(User user, ConsentType consentType) {
    return userConsentRepository.findByUserIdAndConsentTypeAndWithdrawnAtIsNull(
        user.getId(), consentType);
  }
}
