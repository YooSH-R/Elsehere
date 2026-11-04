package com.elsehere.user.dto;

import com.elsehere.user.entity.ConsentType;

/**
 * 학습 활용 동의 하나의 현재 상태(USER-02·03).
 *
 * @param agreed 지금 문구(version)에 동의했고 철회하지 않았으면 true
 */
public record ConsentResponse(
    ConsentType consentType, String version, String title, String url, boolean agreed) {}
