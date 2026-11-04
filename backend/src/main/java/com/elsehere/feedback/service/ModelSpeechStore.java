package com.elsehere.feedback.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.NoSuchFileException;
import java.nio.file.Path;
import java.nio.file.attribute.FileTime;
import java.time.Clock;
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
 * 상세 평가의 모범 음성(WAV) 파일 보관(FB-04).
 *
 * <p>NPC 음성과 같은 영구 볼륨의 하위 폴더(elsehere.model-speech.dir)에 둔다. NPC 음성 정리(NpcAudioRetention)는 NPC 음성
 * 폴더 바로 아래 파일만 보므로 모범 음성을 지우지 않는다. 모범 음성은 {@link ModelSpeechRetention}이 마지막 재생 시각(파일 수정 시각)으로 정리한다.
 */
@Component
public class ModelSpeechStore {

  private static final Pattern KEY_PATTERN = Pattern.compile("[0-9a-f-]{36}\\.wav");

  private final Path directory;
  private final Clock clock;

  public ModelSpeechStore(@Value("${elsehere.model-speech.dir}") String directory, Clock clock) {
    this.directory = Path.of(directory);
    this.clock = clock;
  }

  /** 음성을 저장하고 파일 키(turn_feedback.model_audio_key)를 돌려준다. */
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

  public boolean exists(String key) {
    return isKey(key) && Files.exists(directory.resolve(key));
  }

  /** 음성을 읽고 마지막 재생 시각(파일 수정 시각)을 지금으로 바꾼다. 보관 기간은 마지막 재생부터 센다. */
  public Optional<byte[]> load(String key) {
    if (!isKey(key)) {
      return Optional.empty();
    }
    Path file = directory.resolve(key);
    byte[] wav;
    try {
      wav = Files.readAllBytes(file);
    } catch (NoSuchFileException exception) {
      // 만든 적이 없거나 정리가 지운 파일이다. 다음 만들기 요청 때 다시 합성한다.
      return Optional.empty();
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
    markPlayed(file);
    return Optional.of(wav);
  }

  /** 내용 지우기(게스트 정리·탈퇴)에서 지운 문장 피드백의 음성 파일을 지운다. 없는 파일은 넘어간다. */
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

  /** 보관 정리(ModelSpeechRetention)가 보는 파일 목록. 파일 수정 시각을 마지막 재생 시각으로 쓴다. */
  public List<StoredSpeech> files() {
    if (!Files.isDirectory(directory)) {
      return List.of();
    }
    List<StoredSpeech> files = new ArrayList<>();
    try (Stream<Path> paths = Files.list(directory)) {
      for (Path file : (Iterable<Path>) paths::iterator) {
        String name = file.getFileName().toString();
        if (!isKey(name)) {
          continue;
        }
        try {
          files.add(
              new StoredSpeech(
                  name, Files.size(file), Files.getLastModifiedTime(file).toInstant()));
        } catch (NoSuchFileException exception) {
          // 목록을 읽은 뒤 내용 지우기(게스트 정리·탈퇴)가 지운 파일은 뺀다.
        }
      }
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
    return files;
  }

  private void markPlayed(Path file) {
    try {
      Files.setLastModifiedTime(file, FileTime.from(clock.instant()));
    } catch (NoSuchFileException exception) {
      // 읽은 뒤 정리가 지운 파일이다. 이번 재생은 이미 읽은 음성으로 한다.
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
  }

  // 키는 서버가 만든 값만 받는다. 경로 조작을 막기 위해 형식을 확인한다.
  private static boolean isKey(String key) {
    return key != null && KEY_PATTERN.matcher(key).matches();
  }

  public record StoredSpeech(String key, long size, Instant lastPlayedAt) {}
}
