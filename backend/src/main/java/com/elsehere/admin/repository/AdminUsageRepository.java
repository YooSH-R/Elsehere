package com.elsehere.admin.repository;

import com.elsehere.admin.dto.AdminUserName;
import com.elsehere.admin.dto.OverallUsageReport;
import com.elsehere.admin.dto.UsageReport;
import com.elsehere.common.config.ClockConfig;
import java.sql.Timestamp;
import java.sql.Types;
import java.time.Instant;
import java.time.LocalDate;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.jdbc.core.DataClassRowMapper;
import org.springframework.jdbc.core.RowCallbackHandler;
import org.springframework.jdbc.core.namedparam.MapSqlParameterSource;
import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.stereotype.Repository;

/**
 * 어드민 이용 현황 집계. 로그인 기록 표는 없으므로 일별 접속 기록(user_daily_activities)과 DB에 남은 활동 흔적을 모아 센다. 모든 집계는 {@code
 * since} 뒤의 기록만 보고, {@code userId}가 있으면 그 사용자만 센다. 사용자를 고르지 않으면 팀 계정(users.team_account)은 뺀다. 날짜는
 * 한국 날짜로 묶는다.
 */
@Repository
@RequiredArgsConstructor
public class AdminUsageRepository {

  /** 첫날 미션·마을 여정을 아직 시작하지 않은 사용자의 단계 */
  public static final int NOT_STARTED = -1;

  /**
   * 게스트에서 Google 가입으로 바꾼 사용자와 그 시각. 가입 전환(AUTH-04)은 같은 사용자 행을 회원으로 바꾸고 따로 기록하지 않는다. 게스트는 약관(TERMS)에
   * 동의하지 않고, 바로 가입한 회원은 계정과 약관 동의가 같은 요청에서 생긴다. 그래서 첫 약관 동의가 계정 생성보다 1분 넘게 늦은 회원을 전환으로 본다.
   */
  private static final String CONVERSIONS =
      """
      conversions (user_id, converted_at) as (
        select u.id, min(c.agreed_at)
        from users u
        join user_consents c on c.user_id = u.id and c.consent_type = 'TERMS'
        where u.provider <> 'GUEST'
        group by u.id, u.created_at
        having min(c.agreed_at) > u.created_at + interval '1 minute'
      )
      """;

  /**
   * 활동 흔적 한 줄(사용자, 시각, 종류, 머문 초). 일별 접속 기록(VISIT)은 그날 마지막 접속 시각과 장소 화면에 머문 초를 담고, 다른 흔적의 머문 초는 0이다.
   * 일별 접속 기록이 생기기 전 기간은 게임 시계 마지막 신호(BEAT)만 남아 돌아다니기만 한 날을 세지 못한다.
   */
  private static final String FACTS =
      "with "
          + CONVERSIONS
          + """
      , facts (user_id, at, kind, stayed_seconds) as (
        select id, created_at, 'START', cast(0 as bigint) from users
        union all
        select user_id, converted_at, 'CONVERSION', 0 from conversions
        union all
        select user_id, started_at, 'CONVERSATION', 0 from conversations
        union all
        select user_id, created_at, 'AI', 0 from ai_requests
        where status <> 'RESERVED' and user_id is not null
        union all
        select user_id, saved_at, 'WORD', 0 from user_vocab
        union all
        select user_id, completed_at, 'MISSION', 0 from user_missions where status = 'DONE'
        union all
        select user_id, last_beat_at, 'BEAT', 0 from user_game_clocks where last_beat_at is not null
        union all
        select user_id, last_seen_at, 'VISIT', active_seconds from user_daily_activities
      )
      """;

  /**
   * 집계할 사용자 ID. 사용자를 고르면({@code userId}) 그 사용자만, 고르지 않으면 팀 기기에서 쓴 팀 계정을 뺀 모든 사용자다. 팀 계정도 골라 보면 센다.
   */
  private static final String COUNTED_USERS =
      """
      (select id from users
       where case when cast(:userId as bigint) is null then not team_account
                  else id = :userId end)""";

