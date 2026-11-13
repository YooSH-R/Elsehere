package com.elsehere.admin.service;

import com.elsehere.admin.dto.AdminUserName;
import com.elsehere.admin.dto.OverallUsageReport;
import com.elsehere.admin.dto.OverallUsageReport.UserCount;
import com.elsehere.admin.dto.UsageReport;
import com.elsehere.admin.dto.UserUsageReport;
import com.elsehere.admin.repository.AdminUsageRepository;
import com.elsehere.admin.repository.AdminUsageRepository.ProfileRow;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.entity.TownStoryStage;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.LocalDate;
import java.time.temporal.ChronoUnit;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 어드민 이용 현황 화면. 일별 접속 기록과 DB에 남은 활동 흔적으로 최근 며칠의 사용량, 재방문, 진행 단계를 묶는다. */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AdminUsageService {

  private static final int MAX_DAYS = 90;
  private static final int USER_LIST_LIMIT = 100;
  private static final String NOT_STARTED_LABEL = "시작 전";
  private static final String FINISHED_LABEL = "모두 마침";
  // long 범위를 넘지 않는 사용자 ID
  private static final Pattern USER_ID = Pattern.compile("\\d{1,18}");

  private static final List<String> FIRST_DAY_STAGE_NAMES =
      Arrays.stream(FirstDayStage.values()).map(FirstDayStage::title).toList();
  private static final List<String> TOWN_STORY_STAGE_NAMES =
      Arrays.stream(TownStoryStage.values()).map(TownStoryStage::name).toList();

  private final AdminUsageRepository adminUsageRepository;
  private final UserRepository userRepository;
  private final Clock clock;

  /** 최근 {@code days}일(1~90) 동안 모든 사용자의 이용 현황 */
  public OverallUsageReport report(int days) {
    Instant since = periodStart(days);
    return new OverallUsageReport(
        usage(days, since, null),
        new OverallUsageReport.Progress(
            onboardingSteps(adminUsageRepository.newUsersByOnboardingStep(since)),
            stageCounts(
                adminUsageRepository.activeUsersByFirstDayStage(since), FIRST_DAY_STAGE_NAMES),
            stageCounts(
                adminUsageRepository.activeUsersByTownStoryStage(since), TOWN_STORY_STAGE_NAMES)),
        adminUsageRepository.retention(
            since, LocalDate.ofInstant(clock.instant(), ClockConfig.SERVICE_ZONE)),
        adminUsageRepository.activeUsers(since, USER_LIST_LIMIT));
  }

  /** 최근 {@code days}일(1~90) 동안 사용자 한 명의 이용 현황 */
  public UserUsageReport userReport(long userId, int days) {
    Instant since = periodStart(days);
    ProfileRow profile =
        adminUsageRepository
            .profile(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    return new UserUsageReport(profile(profile), usage(days, since, userId));
  }

  /**
   * 가입 이메일(대소문자 무시)이나 사용자 ID로 사용자를 찾는다. 숫자만 있으면 사용자 ID로 보고, 그 사용자가 있는지는 사용자별 화면이 확인한다. 이메일은 Google
   * 가입자에게만 있다.
   */
  public long findUserId(String user) {
    String query = user.trim();
    if (USER_ID.matcher(query).matches()) {
      return Long.parseLong(query);
    }
    return userRepository
        .findByProviderAndEmailIgnoreCase(AuthProvider.GOOGLE, query)
        .map(User::getId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  private Instant periodStart(int days) {
    if (days < 1 || days > MAX_DAYS) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    return clock.instant().minus(days, ChronoUnit.DAYS);
  }

  private static UserUsageReport.Profile profile(ProfileRow row) {
    return new UserUsageReport.Profile(
        row.userId(),
        row.guest(),
        AdminUserName.of(row.userId(), row.guest(), row.withdrawn(), row.email()),
        row.withdrawn(),
        row.startedAt(),
        row.convertedAt(),
        row.onboardingStep(),
        stageLabel(row.firstDayStage(), FIRST_DAY_STAGE_NAMES),
        stageLabel(row.townStoryStage(), TOWN_STORY_STAGE_NAMES),
        row.playSeconds(),
        row.lastBeatAt());
  }

  // userId가 null이면 모든 사용자를 센다.
  private UsageReport usage(int days, Instant since, Long userId) {
    return new UsageReport(
        days,
        adminUsageRepository.summary(since, userId),
        adminUsageRepository.byDay(since, userId),
        adminUsageRepository.conversationEnds(since, userId),
        adminUsageRepository.conversationsByNpc(since, userId),
        adminUsageRepository.conversationActions(since, userId),
        adminUsageRepository.wordSaves(since, userId),
        adminUsageRepository.feedbacks(since, userId));
  }

  private static List<UserCount> onboardingSteps(Map<String, Long> usersByStep) {
    return Arrays.stream(OnboardingStep.values())
        .map(step -> new UserCount(step.name(), usersByStep.getOrDefault(step.name(), 0L)))
        .toList();
  }

  // 시작 전, 각 단계, 모두 마침(단계 수와 같은 값) 순서로 모든 단계를 넣는다.
  private static List<UserCount> stageCounts(
      Map<Integer, Long> usersByStage, List<String> stageNames) {
    List<UserCount> counts = new ArrayList<>();
    for (int stage = AdminUsageRepository.NOT_STARTED; stage <= stageNames.size(); stage++) {
      counts.add(
          new UserCount(stageLabel(stage, stageNames), usersByStage.getOrDefault(stage, 0L)));
    }
    return counts;
  }

  // 단계 테이블에 행이 없으면(null) 시작 전이다.
  private static String stageLabel(Integer stage, List<String> stageNames) {
    if (stage == null || stage == AdminUsageRepository.NOT_STARTED) {
      return NOT_STARTED_LABEL;
    }
    if (stage >= stageNames.size()) {
      return FINISHED_LABEL;
    }
    return (stage + 1) + ". " + stageNames.get(stage);
  }
}
