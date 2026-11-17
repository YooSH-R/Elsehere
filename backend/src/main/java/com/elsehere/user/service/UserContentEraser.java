package com.elsehere.user.service;

import com.elsehere.conversation.service.NpcAudioRetention;
import com.elsehere.conversation.service.TrainingRecordingService;
import com.elsehere.feedback.service.ModelSpeechStore;
import java.util.List;
import java.util.Set;
import lombok.RequiredArgsConstructor;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;

/**
 * 사용자가 남긴 내용(대화 원문·대화·문장 피드백·NPC 기억·저장 단어·숨김·레벨 테스트 답·학습용 녹음·NPC 음성·모범 음성)을 지운다. 게스트 내용 정리와 회원 탈퇴가
 * 함께 쓴다. 사용자·대화·AI 호출 기록 같은 뼈대는 남겨, 남은 행이 가리키는 사용자·대화가 사라지지 않고 사용자 수·대화 수·사용량 집계도 그대로 할 수 있다.
 *
 * <p>여러 도메인의 테이블을 한 번에 지우므로 SQL로 처리한다. 사용자·대화·턴을 가리키는 칸이 있는 테이블은 {@link #CLEARED_TABLES}와 {@link
 * #RETAINED_TABLES} 중 하나에 넣어 두어야 한다(새 테이블을 빠뜨리면 테스트가 실패한다).
 */
@Service
@RequiredArgsConstructor
public class UserContentEraser {

  /** 사용자가 남긴 내용이라 지우는 테이블 */
  static final Set<String> CLEARED_TABLES =
      Set.of(
          "turns",
          "turn_feedback",
          "conversation_feedback",
          "user_npc_relations",
          "showcase_saved_states",
          "npc_follow_ups",
          "review_cards",
          "user_vocab",
          "user_word_prefs",
          "level_tests",
          "training_recordings");

  /** 사용자·대화 메타데이터와 사용량·진행 상태는 남긴다. 대화의 복습 원문 캐시는 별도로 비운다. */
  static final Set<String> RETAINED_TABLES =
      Set.of(
          "conversations",
          "ai_requests",
          "user_missions",
          "first_day_missions",
          "town_stories",
          // 돌발 부탁: 학습자가 한 말이 없고, 완료한 부탁 보상이 퀘스트 코인 잔액에 들어가는 진행 기록이다.
          "npc_favors",
          // 시연 진행: 시연 퀘스트 단계·우산·시연 결제 기록이다.
          "showcase_journeys",
          "user_consents",
          "user_language_profiles",
          "user_game_clocks",
          "user_daily_activities");

  private static final String USER_CONVERSATIONS =
      "(select id from conversations where user_id = ?)";

  private static final String USER_TURNS =
      "(select id from turns where conversation_id in " + USER_CONVERSATIONS + ")";

  private final JdbcTemplate jdbcTemplate;
  private final NpcAudioRetention npcAudioRetention;
  private final ModelSpeechStore modelSpeechStore;
  private final TrainingRecordingService trainingRecordingService;

  /**
   * 내용과 함께 지울 파일 키. DB를 지운 트랜잭션이 끝난 뒤 {@link #deleteFiles}로 지운다.
   *
   * @param npcAudioKeys 지운 턴이 쓰던 NPC 음성
   * @param modelSpeechKeys 지운 문장 피드백의 모범 음성(FB-04)
   */
  public record ErasedFiles(List<String> npcAudioKeys, List<String> modelSpeechKeys) {}

  /** 사용자의 내용을 DB에서 지우고, 함께 지울 파일 키를 돌려준다. */
  @Transactional(propagation = Propagation.MANDATORY)
  public ErasedFiles deleteContent(Long userId) {
    List<String> npcAudioKeys =
        jdbcTemplate.queryForList(
            "select npc_audio_key from turns where npc_audio_key is not null"
                + " and conversation_id in "
                + USER_CONVERSATIONS,
            String.class,
            userId);
    List<String> modelSpeechKeys =
        jdbcTemplate.queryForList(
            "select model_audio_key from turn_feedback where model_audio_key is not null"
                + " and turn_id in "
                + USER_TURNS,
            String.class,
            userId);
    jdbcTemplate.update(
        "delete from conversation_feedback where conversation_id in " + USER_CONVERSATIONS, userId);
    // 문장 피드백은 턴 ID만 가지므로 턴을 지우기 전에 지운다.
    jdbcTemplate.update("delete from turn_feedback where turn_id in " + USER_TURNS, userId);
    jdbcTemplate.update("delete from turns where conversation_id in " + USER_CONVERSATIONS, userId);
    // 대화 메타데이터는 보존하지만 결과에 복사한 발화 원문은 보존하지 않는다.
    jdbcTemplate.update("update conversations set review_result = null where user_id = ?", userId);
    jdbcTemplate.update("delete from user_npc_relations where user_id = ?", userId);
    // 시연이 보관해 둔 NPC 관계에도 기억 카드가 들어 있어 함께 지운다.
    jdbcTemplate.update("delete from showcase_saved_states where user_id = ?", userId);
    // NPC가 곧 건네기로 한 것은 주문한 내용이 담겨 있어 함께 지운다.
    jdbcTemplate.update("delete from npc_follow_ups where user_id = ?", userId);
    jdbcTemplate.update("delete from review_cards where user_id = ?", userId);
    jdbcTemplate.update("delete from user_vocab where user_id = ?", userId);
    jdbcTemplate.update("delete from user_word_prefs where user_id = ?", userId);
    jdbcTemplate.update(
        "delete from level_test_answers where test_id in"
            + " (select id from level_tests where user_id = ?)",
        userId);
    jdbcTemplate.update("delete from level_tests where user_id = ?", userId);
    trainingRecordingService.deleteAllOf(userId);
    return new ErasedFiles(npcAudioKeys, modelSpeechKeys);
  }

  /**
   * DB를 지운 뒤 파일을 지운다. NPC 음성은 실패하면 어떤 턴도 쓰지 않는 파일로 남아 NPC 음성 정리가 지우고, NPC 공용 음성(말걸기)은 남긴다. 모범 음성은
   * 따로 정리하는 작업이 없어 실패하면 파일이 남는다.
   */
  public void deleteFiles(ErasedFiles files) {
    npcAudioRetention.deleteAudio(files.npcAudioKeys());
    files.modelSpeechKeys().forEach(modelSpeechStore::delete);
  }
}
