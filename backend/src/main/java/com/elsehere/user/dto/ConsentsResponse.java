package com.elsehere.user.dto;

import java.util.List;

/** 학습 활용 동의 목록(USER-02). 대화 기록, 녹음 음성 순서다. */
public record ConsentsResponse(List<ConsentResponse> consents) {}
