package com.elsehere.common.config;

import static org.assertj.core.api.Assertions.assertThat;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.regex.Pattern;
import java.util.stream.Stream;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

/**
 * 시간 규칙을 소스에서 읽어 검사한다.
 *
 * <p>서비스 시간대는 {@link ClockConfig} 한 곳에서 정한다. 테스트는 실행 시점에 따라 결과가 바뀌지 않도록 실제 시계({@link ClockConfig})
 * 대신 고정 시계({@code TestClockConfig})를 쓴다.
 */
class ClockRuleTest {

  private static final Path MAIN_SOURCES = Path.of("src/main/java/com/elsehere");
  private static final Path TEST_SOURCES = Path.of("src/test/java/com/elsehere");

  @Test
  @DisplayName("서비스 시간대 문자열(Asia/Seoul)은 ClockConfig에만 둔다")
  void serviceZoneId_isOnlyInClockConfig() {
    assertThat(filesMatching(MAIN_SOURCES, Pattern.compile("[\"']Asia/Seoul[\"']")))
        .containsExactly("common/config/ClockConfig.java");
  }

  @Test
  @DisplayName("테스트는 실제 시계(ClockConfig)를 불러오지 않고 고정 시계를 쓴다")
  void tests_doNotImportRealClock() {
    // TestClockConfig.class는 고정 시계라 걸리지 않게 이름 앞이 끊긴 ClockConfig만 찾는다.
    assertThat(filesMatching(TEST_SOURCES, Pattern.compile("\\bClockConfig\\.class"))).isEmpty();
  }

  private static List<String> filesMatching(Path sources, Pattern pattern) {
    try (Stream<Path> files = Files.walk(sources)) {
      return files
          .filter(file -> file.toString().endsWith(".java"))
          .filter(file -> !file.endsWith("ClockRuleTest.java"))
          .filter(file -> pattern.matcher(read(file)).find())
          .map(file -> sources.relativize(file).toString().replace('\\', '/'))
          .sorted()
          .toList();
    } catch (IOException exception) {
      throw new UncheckedIOException(exception);
    }
  }

  private static String read(Path file) {
    try {
      return Files.readString(file);
    } catch (IOException exception) {
      throw new UncheckedIOException(exception);
    }
  }
}
