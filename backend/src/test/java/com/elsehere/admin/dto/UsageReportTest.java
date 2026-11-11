package com.elsehere.admin.dto;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class UsageReportTest {

  @Test
  @DisplayName("기간 안에 활동한 사용자와 대화가 없으면 비율과 평균은 0이다")
  void summary_withoutActivity_ratiosAreZero() {
    UsageReport.Summary summary = new UsageReport.Summary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    assertThat(summary.returningRate()).isZero();
    assertThat(summary.averageActiveDays()).isZero();
    assertThat(summary.averagePlayMinutes()).isZero();
    assertThat(summary.averageDailyActiveMinutes()).isZero();
    assertThat(summary.endedRate()).isZero();
    assertThat(summary.conversationsPerUser()).isZero();
  }

  @Test
  @DisplayName("재방문을 확인할 수 있는 사용자가 없으면 재방문율은 0이다")
  void retention_withoutEligibleUsers_ratesAreZero() {
    OverallUsageReport.Retention retention = new OverallUsageReport.Retention(3, 0, 0, 0, 0, 0);

    assertThat(retention.day1Rate()).isZero();
    assertThat(retention.day7Rate()).isZero();
    assertThat(retention.week1Rate()).isZero();
  }

  @Test
  @DisplayName("재방문율은 확인할 수 있는 사용자 가운데 다시 온 사용자 비율이다. 7일 안 재방문은 D7 대상으로 나눈다")
  void retention_ratesDivideByEligibleUsers() {
    OverallUsageReport.Retention retention = new OverallUsageReport.Retention(12, 10, 4, 8, 2, 6);

    assertThat(retention.day1Rate()).isEqualTo(0.4);
    assertThat(retention.day7Rate()).isEqualTo(0.25);
    assertThat(retention.week1Rate()).isEqualTo(0.75);
  }
}
