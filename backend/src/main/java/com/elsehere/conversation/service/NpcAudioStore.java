package com.elsehere.conversation.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.NoSuchFileException;
import java.nio.file.Path;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import java.util.regex.Pattern;
import java.util.stream.Stream;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * NPC 턴 음성(WAV) 파일 보관. 워커는 음성을 10분만 들고 있으므로 받은 즉시 이 폴더에 저장한다.
 *
 * <p>저장 폴더는 elsehere.npc-audio.dir로 지정하며, 앱 배포에서는 환경별 영구 볼륨에 연결한다. 파일 읽기·쓰기만 맡고, 보관 기간과 용량 상한은
 * {@link NpcAudioRetention}이 매일 정리에서 맞춘다. 새 음성은 크기 검사와 실패 처리를 위해 NpcAudioRetention으로 저장한다.
 */
@Component
public class NpcAudioStore {

  private static final Pattern KEY_PATTERN = Pattern.compile("[0-9a-f-]{36}\\.wav");

  private final Path directory;

  public NpcAudioStore(@Value("${elsehere.npc-audio.dir}") String directory) {
    this.directory = Path.of(directory);
  }

  /** 음성을 저장하고 파일 키(turns.npc_audio_key)를 돌려준다. */
  public String save(byte[] wav) {
    String key = UUID.randomUUID() + ".wav";
    try {
      Files.createDirectories(directory);
      Files.write(directory.resolve(key), wav);
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
    return key;
  }

  public Optional<byte[]> load(String key) {
    if (!isKey(key)) {
      return Optional.empty();
    }
    // 재생은 잠그지 않으므로, 확인한 뒤 정리가 지울 수 있다. 읽다가 없으면 없는 파일로 본다.
    try {
      return Optional.of(Files.readAllBytes(directory.resolve(key)));
    } catch (NoSuchFileException exception) {
      return Optional.empty();
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
  }

  /** 음성 파일을 지운다. 이미 없으면 그대로 둔다. */
  public void delete(String key) {
    if (!isKey(key)) {
      return;
    }
    try {
      Files.deleteIfExists(directory.resolve(key));
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
  }

  /** 서버가 만든 음성 파일 목록. 배포 검사용 파일처럼 이름이 키 형식이 아닌 파일은 뺀다. 폴더가 없으면 빈 목록이다. */
  public List<StoredAudio> files() {
    if (!Files.isDirectory(directory)) {
      return List.of();
    }
    List<StoredAudio> files = new ArrayList<>();
    try (Stream<Path> paths = Files.list(directory)) {
      for (Path file : (Iterable<Path>) paths::iterator) {
        String name = file.getFileName().toString();
        if (!isKey(name)) {
          continue;
        }
        try {
          files.add(
              new StoredAudio(name, Files.size(file), Files.getLastModifiedTime(file).toInstant()));
        } catch (NoSuchFileException exception) {
          // 목록을 읽은 뒤 재생(만료 음성)이 지운 파일은 뺀다.
        }
      }
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
    return files;
  }

  public long usedBytes() {
    return files().stream().mapToLong(StoredAudio::size).sum();
  }

  // 키는 서버가 만든 값만 받는다. 경로 조작을 막기 위해 형식을 확인한다.
  private static boolean isKey(String key) {
    return key != null && KEY_PATTERN.matcher(key).matches();
  }

  /**
   * 폴더에 저장된 음성 파일 하나.
   *
   * @param modifiedAt 파일을 마지막으로 쓴 시각
   */
  public record StoredAudio(String key, long size, Instant modifiedAt) {}
}
