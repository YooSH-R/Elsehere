package com.elsehere.user.service;

import com.elsehere.conversation.service.NpcAudioRetention;
import com.elsehere.conversation.service.TrainingRecordingService;
import java.sql.Timestamp;
import java.time.Clock;
import java.time.Duration;
import java.util.List;
import java.util.Set;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 게스트(AUTH-07) 내용 정리. 게스트는 세션이 끝나면 다시 들어올 수 없으므로, 마지막 활동 뒤 보관 기간이 지나면 대화 원문 같은 내용만 지우고 사용자·대화·AI 호출
 * 기록 같은 뼈대는 남긴다. 뼈대를 남겨 두면 남은 행이 가리키는 사용자·대화가 사라지지 않고, 게스트 수·대화 수·사용량 집계도 그대로 할 수 있다.
 *
 * <p>여러 도메인의 테이블을 한 번에 지우므로 SQL로 처리한다. 사용자·대화를 가리키는 칸이 있는 테이블은 {@link #CLEARED_TABLES}와 {@link
 * #RETAINED_TABLES} 중 하나에 넣어 두어야 한다(새 테이블을 빠뜨리면 테스트가 실패한다).
 */
@Slf4j
@Service
public class GuestContentCleanup {

  /** 게스트가 남긴 내용이라 지우는 테이블 */
  static final Set<String> CLEARED_TABLES =
      Set.of(
          "turns",
          "conversation_feedback",
          "user_npc_relations",
          "user_vocab",
          "user_word_prefs",
          "level_tests",
          "training_recordings");

  /** 내용이 없어 남기는 테이블. 사용자·대화 행과 사용량·진행 상태만 담는다 */
  static final Set<String> RETAINED_TABLES =
      Set.of(
          "conversations",
          "ai_requests",
          "user_missions",
          "first_day_missions",
          "user_consents",
          "user_language_profiles");

  private static final String GUEST_CONVERSATIONS =
      "(select id from conversations where user_id = ?)";

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
  private final NpcAudioRetention npcAudioRetention;
  private final TrainingRecordingService trainingRecordingService;
  private final Clock clock;
  private final Duration retention;

  public GuestContentCleanup(
      JdbcTemplate jdbcTemplate,
      TransactionTemplate transactionTemplate,
      NpcAudioRetention npcAudioRetention,
      TrainingRecordingService trainingRecordingService,
      Clock clock,
      @Value("${elsehere.guest.content-retention}") Duration retention) {
    this.jdbcTemplate = jdbcTemplate;
    this.transactionTemplate = transactionTemplate;
    this.npcAudioRetention = npcAudioRetention;
    this.trainingRecordingService = trainingRecordingService;
    this.clock = clock;
    this.retention = retention;
  }

  /** 마지막 활동 뒤 보관 기간이 지난 게스트의 내용을 지운다. 게스트마다 따로 지워, 한 명이 실패해도 다음 정리에서 다시 고른다. */
  @Scheduled(cron = "${elsehere.guest.cleanup-cron}", zone = "Asia/Seoul")
  public void cleanUp() {
    Timestamp cutoff = Timestamp.from(clock.instant().minus(retention));
    List<Long> guests = jdbcTemplate.queryForList(EXPIRED_GUESTS, Long.class, cutoff);
    for (Long guestId : guests) {
      List<String> audioKeys = transactionTemplate.execute(status -> clearContent(guestId));
      // 파일은 DB를 지운 뒤 지운다. 실패하면 어떤 턴도 쓰지 않는 파일로 남아 NPC 음성 정리가 지운다.
      npcAudioRetention.deleteAudio(audioKeys);
    }
    if (!guests.isEmpty()) {
      log.info("게스트 내용을 정리했습니다 guests={}", guests.size());
    }
  }

  /**
   * @return 지운 턴이 쓰던 NPC 음성 파일 키
   */
  private List<String> clearContent(Long guestId) {
    List<String> audioKeys =
        jdbcTemplate.queryForList(
            "select npc_audio_key from turns where npc_audio_key is not null"
                + " and conversation_id in "
                + GUEST_CONVERSATIONS,
            String.class,
            guestId);
    jdbcTemplate.update(
        "delete from conversation_feedback where conversation_id in " + GUEST_CONVERSATIONS,
        guestId);
    jdbcTemplate.update(
        "delete from turns where conversation_id in " + GUEST_CONVERSATIONS, guestId);
    jdbcTemplate.update("delete from user_npc_relations where user_id = ?", guestId);
    jdbcTemplate.update("delete from user_vocab where user_id = ?", guestId);
    jdbcTemplate.update("delete from user_word_prefs where user_id = ?", guestId);
    jdbcTemplate.update(
        "delete from level_test_answers where test_id in"
            + " (select id from level_tests where user_id = ?)",
        guestId);
    jdbcTemplate.update("delete from level_tests where user_id = ?", guestId);
    // 게스트는 녹음 활용에 동의할 수 없어 보통 없지만, 있으면 파일까지 지운다.
    trainingRecordingService.deleteAllOf(guestId);
    return audioKeys;
  }
}
