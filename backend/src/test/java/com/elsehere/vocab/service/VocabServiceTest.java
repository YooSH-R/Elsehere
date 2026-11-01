package com.elsehere.vocab.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.vocab.dto.VocabItemResponse;
import com.elsehere.vocab.dto.VocabListResponse;
import com.elsehere.vocab.dto.VocabSaveResponse;
import com.elsehere.vocab.entity.UserVocabSourceType;
import com.elsehere.vocab.entity.VocabularySource;
import com.elsehere.vocab.entity.VocabularyType;
import com.elsehere.vocab.repository.UserVocabRepository;
import com.elsehere.vocab.repository.VocabularyRepository;
import com.elsehere.world.dto.WordCardResponse;
import com.elsehere.world.service.PronunciationAudioStore;
import com.elsehere.world.service.SceneService;
import jakarta.persistence.EntityManager;
import java.time.Clock;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.time.ZoneOffset;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.autoconfigure.ImportAutoConfiguration;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jackson.autoconfigure.JacksonAutoConfiguration;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;

// 사물·어휘·NPC는 시드(WorldSeedLoader가 시작 때 넣음)를 쓰고, 저장 여부는 WORLD-02 사물 카드로도 확인한다.
// NPC 대사 단어는 대화·턴을 바로 저장해(DialogueTestData) AI를 거치지 않는다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@ImportAutoConfiguration(JacksonAutoConfiguration.class)
@Import({
  TestcontainersConfiguration.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  VocabService.class,
  SceneService.class,
  VocabServiceTest.ClockTestConfiguration.class
})
class VocabServiceTest {

  // 한국 시각 2026-10-27 10:00
  private static final Instant NOW = Instant.parse("2026-10-27T01:00:00Z");

  private static final String LATTE = "[{\"text\": \"latte\", \"meaningKo\": \"라떼\"}]";

  private static final String LATTE_CANDIDATE =
      "[{\"expression\": \"latte\", \"meaningKo\": \"라떼\","
          + " \"exampleEn\": \"I'd like an iced latte.\", \"exampleKo\": \"아이스 라떼 주세요.\"}]";

  @Autowired private VocabService vocabService;

  @Autowired private SceneService sceneService;

  @Autowired private UserVocabRepository userVocabRepository;

  @Autowired private VocabularyRepository vocabularyRepository;

  @Autowired private EntityManager entityManager;

  @Autowired private AdjustableClock clock;

  // Clock 빈은 테스트끼리 공유하므로 매번 기준 시각으로 되돌린다.
  @BeforeEach
  void resetClock() {
    clock.setInstant(NOW);
  }

  @Test
  @DisplayName("사물 카드의 단어를 저장하면 단어 뜻과 사물 예문·예문 뜻을 함께 저장하고, 사물 카드가 저장됨으로 바뀐다")
  void saveObjectWord_savesWordWithExample() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    VocabSaveResponse response = vocabService.saveObjectWord(userId, "sofa");

