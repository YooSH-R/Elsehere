package com.elsehere.common.seed;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.mission.entity.Mission;
import com.elsehere.mission.repository.MissionRepository;
import com.elsehere.vocab.entity.VocabularySource;
import com.elsehere.vocab.entity.VocabularyType;
import com.elsehere.vocab.repository.VocabularyRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.NpcType;
import com.elsehere.world.entity.Scene;
import com.elsehere.world.entity.WorldObject;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.repository.SceneRepository;
import com.elsehere.world.repository.WorldObjectRepository;
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

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({TestcontainersConfiguration.class, WorldSeedLoader.class})
class WorldSeedLoaderTest {

  @Autowired private WorldSeedLoader worldSeedLoader;

  @Autowired private EntityManager entityManager;

  @Autowired private SceneRepository sceneRepository;

  @Autowired private WorldObjectRepository worldObjectRepository;

  @Autowired private VocabularyRepository vocabularyRepository;

  @Autowired private NpcRepository npcRepository;

  @Autowired private MissionRepository missionRepository;

  @Test
  @DisplayName("애플리케이션이 시작되면 따로 부르지 않아도 시드가 들어가 있다")
  void applicationStart_loadsSeed() {
    assertThat(sceneRepository.count()).isEqualTo(4);
  }

  @Test
  @DisplayName("장소 4개를 코드·이름·Unity 씬 이름과 함께 넣고, 모두 들어갈 수 있게 연다")
  void load_insertsScenes() {
    worldSeedLoader.load();

    assertThat(sceneRepository.findAll())
        .extracting(Scene::getCode, Scene::getName, Scene::getUnityKey, Scene::isOpen)
        .containsExactlyInAnyOrder(
            tuple("HOME", "집", "HomeScene", true),
            tuple("TOWN", "마을", "TownScene", true),
            tuple("CAFE", "모퉁이 카페", "CafeScene", true),
            tuple("HOTEL", "호텔", "HotelScene", true));
  }

  @Test
  @DisplayName("사물 8개를 집과 카페에 배치하고, 각 사물을 단어와 뜻이 있는 어휘에 연결한다")
  void load_insertsObjectsLinkedToVocabularies() {
    worldSeedLoader.load();

    assertThat(worldObjectRepository.findAll())
        .extracting(
            object -> object.getScene().getCode(),
            WorldObject::getUnityKey,
            object -> object.getVocabulary().getText(),
            object -> object.getVocabulary().getMeaningKo())
        .containsExactlyInAnyOrder(
            tuple("HOME", "tv", "TV", "텔레비전"),
            tuple("HOME", "refrigerator", "refrigerator", "냉장고"),
            tuple("HOME", "air_conditioner", "air conditioner", "에어컨"),
            tuple("HOME", "sofa", "sofa", "소파"),
            tuple("HOME", "bookshelf", "bookshelf", "책장"),
            tuple("HOME", "bed", "bed", "침대"),
            tuple("CAFE", "counter", "counter", "계산대"),
            tuple("CAFE", "menu", "menu", "메뉴"));
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
                    .isEqualTo(
                        word.pronunciationAudioKey().isBlank()
                            ? null
                            : word.pronunciationAudioKey());
              });
    }
  }

  @Test
  @DisplayName("NPC 3명(행인·카페 직원·호텔 직원)을 장소·이름·유형·역할 이름·먼저 말 걸기 여부·첫 말과 함께 넣는다")
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
            tuple("HOTEL", "HOTEL_STAFF", "Emma", NpcType.STAFF, "호텔 직원", false, null));
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

    assertThat(sceneRepository.count()).isEqualTo(4);
    assertThat(vocabularyRepository.count()).isEqualTo(catalog().words().size());
    assertThat(worldObjectRepository.count()).isEqualTo(8);
    assertThat(npcRepository.count()).isEqualTo(3);
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

  private Catalog catalog() throws IOException {
    try (var input = new ClassPathResource("content/object-words.json").getInputStream()) {
      return new JsonMapper().readValue(input, Catalog.class);
    }
  }

  private record Catalog(List<CatalogWord> words) {}

  private record CatalogWord(
      String key,
      String languageCode,
      String text,
      String meaningKo,
      String example,
      String exampleKo,
      String pronunciationAudioKey) {}
}
