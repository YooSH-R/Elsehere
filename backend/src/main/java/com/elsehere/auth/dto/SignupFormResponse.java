package com.elsehere.auth.dto;

import com.elsehere.auth.service.ConsentTerm;
import java.util.List;

/** AUTH-03 가입 정보 입력 화면에 필요한 정보. */
public record SignupFormResponse(String email, List<ConsentTerm> consents) {}
