package com.elsehere.auth.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;
import static org.mockito.AdditionalAnswers.delegatesTo;
import static org.mockito.BDDMockito.willReturn;
import static org.mockito.Mockito.mock;

import ch.qos.logback.classic.Level;
import com.elsehere.TestcontainersConfiguration;
import com.elsehere.auth.dto.ConsentAgreement;
import com.elsehere.auth.dto.SignupRequest;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.logging.LogCapture;
import com.elsehere.user.UserTestData;
import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.User;
import com.elsehere.user.entity.UserConsent;
import com.elsehere.user.repository.UserConsentRepository;
import com.elsehere.user.repository.UserRepository;
import jakarta.persistence.EntityManager;
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
import org.springframework.dao.DataIntegrityViolationException;

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

  @Autowired private EntityManager entityManager;

  @Autowired private ConsentCatalog consentCatalog;

  @Autowired private Clock clock;

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
  @DisplayName("가입하면 사용자 ID와 로그인 방식만 INFO로 남기고 이메일·닉네임은 남기지 않는다")
  void signUp_logsUserSignedUp() {
    try (LogCapture logs = LogCapture.atInfo(SignupService.class)) {
      signupService.signUp(PENDING, request(requiredConsents()));

      Long userId =
          userRepository
              .findByProviderAndProviderUid(AuthProvider.GOOGLE, "google-sub-new")
              .orElseThrow()
              .getId();
      assertThat(logs.messages(Level.INFO))
          .containsExactly("user_signed_up user_id=" + userId + " provider=GOOGLE");
    }
  }

  @Test
  @DisplayName("가입하면 동의한 약관을 버전과 동의 시각과 함께 저장한다")
  void signUp_savesConsentsWithVersionAndTime() {
    signupService.signUp(PENDING, request(requiredConsents()));

    assertThat(userConsentRepository.findAll())
        .extracting(UserConsent::getConsentType, UserConsent::getVersion, UserConsent::getAgreedAt)
        .containsExactlyInAnyOrder(
            tuple(ConsentType.TERMS, "2026-10", NOW),
            tuple(ConsentType.PRIVACY, "2026-11", NOW),
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
            new ConsentAgreement(ConsentType.PRIVACY, "2026-11"));

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
            new ConsentAgreement(ConsentType.PRIVACY, "2026-11"),
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

    UserResponse response = signupService.signUp(PENDING, request(consents));

    assertThat(userConsentRepository.findAll())
        .extracting(UserConsent::getConsentType)
        .contains(ConsentType.VOICE);
    assertThat(response.voiceConsent()).isTrue();
  }

  @Test
  @DisplayName("음성 처리에 동의하지 않고 가입하면 가입 응답에 음성 입력을 쓸 수 없다고 알린다")
  void signUp_withoutVoiceConsent_respondsNoVoiceConsent() {
    UserResponse response = signupService.signUp(PENDING, request(requiredConsents()));

    assertThat(response.voiceConsent()).isFalse();
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

  @Test
  @DisplayName("다른 사용자가 쓰는 닉네임이면 NICKNAME_TAKEN으로 응답하고 사용자를 만들지 않는다")
  void signUpWithTakenNickname_isRejected() {
    UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-other");

    assertThatThrownBy(() -> signupService.signUp(PENDING, request(requiredConsents())))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NICKNAME_TAKEN));
    assertThat(userRepository.findByProviderAndProviderUid(AuthProvider.GOOGLE, "google-sub-new"))
        .isEmpty();
  }

  @Test
  @DisplayName("동시에 가입해 중복 사전 검사를 지나쳐도 DB 제약이 막고 NICKNAME_TAKEN으로 응답한다")
  void concurrentSignUpWithSameNickname_isRejectedByDatabase() {
    UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-other");
    // 다른 요청이 사전 검사와 저장 사이에 같은 닉네임으로 먼저 가입한 상황이다. 사전 검사만 중복을 보지 못하게 한다.
    UserRepository racingUserRepository = mock(UserRepository.class, delegatesTo(userRepository));
    willReturn(false).given(racingUserRepository).existsByNickname("Sun");
    SignupService racingSignupService =
        new SignupService(racingUserRepository, userConsentRepository, consentCatalog, clock);

    assertThatThrownBy(() -> racingSignupService.signUp(PENDING, request(requiredConsents())))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NICKNAME_TAKEN))
        .hasCauseInstanceOf(DataIntegrityViolationException.class);
  }

  @Test
  @DisplayName("게스트가 가입하면 새 사용자를 만들지 않고 게스트를 그 Google 계정의 회원으로 바꿔 기록을 그대로 잇는다")
  void guestSignUp_convertsGuestIntoMember() {
    Long guestId = insertGuest(AvatarType.M, false);

    UserResponse response = signupService.signUp(pendingFrom(guestId), request(requiredConsents()));

    assertThat(userRepository.count()).isEqualTo(1);
    User member = userRepository.findById(guestId).orElseThrow();
    assertThat(member.getProvider()).isEqualTo(AuthProvider.GOOGLE);
    assertThat(member.getProviderUid()).isEqualTo("google-sub-new");
    assertThat(member.getEmail()).isEqualTo("sun@example.com");
    assertThat(member.getNickname()).isEqualTo("Sun");
    assertThat(response.userId()).isEqualTo(guestId);
    assertThat(response.guest()).isFalse();
  }

  @Test
  @DisplayName("게스트가 가입하면 캐릭터와 초기 설정 단계는 게스트 때 그대로라 언어·레벨 화면 없이 게임으로 들어간다")
  void guestSignUp_keepsAvatarAndOnboardingStep() {
    Long guestId = insertGuest(AvatarType.M, false);

    UserResponse response = signupService.signUp(pendingFrom(guestId), request(requiredConsents()));

    assertThat(response.avatarType()).isEqualTo(AvatarType.M);
    assertThat(response.onboardingStep()).isEqualTo(OnboardingStep.DONE);
    assertThat(response.currentLanguage()).isEqualTo("en-US");
  }

  @Test
  @DisplayName("게스트가 가입하면 필수 약관 동의를 남기고, 게스트 때 한 음성 처리 동의는 다시 동의해도 하나만 둔다")
  void guestSignUp_savesRequiredConsentsAndKeepsOneVoiceConsent() {
    Long guestId = insertGuest(AvatarType.M, true);
    List<ConsentAgreement> consents = new ArrayList<>(requiredConsents());
    consents.add(new ConsentAgreement(ConsentType.VOICE, "2026-10"));

    UserResponse response = signupService.signUp(pendingFrom(guestId), request(consents));

    assertThat(userConsentRepository.findAll())
        .filteredOn(consent -> consent.getWithdrawnAt() == null)
        .extracting(UserConsent::getConsentType)
        .containsExactlyInAnyOrder(
            ConsentType.TERMS, ConsentType.PRIVACY, ConsentType.AGE14, ConsentType.VOICE);
    assertThat(response.voiceConsent()).isTrue();
  }

  @Test
  @DisplayName("게스트 때 음성 처리에 동의했어도 가입 화면에서 동의를 끄고 가입하면 철회한다")
  void guestSignUp_withoutVoiceConsent_withdrawsGuestVoiceConsent() {
    Long guestId = insertGuest(AvatarType.M, true);

    UserResponse response = signupService.signUp(pendingFrom(guestId), request(requiredConsents()));

    assertThat(response.voiceConsent()).isFalse();
    assertThat(userConsentRepository.findAll())
        .filteredOn(consent -> consent.getConsentType() == ConsentType.VOICE)
        .allSatisfy(consent -> assertThat(consent.getWithdrawnAt()).isEqualTo(NOW));
  }

  @Test
  @DisplayName("게스트가 가입하면 게스트에서 회원이 됐다는 것을 INFO로 남긴다")
  void guestSignUp_logsConversion() {
    Long guestId = insertGuest(AvatarType.M, false);

    try (LogCapture logs = LogCapture.atInfo(SignupService.class)) {
      signupService.signUp(pendingFrom(guestId), request(requiredConsents()));

      assertThat(logs.messages(Level.INFO))
          .containsExactly(
              "user_signed_up user_id=" + guestId + " provider=GOOGLE from_guest=true");
    }
  }

  @Test
  @DisplayName("가입 대기 정보의 사용자가 이미 게스트가 아니면 바꾸지 않고 새 사용자를 만든다")
  void pendingFromNonGuest_createsNewUser() {
    Long memberId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-other", "Moon");

    UserResponse response =
        signupService.signUp(pendingFrom(memberId), request(requiredConsents()));

    assertThat(response.userId()).isNotEqualTo(memberId);
    assertThat(userRepository.findById(memberId).orElseThrow().getProviderUid())
        .isEqualTo("google-sub-other");
  }

  @Test
  @DisplayName("게스트가 가입하는 중이면 가입 화면에 게스트 때 고른 캐릭터와 음성 처리 동의를 알려 준다")
  void convertingGuest_isFoundWithAvatarAndVoiceConsent() {
    Long guestId = insertGuest(AvatarType.M, true);

    assertThat(signupService.findConvertingGuest(pendingFrom(guestId)))
        .contains(new ConvertingGuest(AvatarType.M, true));
    assertThat(signupService.findConvertingGuest(PENDING)).isEmpty();
  }

  private Long insertGuest(AvatarType avatarType, boolean voiceConsent) {
    User guest =
        userRepository.save(
            User.signUp(AuthProvider.GUEST, "guest-uid", null, "guest-000001", avatarType));
    guest.chooseLanguage("en-US");
    guest.completeLevelSetting();
    guest.finishTutorial();
    if (voiceConsent) {
      userConsentRepository.save(
          UserConsent.agree(guest, ConsentType.VOICE, "2026-10", NOW.minusSeconds(3600)));
    }
    entityManager.flush();
    entityManager.clear();
    return guest.getId();
  }

  private static PendingSignup pendingFrom(Long guestUserId) {
    return new PendingSignup(AuthProvider.GOOGLE, "google-sub-new", "sun@example.com", guestUserId);
  }

  private static SignupRequest request(List<ConsentAgreement> consents) {
    return new SignupRequest("Sun", AvatarType.F, consents);
  }

  private static List<ConsentAgreement> requiredConsents() {
    return List.of(
        new ConsentAgreement(ConsentType.TERMS, "2026-10"),
        new ConsentAgreement(ConsentType.PRIVACY, "2026-11"),
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
