package com.elsehere.common.logging;

import static org.assertj.core.api.Assertions.assertThat;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.stream.Stream;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

/**
 * 백엔드 로그 메시지가 관측성 문서(docs/observability.md)의 `이벤트명 키=값` 형식인지 소스에서 읽어 검사한다.
 *
 * <p>이벤트명과 키는 영문 소문자 snake_case다. 같은 사용자·대화를 `user_id=`·`conversation_id=`처럼 한 이름으로 찾고, 이벤트 이름으로 세기
 * 위해서다. 메시지를 `+`로 이어 쓴 경우도 이어 붙여 검사한다.
 */
class LogMessageFormatTest {

  private static final Path SOURCES = Path.of("src/main/java/com/elsehere");
  private static final Pattern LOG_CALL =
      Pattern.compile(
          "\\blog\\.(?:trace|debug|info|warn|error)\\(\\s*((?:\"[^\"]*\"\\s*\\+?\\s*)+)");
  private static final Pattern STRING = Pattern.compile("\"([^\"]*)\"");
  private static final Pattern EVENT_FORMAT =
      Pattern.compile("[a-z][a-z0-9_]*( [a-z][a-z0-9_]*=\\{\\})*");

  @Test
  @DisplayName("로그 메시지는 모두 `이벤트명 키=값` 형식이고 이벤트명·키는 영문 소문자 snake_case다")
  void logMessages_useEventFormat() {
    List<LogMessage> messages = logMessages();

    assertThat(messages).isNotEmpty();
    assertThat(messages)
        .filteredOn(message -> !EVENT_FORMAT.matcher(message.text()).matches())
        .as("`이벤트명 키=값` 형식이 아닌 로그")
        .isEmpty();
  }

  private record LogMessage(String location, String text) {}

  private static List<LogMessage> logMessages() {
    List<LogMessage> messages = new ArrayList<>();
    try (Stream<Path> files = Files.walk(SOURCES)) {
      files
          .filter(file -> file.toString().endsWith(".java"))
          .forEach(
              file -> {
                String source = read(file);
                Matcher call = LOG_CALL.matcher(source);
                while (call.find()) {
                  int line = (int) source.substring(0, call.start()).lines().count();
                  messages.add(
                      new LogMessage(SOURCES.relativize(file) + ":" + line, joined(call.group(1))));
                }
              });
    } catch (IOException exception) {
      throw new UncheckedIOException(exception);
    }
    return messages;
  }

  // "a " + "b" 처럼 이어 쓴 문자열을 하나로 붙인다.
  private static String joined(String literals) {
    StringBuilder text = new StringBuilder();
    Matcher string = STRING.matcher(literals);
    while (string.find()) {
      text.append(string.group(1));
    }
    return text.toString();
  }

  private static String read(Path file) {
    try {
      return Files.readString(file);
    } catch (IOException exception) {
      throw new UncheckedIOException(exception);
    }
  }
}