  /** 기간 안의 흔적만 period_facts로 남긴다. */
  private static final String PERIOD_FACTS =
      FACTS
          + """
      , period_facts as (
        select * from facts
        where at >= :since and user_id in %s
      )
      """
              .formatted(COUNTED_USERS);

  /** 기간 안에 시작한 대화(conversations c)만 고르는 조건 */
  private static final String CONVERSATION_IN_PERIOD =
      "c.started_at >= :since and c.user_id in " + COUNTED_USERS;

  private static final String KOREAN_DAY =
      "to_char(f.at at time zone '" + ClockConfig.SERVICE_ZONE_ID + "', 'YYYY-MM-DD')";

  private final NamedParameterJdbcTemplate jdbcTemplate;

  public UsageReport.Summary summary(Instant since, Long userId) {
    String sql =
        PERIOD_FACTS
            + """
            , user_days as (
              select f.user_id, count(distinct %s) as active_days
              from period_facts f
              group by f.user_id
            )
            select activity.*, events.*, conversation_totals.*
            from (
              select count(*) as active_users,
                     count(*) filter (where u.provider = 'GUEST') as active_guests,
                     count(*) filter (where u.provider <> 'GUEST') as active_members,
                     coalesce(sum(d.active_days), 0) as active_user_days,
                     count(*) filter (where d.active_days >= 2) as returning_users,
                     coalesce(sum(g.play_seconds), 0) as play_seconds
              from user_days d
              join users u on u.id = d.user_id
              left join user_game_clocks g on g.user_id = d.user_id
            ) activity
            cross join (
              select count(*) filter (where kind = 'START') as new_users,
                     count(*) filter (where kind = 'CONVERSION') as conversions,
                     count(*) filter (where kind = 'MISSION') as missions_completed,
                     count(*) filter (where kind = 'WORD') as words_saved,
                     coalesce(sum(stayed_seconds), 0) as active_seconds
              from period_facts
            ) events
            cross join (
              select count(*) as conversations,
                     count(*) filter (where c.status <> 'ACTIVE') as ended_conversations,
                     count(distinct c.user_id) as conversing_users
              from conversations c
              where %s
            ) conversation_totals
            """
                .formatted(KOREAN_DAY, CONVERSATION_IN_PERIOD);
    return jdbcTemplate.queryForObject(
        sql, params(since, userId), new DataClassRowMapper<>(UsageReport.Summary.class));
  }

  public List<UsageReport.Day> byDay(Instant since, Long userId) {
    String sql =
        PERIOD_FACTS
            + "select "
            + KOREAN_DAY
            + """
             as day,
              count(distinct f.user_id) as active_users,
              count(distinct f.user_id) filter (where u.provider = 'GUEST') as active_guests,
              count(distinct f.user_id) filter (where u.provider <> 'GUEST') as active_members,
              count(*) filter (where f.kind = 'START') as new_users,
              count(*) filter (where f.kind = 'CONVERSION') as conversions,
              count(*) filter (where f.kind = 'CONVERSATION') as conversations,
              count(*) filter (where f.kind = 'MISSION') as missions_completed,
              count(*) filter (where f.kind = 'WORD') as words_saved,
              coalesce(sum(f.stayed_seconds), 0) as active_seconds
            from period_facts f
            join users u on u.id = f.user_id
            group by 1
            order by 1
            """;
    return jdbcTemplate.query(
        sql, params(since, userId), new DataClassRowMapper<>(UsageReport.Day.class));
  }

