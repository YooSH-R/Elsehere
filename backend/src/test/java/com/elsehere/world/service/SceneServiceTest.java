package com.elsehere.world.service;

import static com.elsehere.vocab.entity.VocabularyType.WORD;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.dto.NpcResponse;
import com.elsehere.world.dto.ObjectVocabResponse;
import com.elsehere.world.dto.SceneDetailResponse;
import com.elsehere.world.dto.SceneSummaryResponse;
import com.elsehere.world.dto.WordCardResponse;
import com.elsehere.world.entity.NpcType;
import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import jakarta.persistence.EntityManager;
import java.io.IOException;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.core.io.ClassPathResource;
import tools.jackson.databind.json.JsonMapper;

// 장소·NPC·장소별 단어는 시드(WorldSeedLoader가 시작 때 넣음)를 그대로 쓴다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  SceneService.class
})
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
            new SceneSummaryResponse("HOTEL", "호텔", "HotelScene", true),
            new SceneSummaryResponse("SHOP", "드럭스토어", "ShopScene", true),
            new SceneSummaryResponse("POLICE_STATION", "경찰서", "PoliceStationScene", true));
  }

  @Test
  @DisplayName("집 상세는 Unity 연결 목록의 집 단어를 단어 키·어휘와 함께 돌려주고, NPC는 없다")
  void getScene_home_returnsSceneWords() throws IOException {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    SceneDetailResponse home = sceneService.getScene("HOME", userId);

    assertThat(home.sceneCode()).isEqualTo("HOME");
    assertThat(home.name()).isEqualTo("집");
    assertThat(home.unityKey()).isEqualTo("HomeScene");
    assertThat(home.isOpen()).isTrue();
    assertThat(home.npcs()).isEmpty();
    assertThat(home.words())
        .extracting(WordCardResponse::wordKey)
        .containsExactlyInAnyOrderElementsOf(boundWordKeys("HOME"));
    assertThat(home.words())
        .filteredOn(card -> card.wordKey().equals("sofa"))
        .singleElement()
        .extracting(WordCardResponse::vocab)
        .satisfies(
            vocab -> {
              assertThat(vocab.vocabId()).isNotNull();
              assertThat(vocab)
                  .extracting(
                      ObjectVocabResponse::type,
                      ObjectVocabResponse::text,
                      ObjectVocabResponse::meaningKo,
                      ObjectVocabResponse::example,
                      ObjectVocabResponse::exampleKo)
                  .containsExactly(
                      WORD, "sofa", "소파", "He is sitting on the sofa.", "그는 소파에 앉아 있어요.");
            });
  }

  @Test
  @DisplayName(
      "마을 상세는 행인 Ben·택시 기사 Joe·경찰관 Dan, 거리 매장 직원 9명, 행인 Ruby·Ethan·Maya·Helen·Frank, 경찰관 Victor·Taylor와 빵집 직원 Kevin와 쉬는 날인 소방관 Ray·Rachel·Andre, 개를 산책시키는 노부부 Arthur·Margaret를 역할 이름·먼저 말 걸기 여부·첫 말과 함께 돌려준다")
  void getScene_town_returnsNpcs() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    SceneDetailResponse town = sceneService.getScene("TOWN", userId);

    assertThat(town.npcs())
        .containsExactly(
            new NpcResponse(
                "TOWN_PASSERBY",
                "Ben",
                NpcType.PASSERBY,
                "행인",
                true,
                "Hi there! Nice weather today, isn't it?"),
            new NpcResponse("TAXI_DRIVER", "Joe", NpcType.STAFF, "택시 기사", false, null),
            new NpcResponse("POLICE_OFFICER", "Dan", NpcType.STAFF, "경찰관", false, null),
            new NpcResponse("POST_OFFICE_STAFF", "Linda", NpcType.STAFF, "우체국 직원", false, null),
            new NpcResponse("DINER_STAFF", "Rosa", NpcType.STAFF, "식당 종업원", false, null),
            new NpcResponse("BANK_STAFF", "Henry", NpcType.STAFF, "은행원", false, null),
            new NpcResponse("LAUNDROMAT_STAFF", "Gus", NpcType.STAFF, "빨래방 주인", false, null),
            new NpcResponse("FLOWER_SHOP_STAFF", "Lily", NpcType.STAFF, "꽃집 주인", false, null),
            new NpcResponse("BARBER_STAFF", "Tony", NpcType.STAFF, "이발사", false, null),
            new NpcResponse("BAKERY_STAFF", "Grace", NpcType.STAFF, "제빵사", false, null),
            new NpcResponse("ARCADE_STAFF", "Max", NpcType.STAFF, "오락실 직원", false, null),
            new NpcResponse("HOME_STORE_STAFF", "Nina", NpcType.STAFF, "매장 직원", false, null),
            new NpcResponse("TOWN_RETIREE", "Ruby", NpcType.STAFF, "우체국 직원", false, null),
            new NpcResponse("TOWN_STUDENT", "Ethan", NpcType.STAFF, "오락실 직원", false, null),
            new NpcResponse("TOWN_EVENING_WALKER", "Maya", NpcType.STAFF, "은행원", false, null),
            new NpcResponse("HOTEL_CONCIERGE", "Walter", NpcType.STAFF, "꽃집 직원", false, null),
            new NpcResponse("TOWN_SHOPPER", "Helen", NpcType.STAFF, "은행원", false, null),
            new NpcResponse("TOWN_HANDYMAN", "Frank", NpcType.STAFF, "생활용품점 직원", false, null),
            new NpcResponse("POLICE_SERGEANT", "Victor", NpcType.STAFF, "경사", false, null),
            new NpcResponse(
                "POLICE_PATROL_OFFICER", "Kevin", NpcType.STAFF, "베이커리 직원", false, null),
            new NpcResponse("POLICE_ROOKIE", "Taylor", NpcType.STAFF, "신입 경찰관", false, null),
            new NpcResponse("FIRE_CAPTAIN", "Ray", NpcType.STAFF, "식당 직원", false, null),
            new NpcResponse("FIRE_FIGHTER", "Rachel", NpcType.STAFF, "빨래방 직원", false, null),
            new NpcResponse("FIRE_ROOKIE", "Andre", NpcType.STAFF, "이발소 직원", false, null),
            new NpcResponse("TOWN_GRANDFATHER", "Arthur", NpcType.PASSERBY, "행인", false, null),
            new NpcResponse("TOWN_GRANDMOTHER", "Margaret", NpcType.PASSERBY, "행인", false, null));
  }

  @Test
  @DisplayName("사용자가 단어장에 저장한 단어의 카드는 saved가 true이고, 나머지는 false다")
  void getScene_marksSavedWords() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    saveWord(userId, "sofa");

    SceneDetailResponse home = sceneService.getScene("HOME", userId);

    assertThat(home.words())
        .filteredOn(WordCardResponse::saved)
        .extracting(WordCardResponse::wordKey)
        .containsExactly("sofa");
  }

  @Test
  @DisplayName("사용자가 자동 표시를 끈 단어의 카드만 hoverHidden이 true이고, 다시 켰거나 설정이 없으면 false다")
  void getScene_marksHoverHiddenWords() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    setHoverHidden(userId, "tv", true);
    setHoverHidden(userId, "bed", false);

    SceneDetailResponse home = sceneService.getScene("HOME", userId);

    assertThat(home.words())
        .filteredOn(WordCardResponse::hoverHidden)
        .extracting(WordCardResponse::wordKey)
        .containsExactly("tv");
  }

  @Test
  @DisplayName("자동 표시를 끈 단어는 그 단어를 쓰는 모든 장소에서 숨김으로 보인다")
  void getScene_hiddenWordIsHiddenInEveryScene() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    // 의자는 집과 카페 모두에 있다.
    setHoverHidden(userId, "chair", true);

    SceneDetailResponse home = sceneService.getScene("HOME", userId);
    SceneDetailResponse cafe = sceneService.getScene("CAFE", userId);

    assertThat(List.of(home, cafe))
        .allSatisfy(
            scene ->
                assertThat(scene.words())
                    .filteredOn(card -> card.wordKey().equals("chair"))
                    .singleElement()
                    .extracting(WordCardResponse::hoverHidden)
                    .isEqualTo(true));
  }

  @Test
  @DisplayName("다른 사용자가 저장하거나 숨긴 단어는 내 단어 카드에 반영되지 않는다")
  void getScene_ignoresOtherUsersSettings() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    saveWord(otherUserId, "sofa");
    setHoverHidden(otherUserId, "tv", true);

    SceneDetailResponse home = sceneService.getScene("HOME", userId);

    assertThat(home.words())
        .allSatisfy(
            card -> {
              assertThat(card.saved()).isFalse();
              assertThat(card.hoverHidden()).isFalse();
            });
  }

  @Test
  @DisplayName("미리 만든 발음 파일이 있는 단어의 카드만 hasPronunciation이 true다")
  void getScene_marksWordsWithPronunciation() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    clearPronunciationAudioKeys();
    setPronunciationAudioKey("sofa", "sofa.wav");

    SceneDetailResponse home = sceneService.getScene("HOME", userId);

    assertThat(home.words())
        .filteredOn(WordCardResponse::hasPronunciation)
        .extracting(WordCardResponse::wordKey)
        .containsExactly("sofa");
  }

  @Test
  @DisplayName("호텔 상세는 교대를 나눠 맡는 호텔 직원 Jake·Owen·Clara·Walter·Diego를 돌려준다")
  void getScene_hotel_returnsHotelStaff() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    SceneDetailResponse hotel = sceneService.getScene("HOTEL", userId);

    assertThat(hotel.isOpen()).isTrue();
    assertThat(hotel.npcs())
        .containsExactly(
            new NpcResponse("HOTEL_STAFF", "Jake", NpcType.STAFF, "호텔 직원", false, null),
            new NpcResponse("HOTEL_RECEPTIONIST", "Owen", NpcType.STAFF, "호텔 직원", false, null),
            new NpcResponse(
                "HOTEL_NIGHT_MANAGER", "Clara", NpcType.STAFF, "호텔 야간 매니저", false, null),
            new NpcResponse("HOTEL_BELLHOP", "Diego", NpcType.STAFF, "벨보이", false, null));
  }

  @Test
  @DisplayName("닫힌 장소도 조회할 수 있으며 isOpen이 false다")
  void getScene_closedScene_returnsNotOpen() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    // MVP 시드에는 닫힌 장소가 없어 테스트에서 만든다.
    entityManager
        .createNativeQuery(
            "insert into scenes (code, name, unity_key, is_open)"
                + " values ('LIBRARY', '도서관', 'LibraryScene', false)")
        .executeUpdate();

    SceneDetailResponse library = sceneService.getScene("LIBRARY", userId);

    assertThat(library.sceneCode()).isEqualTo("LIBRARY");
    assertThat(library.isOpen()).isFalse();
    assertThat(library.npcs()).isEmpty();
    assertThat(library.words()).isEmpty();
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

  // 단어장 저장을 SQL로 넣어 장소 조회만 검증한다. 저장 시각은 DB 기본값으로 채운다.
  private void saveWord(Long userId, String wordKey) {
    entityManager
        .createNativeQuery(
            "insert into user_vocab (user_id, vocab_id, source_type)"
                + " select ?, id, 'OBJECT' from vocabularies where word_key = ?")
        .setParameter(1, userId)
        .setParameter(2, wordKey)
        .executeUpdate();
  }

  // 자동 표시 설정을 SQL로 넣어 장소 조회만 검증한다.
  private void setHoverHidden(Long userId, String wordKey, boolean hidden) {
    entityManager
        .createNativeQuery(
            "insert into user_word_prefs (user_id, vocab_id, hover_hidden)"
                + " select ?, id, ? from vocabularies where word_key = ?")
        .setParameter(1, userId)
        .setParameter(2, hidden)
        .setParameter(3, wordKey)
        .executeUpdate();
  }

  // 시드는 미리 만든 발음 파일이 있는 단어에 키를 넣으므로, 파일 개수와 상관없이 확인하려고 먼저 모두 비운다.
  private void clearPronunciationAudioKeys() {
    entityManager
        .createNativeQuery("update vocabularies set pronunciation_audio_key = null")
        .executeUpdate();
  }

  private void setPronunciationAudioKey(String wordKey, String audioKey) {
    entityManager
        .createNativeQuery("update vocabularies set pronunciation_audio_key = ? where word_key = ?")
        .setParameter(1, audioKey)
        .setParameter(2, wordKey)
        .executeUpdate();
  }

  private static List<String> boundWordKeys(String sceneCode) throws IOException {
    try (var input = new ClassPathResource("content/object-word-bindings.json").getInputStream()) {
      return new JsonMapper()
          .readValue(input, Bindings.class).scenes().stream()
              .filter(scene -> scene.sceneCode().equals(sceneCode))
              .flatMap(scene -> scene.wordKeys().stream())
              .toList();
    }
  }

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record Bindings(List<BindingScene> scenes) {}

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record BindingScene(String sceneCode, List<String> wordKeys) {}
}
