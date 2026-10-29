package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.NullSource;
import org.junit.jupiter.params.provider.ValueSource;

class PronunciationAudioStoreTest {

  private static final byte[] WAV = {82, 73, 70, 70};

  @TempDir private Path volume;

  @Test
  @DisplayName("발음 키는 공용 단어 키에 .wav를 붙인 파일 이름이다")
  void keyOf_appendsWav() {
    assertThat(PronunciationAudioStore.keyOf("air_conditioner")).isEqualTo("air_conditioner.wav");
  }

  @Test
  @DisplayName("발음을 저장하면 폴더가 없어도 만들고, 같은 키로 다시 읽을 수 있다")
  void save_thenLoad_returnsSameAudio() {
    PronunciationAudioStore store =
        new PronunciationAudioStore(volume.resolve("pronunciation").toString());

    store.save("sofa.wav", WAV);

    assertThat(store.exists("sofa.wav")).isTrue();
    assertThat(store.load("sofa.wav")).contains(WAV);
    assertThat(volume.resolve("pronunciation/sofa.wav")).exists();
  }

  @Test
  @DisplayName("저장한 적 없는 발음은 없음으로 본다")
  void missingFile_isEmpty() {
    PronunciationAudioStore store = new PronunciationAudioStore(volume.toString());

    assertThat(store.exists("sofa.wav")).isFalse();
    assertThat(store.load("sofa.wav")).isEmpty();
  }

  @ParameterizedTest
  @NullSource
  @ValueSource(strings = {"../secret.wav", "sofa.mp3", "Sofa.wav"})
  @DisplayName("발음 폴더의 WAV 파일 이름 형식이 아닌 키는 읽지 않는다")
  void invalidKey_isNotRead(String key) throws Exception {
    Files.write(volume.resolve("secret.wav"), WAV);
    PronunciationAudioStore store =
        new PronunciationAudioStore(volume.resolve("pronunciation").toString());

    assertThat(store.exists(key)).isFalse();
    assertThat(store.load(key)).isEmpty();
  }

  @Test
  @DisplayName("발음 폴더의 WAV 파일 이름 형식이 아닌 키로는 저장하지 않는다")
  void save_invalidKey_throws() {
    PronunciationAudioStore store = new PronunciationAudioStore(volume.toString());

    assertThatThrownBy(() -> store.save("../secret.wav", WAV))
        .isInstanceOf(IllegalArgumentException.class);
    assertThat(volume.getParent().resolve("secret.wav")).doesNotExist();
  }
}
