package com.elsehere.world.service;

import java.util.Set;

/** 경찰서 창구에서 신고를 받는 경찰관 NPC(시드 world.sql). 신고 판정과 출동 결과 보고가 함께 쓴다. */
public final class PoliceOfficers {

  private static final Set<String> CODES =
      Set.of("POLICE_OFFICER", "POLICE_SERGEANT", "POLICE_PATROL_OFFICER");

  private PoliceOfficers() {}

  /** 이 NPC가 신고를 받는 경찰관인지 */
  public static boolean includes(String npcCode) {
    return CODES.contains(npcCode);
  }
}
