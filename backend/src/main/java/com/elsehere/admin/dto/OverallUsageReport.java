package com.elsehere.admin.dto;

import java.util.List;

/**
 * 어드민 이용 현황 전체 화면. 모든 사용자의 활동을 묶는다.
 *
 * @param users 기간 안에 활동한 사용자. 마지막 활동이 늦은 순서로 최대 100명
 */
public record OverallUsageReport(
    UsageReport usage, Progress progress, Retention retention, List<UserActivity> users) {

  /**
   * 기간 안에 계정을 만든 사용자(게스트 시작·바로 가입)가 그 뒤 다시 왔는지. 계정을 만든 한국 날짜를 0일째로 세고, 확인할 날이 다 지난 사용자만 대상으로
   * 삼는다(오늘은 아직 끝나지 않아 빼고 센다). 활동은 활동 사용자와 같은 흔적(일별 접속 기록 포함)으로 본다.
   *
   * @param startedUsers 기간 안에 계정을 만든 사용자
   * @param day1Eligible 1일째(다음 날)가 다 지나 확인할 수 있는 사용자
   * @param day1Returned 그 가운데 1일째에 활동한 사용자
   * @param day7Eligible 7일째가 다 지나 확인할 수 있는 사용자
   * @param day7Returned 그 가운데 7일째에 활동한 사용자
   * @param week1Returned 7일째 확인 대상 가운데 1~7일째 중 하루라도 활동한 사용자
   */
  public record Retention(
      long startedUsers,
      long day1Eligible,
      long day1Returned,
      long day7Eligible,
      long day7Returned,
      long week1Returned) {

    /** D1 재방문율(0~1) */
    public double day1Rate() {
      return ratio(day1Returned, day1Eligible);
    }

    /** D7 재방문율(0~1) */
    public double day7Rate() {
      return ratio(day7Returned, day7Eligible);
    }

    /** 7일 안 재방문율(0~1). D7과 같은 사용자(7일째가 다 지난 사용자)로 나눈다. */
    public double week1Rate() {
      return ratio(week1Returned, day7Eligible);
    }

    // 확인할 수 있는 사용자가 없으면 0이다.
    private static double ratio(long part, long whole) {
      return whole == 0 ? 0 : (double) part / whole;
    }
  }

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
   * 사용자 한 명의 기간 안 활동. 닉네임은 담지 않는다.
   *
   * @param guest 지금 게스트 계정인지
   * @param name 화면에 보여 줄 이름({@link AdminUserName}). 가입자는 이메일이다
   * @param activeDays 활동한 한국 날짜 수
   * @param playSeconds 누적 플레이 시간(초). 기간과 관계없이 지금까지 쌓인 값이다
   * @param lastActiveAt 기간 안 마지막 활동 흔적의 한국 시각(yyyy-MM-dd HH:mm)
   */
  public record UserActivity(
      Long userId,
      boolean guest,
      String name,
      long activeDays,
      long conversations,
      long missionsCompleted,
      long playSeconds,
      String lastActiveAt) {}
}
