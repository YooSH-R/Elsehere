package com.elsehere.common.seed;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.mission.entity.Mission;
import com.elsehere.mission.repository.MissionRepository;
import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.entity.VocabularySource;
import com.elsehere.vocab.entity.VocabularyType;
import com.elsehere.vocab.repository.VocabularyRepository;
import com.elsehere.world.PronunciationTestFiles;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.NpcType;
import com.elsehere.world.entity.Scene;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.repository.SceneRepository;
import com.elsehere.world.repository.SceneWordRepository;
import com.elsehere.world.service.PronunciationAudioStore;
import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import jakarta.persistence.EntityManager;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import org.assertj.core.groups.Tuple;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.core.io.ClassPathResource;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import tools.jackson.databind.json.JsonMapper;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({TestcontainersConfiguration.class, WorldSeedLoader.class, PronunciationAudioStore.class})
class WorldSeedLoaderTest {

  // 발음 폴더에는 sofa.wav만 있다.
  @DynamicPropertySource
  static void pronunciationDirectory(DynamicPropertyRegistry registry) {
    registry.add("elsehere.pronunciation.dir", PronunciationTestFiles::directory);
  }

  @Autowired private WorldSeedLoader worldSeedLoader;

  @Autowired private EntityManager entityManager;

  @Autowired private SceneRepository sceneRepository;

  @Autowired private SceneWordRepository sceneWordRepository;

  @Autowired private VocabularyRepository vocabularyRepository;

  @Autowired private NpcRepository npcRepository;

  @Autowired private MissionRepository missionRepository;

  @Test
  @DisplayName("애플리케이션이 시작되면 따로 부르지 않아도 시드가 들어가 있다")
  void applicationStart_loadsSeed() {
    assertThat(sceneRepository.count()).isEqualTo(5);
  }

  @Test
  @DisplayName("편의점 포함 장소 5개를 코드·이름·Unity 씬 이름과 함께 넣고, 모두 들어갈 수 있게 연다")
  void load_insertsScenes() {
    worldSeedLoader.load();

    assertThat(sceneRepository.findAll())
        .extracting(Scene::getCode, Scene::getName, Scene::getUnityKey, Scene::isOpen)
        .containsExactlyInAnyOrder(
            tuple("HOME", "집", "HomeScene", true),
            tuple("TOWN", "마을", "TownScene", true),
            tuple("CAFE", "모퉁이 카페", "CafeScene", true),
            tuple("HOTEL", "호텔", "HotelScene", true),
            tuple("SHOP", "편의점", "ShopScene", true));
  }

  @Test
  @DisplayName("장소마다 Unity가 내보낸 공용 단어 목록(object-word-bindings.json)을 장소별 단어로 넣는다")
  void load_insertsSceneWordsFromBindings() throws IOException {
    worldSeedLoader.load();

    assertThat(sceneWordRepository.findAll())
        .extracting(
            sceneWord -> sceneWord.getScene().getCode(),
            sceneWord -> sceneWord.getVocabulary().getWordKey())
        .containsExactlyInAnyOrderElementsOf(expectedSceneWords());
  }

  @Test
  @DisplayName("Unity 연결 목록에서 빠진 장소별 단어는 다시 실행할 때 지운다")
  void load_removesSceneWordsMissingFromBindings() throws IOException {
    worldSeedLoader.load();
    // 집에는 벤치가 없다. Unity에서 집의 벤치를 지운 상황을 만든다.
    entityManager
        .createNativeQuery(
            "insert into scene_words (scene_id, vocab_id)"
                + " select scene.id, vocabulary.id from scenes scene, vocabularies vocabulary"
                + " where scene.code = 'HOME' and vocabulary.word_key = 'bench'")
        .executeUpdate();

    worldSeedLoader.load();
    entityManager.clear();

    assertThat(sceneWordRepository.findAll())
        .extracting(
            sceneWord -> sceneWord.getScene().getCode(),
            sceneWord -> sceneWord.getVocabulary().getWordKey())
        .containsExactlyInAnyOrderElementsOf(expectedSceneWords());
  }

