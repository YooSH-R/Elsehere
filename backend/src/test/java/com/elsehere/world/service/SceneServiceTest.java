package com.elsehere.world.service;

import static com.elsehere.vocab.entity.VocabularyType.WORD;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.dto.NpcResponse;
import com.elsehere.world.dto.ObjectCardResponse;
import com.elsehere.world.dto.SceneDetailResponse;
import com.elsehere.world.dto.SceneSummaryResponse;
import com.elsehere.world.entity.NpcType;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 장소·NPC·사물 데이터는 시드(WorldSeedLoader가 시작 때 넣음)를 그대로 쓴다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({TestcontainersConfiguration.class, WorldSeedLoader.class, SceneService.class})
class SceneServiceTest {

  @Autowired private SceneService sceneService;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("장소 목록은 시드 순서대로 코드·이름·Unity 씬 이름·열림 여부를 돌려준다")
  void getScenes_returnsScenesInSeedOrder() {
    assertThat(sceneService.getScenes())
        .containsExactly(
            new SceneSummaryResponse("HOME", "집", "HomeScene", true),
            new SceneSummaryResponse("TOWN", "마을", "TownScene", true),
            new SceneSummaryResponse("CAFE", "모퉁이 카페", "CafeScene", true),
            new SceneSummaryResponse("HOTEL", "호텔", "HotelScene", false));
  }

  @Test
  @DisplayName("집 상세는 사물 6개를 어휘와 함께 돌려주고, NPC는 없다")
  void getScene_home_returnsObjectsWithVocabularies() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    SceneDetailResponse home = sceneService.getScene("HOME", userId);

