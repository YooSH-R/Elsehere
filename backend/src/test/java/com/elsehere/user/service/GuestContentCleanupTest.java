package com.elsehere.user.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcId;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.service.DialogueServiceTest;
import com.elsehere.conversation.service.NpcAudioStore;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.sql.Timestamp;
import java.time.Duration;
import java.time.Instant;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.jdbc.core.JdbcTemplate;

// 게스트 정리는 실제 PostgreSQL에서 SQL로 지운다. 대화·턴은 테스트 도우미로, 나머지 내용은 SQL로 넣고 활동 시각을 옮겨 확인한다.
@DialogueServiceTest
class GuestContentCleanupTest {

  private static final byte[] WAV = {82, 73, 70, 70};

  @Autowired private GuestContentCleanup guestContentCleanup;

  @Autowired private NpcAudioStore npcAudioStore;

  @Autowired private EntityManager entityManager;

  @Autowired private JdbcTemplate jdbcTemplate;

  @Test
  @DisplayName("마지막 활동이 하루 지난 게스트는 발화·피드백·기억·저장 단어·숨김·레벨 테스트를 지우고 사용자·대화·사용량은 남긴다")
  void cleanUp_expiredGuest_clearsContentAndKeepsSkeleton() {
    Long guestId = userWithContent(AuthProvider.GUEST, "guest-uid-1");
    lastActive(guestId, Duration.ofDays(2));

    guestContentCleanup.cleanUp();

    assertThat(contentRows(guestId))
        .allSatisfy((table, rows) -> assertThat(rows).as(table).isZero());
    assertThat(rows("select count(*) from users where id = ?", guestId)).isEqualTo(1);
    assertThat(rows("select count(*) from conversations where user_id = ?", guestId)).isEqualTo(1);
    assertThat(rows("select count(*) from ai_requests where user_id = ?", guestId)).isEqualTo(1);
  }

  @Test
  @DisplayName("마지막 활동이 하루가 지나지 않은 게스트는 지우지 않는다")
  void cleanUp_recentGuest_keepsContent() {
    Long guestId = userWithContent(AuthProvider.GUEST, "guest-uid-1");
    lastActive(guestId, Duration.ofHours(3));

    guestContentCleanup.cleanUp();

    assertThat(contentRows(guestId))
        .allSatisfy((table, rows) -> assertThat(rows).as(table).isPositive());
  }

  @Test
  @DisplayName("가입자는 오래 활동하지 않아도 지우지 않는다")
  void cleanUp_member_keepsContent() {
    Long memberId = userWithContent(AuthProvider.GOOGLE, "google-sub-1");
    lastActive(memberId, Duration.ofDays(30));

    guestContentCleanup.cleanUp();

    assertThat(contentRows(memberId))
        .allSatisfy((table, rows) -> assertThat(rows).as(table).isPositive());
  }

  @Test
  @DisplayName("지운 발화의 NPC 음성 파일도 지우되, NPC 공용 음성(말걸기)은 남긴다")
  void cleanUp_expiredGuest_deletesOwnNpcAudioButKeepsShared() {
    String ownKey = npcAudioStore.save(WAV);
    String sharedKey = npcAudioStore.save(WAV);
    jdbcTemplate.update(
        "update npcs set approach_audio_key = ? where code = 'TOWN_PASSERBY'", sharedKey);
    Long guestId = UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1");
    Conversation conversation = conversation(entityManager, guestId, "TOWN_PASSERBY");
    entityManager.persist(Turn.npc(conversation, 1, "Hi there!", null, sharedKey));
    textTurn(entityManager, conversation, 2, "Hello");
    entityManager.persist(Turn.npc(conversation, 3, "Nice weather!", null, ownKey));
    entityManager.flush();
    lastActive(guestId, Duration.ofDays(2));

    guestContentCleanup.cleanUp();

    assertThat(npcAudioStore.load(ownKey)).isEmpty();
    assertThat(npcAudioStore.load(sharedKey)).isPresent();
  }

  @Test
  @DisplayName("사용자·대화를 가리키는 칸이 있는 테이블은 모두 게스트 정리에서 지우거나 남기는 쪽으로 정해 둔다")
  void everyUserOrConversationTable_isClassified() {
    List<String> tables =
        jdbcTemplate.queryForList(
            "select distinct table_name from information_schema.columns"
                + " where table_schema = current_schema()"
                + " and (column_name = 'user_id' or column_name like '%conversation_id')",
            String.class);

    Set<String> classified = new HashSet<>(GuestContentCleanup.CLEARED_TABLES);
    classified.addAll(GuestContentCleanup.RETAINED_TABLES);
    assertThat(classified).containsExactlyInAnyOrderElementsOf(tables);
  }

