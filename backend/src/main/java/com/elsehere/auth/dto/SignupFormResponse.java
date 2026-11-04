package com.elsehere.auth.dto;

import com.elsehere.auth.service.ConsentTerm;
import com.elsehere.auth.service.ConvertingGuest;
import java.util.List;

/**
 * AUTH-03 가입 정보 입력 화면에 필요한 정보.
 *
 * @param guest 게스트가 가입하는 중이면 게스트 때 고른 캐릭터와 음성 처리 동의. 아니면 {@code null}이라 응답에 담지 않는다.
 */
public record SignupFormResponse(String email, List<ConsentTerm> consents, ConvertingGuest guest) {}
