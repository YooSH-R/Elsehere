package com.elsehere.conversation.dto;

import com.elsehere.conversation.entity.InputType;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import java.util.UUID;

/**
 * CONV-03 발화 전송 요청.
 *
 * @param text 사용자가 보낸 최종 문장. 음성이면 전사 결과를 그대로 보낸다
 * @param sttRawText 음성 입력의 전사 원본. 텍스트 입력이면 null
 */
public record TurnRequest(
    @NotNull UUID requestId,
    @NotNull InputType inputType,
    @NotBlank @Size(max = 500) String text,
    @Size(max = 500) String sttRawText) {}
