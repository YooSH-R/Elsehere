package com.elsehere.auth.dto;

import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.ConsentType;
import java.util.List;

/**
 * AUTH-03 가입 정보 입력 화면에 필요한 정보.
 *
 * @param consents 지금 받는 약관
 * @param guest 게스트가 가입하는 중이면 게스트 때 고른 캐릭터와 음성 처리 동의. 아니면 {@code null}이라 응답에 담지 않는다.
 */
public record SignupFormResponse(String email, List<Consent> consents, Guest guest) {

  /**
   * 지금 받는 약관 한 가지.
   *
   * @param url 약관 문서 주소. 문서가 없는 항목(만 14세 확인)은 {@code null}이다.
   */
  public record Consent(
      ConsentType consentType, String version, boolean required, String title, String url) {}

  /**
   * 가입하면 회원으로 바뀔 게스트. 가입 화면은 캐릭터 고르기를 숨기고 음성 처리 동의를 게스트 때 값으로 채운다.
   *
   * @param avatarType 게스트 때 고른 캐릭터. 가입해도 그대로 쓴다.
   * @param voiceConsent 게스트 때 지금 버전의 음성 처리(VOICE)에 동의했는지
   */
  public record Guest(AvatarType avatarType, boolean voiceConsent) {}
}