  // 대화 하나(NPC·학습자 턴, 피드백)와 기억·저장 단어·숨김·레벨 테스트·AI 호출 기록을 가진 사용자
  private Long userWithContent(AuthProvider provider, String providerUid) {
    Long userId = UserTestData.insertUser(entityManager, provider, providerUid);
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    npcTurn(entityManager, conversation, 1, "Hi! What can I get for you?");
    textTurn(entityManager, conversation, 2, "One latte, please.");
    entityManager.flush();
    Long vocabId = jdbcTemplate.queryForObject("select min(id) from vocabularies", Long.class);
    jdbcTemplate.update(
        "insert into conversation_feedback (conversation_id, status) values (?, 'SKIPPED')",
        conversation.getId());
    jdbcTemplate.update(
        "insert into user_npc_relations (user_id, npc_id) values (?, ?)",
        userId,
        npcId(entityManager, "CAFE_STAFF"));
    jdbcTemplate.update(
        "insert into user_vocab (user_id, vocab_id, source_type) values (?, ?, 'OBJECT')",
        userId,
        vocabId);
    jdbcTemplate.update(
        "insert into user_word_prefs (user_id, vocab_id, hover_hidden) values (?, ?, true)",
        userId,
        vocabId);
    Long testId =
        jdbcTemplate.queryForObject(
            "insert into level_tests (user_id, language_code, status)"
                + " values (?, 'en-US', 'SKIPPED') returning id",
            Long.class,
            userId);
    jdbcTemplate.update(
        "insert into level_test_answers (test_id, item_code, answer) values (?, 'q1', 'a')",
        testId);
    jdbcTemplate.update(
        "insert into ai_requests (request_id, kind, status, user_id, conversation_id)"
            + " values (?, 'NPC', 'OK', ?, ?)",
        UUID.randomUUID(),
        userId,
        conversation.getId());
    return userId;
  }

  // 사용자가 남긴 모든 행의 시각을 그만큼 앞으로 옮긴다.
  private void lastActive(Long userId, Duration ago) {
    Timestamp at = Timestamp.from(Instant.now().minus(ago));
    String conversations = "(select id from conversations where user_id = ?)";
    jdbcTemplate.update(
        "update users set created_at = ?, updated_at = ? where id = ?", at, at, userId);
    jdbcTemplate.update(
        "update conversations set created_at = ?, updated_at = ?, started_at = ? where user_id = ?",
        at,
        at,
        at,
        userId);
    jdbcTemplate.update(
        "update turns set created_at = ?, updated_at = ? where conversation_id in " + conversations,
        at,
        at,
        userId);
    for (String table :
        List.of("user_npc_relations", "user_vocab", "user_word_prefs", "level_tests")) {
      jdbcTemplate.update(
          "update " + table + " set created_at = ?, updated_at = ? where user_id = ?",
          at,
          at,
          userId);
    }
  }

  // 게스트 정리가 지우는 내용 테이블별 행 수
  private Map<String, Integer> contentRows(Long userId) {
    String conversations = "(select id from conversations where user_id = ?)";
    Map<String, Integer> rows = new LinkedHashMap<>();
    rows.put(
        "turns",
        rows("select count(*) from turns where conversation_id in " + conversations, userId));
    rows.put(
        "conversation_feedback",
        rows(
            "select count(*) from conversation_feedback where conversation_id in " + conversations,
            userId));
    rows.put(
        "user_npc_relations",
        rows("select count(*) from user_npc_relations where user_id = ?", userId));
    rows.put("user_vocab", rows("select count(*) from user_vocab where user_id = ?", userId));
    rows.put(
        "user_word_prefs", rows("select count(*) from user_word_prefs where user_id = ?", userId));
    rows.put("level_tests", rows("select count(*) from level_tests where user_id = ?", userId));
    rows.put(
        "level_test_answers",
        rows(
            "select count(*) from level_test_answers"
                + " where test_id in (select id from level_tests where user_id = ?)",
            userId));
    return rows;
  }

  private int rows(String sql, Long userId) {
    entityManager.flush();
    return jdbcTemplate.queryForObject(sql, Integer.class, userId);
  }
}
