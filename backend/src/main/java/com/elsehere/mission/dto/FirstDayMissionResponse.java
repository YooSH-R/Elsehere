package com.elsehere.mission.dto;

import java.util.List;

public record FirstDayMissionResponse(
    boolean started,
    int stage,
    String quest,
    boolean completed,
    String scene,
    boolean canRest,
    boolean raining,
    String weather,
    int balance,
    String lastPurchase,
    String situation,
    String direction,
    List<Step> steps) {
  public record Step(
      int index, String title, String scene, String npcCode, String status, List<Goal> goals) {}

  public record Goal(String id, String title, boolean completed, String hint) {}
}