    assertThat(home.sceneCode()).isEqualTo("HOME");
    assertThat(home.name()).isEqualTo("집");
    assertThat(home.unityKey()).isEqualTo("HomeScene");
    assertThat(home.isOpen()).isTrue();
    assertThat(home.npcs()).isEmpty();
    assertThat(home.objects())
        .extracting(
            ObjectCardResponse::objectKey,
            card -> card.vocab().type(),
            card -> card.vocab().text(),
            card -> card.vocab().meaningKo(),
            card -> card.vocab().example(),
            card -> card.vocab().exampleKo())
        .containsExactlyInAnyOrder(
            tuple("tv", WORD, "TV", "텔레비전", "Let's watch TV together.", "같이 TV 보자."),
            tuple(
                "refrigerator",
                WORD,
                "refrigerator",
                "냉장고",
                "The milk is in the refrigerator.",
                "우유는 냉장고에 있어요."),
            tuple(
                "air_conditioner",
                WORD,
                "air conditioner",
                "에어컨",
                "Can you turn on the air conditioner?",
                "에어컨 좀 켜 줄래요?"),
            tuple("sofa", WORD, "sofa", "소파", "He is sitting on the sofa.", "그는 소파에 앉아 있어요."),
            tuple(
                "bookshelf",
                WORD,
                "bookshelf",
                "책장",
                "The bookshelf is full of books.",
                "책장에 책이 가득해요."),
            tuple("bed", WORD, "bed", "침대", "I make my bed every morning.", "나는 매일 아침 침대를 정리해요."));
    assertThat(home.objects()).allSatisfy(card -> assertThat(card.vocab().vocabId()).isNotNull());
  }

  @Test
  @DisplayName("마을 상세는 행인 Ben을 역할 이름·먼저 말 걸기 여부·첫 말과 함께 돌려주고, 사물은 없다")
  void getScene_town_returnsNpcs() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    SceneDetailResponse town = sceneService.getScene("TOWN", userId);

    assertThat(town.objects()).isEmpty();
    assertThat(town.npcs())
        .containsExactly(
            new NpcResponse(
                "TOWN_PASSERBY",
                "Ben",
                NpcType.PASSERBY,
                "행인",
                true,
                "Hi there! Nice weather today, isn't it?"));
  }

  @Test
  @DisplayName("사용자가 단어장에 저장한 단어의 사물 카드는 saved가 true이고, 나머지는 false다")
  void getScene_marksSavedWords() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    saveWord(userId, "sofa");

    SceneDetailResponse home = sceneService.getScene("HOME", userId);

    assertThat(home.objects())
        .filteredOn(ObjectCardResponse::saved)
        .extracting(ObjectCardResponse::objectKey)
        .containsExactly("sofa");
  }

  @Test
  @DisplayName("사용자가 자동 표시를 끈 사물 카드만 hoverHidden이 true이고, 다시 켰거나 설정이 없으면 false다")
  void getScene_marksHoverHiddenObjects() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    setHoverHidden(userId, "tv", true);
    setHoverHidden(userId, "bed", false);

    SceneDetailResponse home = sceneService.getScene("HOME", userId);

    assertThat(home.objects())
        .filteredOn(ObjectCardResponse::hoverHidden)
        .extracting(ObjectCardResponse::objectKey)
        .containsExactly("tv");
  }

  @Test
  @DisplayName("다른 사용자가 저장하거나 숨긴 사물은 내 사물 카드에 반영되지 않는다")
  void getScene_ignoresOtherUsersSettings() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2");
    saveWord(otherUserId, "sofa");
    setHoverHidden(otherUserId, "tv", true);

    SceneDetailResponse home = sceneService.getScene("HOME", userId);

    assertThat(home.objects())
        .allSatisfy(
            card -> {
              assertThat(card.saved()).isFalse();
              assertThat(card.hoverHidden()).isFalse();
            });
  }

  @Test
  @DisplayName("미리 만든 발음 파일이 있는 단어의 사물 카드만 hasPronunciation이 true다")
  void getScene_marksWordsWithPronunciation() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    setPronunciationAudioKey("sofa", "vocab/sofa.mp3");

    SceneDetailResponse home = sceneService.getScene("HOME", userId);

    assertThat(home.objects())
        .filteredOn(ObjectCardResponse::hasPronunciation)
        .extracting(ObjectCardResponse::objectKey)
        .containsExactly("sofa");
  }

  @Test
  @DisplayName("닫힌 장소(호텔)도 조회할 수 있으며 isOpen이 false다")
  void getScene_closedScene_returnsNotOpen() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    SceneDetailResponse hotel = sceneService.getScene("HOTEL", userId);

    assertThat(hotel.sceneCode()).isEqualTo("HOTEL");
    assertThat(hotel.isOpen()).isFalse();
    assertThat(hotel.npcs()).isEmpty();
    assertThat(hotel.objects()).isEmpty();
  }

  @Test
  @DisplayName("없는 장소 코드로 조회하면 NOT_FOUND 오류를 낸다")
  void getScene_unknownScene_throwsNotFound() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    assertThatThrownBy(() -> sceneService.getScene("MOON", userId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  // 단어 저장(VOCAB-02) 전이라 SQL로 단어장에 넣는다. 저장 시각은 DB 기본값으로 채운다.
  private void saveWord(Long userId, String word) {
    entityManager
        .createNativeQuery(
            "insert into user_vocab (user_id, vocab_id, source_type)"
                + " select ?, id, 'OBJECT' from vocabularies where text = ?")
        .setParameter(1, userId)
        .setParameter(2, word)
        .executeUpdate();
  }

  // 사물 카드 표시 설정(WORLD-03) 전이라 SQL로 넣는다.
  private void setHoverHidden(Long userId, String objectKey, boolean hidden) {
    entityManager
        .createNativeQuery(
            "insert into user_object_prefs (user_id, object_id, hover_hidden)"
                + " select ?, id, ? from world_objects where unity_key = ?")
        .setParameter(1, userId)
        .setParameter(2, hidden)
        .setParameter(3, objectKey)
        .executeUpdate();
  }

  // 발음 파일은 시드에 아직 없어서 테스트에서 파일 키만 넣는다.
  private void setPronunciationAudioKey(String word, String audioKey) {
    entityManager
        .createNativeQuery("update vocabularies set pronunciation_audio_key = ? where text = ?")
        .setParameter(1, audioKey)
        .setParameter(2, word)
        .executeUpdate();
  }
}
