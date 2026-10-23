package com.elsehere.auth.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.auth.dto.ConsentAgreement;
import com.elsehere.auth.dto.SignupRequest;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.User;
import com.elsehere.user.entity.UserConsent;
import com.elsehere.user.repository.UserConsentRepository;
import com.elsehere.user.repository.UserRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  JpaAuditingConfig.class,
  SignupService.class,
  ConsentCatalog.class,
  SignupServiceTest.FixedClockConfiguration.class
})
class SignupServiceTest {

  private static final Instant NOW = Instant.parse("2026-10-23T01:00:00Z");
  private static final PendingSignup PENDING =
      new PendingSignup(AuthProvider.GOOGLE, "google-sub-new", "sun@example.com");

  @Autowired private SignupService signupService;

  @Autowired private UserRepository userRepository;

  @Autowired private UserConsentRepository userConsentRepository;

  @Test
  @DisplayName("가입하면 가입 대기 정보와 가입 정보로 사용자를 만들고 초기 설정을 언어 선택부터 시작한다")
  void signUp_createsUserStartingAtLanguageStep() {
    signupService.signUp(PENDING, request(requiredConsents()));

    User user =
        userRepository
            .findByProviderAndProviderUid(AuthProvider.GOOGLE, "google-sub-new")
            .orElseThrow();
    assertThat(user.getEmail()).isEqualTo("sun@example.com");
    assertThat(user.getNickname()).isEqualTo("Sun");
    assertThat(user.getAvatarType()).isEqualTo(AvatarType.F);
    assertThat(user.getOnboardingStep()).isEqualTo(OnboardingStep.LANG);
    assertThat(user.getCurrentLanguage()).isEqualTo("en-US");
  }

  @Test
  @DisplayName("가입하면 동의한 약관을 버전과 동의 시각과 함께 저장한다")
  void signUp_savesConsentsWithVersionAndTime() {
    signupService.signUp(PENDING, request(requiredConsents()));

    assertThat(userConsentRepository.findAll())
        .extracting(UserConsent::getConsentType, UserConsent::getVersion, UserConsent::getAgreedAt)
        .containsExactlyInAnyOrder(
            tuple(ConsentType.TERMS, "2026-10", NOW),
            tuple(ConsentType.PRIVACY, "2026-10", NOW),
            tuple(ConsentType.AGE14, "2026-10", NOW));
  }

  @Test
  @DisplayName("가입 응답의 학습 언어는 언어를 고르기 전이라 비어 있다")
  void signUp_respondsWithoutLanguageBeforeLanguageStep() {
    UserResponse response = signupService.signUp(PENDING, request(requiredConsents()));

    assertThat(response.userId()).isNotNull();
    assertThat(response.nickname()).isEqualTo("Sun");
    assertThat(response.avatarType()).isEqualTo(AvatarType.F);
    assertThat(response.currentLanguage()).isNull();
    assertThat(response.onboardingStep()).isEqualTo(OnboardingStep.LANG);
  }

  @Test
  @DisplayName("필수 약관이 빠지면 VALIDATION_FAILED로 빠진 약관을 알려 주고 사용자를 만들지 않는다")
  void signUpWithoutRequiredConsent_isRejectedWithMissingConsents() {
    List<ConsentAgreement> withoutAge14 =
        List.of(
            new ConsentAgreement(ConsentType.TERMS, "2026-10"),
            new ConsentAgreement(ConsentType.PRIVACY, "2026-10"));

    assertThatThrownBy(() -> signupService.signUp(PENDING, request(withoutAge14)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> {
              assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED);
              assertThat(exception.getDetails())
                  .containsEntry("missingConsents", List.of(ConsentType.AGE14));
            });
    assertThat(userRepository.count()).isZero();
  }

  @Test
  @DisplayName("현재 버전과 다른 약관에 동의했으면 동의하지 않은 것으로 보고 VALIDATION_FAILED로 응답한다")
  void signUpWithOutdatedConsentVersion_isRejected() {
    List<ConsentAgreement> outdatedTerms =
        List.of(
            new ConsentAgreement(ConsentType.TERMS, "2025-01"),
            new ConsentAgreement(ConsentType.PRIVACY, "2026-10"),
            new ConsentAgreement(ConsentType.AGE14, "2026-10"));

    assertThatThrownBy(() -> signupService.signUp(PENDING, request(outdatedTerms)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getDetails())
                    .containsEntry("missingConsents", List.of(ConsentType.TERMS)));
  }

  @Test
  @DisplayName("선택 약관도 현재 버전과 다르면 동의하지 않은 것으로 보고 저장하지 않는다")
  void outdatedOptionalConsent_isNotSaved() {
    List<ConsentAgreement> consents = new ArrayList<>(requiredConsents());
    consents.add(new ConsentAgreement(ConsentType.VOICE, "2025-01"));

    signupService.signUp(PENDING, request(consents));

    assertThat(userConsentRepository.findAll())
        .extracting(UserConsent::getConsentType)
        .containsExactlyInAnyOrder(ConsentType.TERMS, ConsentType.PRIVACY, ConsentType.AGE14);
  }

  @Test
  @DisplayName("선택 약관(음성 처리)에 현재 버전으로 동의하면 함께 저장한다")
  void optionalConsent_isSavedWhenAgreed() {
    List<ConsentAgreement> consents = new ArrayList<>(requiredConsents());
    consents.add(new ConsentAgreement(ConsentType.VOICE, "2026-10"));

    signupService.signUp(PENDING, request(consents));

    assertThat(userConsentRepository.findAll())
        .extracting(UserConsent::getConsentType)
        .contains(ConsentType.VOICE);
  }

  @Test
  @DisplayName("이미 가입한 계정이 다시 가입하면 ALREADY_SIGNED_UP으로 응답한다")
  void signUpTwice_isRejectedAsAlreadySignedUp() {
    signupService.signUp(PENDING, request(requiredConsents()));

    assertThatThrownBy(() -> signupService.signUp(PENDING, request(requiredConsents())))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.ALREADY_SIGNED_UP));
  }

  private static SignupRequest request(List<ConsentAgreement> consents) {
    return new SignupRequest("Sun", AvatarType.F, consents);
  }

  private static List<ConsentAgreement> requiredConsents() {
    return List.of(
        new ConsentAgreement(ConsentType.TERMS, "2026-10"),
        new ConsentAgreement(ConsentType.PRIVACY, "2026-10"),
        new ConsentAgreement(ConsentType.AGE14, "2026-10"));
  }

  @TestConfiguration(proxyBeanMethods = false)
  static class FixedClockConfiguration {

    @Bean
    Clock clock() {
      return Clock.fixed(NOW, ZoneOffset.UTC);
    }
  }
}
