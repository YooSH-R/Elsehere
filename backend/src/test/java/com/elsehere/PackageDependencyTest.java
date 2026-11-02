package com.elsehere;

import static org.assertj.core.api.Assertions.assertThat;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayDeque;
import java.util.Deque;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.TreeSet;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.stream.Stream;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

/**
 * 도메인 패키지(com.elsehere 바로 아래) 사이의 import 순환을 소스에서 읽어 검사한다.
 *
 * <p>대화 기록(conversation)은 다른 도메인이 읽는 아래쪽, 대화 흐름(dialogue)은 여러 도메인을 엮는 위쪽이라 둘 다 순환에 끼면 안 된다. common은
 * 모든 도메인이 쓰는 기반 코드와 앱 조립 설정이라 검사에서 뺀다.
 */
class PackageDependencyTest {

  private static final Path SOURCES = Path.of("src/main/java/com/elsehere");
  private static final String SHARED = "common";
  private static final Pattern IMPORT =
      Pattern.compile("^import (?:static )?com\\.elsehere\\.([a-z]+)\\.", Pattern.MULTILINE);

  @ParameterizedTest
  @ValueSource(strings = {"conversation", "dialogue"})
  @DisplayName("대화 기록·대화 흐름 패키지는 다른 도메인과 서로 import하는 순환에 끼지 않는다")
  void conversationPackages_areNotInImportCycles(String domain) {
    Map<String, Set<String>> imports = domainImports();

    assertThat(cycleMembers(imports, domain)).as(domain + "와 순환을 이루는 패키지").isEmpty();
  }

  // domain에서 출발해 닿을 수 있고, 그 패키지에서 다시 domain으로 돌아올 수 있는 패키지
  private static Set<String> cycleMembers(Map<String, Set<String>> imports, String domain) {
    Set<String> members = new TreeSet<>();
    for (String reached : reachable(imports, domain)) {
      if (!reached.equals(domain) && reachable(imports, reached).contains(domain)) {
        members.add(reached);
      }
    }
    return members;
  }

  private static Set<String> reachable(Map<String, Set<String>> imports, String start) {
    Set<String> seen = new HashSet<>();
    Deque<String> queue = new ArrayDeque<>(imports.getOrDefault(start, Set.of()));
    while (!queue.isEmpty()) {
      String next = queue.poll();
      if (seen.add(next)) {
        queue.addAll(imports.getOrDefault(next, Set.of()));
      }
    }
    return seen;
  }

  // 도메인 패키지마다 소스가 import하는 다른 도메인 패키지
  private static Map<String, Set<String>> domainImports() {
    Map<String, Set<String>> imports = new TreeMap<>();
    try (Stream<Path> files = Files.walk(SOURCES)) {
      files
          .filter(file -> file.toString().endsWith(".java"))
          .forEach(
              file -> {
                String domain = SOURCES.relativize(file).getName(0).toString();
                if (domain.endsWith(".java") || domain.equals(SHARED)) {
                  return;
                }
                Set<String> targets = imports.computeIfAbsent(domain, key -> new TreeSet<>());
                Matcher matcher = IMPORT.matcher(read(file));
                while (matcher.find()) {
                  String target = matcher.group(1);
                  if (!target.equals(domain) && !target.equals(SHARED)) {
                    targets.add(target);
                  }
                }
              });
    } catch (IOException exception) {
      throw new UncheckedIOException(exception);
    }
    return imports;
  }

  private static String read(Path file) {
    try {
      return Files.readString(file);
    } catch (IOException exception) {
      throw new UncheckedIOException(exception);
    }
  }
}
