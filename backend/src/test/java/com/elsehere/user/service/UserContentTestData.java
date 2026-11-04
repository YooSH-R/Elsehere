package com.elsehere.user.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcId;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;
import org.springframework.jdbc.core.JdbcTemplate;

/** 내용 지우기(게스트 정리·탈퇴) 테스트에서 쓰는 사용자 내용. 대화·턴은 테스트 도우미로, 나머지는 SQL로 넣는다. */
final class UserContentTestData {

  private UserContentTestData() {}

  // 대화 하나(NPC·학습자 턴, 대화·문장 피드백)와 기억·저장 단어·숨김·레벨 테스트·AI 호출 기록을 가진 사용자
  static Long userWithContent(
      EntityManager entityManager,
      JdbcTemplate jdbcTemplate,
      AuthProvider provider,
      String providerUid) {
    return userWithContent(entityManager, jdbcTemplate, provider, providerUid, "Sun");
  }

  /** 닉네임은 중복될 수 없으므로 한 테스트에서 사용자를 여럿 만들 때 쓴다. */
  static Long userWithContent(
      EntityManager entityManager,
      JdbcTemplate jdbcTemplate,
      AuthProvider provider,
      String providerUid,
      String nickname) {
    Long userId = UserTestData.insertUser(entityManager, provider, providerUid, nickname);
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    npcTurn(entityManager, conversation, 1, "Hi! What can I get for you?");
    Turn learnerTurn = textTurn(entityManager, conversation, 2, "One latte, please.");
    entityManager.flush();
    Long vocabId = jdbcTemplate.queryForObject("select min(id) from vocabularies", Long.class);
    jdbcTemplate.update(
        "insert into conversation_feedback (conversation_id, status) values (?, 'SKIPPED')",
        conversation.getId());
    jdbcTemplate.update(
        "insert into turn_feedback (turn_id, better_text, reason) values (?, ?, ?)",
        learnerTurn.getId(),
        "Can I get a latte, please?",
        turnFeedbackReason(userId));
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

  // 내용 지우기가 지우는 테이블별 행 수
  static Map<String, Integer> contentRows(
      EntityManager entityManager, JdbcTemplate jdbcTemplate, Long userId) {
    String conversations = "(select id from conversations where user_id = ?)";
    Map<String, Integer> rows = new LinkedHashMap<>();
    rows.put(
        "turns",
        rows(
            entityManager,
            jdbcTemplate,
            "select count(*) from turns where conversation_id in " + conversations,
            userId));
    rows.put(
        "conversation_feedback",
        rows(
            entityManager,
            jdbcTemplate,
            "select count(*) from conversation_feedback where conversation_id in " + conversations,
            userId));
    // turn_feedback은 턴 ID만 가지므로 턴을 지운 뒤에도 남는지 보려면 사용자별로 넣은 이유 문구로 센다.
    rows.put(
        "turn_feedback",
        rows(
            entityManager,
            jdbcTemplate,
            "select count(*) from turn_feedback where reason = 'turn feedback of user ' || ?",
            userId));
    rows.put(
        "user_npc_relations",
        rows(
            entityManager,
            jdbcTemplate,
            "select count(*) from user_npc_relations where user_id = ?",
            userId));
    rows.put(
        "user_vocab",
        rows(
            entityManager,
            jdbcTemplate,
            "select count(*) from user_vocab where user_id = ?",
            userId));
    rows.put(
        "user_word_prefs",
        rows(
            entityManager,
            jdbcTemplate,
            "select count(*) from user_word_prefs where user_id = ?",
            userId));
    rows.put(
        "level_tests",
        rows(
            entityManager,
            jdbcTemplate,
            "select count(*) from level_tests where user_id = ?",
            userId));
    rows.put(
        "level_test_answers",
        rows(
            entityManager,
            jdbcTemplate,
            "select count(*) from level_test_answers"
                + " where test_id in (select id from level_tests where user_id = ?)",
            userId));
    return rows;
  }

  /** {@link #userWithContent}로 만든 사용자의 문장 피드백에 모범 음성(FB-04) 파일 키를 붙인다. */
  static void attachModelSpeech(JdbcTemplate jdbcTemplate, Long userId, String modelAudioKey) {
    jdbcTemplate.update(
        "update turn_feedback set model_audio_key = ? where reason = ?",
        modelAudioKey,
        turnFeedbackReason(userId));
  }

  private static String turnFeedbackReason(Long userId) {
    return "turn feedback of user " + userId;
  }

  static int rows(EntityManager entityManager, JdbcTemplate jdbcTemplate, String sql, Long userId) {
    entityManager.flush();
    return jdbcTemplate.queryForObject(sql, Integer.class, userId);
  }
}
