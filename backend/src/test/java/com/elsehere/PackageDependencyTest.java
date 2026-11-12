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
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

/**
 * 패키지 사이의 import 순환을 소스에서 읽어 검사한다.
 *
 * <p>도메인 패키지(com.elsehere 바로 아래): 대화 기록(conversation)은 다른 도메인이 읽는 아래쪽, 대화 흐름(dialogue)은 여러 도메인을 엮는
 * 위쪽이라 둘 다 순환에 끼면 안 된다. 이 검사에서는 모든 도메인이 쓰는 common을 뺀다.
 *
 * <p>계층 패키지(auth.service·common.config처럼 도메인 아래 패키지): common까지 넣어 순환이 없어야 한다. common이 도메인을 거꾸로 참조하면
 * common을 쓰는 모든 패키지가 순환에 묶인다. 응답 DTO가 서비스 타입을 담으면 dto와 service가 서로 참조하므로, 응답은 자기 칸 타입을 두고 서비스·컨트롤러가
 * 옮겨 담는다.
 *
 * <p>AI 워커는 창구(ai.service.AiGateway)로만 부른다. 워커 클라이언트(ai.client)와 호출 기록기는 다른 패키지에서 보일 수 있어, 창구 말고 쓰는
 * 곳이 없는지 이름으로 검사한다.
 */
class PackageDependencyTest {

  private static final Path SOURCES = Path.of("src/main/java/com/elsehere");
  private static final String SHARED = "common";
  private static final Pattern IMPORT =
      Pattern.compile("^import (?:static )?com\\.elsehere\\.([a-z]+)\\.", Pattern.MULTILINE);
  // import한 클래스의 패키지(com.elsehere 뒤, 클래스 이름 앞)
  private static final Pattern LAYER_IMPORT =
      Pattern.compile(
          "^import (?:static )?com\\.elsehere\\.([a-z][a-z0-9]*(?:\\.[a-z][a-z0-9]*)*)\\.[A-Z]",
          Pattern.MULTILINE);

  @Test
  @DisplayName("AI 워커 클라이언트와 호출 기록기는 AI 창구(AiGateway)만 쓴다. 모든 워커 호출이 창구를 거쳐 기록되게 한다")
  void workerClientAndRecorder_areUsedOnlyByTheGateway() {
    assertThat(codeUsersOf("AiWorkerClient", "AiRequestRecorder"))
        .as("AI 워커 클라이언트·호출 기록기를 쓰는 클래스")
        .containsOnly("AiGateway");
  }

  @Test
  @DisplayName("계층 패키지 사이에 import 순환이 없다. common도 검사한다")
  void layerPackages_haveNoImportCycles() {
    assertThat(layerCycles()).as("서로 import하는 계층 패키지 묶음").isEmpty();
  }

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

  // 주석이 아닌 코드에 names 중 하나가 나오는 클래스. 그 이름의 클래스 자신은 뺀다.
  private static Set<String> codeUsersOf(String... names) {
    Pattern name = Pattern.compile("\\b(?:" + String.join("|", names) + ")\\b");
    Set<String> excluded = Set.of(names);
    Set<String> users = new TreeSet<>();
    try (Stream<Path> files = Files.walk(SOURCES)) {
      files
          .filter(file -> file.toString().endsWith(".java"))
          .forEach(
              file -> {
                String className = file.getFileName().toString().replace(".java", "");
                boolean uses =
                    !excluded.contains(className)
                        && read(file)
                            .lines()
                            .map(String::strip)
                            .filter(line -> !line.startsWith("*") && !line.startsWith("/"))
                            .anyMatch(line -> name.matcher(line).find());
                if (uses) {
                  users.add(className);
                }
              });
    } catch (IOException exception) {
      throw new UncheckedIOException(exception);
    }
    return users;
  }

  // 서로 오갈 수 있는 계층 패키지 묶음. 두 개 이상 묶인 것만 순환이다.
  private static Set<Set<String>> layerCycles() {
    Map<String, Set<String>> imports = layerImports();
    Set<Set<String>> cycles = new HashSet<>();
    for (String layer : imports.keySet()) {
      Set<String> cycle = new TreeSet<>(cycleMembers(imports, layer));
      if (!cycle.isEmpty()) {
        cycle.add(layer);
        cycles.add(cycle);
      }
    }
    return cycles;
  }

  // 계층 패키지마다 소스가 import하는 다른 계층 패키지. com.elsehere 바로 아래 파일(앱 시작 클래스)은 뺀다.
  private static Map<String, Set<String>> layerImports() {
    Map<String, Set<String>> imports = new TreeMap<>();
    try (Stream<Path> files = Files.walk(SOURCES)) {
      files
          .filter(file -> file.toString().endsWith(".java"))
          .forEach(
              file -> {
                Path folder = SOURCES.relativize(file).getParent();
                if (folder == null) {
                  return;
                }
                String layer =
                    folder.toString().replace(folder.getFileSystem().getSeparator(), ".");
                Set<String> targets = imports.computeIfAbsent(layer, key -> new TreeSet<>());
                Matcher matcher = LAYER_IMPORT.matcher(read(file));
                while (matcher.find()) {
                  String target = matcher.group(1);
                  if (!target.equals(layer)) {
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
