package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.vocab.repository.VocabularyRepository;
import com.elsehere.world.PronunciationTestFiles;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.NullSource;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;

// 발음 폴더는 sofa.wav만 있는 테스트 폴더(PronunciationTestFiles)를 쓰고, 시드가 그 파일 이름을 sofa의 발음 키로 넣는다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  PronunciationService.class
})
class PronunciationServiceTest {

  @DynamicPropertySource
  static void pronunciationDirectory(DynamicPropertyRegistry registry) {
    registry.add("elsehere.pronunciation.dir", PronunciationTestFiles::directory);
  }

  @Autowired private PronunciationService pronunciationService;

  @Autowired private VocabularyRepository vocabularyRepository;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("미리 만든 발음 파일이 있는 단어는 그 WAV 파일을 그대로 돌려준다")
  void getPronunciation_returnsAudioFile() {
    Long sofaId = vocabularyRepository.findByWordKey("sofa").orElseThrow().getId();

    byte[] audio = pronunciationService.getPronunciation(sofaId);

    assertThat(audio).isEqualTo(PronunciationTestFiles.SOFA_WAV);
  }

  @Test
  @DisplayName("없는 어휘 ID면 NOT_FOUND 오류를 낸다")
  void getPronunciation_unknownVocab_throwsNotFound() {
    assertThatThrownBy(() -> pronunciationService.getPronunciation(999_999L))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @ParameterizedTest
  @NullSource
  @ValueSource(strings = {"moon_rock.wav", "../../application.properties", "sofa.mp3"})
  @DisplayName("발음 키가 없거나, 파일이 없거나, 발음 폴더의 WAV 파일 이름이 아니면 NOT_FOUND 오류를 낸다")
  void getPronunciation_unusableKey_throwsNotFound(String key) {
    Long sofaId = vocabularyRepository.findByWordKey("sofa").orElseThrow().getId();
    entityManager
        .createNativeQuery("update vocabularies set pronunciation_audio_key = ? where id = ?")
        .setParameter(1, key)
        .setParameter(2, sofaId)
        .executeUpdate();
    entityManager.clear();

    assertThatThrownBy(() -> pronunciationService.getPronunciation(sofaId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }
}
