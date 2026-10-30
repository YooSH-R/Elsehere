package com.elsehere.user.dto;

import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.UiTheme;
import com.elsehere.user.entity.User;

/**
 * 응답에 담는 사용자 정보.
 *
 * @param currentLanguage 학습 언어. 언어를 고르기 전(LANG 단계)에는 DB에 기본값이 있어도 {@code null}이다.
 * @param voiceConsent 지금 버전의 음성 처리(VOICE)에 동의했는지. 동의하지 않았으면 음성 입력(CONV-02)을 쓸 수 없다.
 */
public record UserResponse(
    Long userId,
    String nickname,
    AvatarType avatarType,
    String currentLanguage,
    OnboardingStep onboardingStep,
    UiTheme uiTheme,
    boolean voiceConsent) {

  public static UserResponse from(User user, boolean voiceConsent) {
    boolean languageChosen = user.getOnboardingStep() != OnboardingStep.LANG;
    return new UserResponse(
        user.getId(),
        user.getNickname(),
        user.getAvatarType(),
        languageChosen ? user.getCurrentLanguage() : null,
        user.getOnboardingStep(),
        user.getUiTheme(),
        voiceConsent);
  }
}
