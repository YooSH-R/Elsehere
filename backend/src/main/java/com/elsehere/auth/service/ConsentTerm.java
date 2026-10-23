package com.elsehere.auth.service;

import com.elsehere.user.entity.ConsentType;

/**
 * 현재 받는 약관 한 가지.
 *
 * @param url 약관 문서 주소. 문서가 없는 항목(만 14세 확인)은 {@code null}이다.
 */
public record ConsentTerm(
    ConsentType consentType, String version, boolean required, String title, String url) {}
