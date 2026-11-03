package com.elsehere.feedback.service;

import static java.time.temporal.ChronoUnit.DAYS;
import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.common.config.TestClockConfig;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.attribute.FileTime;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.stream.Stream;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

// 모범 음성 보관(FB-04): 마지막 재생 뒤 30일, 전체 상한. 파일 수정 시각을 마지막 재생 시각으로 쓴다.
class ModelSpeechRetentionTest {

  private static final Instant NOW = TestClockConfig.NOW;
  private static final long LIMIT = 1_000;

  @TempDir private Path directory;

  private final Clock clock = Clock.fixed(NOW, ZoneOffset.UTC);
  private ModelSpeechStore store;

  @BeforeEach
  void setUp() {
    store = new ModelSpeechStore(directory.toString(), clock);
  }

  @Test
  @DisplayName("정리: 마지막 재생 뒤 30일이 지난 모범 음성은 지우고 30일 안의 음성은 둔다")
  void cleanUp_deletesSpeechNotPlayedWithinRetention() throws IOException {
    speechPlayedAt(4, NOW.minus(31, DAYS));
    String recent = speechPlayedAt(4, NOW.minus(29, DAYS));

    retention(LIMIT).cleanUp();

    assertThat(savedKeys()).containsExactly(recent);
  }

  @Test
  @DisplayName("정리: 30일 안의 음성 합계가 상한을 넘으면 오래 재생하지 않은 음성부터 상한 안까지 지운다")
  void cleanUp_overLimit_deletesLeastRecentlyPlayedFirst() throws IOException {
    speechPlayedAt(4, NOW.minus(3, DAYS));
    String middle = speechPlayedAt(4, NOW.minus(2, DAYS));
    String latest = speechPlayedAt(4, NOW.minus(1, DAYS));

    retention(8).cleanUp();

    assertThat(savedKeys()).containsExactlyInAnyOrder(middle, latest);
  }

  @Test
  @DisplayName("모범 음성을 재생하면 그때부터 30일을 다시 센다")
  void load_restartsRetention() throws IOException {
    String played = speechPlayedAt(4, NOW.minus(31, DAYS));

    assertThat(store.load(played)).isPresent();
    retention(LIMIT).cleanUp();

    assertThat(savedKeys()).containsExactly(played);
  }

  private ModelSpeechRetention retention(long maxBytes) {
    return new ModelSpeechRetention(store, clock, 30, maxBytes);
  }

  private String speechPlayedAt(int bytes, Instant playedAt) throws IOException {
    String key = store.save(new byte[bytes]);
    Files.setLastModifiedTime(directory.resolve(key), FileTime.from(playedAt));
    return key;
  }

  private List<String> savedKeys() throws IOException {
    try (Stream<Path> files = Files.list(directory)) {
      return files.map(file -> file.getFileName().toString()).toList();
    }
  }
}