  /** 끝난 방식은 대화를 시작한 날 기준이다. EXPIRED·QUOTA_EXHAUSTED는 아직 기록하는 곳이 없어, 나가 버린 대화는 ACTIVE로 남는다. */
  public List<UsageReport.Count> conversationEnds(Instant since, Long userId) {
    String sql =
        """
        select case when c.status = 'ACTIVE' then 'ACTIVE' else c.end_reason end as label,
               count(*) as count,
               count(distinct c.user_id) as users
        from conversations c
        where %s
        group by 1
        order by count desc, 1
        """
            .formatted(CONVERSATION_IN_PERIOD);
    return counts(sql, since, userId);
  }

  public List<UsageReport.Count> conversationsByNpc(Instant since, Long userId) {
    String sql =
        """
        select n.name || ' · ' || n.role_label as label,
               count(*) as count,
               count(distinct c.user_id) as users
        from conversations c
        join npcs n on n.id = c.npc_id
        where %s
        group by n.id, n.name, n.role_label
        order by count desc, 1
        """
            .formatted(CONVERSATION_IN_PERIOD);
    return counts(sql, since, userId);
  }

  /**
   * 학습자가 보낸 말은 턴(turns)에서, 힌트·추천 단어(HINT)·뜻 보기(MEANING)는 AI 호출 기록에서 센다. 게스트의 턴은 하루 뒤
   * 지워져(GuestContentCleanup) 음성·글 입력은 게스트 수가 적게 잡히고, AI 호출 기록은 남는다.
   */
  public List<UsageReport.Count> conversationActions(Instant since, Long userId) {
    String sql =
        """
        select t.input_type as label,
               count(*) as count,
               count(distinct c.user_id) as users
        from turns t
        join conversations c on c.id = t.conversation_id
        where t.speaker = 'USER' and t.input_type is not null
          and t.created_at >= :since
          and c.user_id in %1$s
        group by t.input_type
        union all
        select r.kind, count(*), count(distinct r.user_id)
        from ai_requests r
        where r.kind in ('HINT', 'MEANING') and r.status <> 'RESERVED'
          and r.created_at >= :since
          and r.user_id in %1$s
        group by r.kind
        order by count desc, label
        """
            .formatted(COUNTED_USERS);
    return counts(sql, since, userId);
  }

  /** 게스트의 저장 단어는 하루 뒤 지워져(GuestContentCleanup) 게스트 수가 적게 잡힌다. */
  public List<UsageReport.Count> wordSaves(Instant since, Long userId) {
    String sql =
        """
        select v.source_type as label,
               count(*) as count,
               count(distinct v.user_id) as users
        from user_vocab v
        where v.saved_at >= :since
          and v.user_id in %s
        group by v.source_type
        order by count desc, label
        """
            .formatted(COUNTED_USERS);
    return counts(sql, since, userId);
  }

  /**
   * 피드백은 만든 날 기준이다. 사용자가 건너뛰면 이유(error_code)가 비어 있다(ConversationFeedback.skip). 게스트의 피드백은 하루 뒤 지워져
   * 게스트 수가 적게 잡힌다.
   */
  public List<UsageReport.Count> feedbacks(Instant since, Long userId) {
    String sql =
        """
        select case when f.status = 'SKIPPED' and f.error_code is null then 'SKIPPED_BY_USER'
                    else f.status end as label,
               count(*) as count,
               count(distinct c.user_id) as users
        from conversation_feedback f
        join conversations c on c.id = f.conversation_id
        where f.created_at >= :since
          and c.user_id in %s
        group by 1
        order by count desc, label
        """
            .formatted(COUNTED_USERS);
    return counts(sql, since, userId);
  }

  /** 기간 안에 계정을 만든 사용자(팀 계정 빼고)의 초기 설정 단계(users.onboarding_step)별 사용자 수 */
  public Map<String, Long> newUsersByOnboardingStep(Instant since) {
    String sql =
        """
        select u.onboarding_step as label, count(*) as users
        from users u
        where u.created_at >= :since and not u.team_account
        group by u.onboarding_step
        """;
    Map<String, Long> usersByStep = new HashMap<>();
    jdbcTemplate.query(
        sql,
        params(since, null),
        (RowCallbackHandler) row -> usersByStep.put(row.getString("label"), row.getLong("users")));
    return usersByStep;
  }

