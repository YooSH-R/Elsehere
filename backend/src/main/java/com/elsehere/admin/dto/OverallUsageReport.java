package com.elsehere.admin.dto;

import java.util.List;

/**
 * 어드민 이용 현황 전체 화면. 모든 사용자의 활동을 묶는다.
 *
 * @param users 기간 안에 활동한 사용자. 마지막 활동이 늦은 순서로 최대 100명
 */
public record OverallUsageReport(UsageReport usage, Progress progress, List<UserActivity> users) {

  /**
   * 사용자가 어디까지 진행했는지. 단계마다 사용자 수를 세며 아무도 없는 단계도 0명으로 넣는다.
   *
   * @param onboardingSteps 기간 안에 계정을 만든 사용자의 초기 설정 단계(LANG → LEVEL → TUTORIAL → DONE)
   * @param firstDayStages 활동 사용자의 첫날 미션 지금 단계. 시작 전, 단계별, 모두 마침 순서
   * @param townStoryStages 활동 사용자의 마을 여정 지금 단계. 시작 전, 단계별, 모두 마침 순서
   */
  public record Progress(
      List<UserCount> onboardingSteps,
      List<UserCount> firstDayStages,
      List<UserCount> townStoryStages) {}

  /** 기준 하나에 해당하는 사용자 수 */
  public record UserCount(String label, long users) {}

  /**
   * 사용자 한 명의 기간 안 활동. 이메일·닉네임은 담지 않는다.
   *
   * @param guest 지금 게스트 계정인지
   * @param activeDays 활동한 한국 날짜 수
   * @param playSeconds 누적 플레이 시간(초). 기간과 관계없이 지금까지 쌓인 값이다
   * @param lastActiveAt 기간 안 마지막 활동 흔적의 한국 시각(yyyy-MM-dd HH:mm)
   */
  public record UserActivity(
      Long userId,
      boolean guest,
      long activeDays,
      long conversations,
      long missionsCompleted,
      long playSeconds,
      String lastActiveAt) {}
}
