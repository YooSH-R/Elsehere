package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;

/**
 * 대화 상태. 대화 시작(CONV-01)과 발화 전송(CONV-03) 뒤의 값이다.
 *
 * @param rudeCount 이 대화에서 무례로 센 말 수
 * @param rudeLimit 여기에 닿으면 NPC가 대화를 끝내는 무례 횟수
 * @param affinity 이 NPC의 지금 호감도(-100~100). 처음 만난 NPC는 0이다
 */
public record ConversationStateResponse(
    ConversationStatus status,
    ConversationEndReason endReason,
    int rudeCount,
    int rudeLimit,
    int affinity) {}
