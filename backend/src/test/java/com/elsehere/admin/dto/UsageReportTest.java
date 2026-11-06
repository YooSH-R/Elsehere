package com.elsehere.admin.dto;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class UsageReportTest {

  @Test
  @DisplayName("기간 안에 활동한 사용자와 대화가 없으면 비율과 평균은 0이다")
  void summary_withoutActivity_ratiosAreZero() {
    UsageReport.Summary summary = new UsageReport.Summary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    assertThat(summary.returningRate()).isZero();
    assertThat(summary.averageActiveDays()).isZero();
    assertThat(summary.averagePlayMinutes()).isZero();
    assertThat(summary.endedRate()).isZero();
    assertThat(summary.conversationsPerUser()).isZero();
  }
}
