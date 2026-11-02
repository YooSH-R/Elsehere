package com.elsehere.feedback.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Optional;
import java.util.UUID;
import java.util.regex.Pattern;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * 상세 평가의 모범 음성(WAV) 파일 보관(FB-04).
 *
 * <p>NPC 음성과 같은 영구 볼륨의 하위 폴더(elsehere.model-speech.dir)에 둔다. NPC 음성 정리(NpcAudioRetention)는 NPC 음성
 * 폴더 바로 아래 파일만 보므로 모범 음성을 지우지 않는다.
 */
@Component
public class ModelSpeechStore {

  private static final Pattern KEY_PATTERN = Pattern.compile("[0-9a-f-]{36}\\.wav");

  private final Path directory;

  public ModelSpeechStore(@Value("${elsehere.model-speech.dir}") String directory) {
    this.directory = Path.of(directory);
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

  // 키는 서버가 만든 값만 받는다. 경로 조작을 막기 위해 형식을 확인한다.
  private static boolean isKey(String key) {
    return key != null && KEY_PATTERN.matcher(key).matches();
  }
}
