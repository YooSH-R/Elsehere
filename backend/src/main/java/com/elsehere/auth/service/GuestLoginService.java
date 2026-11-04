package com.elsehere.auth.service;

import com.elsehere.onboarding.service.OnboardingService;
import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.entity.User;
import com.elsehere.user.entity.UserConsent;
import com.elsehere.user.repository.UserConsentRepository;
import com.elsehere.user.repository.UserRepository;
import java.time.Clock;
import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.ThreadLocalRandom;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 소셜 계정 없이 바로 시작하는 게스트 사용자를 만든다(AUTH-07).
 *
 * <p>게스트는 캐릭터와 음성 처리 동의(선택)만 고른다. 닉네임은 서버가 정하고, 가입 약관 동의 기록은 남기지 않는다. 음성 처리에 동의하면 그 동의만 남긴다. 학습 언어와
 * 대화 난이도는 MVP에서 고를 수 있는 값(영어·초급)으로 정해 튜토리얼부터 시작한다.
 */
@Slf4j
@Service
@RequiredArgsConstructor
@Transactional
public class GuestLoginService {

  // 회원 닉네임 규칙은 '-'를 허용하지 않으므로 게스트 닉네임과 회원 닉네임은 겹치지 않는다.
  private static final String NICKNAME_PREFIX = "guest-";
  private static final int NICKNAME_NUMBER_BOUND = 1_000_000;
  private static final String GUEST_LANGUAGE = User.DEFAULT_LANGUAGE;
  private static final int GUEST_LEVEL = 1;

  private final UserRepository userRepository;
  private final UserConsentRepository userConsentRepository;
  private final ConsentCatalog consentCatalog;
  private final OnboardingService onboardingService;
  private final Clock clock;

  public UserResponse startAsGuest(AvatarType avatarType, boolean voiceConsent) {
    User guest =
        userRepository.save(
            User.signUp(
                AuthProvider.GUEST,
                UUID.randomUUID().toString(),
                null,
                unusedNickname(),
                avatarType));
    onboardingService.chooseLanguage(guest.getId(), GUEST_LANGUAGE);
    onboardingService.selectLevel(guest.getId(), GUEST_LANGUAGE, GUEST_LEVEL);
    if (voiceConsent) {
      ConsentTerm voice = consentCatalog.currentTerm(ConsentType.VOICE);
      userConsentRepository.save(
          UserConsent.agree(guest, ConsentType.VOICE, voice.version(), Instant.now(clock)));
    }
    log.info("guest_started user_id={}", guest.getId());
    return UserResponse.from(guest, voiceConsent);
  }

  /** "guest-" 뒤에 여섯 자리 숫자를 붙인다. 이미 쓰는 닉네임이면 숫자를 다시 뽑는다. */
  private String unusedNickname() {
    String nickname;
    do {
      int number = ThreadLocalRandom.current().nextInt(NICKNAME_NUMBER_BOUND);
      nickname = NICKNAME_PREFIX + String.format("%06d", number);
    } while (userRepository.existsByNickname(nickname));
    return nickname;
  }
}
