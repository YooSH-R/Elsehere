package com.elsehere.conversation.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static java.time.temporal.ChronoUnit.DAYS;
import static java.time.temporal.ChronoUnit.HOURS;
import static java.util.concurrent.TimeUnit.SECONDS;
import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.NpcAudioStore.StoredAudio;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.repository.NpcRepository;
import jakarta.persistence.EntityManager;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.attribute.FileTime;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.transaction.support.TransactionTemplate;

// NPC 턴 음성 보관 정책. 실제 DB(PostgreSQL 컨테이너)와 테스트마다 빈 임시 폴더를 쓰고, 정책 시계는 고정한다.
@DialogueServiceTest
class NpcAudioRetentionTest {

  private static final Instant NOW = Instant.parse("2026-10-28T00:00:00Z");
  // 정리 테스트에서 상한 때문에 지우지 않도록 넉넉히 둔다.
  private static final long LIMIT = 1_000;

  @TempDir private Path directory;

  @Autowired private TurnRepository turnRepository;

  @Autowired private NpcRepository npcRepository;

  @Autowired private TransactionTemplate transactionTemplate;

  @Autowired private EntityManager entityManager;

  private NpcAudioStore store;
  private Conversation conversation;

  @BeforeEach
  void setUp() {
    store = new NpcAudioStore(directory.toString());
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    conversation = conversation(entityManager, userId, "CAFE_STAFF");
  }

  @Test
  @DisplayName("상한 안이면 음성을 저장하고 파일 키를 돌려준다")
  void save_withinLimit_storesAudio() {
    String key = retention(10).save(new byte[4]);

    assertThat(store.load(key)).hasValueSatisfying(wav -> assertThat(wav).hasSize(4));
    assertThat(store.usedBytes()).isEqualTo(4);
  }

  @Test
  @DisplayName("상한을 넘으면 오래된 파일부터 지우고, 지운 파일을 쓰던 턴은 키를 비우고 삭제 시각을 남긴 뒤 저장한다")
  void save_overLimit_deletesOldestFirst() throws IOException {
    String oldest = audioWrittenAt(4, NOW.minus(3, DAYS));
    String older = audioWrittenAt(4, NOW.minus(2, DAYS));
    Turn oldestTurn = npcTurnWithAudio(1, oldest);
    Turn olderTurn = npcTurnWithAudio(2, older);

    String key = retention(10).save(new byte[4]);

    assertThat(store.files()).extracting(StoredAudio::key).containsExactlyInAnyOrder(older, key);
    Turn deleted = reload(oldestTurn);
    assertThat(deleted.getNpcAudioKey()).isNull();
    assertThat(deleted.getNpcAudioDeletedAt()).isEqualTo(NOW);
    assertThat(reload(olderTurn).getNpcAudioKey()).isEqualTo(older);
  }

  @Test
  @DisplayName("다른 파일을 모두 지워도 상한에 들어가지 않는 음성은 아무것도 지우지 않고 저장하지 않는다")
  void save_largerThanLimit_returnsNullWithoutDeleting() throws IOException {
    String existing = audioWrittenAt(4, NOW.minus(1, DAYS));
    Turn turn = npcTurnWithAudio(1, existing);

    String key = retention(10).save(new byte[11]);

    assertThat(key).isNull();
    assertThat(store.files()).extracting(StoredAudio::key).containsExactly(existing);
    assertThat(reload(turn).getNpcAudioDeletedAt()).isNull();
  }

  @Test
  @DisplayName("여러 요청이 동시에 저장해도 전체 사용량이 상한을 넘지 않는다")
  void save_concurrently_staysWithinLimit() throws Exception {
    NpcAudioRetention retention = retention(20);
    ExecutorService executor = Executors.newFixedThreadPool(8);
    try {
      List<Future<String>> saves = new ArrayList<>();
      for (int i = 0; i < 16; i++) {
        saves.add(executor.submit(() -> retention.save(new byte[4])));
      }
      for (Future<String> save : saves) {
        assertThat(save.get(30, SECONDS)).isNotNull();
      }
    } finally {
      executor.shutdownNow();
    }

    assertThat(store.usedBytes()).isLessThanOrEqualTo(20);
  }

  @Test
  @DisplayName("정리: 턴이 만들어진 지 30일이 지난 음성은 파일을 지우고 삭제로 표시하며, 30일 안의 음성은 둔다")
  void cleanUp_expiresAudioOlderThanRetention() throws IOException {
    String expiredKey = audioWrittenAt(4, NOW.minus(31, DAYS));
    String recentKey = audioWrittenAt(4, NOW.minus(1, DAYS));
    Turn expired = npcTurnWithAudio(1, expiredKey, NOW.minus(31, DAYS));
    Turn recent = npcTurnWithAudio(2, recentKey, NOW.minus(1, DAYS));

    retention(LIMIT).cleanUp();

    assertThat(store.files()).extracting(StoredAudio::key).containsExactly(recentKey);
    assertThat(reload(expired).getNpcAudioKey()).isNull();
    assertThat(reload(expired).getNpcAudioDeletedAt()).isEqualTo(NOW);
    assertThat(reload(recent).getNpcAudioKey()).isEqualTo(recentKey);
    assertThat(reload(recent).getNpcAudioDeletedAt()).isNull();
  }

