package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import java.time.OffsetDateTime;

/**
 * 대화 상태. 대화 시작(CONV-01)과 발화 전송(CONV-03) 뒤의 값이다.
 *
 * @param rudeCount 이 대화에서 무례로 센 말 수
 * @param rudeLimit 여기에 닿으면 NPC가 대화를 끝내는 무례 횟수
 * @param affinity 이 NPC의 지금 호감도(-100~100). 처음 만난 NPC는 0이다
 * @param timeLimitAt 서버 기준 대화 5분 제한 시각. Unity 카운트다운의 기준이다
 * @param graceUntil 마지막 발화도 새로 받을 수 없는 서버 기준 시각
 * @param closing 5분이 지나 자연스러운 마무리를 기다리는 중인지
 * @param finalTurnsRemaining 마무리 구간에 새로 보낼 수 있는 사용자 발화 수
 */
public record ConversationStateResponse(
    ConversationStatus status,
    ConversationEndReason endReason,
    int rudeCount,
    int rudeLimit,
    int affinity,
    OffsetDateTime timeLimitAt,
    OffsetDateTime graceUntil,
    boolean closing,
    int finalTurnsRemaining) {}