  @Test
  @DisplayName("사물 어휘는 미리 등록한 영어 단어이며 예문과 예문 뜻을 가진다")
  void load_insertsMasterWordsWithExamples() throws IOException {
    worldSeedLoader.load();
    var expectedWords = catalog().words();
    var actualWords = vocabularyRepository.findAll();

    assertThat(actualWords)
        .hasSize(expectedWords.size())
        .allSatisfy(
            vocabulary -> {
              assertThat(vocabulary.getLanguageCode()).isEqualTo("en-US");
              assertThat(vocabulary.getType()).isEqualTo(VocabularyType.WORD);
              assertThat(vocabulary.getSource()).isEqualTo(VocabularySource.MASTER);
              assertThat(vocabulary.getExample()).isNotBlank();
              assertThat(vocabulary.getExampleKo()).isNotBlank();
            });
    for (CatalogWord word : expectedWords) {
      assertThat(actualWords)
          .filteredOn(
              vocabulary ->
                  vocabulary.getLanguageCode().equals(word.languageCode())
                      && vocabulary.getText().equals(word.text()))
          .singleElement()
          .satisfies(
              vocabulary -> {
                assertThat(vocabulary.getMeaningKo()).isEqualTo(word.meaningKo());
                assertThat(vocabulary.getExample()).isEqualTo(word.example());
                assertThat(vocabulary.getExampleKo()).isEqualTo(word.exampleKo());
                assertThat(vocabulary.getPronunciationAudioKey())
                    .isEqualTo(expectedPronunciationAudioKey(word));
              });
    }
  }

  @Test
  @DisplayName("Unity에 발음 키가 없어도 발음 폴더에 발음 파일({wordKey}.wav)이 있으면 그 파일 이름을 발음 키로 넣는다")
  void load_setsPronunciationKeyFromAudioFile() {
    worldSeedLoader.load();

    assertThat(vocabularyRepository.findByWordKey("sofa"))
        .get()
        .extracting(Vocabulary::getPronunciationAudioKey)
        .isEqualTo("sofa.wav");
  }

  @Test
  @DisplayName("사물 어휘마다 Unity 공용 단어 키(object-words.json의 key)를 넣는다")
  void load_setsWordKeysFromCatalog() throws IOException {
    worldSeedLoader.load();
    var actualWords = vocabularyRepository.findAll();

    for (CatalogWord word : catalog().words()) {
      assertThat(actualWords)
          .filteredOn(vocabulary -> word.key().equals(vocabulary.getWordKey()))
          .singleElement()
          .satisfies(
              vocabulary -> {
                assertThat(vocabulary.getLanguageCode()).isEqualTo(word.languageCode());
                assertThat(vocabulary.getText()).isEqualTo(word.text());
              });
    }
  }

  @Test
  @DisplayName("NPC 4명(행인·카페 직원·호텔 직원·편의점 직원)을 장소·이름·유형·역할 이름·먼저 말 걸기 여부·첫 말과 함께 넣는다")
  void load_insertsNpcs() {
    worldSeedLoader.load();

    assertThat(npcRepository.findAll())
        .extracting(
            npc -> npc.getScene().getCode(),
            Npc::getCode,
            Npc::getName,
            Npc::getNpcType,
            Npc::getRoleLabel,
            Npc::isCanInitiate,
            Npc::getApproachLine)
        .containsExactlyInAnyOrder(
            tuple(
                "TOWN",
                "TOWN_PASSERBY",
                "Ben",
                NpcType.PASSERBY,
                "행인",
                true,
                "Hi there! Nice weather today, isn't it?"),
            tuple("CAFE", "CAFE_STAFF", "Mia", NpcType.STAFF, "카페 직원", false, null),
            tuple("HOTEL", "HOTEL_STAFF", "Emma", NpcType.STAFF, "호텔 직원", false, null),
            tuple("SHOP", "SHOP_STAFF", "Alex", NpcType.STAFF, "편의점 직원", false, null));
  }