  /** 활동 사용자의 첫날 미션 지금 단계(first_day_missions.stage)별 사용자 수. 시작하지 않았으면 {@link #NOT_STARTED} */
  public Map<Integer, Long> activeUsersByFirstDayStage(Instant since) {
    return activeUsersByStage("first_day_missions", since);
  }

  /** 활동 사용자의 마을 여정 지금 단계(town_stories.stage)별 사용자 수. 시작하지 않았으면 {@link #NOT_STARTED} */
  public Map<Integer, Long> activeUsersByTownStoryStage(Instant since) {
    return activeUsersByStage("town_stories", since);
  }

  // 단계 테이블 이름은 위 두 메서드가 정한 값만 들어온다.
  private Map<Integer, Long> activeUsersByStage(String stageTable, Instant since) {
    String sql =
        PERIOD_FACTS
            + """
            select coalesce(s.stage, %d) as stage, count(*) as users
            from (select distinct user_id from period_facts) active
            left join %s s on s.user_id = active.user_id
            group by 1
            """
                .formatted(NOT_STARTED, stageTable);
    Map<Integer, Long> usersByStage = new HashMap<>();
    jdbcTemplate.query(
        sql,
        params(since, null),
        (RowCallbackHandler) row -> usersByStage.put(row.getInt("stage"), row.getLong("users")));
    return usersByStage;
  }

  /** 기간 안에 활동한 사용자. 마지막 활동이 늦은 순서로 {@code limit}명 */
  public List<OverallUsageReport.UserActivity> activeUsers(Instant since, int limit) {
    String sql =
        PERIOD_FACTS
            + """
            select f.user_id,
                   u.provider = 'GUEST' as guest,
                   u.provider_uid like 'withdrawn-%%' as withdrawn,
                   u.email,
                   count(distinct %s) as active_days,
                   count(*) filter (where f.kind = 'CONVERSATION') as conversations,
                   count(*) filter (where f.kind = 'MISSION') as missions_completed,
                   coalesce(g.play_seconds, 0) as play_seconds,
                   to_char(max(f.at) at time zone '%s', 'YYYY-MM-DD HH24:MI') as last_active_at
            from period_facts f
            join users u on u.id = f.user_id
            left join user_game_clocks g on g.user_id = f.user_id
            group by f.user_id, u.provider, u.provider_uid, u.email, g.play_seconds
            order by max(f.at) desc, f.user_id
            limit :limit
            """
                .formatted(KOREAN_DAY, ClockConfig.SERVICE_ZONE_ID);
    return jdbcTemplate.query(
        sql,
        params(since, null).addValue("limit", limit),
        (row, rowNum) -> {
          long userId = row.getLong("user_id");
          boolean guest = row.getBoolean("guest");
          return new OverallUsageReport.UserActivity(
              userId,
              guest,
              AdminUserName.of(userId, guest, row.getBoolean("withdrawn"), row.getString("email")),
              row.getLong("active_days"),
              row.getLong("conversations"),
              row.getLong("missions_completed"),
              row.getLong("play_seconds"),
              row.getString("last_active_at"));
        });
  }

