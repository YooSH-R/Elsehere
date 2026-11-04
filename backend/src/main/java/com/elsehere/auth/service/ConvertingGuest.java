package com.elsehere.auth.service;

import com.elsehere.user.entity.AvatarType;

/**
 * 가입하면 회원으로 바뀔 게스트(AUTH-03). 가입 화면은 캐릭터 고르기를 숨기고 음성 처리 동의를 게스트 때 값으로 채운다.
 *
 * @param avatarType 게스트 때 고른 캐릭터. 가입해도 그대로 쓴다.
 * @param voiceConsent 게스트 때 지금 버전의 음성 처리(VOICE)에 동의했는지
 */
public record ConvertingGuest(AvatarType avatarType, boolean voiceConsent) {}