  @Test
  @DisplayName("미션 2개를 코드와 함께 카페 직원·호텔 직원에게 하나씩 연결하고 완료 키워드를 넣는다")
  void load_insertsMissions() {
    worldSeedLoader.load();

    assertThat(missionRepository.findAll())
        .extracting(
            Mission::getCode,
            mission -> mission.getNpc().getCode(),
            Mission::getTitle,
            Mission::getKeywords)
        .containsExactlyInAnyOrder(
            tuple("CAFE_SOY_LATTE", "CAFE_STAFF", "나만의 카페라테 주문하기", "[\"soy\", \"latte\"]"),
            tuple("HOTEL_CHECK_IN", "HOTEL_STAFF", "호텔에 체크인하기", "[\"check in\"]"));
  }

  @Test
  @DisplayName("서버를 다시 시작해 두 번 실행해도 행이 늘지 않는다")
  void loadTwice_doesNotDuplicateRows() throws IOException {
    worldSeedLoader.load();
    worldSeedLoader.load();

    assertThat(sceneRepository.count()).isEqualTo(5);
    assertThat(vocabularyRepository.count()).isEqualTo(catalog().words().size());
    assertThat(sceneWordRepository.count()).isEqualTo(expectedSceneWords().size());
    assertThat(npcRepository.count()).isEqualTo(4);
    assertThat(missionRepository.count()).isEqualTo(2);
  }

  @Test
  @DisplayName("이미 있는 행의 값이 시드와 다르면 다시 실행할 때 시드 값으로 갱신한다")
  void load_updatesExistingRowsToSeedValues() {
    worldSeedLoader.load();
    entityManager
        .createNativeQuery("update scenes set name = '옛 카페', is_open = false where code = 'CAFE'")
        .executeUpdate();

    worldSeedLoader.load();
    entityManager.clear();

    assertThat(sceneRepository.findAll())
        .filteredOn(scene -> scene.getCode().equals("CAFE"))
        .singleElement()
        .satisfies(
            cafe -> {
              assertThat(cafe.getName()).isEqualTo("모퉁이 카페");
              assertThat(cafe.isOpen()).isTrue();
            });
  }

  @Test
  @DisplayName("NPC 페르소나는 처음 넣을 때만 시드 값을 쓰고, 다시 실행해도 관리자가 고친 값을 유지한다")
  void load_keepsPersonaEditedByAdmin() {
    worldSeedLoader.load();
    entityManager
        .createNativeQuery(
            "update npcs set persona = '{\"role\": \"edited role\", \"facts\": \"\", \"scene\": \"\"}'"
                + " where code = 'CAFE_STAFF'")
        .executeUpdate();

    worldSeedLoader.load();
    entityManager.clear();

    assertThat(npcRepository.findByCode("CAFE_STAFF"))
        .get()
        .extracting(Npc::getPersona)
        .asString()
        .contains("edited role");
  }

  private Catalog catalog() throws IOException {
    try (var input = new ClassPathResource("content/object-words.json").getInputStream()) {
      return new JsonMapper().readValue(input, Catalog.class);
    }
  }

  /** Unity에 적은 발음 키가 우선이고, 없으면 발음 폴더에 발음 파일이 있을 때 그 파일 이름이다. */
  private static String expectedPronunciationAudioKey(CatalogWord word) {
    if (!word.pronunciationAudioKey().isBlank()) {
      return word.pronunciationAudioKey();
    }
    String fileName = word.key() + ".wav";
    return Files.exists(Path.of(PronunciationTestFiles.directory(), fileName)) ? fileName : null;
  }

  private List<Tuple> expectedSceneWords() throws IOException {
    try (var input = new ClassPathResource("content/object-word-bindings.json").getInputStream()) {
      return new JsonMapper()
          .readValue(input, Bindings.class).scenes().stream()
              .flatMap(scene -> scene.wordKeys().stream().map(key -> tuple(scene.sceneCode(), key)))
              .toList();
    }
  }

  private record Catalog(List<CatalogWord> words) {}

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record Bindings(List<BindingScene> scenes) {}

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record BindingScene(String sceneCode, List<String> wordKeys) {}

  private record CatalogWord(
      String key,
      String languageCode,
      String text,
      String meaningKo,
      String example,
      String exampleKo,
      String pronunciationAudioKey) {}
}
