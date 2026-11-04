package com.elsehere.followup.dto;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import java.time.OffsetDateTime;

/**
 * NPC가 곧 건네기로 한 것(CONV-08 followUp, CONV-13 items).
 *
 * @param item 대화에 나온 영어 이름
 * @param itemKo 화면에 보일 한국어 이름
 * @param remainingSeconds 준비까지 남은 초. 서버 시각으로 세며 준비됐으면 0이다. 클라이언트는 받은 때부터 이 값을 줄여 보인다
 * @param ready 준비됐는지. 준비된 뒤 그 NPC와 대화를 시작하면 첫마디로 건넨다
 */
public record FollowUpResponse(
    Long followUpId,
    NpcSummaryResponse npc,
    String sceneCode,
    String item,
    String itemKo,
    OffsetDateTime readyAt,
    long remainingSeconds,
    boolean ready) {}
