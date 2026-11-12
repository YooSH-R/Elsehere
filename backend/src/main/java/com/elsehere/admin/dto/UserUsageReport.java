package com.elsehere.admin.dto;

/** 어드민 이용 현황 사용자별 화면. 사용자 한 명의 개요와 활동만 묶는다. */
public record UserUsageReport(Profile profile, UsageReport usage) {

  /**
   * 사용자 개요. 닉네임은 담지 않는다. 시각은 한국 시각(yyyy-MM-dd HH:mm)이다.
   *
   * @param guest 지금 게스트 계정인지
   * @param name 화면에 보여 줄 이름({@link AdminUserName}). 가입자는 이메일이다
   * @param withdrawn 탈퇴했는지
   * @param startedAt 계정을 만든 시각(게스트로 시작했으면 게스트 시작 시각)
   * @param convertedAt 게스트에서 Google 가입으로 바꾼 시각. 바꾸지 않았으면 null
   * @param onboardingStep 초기 설정 단계(users.onboarding_step)
   * @param firstDayStage 첫날 미션 지금 단계("시작 전", "n. 단계 이름", "모두 마침")
   * @param townStoryStage 마을 여정 지금 단계("시작 전", "n. 단계 이름", "모두 마침")
   * @param playSeconds 누적 플레이 시간(초)
   * @param lastBeatAt 게임 시계 마지막 신호 시각. 없으면 null
   */
  public record Profile(
      Long userId,
      boolean guest,
      String name,
      boolean withdrawn,
      String startedAt,
      String convertedAt,
      String onboardingStep,
      String firstDayStage,
      String townStoryStage,
      long playSeconds,
      String lastBeatAt) {}
}
