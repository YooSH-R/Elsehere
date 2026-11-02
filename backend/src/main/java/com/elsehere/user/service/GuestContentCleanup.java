package com.elsehere.user.service;

import com.elsehere.user.service.UserContentEraser.ErasedFiles;
import java.sql.Timestamp;
import java.time.Clock;
import java.time.Duration;
import java.util.List;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 게스트(AUTH-07) 내용 정리. 게스트는 세션이 끝나면 다시 들어올 수 없으므로, 마지막 활동 뒤 보관 기간이 지나면 대화 원문 같은 내용만 지우고 사용자·대화·AI 호출
 * 기록 같은 뼈대는 남긴다. 지우는 범위는 {@link UserContentEraser}가 정한다.
 */
@Slf4j
@Service
public class GuestContentCleanup {

  // 마지막 활동은 게스트가 남긴 행 중 가장 늦은 시각이다. 이미 지운 게스트는 내용이 없어 다시 고르지 않는다.
  private static final String EXPIRED_GUESTS =
      """
      select u.id from users u
      where u.provider = 'GUEST'
        and greatest(
              u.updated_at,
              (select max(c.updated_at) from conversations c where c.user_id = u.id),
              (select max(t.created_at) from turns t
                 join conversations c on c.id = t.conversation_id where c.user_id = u.id),
              (select max(r.updated_at) from user_npc_relations r where r.user_id = u.id),
              (select max(v.updated_at) from user_vocab v where v.user_id = u.id),
              (select max(p.updated_at) from user_word_prefs p where p.user_id = u.id),
              (select max(l.updated_at) from level_tests l where l.user_id = u.id)
            ) < ?
        and (exists (select 1 from turns t
                       join conversations c on c.id = t.conversation_id where c.user_id = u.id)
             or exists (select 1 from user_npc_relations r where r.user_id = u.id)
             or exists (select 1 from user_vocab v where v.user_id = u.id)
             or exists (select 1 from user_word_prefs p where p.user_id = u.id)
             or exists (select 1 from level_tests l where l.user_id = u.id))
      """;

  private final JdbcTemplate jdbcTemplate;
  private final TransactionTemplate transactionTemplate;
  private final UserContentEraser userContentEraser;
  private final Clock clock;
  private final Duration retention;

  public GuestContentCleanup(
      JdbcTemplate jdbcTemplate,
      TransactionTemplate transactionTemplate,
      UserContentEraser userContentEraser,
      Clock clock,
      @Value("${elsehere.guest.content-retention}") Duration retention) {
    this.jdbcTemplate = jdbcTemplate;
    this.transactionTemplate = transactionTemplate;
    this.userContentEraser = userContentEraser;
    this.clock = clock;
    this.retention = retention;
  }

  /** 마지막 활동 뒤 보관 기간이 지난 게스트의 내용을 지운다. 게스트마다 따로 지워, 한 명이 실패해도 다음 정리에서 다시 고른다. */
  @Scheduled(cron = "${elsehere.guest.cleanup-cron}", zone = "Asia/Seoul")
  public void cleanUp() {
    Timestamp cutoff = Timestamp.from(clock.instant().minus(retention));
    List<Long> guests = jdbcTemplate.queryForList(EXPIRED_GUESTS, Long.class, cutoff);
    for (Long guestId : guests) {
      ErasedFiles files =
          transactionTemplate.execute(status -> userContentEraser.deleteContent(guestId));
      userContentEraser.deleteFiles(files);
    }
    if (!guests.isEmpty()) {
      log.info("게스트 내용을 정리했습니다 guests={}", guests.size());
    }
  }
}