    assertThat(response.created()).isTrue();
    VocabItemResponse item = response.item();
    assertThat(item.userVocabId()).isNotNull();
    assertThat(item.vocab().vocabId()).isNotNull();
    assertThat(item.vocab().type()).isEqualTo(VocabularyType.WORD);
    assertThat(item.vocab().text()).isEqualTo("sofa");
    assertThat(item.vocab().meaningKo()).isEqualTo("소파");
    assertThat(item.context())
        .isEqualTo(new VocabItemResponse.Context("He is sitting on the sofa.", "그는 소파에 앉아 있어요."));
    assertThat(item.memo()).isNull();
    assertThat(item.memorized()).isFalse();
    assertThat(item.savedAt()).isEqualTo(OffsetDateTime.parse("2026-10-27T10:00:00+09:00"));
    assertThat(item.source())
        .isEqualTo(new VocabItemResponse.Source(UserVocabSourceType.OBJECT, null, null));
    assertThat(savedOnObjectCard(userId, "sofa")).isTrue();
  }

  @Test
  @DisplayName("이미 저장한 단어를 다시 저장하면 새로 만들지 않고 처음 저장한 항목을 created=false로 돌려준다")
  void saveObjectWordTwice_returnsFirstEntry() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    VocabSaveResponse first = vocabService.saveObjectWord(userId, "sofa");

    VocabSaveResponse second = vocabService.saveObjectWord(userId, "sofa");

    assertThat(second.created()).isFalse();
    assertThat(second.item()).isEqualTo(first.item());
    assertThat(userVocabRepository.count()).isEqualTo(1);
  }

  @Test
  @DisplayName("없는 단어 키로 저장하면 NOT_FOUND 오류를 내고 아무것도 저장하지 않는다")
  void saveObjectWord_unknownWord_throwsNotFound() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    assertThatThrownBy(() -> vocabService.saveObjectWord(userId, "moon_rock"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(userVocabRepository.count()).isZero();
  }

  @Test
  @DisplayName("단어장 목록은 최근에 저장한 단어부터 보여주고, 전체 개수를 함께 돌려준다")
  void getVocab_returnsRecentlySavedFirst() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    saveAt(userId, "sofa", "2026-10-27T01:00:00Z");
    saveAt(userId, "tv", "2026-10-27T02:00:00Z");
    saveAt(userId, "bed", "2026-10-27T03:00:00Z");

    VocabListResponse response = vocabService.getVocab(userId, false, null, 20);

    assertThat(response.counts().all()).isEqualTo(3);
    assertThat(response.items())
        .extracting(item -> item.vocab().text())
        .containsExactly("bed", "TV", "sofa");
    assertThat(response.nextCursor()).isNull();
  }

  @Test
  @DisplayName("외운 단어는 기본 목록과 개수에서 빠지고, 외운 단어도 보기를 켜면 함께 나온다")
  void getVocab_excludesMemorizedUnlessIncluded() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    saveAt(userId, "sofa", "2026-10-27T01:00:00Z");
    saveAt(userId, "tv", "2026-10-27T02:00:00Z");
    saveAt(userId, "bed", "2026-10-27T03:00:00Z");
    markMemorized(userId, "TV");

    VocabListResponse withoutMemorized = vocabService.getVocab(userId, false, null, 20);
    VocabListResponse withMemorized = vocabService.getVocab(userId, true, null, 20);

    assertThat(withoutMemorized.counts().all()).isEqualTo(2);
    assertThat(withoutMemorized.items())
        .extracting(item -> item.vocab().text())
        .containsExactly("bed", "sofa");
    assertThat(withMemorized.counts().all()).isEqualTo(3);
    assertThat(withMemorized.items())
        .extracting(item -> item.vocab().text(), VocabItemResponse::memorized)
        .containsExactly(tuple("bed", false), tuple("TV", true), tuple("sofa", false));
  }

  @Test
  @DisplayName("한 페이지 개수보다 많으면 다음 페이지 커서를 주고, 그 커서로 이어서 조회하면 나머지를 돌려준다")
  void getVocab_pagesWithCursor() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    saveAt(userId, "sofa", "2026-10-27T01:00:00Z");
    saveAt(userId, "tv", "2026-10-27T02:00:00Z");
    saveAt(userId, "bed", "2026-10-27T03:00:00Z");

    VocabListResponse firstPage = vocabService.getVocab(userId, false, null, 2);
    VocabListResponse secondPage = vocabService.getVocab(userId, false, firstPage.nextCursor(), 2);

    assertThat(firstPage.items())
        .extracting(item -> item.vocab().text())
        .containsExactly("bed", "TV");
    assertThat(firstPage.nextCursor()).isNotNull();
    assertThat(firstPage.counts().all()).isEqualTo(3);
    assertThat(secondPage.items()).extracting(item -> item.vocab().text()).containsExactly("sofa");
    assertThat(secondPage.nextCursor()).isNull();
    assertThat(secondPage.counts().all()).isEqualTo(3);
  }

  @Test
  @DisplayName("다른 사용자가 저장한 단어는 내 단어장 목록과 개수에 섞이지 않는다")
  void getVocab_ignoresOtherUsersWords() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    saveAt(userId, "sofa", "2026-10-27T01:00:00Z");
    saveAt(otherUserId, "tv", "2026-10-27T02:00:00Z");

    VocabListResponse response = vocabService.getVocab(userId, true, null, 20);

    assertThat(response.counts().all()).isEqualTo(1);
    assertThat(response.items()).extracting(item -> item.vocab().text()).containsExactly("sofa");
  }

  @ParameterizedTest
  @ValueSource(strings = {"abc", "999999"})
  @DisplayName("숫자가 아니거나 없는 항목을 가리키는 커서는 VALIDATION_FAILED로 거절한다")
  void getVocab_invalidCursor_throwsValidationFailed(String cursor) {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    assertThatThrownBy(() -> vocabService.getVocab(userId, false, cursor, 20))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED));
  }

  @Test
  @DisplayName("다른 사용자의 단어장 항목을 가리키는 커서는 VALIDATION_FAILED로 거절한다")
  void getVocab_otherUsersCursor_throwsValidationFailed() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    String othersCursor =
        String.valueOf(vocabService.saveObjectWord(otherUserId, "tv").item().userVocabId());

    assertThatThrownBy(() -> vocabService.getVocab(userId, false, othersCursor, 20))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED));
  }

  @Test
  @DisplayName("메모만 보내면 메모만 바꾸고 외웠어요 상태는 그대로 둔다")
  void updateVocab_memoOnly_changesMemo() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long userVocabId = vocabService.saveObjectWord(userId, "sofa").item().userVocabId();

    VocabItemResponse updated = vocabService.updateVocab(userId, userVocabId, "앉을 때 쓰기", null);

    assertThat(updated.memo()).isEqualTo("앉을 때 쓰기");
    assertThat(updated.memorized()).isFalse();
    assertThat(vocabService.getVocab(userId, false, null, 20).items())
        .singleElement()
        .extracting(VocabItemResponse::memo)
        .isEqualTo("앉을 때 쓰기");
  }

  @Test
  @DisplayName("외웠어요를 켜면 외운 단어가 되어 기본 목록에서 빠지고, 끄면 다시 나오며 메모는 그대로 둔다")
  void updateVocab_toggleMemorized_keepsMemo() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long userVocabId = vocabService.saveObjectWord(userId, "sofa").item().userVocabId();
    vocabService.updateVocab(userId, userVocabId, "앉을 때 쓰기", null);

    VocabItemResponse memorized = vocabService.updateVocab(userId, userVocabId, null, true);
    long countWhileMemorized = vocabService.getVocab(userId, false, null, 20).counts().all();
    VocabItemResponse forgotten = vocabService.updateVocab(userId, userVocabId, null, false);
    long countAfterForgotten = vocabService.getVocab(userId, false, null, 20).counts().all();

    assertThat(memorized.memorized()).isTrue();
    assertThat(memorized.memo()).isEqualTo("앉을 때 쓰기");
    assertThat(countWhileMemorized).isZero();
    assertThat(forgotten.memorized()).isFalse();
    assertThat(forgotten.memo()).isEqualTo("앉을 때 쓰기");
    assertThat(countAfterForgotten).isEqualTo(1);
  }

  @Test
  @DisplayName("다른 사용자의 단어장 항목을 수정하면 NOT_FOUND 오류를 내고 그 항목을 바꾸지 않는다")
  void updateVocab_otherUsersEntry_throwsNotFound() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    Long othersEntryId = vocabService.saveObjectWord(otherUserId, "tv").item().userVocabId();

    assertThatThrownBy(() -> vocabService.updateVocab(userId, othersEntryId, "내 메모", true))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(vocabService.getVocab(otherUserId, false, null, 20).items())
        .singleElement()
        .satisfies(
            item -> {
              assertThat(item.memo()).isNull();
              assertThat(item.memorized()).isFalse();
            });
  }

  @Test
  @DisplayName("없는 단어장 항목을 수정하면 NOT_FOUND 오류를 낸다")
  void updateVocab_unknownEntry_throwsNotFound() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    assertThatThrownBy(() -> vocabService.updateVocab(userId, 999999L, "메모", null))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("단어장에서 삭제하면 내 단어장과 사물 카드의 저장 표시에서 빠지고, 공용 어휘는 남는다")
  void deleteVocab_removesOnlyUserEntry() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long userVocabId = vocabService.saveObjectWord(userId, "sofa").item().userVocabId();
    long vocabularyCount = vocabularyRepository.count();

    vocabService.deleteVocab(userId, userVocabId);

    assertThat(vocabService.getVocab(userId, true, null, 20).items()).isEmpty();
    assertThat(savedOnObjectCard(userId, "sofa")).isFalse();
    assertThat(vocabularyRepository.count()).isEqualTo(vocabularyCount);
  }

  @Test
  @DisplayName("다른 사용자의 단어장 항목을 삭제하면 NOT_FOUND 오류를 내고 그 항목은 남는다")
  void deleteVocab_otherUsersEntry_throwsNotFound() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    Long othersEntryId = vocabService.saveObjectWord(otherUserId, "tv").item().userVocabId();

    assertThatThrownBy(() -> vocabService.deleteVocab(userId, othersEntryId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(vocabService.getVocab(otherUserId, true, null, 20).items()).hasSize(1);
  }

  @Test
  @DisplayName("NPC 대사 단어 풀이의 단어를 저장하면 풀이 뜻과 대사 전문·대사 뜻을 함께 저장하고, 사전에 없던 단어는 NPC 출처 어휘로 만든다")
  void saveNpcLineWord_savesGlossaryWordWithLine() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    Turn line = viewedNpcLine(conversation, "Would you like an iced latte?", "아이스 라떼 드릴까요?", LATTE);

    VocabSaveResponse response = vocabService.saveNpcLineWord(userId, line.getId(), "latte");

    assertThat(response.created()).isTrue();
    VocabItemResponse item = response.item();
    assertThat(item.vocab().type()).isEqualTo(VocabularyType.WORD);
    assertThat(item.vocab().text()).isEqualTo("latte");
    assertThat(item.vocab().meaningKo()).isEqualTo("라떼");
    assertThat(item.context())
        .isEqualTo(new VocabItemResponse.Context("Would you like an iced latte?", "아이스 라떼 드릴까요?"));
    assertThat(item.savedAt()).isEqualTo(OffsetDateTime.parse("2026-10-27T10:00:00+09:00"));
    assertThat(item.source())
        .isEqualTo(
            new VocabItemResponse.Source(
                UserVocabSourceType.NPC_LINE, "Mia", conversation.getId()));
    assertThat(userVocabRepository.findById(item.userVocabId()).orElseThrow().getSourceTurnId())
        .isEqualTo(line.getId());
    assertThat(vocabularySource("latte")).isEqualTo(VocabularySource.NPC);
  }

  @Test
  @DisplayName("사전에 이미 있는 단어면 새 어휘를 만들지 않고 기존 뜻을 그대로 쓴다")
  void saveNpcLineWord_existingVocabulary_keepsMeaning() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Conversation conversation = conversation(entityManager, userId, "HOTEL_STAFF");
    Turn line =
        viewedNpcLine(
            conversation,
            "Please have a seat on the sofa.",
            "소파에 앉아 계세요.",
            "[{\"text\": \"sofa\", \"meaningKo\": \"긴 의자\"}]");
    long vocabularyCount = vocabularyRepository.count();

    VocabSaveResponse response = vocabService.saveNpcLineWord(userId, line.getId(), "Sofa");

    assertThat(response.item().vocab().text()).isEqualTo("sofa");
    assertThat(response.item().vocab().meaningKo()).isEqualTo("소파");
    assertThat(response.item().context().text()).isEqualTo("Please have a seat on the sofa.");
    assertThat(vocabularyRepository.count()).isEqualTo(vocabularyCount);
  }

  @Test
  @DisplayName("이미 저장한 단어를 NPC 대사에서 다시 저장하면 처음 저장한 항목을 created=false로 돌려준다")
  void saveNpcLineWordTwice_returnsFirstEntry() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    Turn line = viewedNpcLine(conversation, "Would you like an iced latte?", "아이스 라떼 드릴까요?", LATTE);
    VocabSaveResponse first = vocabService.saveNpcLineWord(userId, line.getId(), "latte");

    VocabSaveResponse second = vocabService.saveNpcLineWord(userId, line.getId(), "latte");

    assertThat(second.created()).isFalse();
    assertThat(second.item()).isEqualTo(first.item());
    assertThat(userVocabRepository.count()).isEqualTo(1);
  }

  @ParameterizedTest
  @ValueSource(strings = {"coffee", "soy milk"})
  @DisplayName("단어 풀이에 없는 단어와 두 단어 이상인 표현은 VOCAB_SOURCE_MISMATCH로 거절하고 저장하지 않는다")
  void saveNpcLineWord_notSavableWord_throwsMismatch(String text) {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    Turn line =
        viewedNpcLine(
            conversation,
            "Regular milk or soy milk?",
            "일반 우유로 드릴까요, 두유로 드릴까요?",
            "[{\"text\": \"soy milk\", \"meaningKo\": \"두유\"}]");
    long vocabularyCount = vocabularyRepository.count();

    assertThatThrownBy(() -> vocabService.saveNpcLineWord(userId, line.getId(), text))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VOCAB_SOURCE_MISMATCH));
    assertThat(userVocabRepository.count()).isZero();
    assertThat(vocabularyRepository.count()).isEqualTo(vocabularyCount);
  }

  @Test
  @DisplayName("뜻 보기를 하지 않은 NPC 대사의 단어는 VOCAB_SOURCE_MISMATCH로 거절한다")
  void saveNpcLineWord_beforeMeaning_throwsMismatch() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    Turn line = npcTurn(entityManager, conversation, 1, "Would you like an iced latte?");

    assertThatThrownBy(() -> vocabService.saveNpcLineWord(userId, line.getId(), "latte"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VOCAB_SOURCE_MISMATCH));
    assertThat(userVocabRepository.count()).isZero();
  }

  @Test
  @DisplayName("다른 사용자의 NPC 대사와 내가 말한 턴은 NOT_FOUND로 거절한다")
  void saveNpcLineWord_othersOrUserTurn_throwsNotFound() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    Turn othersLine =
        viewedNpcLine(
            conversation(entityManager, otherUserId, "CAFE_STAFF"),
            "Would you like an iced latte?",
            "아이스 라떼 드릴까요?",
            LATTE);
    Turn myTurn =
        textTurn(
            entityManager,
            conversation(entityManager, userId, "CAFE_STAFF"),
            1,
            "A latte, please.");

    for (Turn turn : new Turn[] {othersLine, myTurn}) {
      assertThatThrownBy(() -> vocabService.saveNpcLineWord(userId, turn.getId(), "latte"))
          .isInstanceOfSatisfying(
              BusinessException.class,
              exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    }
    assertThat(userVocabRepository.count()).isZero();
  }

  @Test
  @DisplayName("단어장 목록에서 NPC 대사로 저장한 단어는 출처 NPC 이름과 대화를 보여 준다")
  void getVocab_showsNpcNameOfConversationSource() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    Turn line = viewedNpcLine(conversation, "Would you like an iced latte?", "아이스 라떼 드릴까요?", LATTE);
    vocabService.saveNpcLineWord(userId, line.getId(), "latte");
    entityManager.clear();

    VocabListResponse response = vocabService.getVocab(userId, false, null, 20);

    assertThat(response.items())
        .singleElement()
        .extracting(VocabItemResponse::source)
        .isEqualTo(
            new VocabItemResponse.Source(
                UserVocabSourceType.NPC_LINE, "Mia", conversation.getId()));
  }

  @Test
  @DisplayName("피드백 단어장 후보의 단어를 저장하면 후보의 뜻과 예문·예문 뜻을 저장하고, 사전에 없던 단어는 피드백 출처 어휘로 만든다")
  void saveFeedbackWord_savesCandidateWithExample() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    completeFeedback(conversation, LATTE_CANDIDATE);

    VocabSaveResponse response =
        vocabService.saveFeedbackWord(userId, conversation.getId(), "latte");

    assertThat(response.created()).isTrue();
    VocabItemResponse item = response.item();
    assertThat(item.vocab().text()).isEqualTo("latte");
    assertThat(item.vocab().meaningKo()).isEqualTo("라떼");
    assertThat(item.context())
        .isEqualTo(new VocabItemResponse.Context("I'd like an iced latte.", "아이스 라떼 주세요."));
    assertThat(item.source())
        .isEqualTo(
            new VocabItemResponse.Source(
                UserVocabSourceType.FEEDBACK, "Mia", conversation.getId()));
    assertThat(userVocabRepository.findById(item.userVocabId()).orElseThrow().getSourceTurnId())
        .isNull();
    assertThat(vocabularySource("latte")).isEqualTo(VocabularySource.FEEDBACK);
  }

  @Test
  @DisplayName("단어장 후보에 없는 단어나 피드백이 없는 대화의 단어는 VOCAB_SOURCE_MISMATCH로 거절한다")
  void saveFeedbackWord_notCandidate_throwsMismatch() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Conversation withFeedback = conversation(entityManager, userId, "CAFE_STAFF");
    completeFeedback(withFeedback, LATTE_CANDIDATE);
    Conversation withoutFeedback = conversation(entityManager, userId, "CAFE_STAFF");
    entityManager.flush();

    assertThatThrownBy(() -> vocabService.saveFeedbackWord(userId, withFeedback.getId(), "coffee"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VOCAB_SOURCE_MISMATCH));
    assertThatThrownBy(
            () -> vocabService.saveFeedbackWord(userId, withoutFeedback.getId(), "latte"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VOCAB_SOURCE_MISMATCH));
    assertThat(userVocabRepository.count()).isZero();
  }

  @Test
  @DisplayName("다른 사용자의 대화 피드백에서는 저장하지 못하고 NOT_FOUND로 거절한다")
  void saveFeedbackWord_othersConversation_throwsNotFound() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    Conversation othersConversation = conversation(entityManager, otherUserId, "CAFE_STAFF");
    completeFeedback(othersConversation, LATTE_CANDIDATE);

    assertThatThrownBy(
            () -> vocabService.saveFeedbackWord(userId, othersConversation.getId(), "latte"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(userVocabRepository.count()).isZero();
  }

  /** 단어장 후보가 있는 완료된 피드백. reviews는 피드백 저장(FeedbackService)이 쓰는 JSON 형식이다. */
  private void completeFeedback(Conversation conversation, String vocabCandidatesJson) {
    ConversationFeedback feedback = ConversationFeedback.of(conversation.getId());
    feedback.complete(
        "라테를 주문했어요.",
        "{\"good\": [], \"improve\": [], \"suggestion\": null, \"situationExpressions\": [],"
            + " \"vocabCandidates\": "
            + vocabCandidatesJson
            + "}",
        "test-model",
        null);
    entityManager.persist(feedback);
    entityManager.flush();
  }

  /** 뜻 보기(CONV-04)를 마친 NPC 대사. 단어 풀이는 CONV-04가 저장하는 JSON 형식이다. */
  private Turn viewedNpcLine(
      Conversation conversation, String text, String meaningKo, String glossaryJson) {
    Turn turn = npcTurn(entityManager, conversation, 1, text);
    turn.attachMeaning(meaningKo, glossaryJson);
    entityManager.flush();
    return turn;
  }

  private VocabularySource vocabularySource(String text) {
    return entityManager
        .createQuery(
            "select v.source from Vocabulary v where v.text = :text", VocabularySource.class)
        .setParameter("text", text)
        .getSingleResult();
  }

  // 외웠어요(VOCAB-03) 전이라 SQL로 외운 시각을 넣는다.
  private void markMemorized(Long userId, String word) {
    entityManager
        .createNativeQuery(
            "update user_vocab set memorized_at = ? where user_id = ?"
                + " and vocab_id = (select id from vocabularies where text = ?)")
        .setParameter(1, clock.instant())
        .setParameter(2, userId)
        .setParameter(3, word)
        .executeUpdate();
    // SQL로 바꾼 값을 다음 조회가 읽도록 이미 불러온 엔티티를 비운다.
    entityManager.clear();
  }

  private void saveAt(Long userId, String wordKey, String savedAt) {
    clock.setInstant(Instant.parse(savedAt));
    vocabService.saveObjectWord(userId, wordKey);
  }

  private boolean savedOnObjectCard(Long userId, String wordKey) {
    return sceneService.getScene("HOME", userId).words().stream()
        .filter(card -> card.wordKey().equals(wordKey))
        .findFirst()
        .map(WordCardResponse::saved)
        .orElseThrow();
  }

  @TestConfiguration(proxyBeanMethods = false)
  static class ClockTestConfiguration {

    @Bean
    AdjustableClock clock() {
      return new AdjustableClock(NOW);
    }
  }

  /** 테스트 안에서 현재 시각을 바꿀 수 있는 Clock. 저장 순서를 검증할 때 쓴다. */
  static class AdjustableClock extends Clock {

    private Instant instant;

    AdjustableClock(Instant instant) {
      this.instant = instant;
    }

    void setInstant(Instant instant) {
      this.instant = instant;
    }

    @Override
    public ZoneId getZone() {
      return ZoneOffset.UTC;
    }

    @Override
    public Clock withZone(ZoneId zone) {
      return Clock.fixed(instant, zone);
    }

    @Override
    public Instant instant() {
      return instant;
    }
  }
}
