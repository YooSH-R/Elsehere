package com.elsehere.admin.service;

import com.elsehere.admin.dto.AiUsageReport;
import com.elsehere.admin.dto.AiUsageRow;
import com.elsehere.admin.dto.AiUserUsage;
import com.elsehere.ai.repository.AiRequestRepository;
import com.elsehere.ai.repository.AiRequestRepository.AiUsageTotals;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.time.Clock;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 어드민 AI 사용량 화면. AI 호출 기록(ai_requests)을 최근 며칠 동안 날짜·종류·모델·사용자 구분별로 묶는다. */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AdminAiUsageService {

  private static final int MAX_DAYS = 90;
  private static final int TOP_USERS = 10;
  private static final String TOTAL_LABEL = "전체";
  // 모델을 알리지 않은 호출(워커 오류, 음성 합성)
  private static final String NO_MODEL_LABEL = "없음";

  private final AiRequestRepository aiRequestRepository;
  private final Clock clock;

  /** 최근 {@code days}일(1~90) 동안의 AI 사용량 */
  public AiUsageReport report(int days) {
    if (days < 1 || days > MAX_DAYS) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    Instant since = clock.instant().minus(days, ChronoUnit.DAYS);
    return new AiUsageReport(
        days,
        row(TOTAL_LABEL, aiRequestRepository.usageTotal(since)),
        rows(aiRequestRepository.usageByDay(since), null),
        rows(aiRequestRepository.usageByKind(since), null),
        rows(aiRequestRepository.usageByModel(since), NO_MODEL_LABEL),
        rows(aiRequestRepository.usageByUserType(since), null),
        aiRequestRepository.usageByUser(since, TOP_USERS).stream()
            .map(
                user ->
                    new AiUserUsage(
                        user.getUserId(),
                        "GUEST".equals(user.getProvider()),
                        user.getCalls(),
                        user.getFailed(),
                        user.getInputTokens(),
                        user.getOutputTokens()))
            .toList());
  }

  private static List<AiUsageRow> rows(List<AiUsageTotals> totals, String labelWhenNull) {
    return totals.stream()
        .map(total -> row(total.getLabel() == null ? labelWhenNull : total.getLabel(), total))
        .toList();
  }

  private static AiUsageRow row(String label, AiUsageTotals total) {
    return new AiUsageRow(
        label,
        total.getUsers(),
        total.getCalls(),
        total.getFailed(),
        total.getInputTokens(),
        total.getOutputTokens(),
        total.getAudioMs());
  }
}
