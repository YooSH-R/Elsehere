package com.elsehere.dialogue.dto;

import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.world.service.ConversationPlace;
import jakarta.validation.Valid;
import jakarta.validation.constraints.DecimalMax;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import java.util.Collections;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.TreeMap;
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
 * @param needs 대화를 시작한 순간 NPC의 생활 욕구(HUNGER·ENERGY·SOCIAL·FUN·WORK, 0~100). 대화에 저장해 매 턴 워커에 보내고 돌발
 *     부탁을 고르는 데 쓴다. 모르는 키는 버리고 0~100으로 자른다. 생활하지 않는 NPC이거나 이전 클라이언트면 null
 */
public record StartConversationRequest(
    @NotNull UUID requestId,
    @NotBlank String npcCode,
    @NotNull InitiatedBy initiatedBy,
    Boolean npcOffDuty,
    @Valid NpcTravelContext npcTravelContext,
    @DecimalMin(NpcSpeechSpeed.MIN) @DecimalMax(NpcSpeechSpeed.MAX) Double speechSpeed,
    @Pattern(regexp = "[A-Z][A-Z_]{0,31}") String placeCode,
    Map<String, Double> needs) {

  /** 공통 규칙(ai/npcs/rules/world-rules.json)의 욕구 키. */
  public static final Set<String> NEED_KEYS = Set.of("HUNGER", "ENERGY", "SOCIAL", "FUN", "WORK");

  public StartConversationRequest(
      UUID requestId,
      String npcCode,
      InitiatedBy initiatedBy,
      Boolean npcOffDuty,
      NpcTravelContext npcTravelContext,
      Double speechSpeed,
      String placeCode) {
    this(
        requestId,
        npcCode,
        initiatedBy,
        npcOffDuty,
        npcTravelContext,
        speechSpeed,
        placeCode,
        null);
  }

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
    needs = knownNeeds(needs);
  }

  private static Map<String, Double> knownNeeds(Map<String, Double> needs) {
    if (needs == null) {
      return null;
    }
    Map<String, Double> known = new TreeMap<>();
    needs.forEach(
        (key, value) -> {
          if (NEED_KEYS.contains(key) && value != null) {
            known.put(key, Math.clamp(value, 0.0, 100.0));
          }
        });
    return Collections.unmodifiableMap(known);
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
