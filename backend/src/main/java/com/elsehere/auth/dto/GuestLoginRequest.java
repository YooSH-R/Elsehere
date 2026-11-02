package com.elsehere.auth.dto;

import com.elsehere.user.entity.AvatarType;
import jakarta.validation.constraints.NotNull;

/**
 * AUTH-07 게스트로 시작할 때 고른 캐릭터와 음성 처리 동의.
 *
 * @param voiceConsent 음성 처리(VOICE) 선택 동의. 없으면 동의하지 않은 것으로 본다.
 */
public record GuestLoginRequest(@NotNull AvatarType avatarType, Boolean voiceConsent) {

  public boolean agreedToVoice() {
    return Boolean.TRUE.equals(voiceConsent);
  }
}
