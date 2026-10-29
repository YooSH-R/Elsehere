package com.elsehere.world.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Optional;
import java.util.regex.Pattern;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * 사물 단어 발음 음성(WAV) 파일 보관(WORLD-04).
 *
 * <p>NPC 음성과 같은 영구 볼륨의 하위 폴더(elsehere.pronunciation.dir)에 {@code {wordKey}.wav}로 둔다. 관리자가 발음 생성
 * API로 AI 워커 TTS에서 만들며(docs/admin.md), 듣기 요청 때는 만들지 않으므로 AI 워커가 꺼져 있거나 AI 사용량이 소진돼도 들을 수 있다. NPC 음성
 * 정리(NpcAudioRetention)는 NPC 음성 폴더 바로 아래 파일만 보므로 발음 파일을 지우지 않는다.
 */
@Component
public class PronunciationAudioStore {

  /** 발음 키는 공용 단어 키에 .wav를 붙인 파일 이름이다. 폴더 밖 파일을 읽지 않도록 형식을 확인한다. */
  private static final Pattern KEY_PATTERN = Pattern.compile("[a-z0-9_]+\\.wav");

  private final Path directory;

  public PronunciationAudioStore(@Value("${elsehere.pronunciation.dir}") String directory) {
    this.directory = Path.of(directory);
  }

  /** 공용 단어 키의 발음 키(파일 이름). */
  public static String keyOf(String wordKey) {
    return wordKey + ".wav";
  }

  /** 발음을 저장한다. 같은 키의 파일이 있으면 바꾼다. */
  public void save(String key, byte[] wav) {
    if (!isKey(key)) {
      throw new IllegalArgumentException("발음 키 형식이 아닙니다: " + key);
    }
    try {
      Files.createDirectories(directory);
      Files.write(directory.resolve(key), wav);
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
  }

  public boolean exists(String key) {
    return isKey(key) && Files.exists(directory.resolve(key));
  }

  public Optional<byte[]> load(String key) {
    if (!exists(key)) {
      return Optional.empty();
    }
    try {
      return Optional.of(Files.readAllBytes(directory.resolve(key)));
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
  }

  private static boolean isKey(String key) {
    return key != null && KEY_PATTERN.matcher(key).matches();
  }
}
