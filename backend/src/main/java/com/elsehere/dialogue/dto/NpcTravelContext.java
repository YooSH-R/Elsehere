package com.elsehere.dialogue.dto;

import jakarta.validation.constraints.DecimalMax;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;

/** 클라이언트의 실제 보행 상태. 자유 문장 대신 제한된 상태와 수치만 받는다. */
public record NpcTravelContext(
    @NotNull Phase phase,
    @DecimalMin("0") @DecimalMax("2000") double routeMeters,
    @DecimalMin("0") @DecimalMax("10000") double walkedMeters,
    @Min(0) @Max(1440) int departureLeadMinutes,
    @DecimalMin("0") @DecimalMax("1440") double lateMinutes) {
  public enum Phase {
    GOING_TO_WORK,
    AT_WORK,
    GOING_HOME,
    LEISURE,
    BREAK,
    RESTING,
    WALKING
  }

  public String describe() {
    String activity =
        switch (phase) {
          case GOING_TO_WORK -> "travelling to work";
          case AT_WORK -> "arrived at work after the commute";
          case GOING_HOME -> "going home";
          case LEISURE -> "spending free time visiting a facility or walking";
          case BREAK -> "taking a break";
          case RESTING -> "resting";
          case WALKING -> "out for a walk";
        };
    String delay =
        phase == Phase.GOING_TO_WORK && lateMinutes > 0
            ? " Currently late for work by %.1f game minutes.".formatted(lateMinutes)
            : "";
    return "Movement facts at the start of this conversation: currently %s. Planned route %.1f metres; actually walked %.1f metres on this trip. Planned departure allowance %d game minutes.%s After a long walk, you can feel tired; a long commute means needing to leave early. Mention these facts naturally when relevant, without repeating them every turn. Do not invent exhaustion, injuries or a delay not stated here. Free time or going home does not mean refusing conversation."
        .formatted(activity, routeMeters, walkedMeters, departureLeadMinutes, delay);
  }
}