  @Test
  @DisplayName("정리: DB가 가리키는 파일이 없으면(DB 복원·이전 실패) 키를 비우고 삭제로 표시한다")
  void cleanUp_marksTurnWhoseFileIsMissing() {
    Turn turn = npcTurnWithAudio(1, UUID.randomUUID() + ".wav", NOW.minus(1, DAYS));

    retention(LIMIT).cleanUp();

    Turn cleaned = reload(turn);
    assertThat(cleaned.getNpcAudioKey()).isNull();
    assertThat(cleaned.getNpcAudioDeletedAt()).isEqualTo(NOW);
  }

  @Test
  @DisplayName("정리: 어떤 턴도 가리키지 않는 파일은 30일이 지났으면 지우고, 막 저장해 턴이 아직 없는 파일은 둔다")
  void cleanUp_deletesOnlyOldUnreferencedFiles() throws IOException {
    String oldOrphan = audioWrittenAt(4, NOW.minus(31, DAYS));
    String justSaved = audioWrittenAt(4, NOW.minus(1, HOURS));

    retention(LIMIT).cleanUp();

    assertThat(store.files()).extracting(StoredAudio::key).containsExactly(justSaved);
    assertThat(store.load(oldOrphan)).isEmpty();
  }

  @Test
  @DisplayName("정리: NPC 공용 음성은 기간이 지나도 지우지 않고, 그 음성을 쓰던 턴만 삭제로 표시한다")
  void cleanUp_keepsSharedNpcAudio() throws IOException {
    String sharedKey = audioWrittenAt(4, NOW.minus(40, DAYS));
    setApproachAudio("TOWN_PASSERBY", sharedKey);
    Turn turn = npcTurnWithAudio(1, sharedKey, NOW.minus(31, DAYS));

    retention(LIMIT).cleanUp();

    assertThat(store.load(sharedKey)).isPresent();
    assertThat(reload(turn).getNpcAudioKey()).isNull();
    assertThat(reload(turn).getNpcAudioDeletedAt()).isEqualTo(NOW);
  }

  @Test
  @DisplayName("정리: 상한을 넘었으면 공용 음성을 빼고 오래된 파일부터 지워 상한 안으로 맞춘다")
  void cleanUp_overLimit_deletesOldestUntilWithinLimit() throws IOException {
    String sharedKey = audioWrittenAt(4, NOW.minus(5, DAYS));
    setApproachAudio("TOWN_PASSERBY", sharedKey);
    String oldest = audioWrittenAt(4, NOW.minus(4, DAYS));
    String older = audioWrittenAt(4, NOW.minus(3, DAYS));
    String newest = audioWrittenAt(4, NOW.minus(2, DAYS));
    Turn oldestTurn = npcTurnWithAudio(1, oldest, NOW.minus(4, DAYS));
    npcTurnWithAudio(2, older, NOW.minus(3, DAYS));
    npcTurnWithAudio(3, newest, NOW.minus(2, DAYS));

    retention(8).cleanUp();

    assertThat(store.files())
        .extracting(StoredAudio::key)
        .containsExactlyInAnyOrder(sharedKey, newest);
    assertThat(reload(oldestTurn).getNpcAudioDeletedAt()).isEqualTo(NOW);
  }

  private NpcAudioRetention retention(long maxBytes) {
    return new NpcAudioRetention(
        store,
        turnRepository,
        npcRepository,
        transactionTemplate,
        Clock.fixed(NOW, ZoneOffset.UTC),
        30,
        maxBytes);
  }

  private String audioWrittenAt(int size, Instant writtenAt) throws IOException {
    String key = store.save(new byte[size]);
    Files.setLastModifiedTime(directory.resolve(key), FileTime.from(writtenAt));
    return key;
  }

  private Turn npcTurnWithAudio(int seq, String key) {
    Turn turn = Turn.npc(conversation, seq, "Hi! What can I get for you?", null, key);
    entityManager.persist(turn);
    entityManager.flush();
    return turn;
  }

  // 턴 생성 시각은 저장할 때 자동으로 기록되므로, 보관 기간을 확인하려고 DB에서 바꾼다.
  private Turn npcTurnWithAudio(int seq, String key, Instant createdAt) {
    Turn turn = npcTurnWithAudio(seq, key);
    entityManager
        .createNativeQuery("update turns set created_at = :createdAt where id = :id")
        .setParameter("createdAt", createdAt)
        .setParameter("id", turn.getId())
        .executeUpdate();
    entityManager.clear();
    return turn;
  }

  private void setApproachAudio(String npcCode, String key) {
    entityManager
        .createNativeQuery("update npcs set approach_audio_key = :key where code = :code")
        .setParameter("key", key)
        .setParameter("code", npcCode)
        .executeUpdate();
  }

  private Turn reload(Turn turn) {
    entityManager.flush();
    entityManager.clear();
    return entityManager.find(Turn.class, turn.getId());
  }
}
