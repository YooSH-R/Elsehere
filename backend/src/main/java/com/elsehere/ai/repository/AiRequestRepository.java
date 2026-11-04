package com.elsehere.ai.repository;

import com.elsehere.ai.entity.AiRequest;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.common.config.ClockConfig;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;

/**
 * AI 호출 기록. 사용량 집계(어드민 AI 사용량)는 기간 안의 기록을 기준 하나로 묶는다. 예약만 하고 끝내지 않은 기록(RESERVED)은 세지 않고, 날짜는 한국 날짜로
 * 묶는다.
 */
public interface AiRequestRepository extends JpaRepository<AiRequest, Long> {

  String TOTALS =
      """
      count(distinct r.user_id) as "users",
      count(*) as "calls",
      count(*) filter (where r.status = 'FAILED') as "failed",
      coalesce(sum(r.input_tokens), 0) as "inputTokens",
      coalesce(sum(r.output_tokens), 0) as "outputTokens",
      coalesce(sum(r.audio_ms), 0) as "audioMs"
      from ai_requests r
      """;

  String IN_PERIOD = " where r.created_at >= :since and r.status <> 'RESERVED' ";

  Optional<AiRequest> findByRequestIdAndKind(UUID requestId, AiRequestKind kind);

  @Query(value = "select null as \"label\", " + TOTALS + IN_PERIOD, nativeQuery = true)
  AiUsageTotals usageTotal(Instant since);

  @Query(
      value =
          "select to_char(r.created_at at time zone '"
              + ClockConfig.SERVICE_ZONE_ID
              + "', 'YYYY-MM-DD') as \"label\", "
              + TOTALS
              + IN_PERIOD
              + "group by 1 order by 1",
      nativeQuery = true)
  List<AiUsageTotals> usageByDay(Instant since);

  @Query(
      value =
          "select r.kind as \"label\", "
              + TOTALS
              + IN_PERIOD
              + "group by r.kind order by \"calls\" desc, 1",
      nativeQuery = true)
  List<AiUsageTotals> usageByKind(Instant since);

  /** 모델을 알리지 않은 호출(워커 오류, 음성 합성)은 label이 null이다. */
  @Query(
      value =
          "select r.model as \"label\", "
              + TOTALS
              + IN_PERIOD
              + "group by r.model order by \"calls\" desc, 1 nulls last",
      nativeQuery = true)
  List<AiUsageTotals> usageByModel(Instant since);

  /** label은 게스트면 GUEST, 가입자면 MEMBER다. */
  @Query(
      value =
          "select case when u.provider = 'GUEST' then 'GUEST' else 'MEMBER' end as \"label\", "
              + TOTALS
              + "join users u on u.id = r.user_id"
              + IN_PERIOD
              + "group by 1 order by 1",
      nativeQuery = true)
  List<AiUsageTotals> usageByUserType(Instant since);

  /** 입력·출력 토큰 합이 많은 사용자부터 {@code limit}명 */
  @Query(
      value =
          """
          select r.user_id as "userId",
                 u.provider as "provider",
                 count(*) as "calls",
                 count(*) filter (where r.status = 'FAILED') as "failed",
                 coalesce(sum(r.input_tokens), 0) as "inputTokens",
                 coalesce(sum(r.output_tokens), 0) as "outputTokens"
          from ai_requests r
          join users u on u.id = r.user_id
          where r.created_at >= :since and r.status <> 'RESERVED'
          group by r.user_id, u.provider
          order by coalesce(sum(r.input_tokens), 0) + coalesce(sum(r.output_tokens), 0) desc,
                   r.user_id
          limit :limit
          """,
      nativeQuery = true)
  List<AiUserTotals> usageByUser(Instant since, int limit);

  /** 기준 하나로 묶은 사용량 합계 */
  interface AiUsageTotals {
    String getLabel();

    long getUsers();

    long getCalls();

    long getFailed();

    long getInputTokens();

    long getOutputTokens();

    long getAudioMs();
  }

  /** 사용자 한 명의 사용량 합계 */
  interface AiUserTotals {
    Long getUserId();

    String getProvider();

    long getCalls();

    long getFailed();

    long getInputTokens();

    long getOutputTokens();
  }
}
