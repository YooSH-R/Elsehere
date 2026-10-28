package com.elsehere.common.seed;

import com.elsehere.world.service.PronunciationAudioStore;
import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import java.io.IOException;
import java.io.InputStream;
import java.io.UncheckedIOException;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Map;
import javax.sql.DataSource;
import lombok.RequiredArgsConstructor;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.core.io.ClassPathResource;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.core.namedparam.NamedParameterJdbcTemplate;
import org.springframework.jdbc.datasource.init.ResourceDatabasePopulator;
import org.springframework.stereotype.Component;
import tools.jackson.databind.json.JsonMapper;

/**
 * 장소·NPC·사물·어휘·미션 시드를 애플리케이션이 시작될 때 넣는다.
 *
 * <p>시드 SQL은 고유 키가 겹치면 시드 값으로 갱신하므로 여러 번 실행해도 행이 늘지 않는다. SQL 뒤에 Unity가 내보낸 공용 단어 JSON으로 어휘의 단어 키와
 * 장소별 단어를 채우고, 미리 만든 발음 파일이 있는 단어에 발음 키를 채운다.
 *
 * <p>Spring의 SQL 초기화({@code spring.sql.init})를 쓰지 않는 이유: 그 기능은 {@code @DataJpaTest}에서도 실행되어, 테스트가
 * 직접 넣는 장소 데이터와 겹친다. 이 클래스는 빈으로 등록된 컨텍스트에서만 실행된다.
 */
@Component
@RequiredArgsConstructor
public class WorldSeedLoader implements ApplicationRunner {

  static final String SCRIPT = "db/seed/world.sql";
  static final String OBJECT_WORDS = "content/object-words.json";
  static final String OBJECT_WORD_BINDINGS = "content/object-word-bindings.json";

  private final DataSource dataSource;

  @Override
  public void run(ApplicationArguments args) {
    load();
  }

  public void load() {
    ResourceDatabasePopulator populator =
        new ResourceDatabasePopulator(new ClassPathResource(SCRIPT));
    populator.setSqlScriptEncoding(StandardCharsets.UTF_8.name());
    populator.execute(dataSource);
    JdbcTemplate jdbcTemplate = new JdbcTemplate(dataSource);
    loadWordKeys(jdbcTemplate);
    loadPronunciationAudioKeys(jdbcTemplate);
    loadSceneWords(jdbcTemplate);
  }

  /** 단어 키를 어휘(언어·철자)에 연결한다. 키가 다른 철자로 옮겨 갔으면 이전 행의 키를 먼저 비운다. */
  private static void loadWordKeys(JdbcTemplate jdbcTemplate) {
    for (ObjectWord word : read(OBJECT_WORDS, ObjectWords.class).words()) {
      jdbcTemplate.update(
          "update vocabularies set word_key = null"
              + " where word_key = ? and not (language_code = ? and text = ?)",
          word.key(),
          word.languageCode(),
          word.text());
      jdbcTemplate.update(
          "update vocabularies set word_key = ? where language_code = ? and text = ?",
          word.key(),
          word.languageCode(),
          word.text());
    }
  }

  /**
   * 미리 만든 발음 파일({@code {wordKey}.wav})이 있는 단어에 그 파일 이름을 발음 키로 넣는다. Unity 에셋에 발음 키를 적은 단어는 SQL이 이미 그
   * 값을 넣었으므로 바꾸지 않는다.
   */
  private static void loadPronunciationAudioKeys(JdbcTemplate jdbcTemplate) {
    for (ObjectWord word : read(OBJECT_WORDS, ObjectWords.class).words()) {
      String fileName = word.key() + ".wav";
      if (!new ClassPathResource(PronunciationAudioStore.DIRECTORY + fileName).exists()) {
        continue;
      }
      jdbcTemplate.update(
          "update vocabularies set pronunciation_audio_key = ?"
              + " where word_key = ? and pronunciation_audio_key is null",
          fileName,
          word.key());
    }
  }

  /**
   * 장소마다 그 장소의 사물에 붙은 단어 키를 장소별 단어로 넣는다. 이미 있는 행은 그대로 두고, 연결 목록에서 빠진 단어는 지운다. 장소별 단어를 가리키는 사용자 데이터는
   * 없으므로 지워도 학습 기록은 남는다.
   */
  private static void loadSceneWords(JdbcTemplate jdbcTemplate) {
    NamedParameterJdbcTemplate namedJdbcTemplate = new NamedParameterJdbcTemplate(jdbcTemplate);
    for (BindingScene scene : read(OBJECT_WORD_BINDINGS, Bindings.class).scenes()) {
      removeUnboundWords(namedJdbcTemplate, scene);
      for (String wordKey : scene.wordKeys()) {
        jdbcTemplate.update(
            "insert into scene_words (scene_id, vocab_id)"
                + " select scene.id, vocabulary.id from scenes scene, vocabularies vocabulary"
                + " where scene.code = ? and vocabulary.word_key = ?"
                + " on conflict (scene_id, vocab_id) do nothing",
            scene.sceneCode(),
            wordKey);
      }
    }
  }

  private static void removeUnboundWords(
      NamedParameterJdbcTemplate namedJdbcTemplate, BindingScene scene) {
    String sceneCondition = "scene_id = (select id from scenes where code = :sceneCode)";
    if (scene.wordKeys().isEmpty()) {
      namedJdbcTemplate.update(
          "delete from scene_words where " + sceneCondition,
          Map.of("sceneCode", scene.sceneCode()));
      return;
    }
    namedJdbcTemplate.update(
        "delete from scene_words where "
            + sceneCondition
            + " and vocab_id not in (select id from vocabularies where word_key in (:wordKeys))",
        Map.of("sceneCode", scene.sceneCode(), "wordKeys", scene.wordKeys()));
  }

  private static <T> T read(String path, Class<T> type) {
    try (InputStream input = new ClassPathResource(path).getInputStream()) {
      return new JsonMapper().readValue(input, type);
    } catch (IOException exception) {
      throw new UncheckedIOException(path + "을 읽지 못했습니다.", exception);
    }
  }

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record ObjectWords(List<ObjectWord> words) {}

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record ObjectWord(String key, String languageCode, String text) {}

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record Bindings(List<BindingScene> scenes) {}

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record BindingScene(String sceneCode, List<String> wordKeys) {}
}