  /**
   * 기간 안에 계정을 만든 사용자(팀 계정 빼고)의 재방문. 계정을 만든 한국 날짜를 0일째로 세고, 활동은 기간과 관계없이 모든 흔적으로 본다. {@code
   * today}(오늘 한국 날짜)는 아직 끝나지 않아 확인할 날로 치지 않는다.
   */
  public OverallUsageReport.Retention retention(Instant since, LocalDate today) {
    String sql =
        FACTS
            + """
            , active_days as (
              select distinct user_id, cast(at at time zone '%1$s' as date) as day from facts
            ),
            started as (
              select id as user_id, cast(created_at at time zone '%1$s' as date) as start_day
              from users
              where created_at >= :since and not team_account
            ),
            returns as (
              select s.start_day,
                     exists (select 1 from active_days a
                             where a.user_id = s.user_id and a.day = s.start_day + 1) as day1,
                     exists (select 1 from active_days a
                             where a.user_id = s.user_id and a.day = s.start_day + 7) as day7,
                     exists (select 1 from active_days a
                             where a.user_id = s.user_id
                               and a.day between s.start_day + 1 and s.start_day + 7) as week1
              from started s
            )
            select count(*) as started_users,
                   count(*) filter (where start_day + 1 < :today) as day1_eligible,
                   count(*) filter (where start_day + 1 < :today and day1) as day1_returned,
                   count(*) filter (where start_day + 7 < :today) as day7_eligible,
                   count(*) filter (where start_day + 7 < :today and day7) as day7_returned,
                   count(*) filter (where start_day + 7 < :today and week1) as week1_returned
            from returns
            """
                .formatted(ClockConfig.SERVICE_ZONE_ID);
    return jdbcTemplate.queryForObject(
        sql,
        new MapSqlParameterSource()
            .addValue("since", Timestamp.from(since))
            .addValue("today", today, Types.DATE),
        new DataClassRowMapper<>(OverallUsageReport.Retention.class));
  }

  /** 사용자 한 명의 개요. 없는 사용자면 비어 있다. */
  public Optional<ProfileRow> profile(long userId) {
    String sql =
        "with "
            + CONVERSIONS
            + """
            select u.id as user_id,
                   u.provider = 'GUEST' as guest,
                   u.provider_uid like 'withdrawn-%%' as withdrawn,
                   u.email,
                   to_char(u.created_at at time zone '%1$s', 'YYYY-MM-DD HH24:MI') as started_at,
                   to_char(cv.converted_at at time zone '%1$s', 'YYYY-MM-DD HH24:MI')
                     as converted_at,
                   u.onboarding_step,
                   fd.stage as first_day_stage,
                   ts.stage as town_story_stage,
                   coalesce(g.play_seconds, 0) as play_seconds,
                   to_char(g.last_beat_at at time zone '%1$s', 'YYYY-MM-DD HH24:MI') as last_beat_at
            from users u
            left join conversions cv on cv.user_id = u.id
            left join first_day_missions fd on fd.user_id = u.id
            left join town_stories ts on ts.user_id = u.id
            left join user_game_clocks g on g.user_id = u.id
            where u.id = :userId
            """
                .formatted(ClockConfig.SERVICE_ZONE_ID);
    return jdbcTemplate
        .query(
            sql,
            new MapSqlParameterSource("userId", userId),
            new DataClassRowMapper<>(ProfileRow.class))
        .stream()
        .findFirst();
  }

  /**
   * 사용자 개요 한 줄. 시각은 한국 시각(yyyy-MM-dd HH:mm) 문자열이다.
   *
   * @param withdrawn 탈퇴하면 provider_uid가 withdrawn-으로 시작한다(User.withdraw)
   * @param email 가입 이메일. 게스트와 탈퇴한 사용자는 null
   * @param firstDayStage 첫날 미션 단계. 시작하지 않았으면 null
   * @param townStoryStage 마을 여정 단계. 시작하지 않았으면 null
   */
  public record ProfileRow(
      Long userId,
      boolean guest,
      boolean withdrawn,
      String email,
      String startedAt,
      String convertedAt,
      String onboardingStep,
      Integer firstDayStage,
      Integer townStoryStage,
      long playSeconds,
      String lastBeatAt) {}

  private List<UsageReport.Count> counts(String sql, Instant since, Long userId) {
    return jdbcTemplate.query(
        sql, params(since, userId), new DataClassRowMapper<>(UsageReport.Count.class));
  }

  private static MapSqlParameterSource params(Instant since, Long userId) {
    return new MapSqlParameterSource()
        .addValue("since", Timestamp.from(since))
        .addValue("userId", userId, Types.BIGINT);
  }
}
