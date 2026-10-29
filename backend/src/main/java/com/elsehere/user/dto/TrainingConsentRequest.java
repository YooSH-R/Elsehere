package com.elsehere.user.dto;

import com.elsehere.user.entity.ConsentType;
import jakarta.validation.constraints.NotNull;

/**
 * USER-03 학습 활용 동의 변경 요청.
 *
 * @param consentType TRAINING_TEXT 또는 TRAINING_VOICE. 모르는 값은 본문을 읽을 때 거절한다.
 * @param agreed 켜면 true, 끄면(철회) false
 */
public record TrainingConsentRequest(@NotNull ConsentType consentType, @NotNull Boolean agreed) {}
