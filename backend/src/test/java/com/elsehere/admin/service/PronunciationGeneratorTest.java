package com.elsehere.admin.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.argThat;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.admin.dto.PronunciationProgress;
import com.elsehere.admin.dto.PronunciationRun;
import com.elsehere.ai.client.AiGateway;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.repository.VocabularyRepository;
import com.elsehere.world.service.PronunciationAudioStore;
import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.stream.Stream;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import org.springframework.test.context.bean.override.mockito.MockitoBean;

// AI 워커는 Mock이고, 발음 파일은 테스트마다 비우는 임시 폴더에 저장한다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  PronunciationGenerator.class
})
@MockitoBean(types = AiGateway.class)
class PronunciationGeneratorTest {

  private static final Path PRONUNCIATION_DIR = createDirectory();
  private static final byte[] WAV = {82, 73, 70, 70};
  private static final Long ADMIN_ID = 1L;

  @DynamicPropertySource
  static void pronunciationDirectory(DynamicPropertyRegistry registry) {
    registry.add("elsehere.pronunciation.dir", PRONUNCIATION_DIR::toString);
  }

  @Autowired private PronunciationGenerator pronunciationGenerator;

  @Autowired private AiGateway aiGateway;

  @Autowired private PronunciationAudioStore pronunciationAudioStore;

  @Autowired private VocabularyRepository vocabularyRepository;

  private int objectWordCount;

  @BeforeEach
  void setUp() throws IOException {
    try (Stream<Path> files = Files.list(PRONUNCIATION_DIR)) {
      for (Path file : files.toList()) {
        Files.delete(file);
      }
    }
    objectWordCount = vocabularyRepository.findAllByWordKeyIsNotNullOrderByIdAsc().size();
  }

  @Test
  @DisplayName("발음 파일이 없는 사물 단어마다 영어 표현을 고정 목소리로 합성해 {wordKey}.wav로 저장하고 발음 키를 넣는다")
  void generateMissing_createsPronunciationForEachObjectWord() {
    given(aiGateway.synthesize(any(), anyString(), eq("af_heart"), eq(1.0))).willReturn(WAV);

    PronunciationRun run = pronunciationGenerator.generateMissing(ADMIN_ID);

    assertThat(objectWordCount).isPositive();
    assertThat(run.created()).isEqualTo(objectWordCount);
    assertThat(run.failedWordKeys()).isEmpty();
    then(aiGateway).should().synthesize(any(), eq("air conditioner"), eq("af_heart"), eq(1.0));
    assertThat(pronunciationAudioStore.load("air_conditioner.wav")).contains(WAV);
    assertThat(vocabularyRepository.findByWordKey("air_conditioner"))
        .get()
        .extracting(Vocabulary::getPronunciationAudioKey)
        .isEqualTo("air_conditioner.wav");
  }

  @Test
  @DisplayName("이미 발음 파일이 있는 단어는 다시 합성하지 않고 발음 키만 넣는다")
  void generateMissing_skipsWordWithFile() {
    pronunciationAudioStore.save("sofa.wav", WAV);
    given(aiGateway.synthesize(any(), anyString(), anyString(), eq(1.0))).willReturn(WAV);

    PronunciationRun run = pronunciationGenerator.generateMissing(ADMIN_ID);

    assertThat(run.created()).isEqualTo(objectWordCount - 1);
    then(aiGateway).should(never()).synthesize(any(), eq("sofa"), anyString(), eq(1.0));
    assertThat(vocabularyRepository.findByWordKey("sofa"))
        .get()
        .extracting(Vocabulary::getPronunciationAudioKey)
        .isEqualTo("sofa.wav");
  }

  @Test
  @DisplayName("합성에 실패한 단어는 실패 목록에 넣고 건너뛰며, 나머지 단어는 계속 만든다")
  void generateMissing_failedWord_isReportedAndOthersContinue() {
    given(aiGateway.synthesize(any(), anyString(), anyString(), eq(1.0))).willReturn(WAV);
    given(aiGateway.synthesize(any(), eq("sofa"), anyString(), eq(1.0)))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    PronunciationRun run = pronunciationGenerator.generateMissing(ADMIN_ID);

    assertThat(run.failedWordKeys()).containsExactly("sofa");
    assertThat(run.created()).isEqualTo(objectWordCount - 1);
    assertThat(pronunciationAudioStore.exists("sofa.wav")).isFalse();
    assertThat(vocabularyRepository.findByWordKey("sofa"))
        .get()
        .extracting(Vocabulary::getPronunciationAudioKey)
        .isNull();
  }

  @Test
  @DisplayName("합성할 때마다 실행한 관리자를 AI 호출의 사용자로 넘긴다. 대화와는 관계없다")
  void generateMissing_tracesAdmin() {
    given(aiGateway.synthesize(any(), anyString(), anyString(), eq(1.0))).willReturn(WAV);

    PronunciationRun run = pronunciationGenerator.generateMissing(ADMIN_ID);

    then(aiGateway)
        .should(times(run.created()))
        .synthesize(
            argThat(trace -> ADMIN_ID.equals(trace.userId()) && trace.conversationId() == null),
            anyString(),
            anyString(),
            eq(1.0));
  }

  @Test
  @DisplayName("진행 상황은 사물 단어 수와 그중 발음 파일이 있는 단어 수다")
  void progress_countsObjectWordsAndReadyFiles() {
    pronunciationAudioStore.save("sofa.wav", WAV);

    PronunciationProgress progress = pronunciationGenerator.progress();

    assertThat(progress.total()).isEqualTo(objectWordCount);
    assertThat(progress.ready()).isEqualTo(1);
  }

  private static Path createDirectory() {
    try {
      Path directory = Files.createTempDirectory("pronunciation-generator-test");
      directory.toFile().deleteOnExit();
      return directory;
    } catch (IOException exception) {
      throw new UncheckedIOException(exception);
    }
  }
}
