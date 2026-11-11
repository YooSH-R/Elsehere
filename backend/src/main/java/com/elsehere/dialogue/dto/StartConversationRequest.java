package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.InitiatedBy;
import jakarta.validation.Valid;
import jakarta.validation.constraints.DecimalMax;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import java.util.Optional;
import java.util.UUID;
import java.util.stream.Collectors;
import java.util.stream.Stream;

/**
 * CONV-01 대화 시작 요청.
 *
 * @param npcOffDuty NPC가 근무 시간이 아닌지(교대 밖). 없거나 null이면 근무 중으로 보아, 이 칸을 보내지 않는 클라이언트는 지금처럼 응대 상황으로
 *     대화한다
 * @param npcTravelContext Unity가 관찰한 NPC의 이동 상태. 이전 클라이언트는 보내지 않는다
 * @param placeCode 대화가 열린 장소 코드(플레이어가 있는 곳). 없거나 모르는 코드면 위치 사실을 넣지 않는다
 */
public record StartConversationRequest(
    @NotNull UUID requestId,
    @NotBlank String npcCode,
    @NotNull InitiatedBy initiatedBy,
    Boolean npcOffDuty,
    @Valid NpcTravelContext npcTravelContext,
    @DecimalMin(NpcSpeechSpeed.MIN) @DecimalMax(NpcSpeechSpeed.MAX) Double speechSpeed,
    @Pattern(regexp = "[A-Z][A-Z_]{0,31}") String placeCode) {

  public StartConversationRequest(
      UUID requestId,
      String npcCode,
      InitiatedBy initiatedBy,
      Boolean npcOffDuty,
      NpcTravelContext npcTravelContext) {
    this(requestId, npcCode, initiatedBy, npcOffDuty, npcTravelContext, null, null);
  }

  /** 대화가 열린 장소를 보내지 않는 시작(복습·장소 연습). */
  public StartConversationRequest(
      UUID requestId,
      String npcCode,
      InitiatedBy initiatedBy,
      Boolean npcOffDuty,
      NpcTravelContext npcTravelContext,
      Double speechSpeed) {
    this(requestId, npcCode, initiatedBy, npcOffDuty, npcTravelContext, speechSpeed, null);
  }

  public StartConversationRequest(
      UUID requestId, String npcCode, InitiatedBy initiatedBy, Boolean npcOffDuty) {
    this(requestId, npcCode, initiatedBy, npcOffDuty, null, null, null);
  }

  public StartConversationRequest {
    npcOffDuty = Boolean.TRUE.equals(npcOffDuty);
    speechSpeed = NpcSpeechSpeed.orDefault(speechSpeed);
  }

  /** 근무 중인 NPC와의 대화. */
  public StartConversationRequest(UUID requestId, String npcCode, InitiatedBy initiatedBy) {
    this(requestId, npcCode, initiatedBy, false);
  }

  /** 대화 동안 워커에 덧붙일 NPC의 시작 상황(이동 사실과 지금 있는 곳). 둘 다 없으면 null. */
  public String npcSituation() {
    String situation =
        Stream.of(
                Optional.ofNullable(npcTravelContext).map(NpcTravelContext::describe),
                ConversationPlace.describe(placeCode))
            .flatMap(Optional::stream)
            .collect(Collectors.joining(" "));
    return situation.isEmpty() ? null : situation;
  }
}
