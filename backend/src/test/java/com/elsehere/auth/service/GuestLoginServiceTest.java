package com.elsehere.auth.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
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
  GuestLoginService.class
})
class GuestLoginServiceTest {

  @Autowired private GuestLoginService guestLoginService;

  @Autowired private UserRepository userRepository;

  @Autowired private UserConsentRepository userConsentRepository;

  @Test
  @DisplayName("게스트로 시작하면 게스트 닉네임과 기본 캐릭터로 사용자를 만들고 초기 설정을 언어 선택부터 시작한다")
  void startAsGuest_createsGuestUserStartingAtLanguageStep() {
    UserResponse response = guestLoginService.startAsGuest();

    User user = userRepository.findById(response.userId()).orElseThrow();
    assertThat(user.getProvider()).isEqualTo(AuthProvider.GUEST);
    assertThat(user.getEmail()).isNull();
    assertThat(user.getNickname()).matches("게스트\\d{6}");
    assertThat(user.getAvatarType()).isEqualTo(AvatarType.F);
    assertThat(user.getOnboardingStep()).isEqualTo(OnboardingStep.LANG);
  }

  @Test
  @DisplayName("게스트는 약관 동의 화면을 거치지 않으므로 동의 기록 없이 만든다")
  void startAsGuest_savesNoConsents() {
    guestLoginService.startAsGuest();

    assertThat(userConsentRepository.findAll()).isEmpty();
  }

  @Test
  @DisplayName("게스트로 시작할 때마다 새 사용자를 만든다")
  void startAsGuest_createsNewUserEachTime() {
    UserResponse first = guestLoginService.startAsGuest();
    UserResponse second = guestLoginService.startAsGuest();

    assertThat(second.userId()).isNotEqualTo(first.userId());
    assertThat(userRepository.count()).isEqualTo(2);
  }
}
