package com.elsehere.auth.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.onboarding.entity.UserLanguageProfile;
import com.elsehere.onboarding.entity.UserLanguageProfileId;
import com.elsehere.onboarding.repository.UserLanguageProfileRepository;
import com.elsehere.onboarding.service.LanguageCatalog;
import com.elsehere.onboarding.service.LevelCatalog;
import com.elsehere.onboarding.service.OnboardingService;
import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserConsentRepository;
import com.elsehere.user.repository.UserRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  ClockConfig.class,
  JpaAuditingConfig.class,
  GuestLoginService.class,
  ConsentCatalog.class,
  OnboardingService.class,
  LanguageCatalog.class,
  LevelCatalog.class
})
class GuestLoginServiceTest {

  @Autowired private GuestLoginService guestLoginService;

  @Autowired private UserRepository userRepository;

  @Autowired private UserConsentRepository userConsentRepository;

  @Autowired private UserLanguageProfileRepository profileRepository;

  @Test
  @DisplayName("게스트로 시작하면 고른 캐릭터와 guest- 닉네임으로 사용자를 만든다")
  void startAsGuest_createsGuestUserWithChosenAvatar() {
    UserResponse response = guestLoginService.startAsGuest(AvatarType.M, false);

    User user = userRepository.findById(response.userId()).orElseThrow();
    assertThat(user.getProvider()).isEqualTo(AuthProvider.GUEST);
    assertThat(user.getEmail()).isNull();
    assertThat(user.getNickname()).matches("guest-\\d{6}");
    assertThat(user.getAvatarType()).isEqualTo(AvatarType.M);
  }

  @Test
  @DisplayName("게스트는 언어·난이도 설정 없이 영어·초급으로 정해 튜토리얼부터 시작한다")
  void startAsGuest_skipsLanguageAndLevelSetting() {
    UserResponse response = guestLoginService.startAsGuest(AvatarType.F, false);

    assertThat(response.onboardingStep()).isEqualTo(OnboardingStep.TUTORIAL);
    assertThat(response.currentLanguage()).isEqualTo("en-US");
    UserLanguageProfile profile =
        profileRepository
            .findById(new UserLanguageProfileId(response.userId(), "en-US"))
            .orElseThrow();
    assertThat(profile.getSelectedLevel()).isEqualTo(1);
  }

  @Test
  @DisplayName("게스트는 가입 약관을 거치지 않고, 음성 처리에 동의하지 않으면 동의 기록 없이 만든다")
  void startAsGuest_savesNoConsents() {
    UserResponse response = guestLoginService.startAsGuest(AvatarType.F, false);

    assertThat(response.voiceConsent()).isFalse();
    assertThat(userConsentRepository.findAll()).isEmpty();
  }

  @Test
  @DisplayName("음성 처리에 동의하고 시작하면 지금 버전의 음성 처리 동의를 남기고 응답에 동의했다고 알린다")
  void startAsGuest_withVoiceConsent_savesVoiceConsent() {
    UserResponse response = guestLoginService.startAsGuest(AvatarType.F, true);

    assertThat(response.voiceConsent()).isTrue();
    assertThat(userConsentRepository.findAll())
        .singleElement()
        .satisfies(
            consent -> {
              assertThat(consent.getConsentType()).isEqualTo(ConsentType.VOICE);
              assertThat(consent.getVersion()).isEqualTo("2026-10");
            });
  }

  @Test
  @DisplayName("게스트로 시작할 때마다 새 사용자를 만든다")
  void startAsGuest_createsNewUserEachTime() {
    UserResponse first = guestLoginService.startAsGuest(AvatarType.F, false);
    UserResponse second = guestLoginService.startAsGuest(AvatarType.F, false);

    assertThat(second.userId()).isNotEqualTo(first.userId());
    assertThat(second.nickname()).isNotEqualTo(first.nickname());
    assertThat(userRepository.count()).isEqualTo(2);
  }
}
