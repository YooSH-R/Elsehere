package com.elsehere.conversation.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Locale;
import java.util.Map;
import java.util.UUID;
import java.util.regex.Pattern;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * 학습 활용에 동의한 사용자의 녹음 파일 보관. 받은 형식 그대로 저장한다.
 *
 * <p>저장 폴더는 elsehere.training-recording.dir로 지정하며, 기본값은 NPC 음성 볼륨의 하위 폴더다. NPC 음성 정리는 볼륨 바로 아래 파일만
 * 다루므로 이 폴더는 건드리지 않는다.
 */
@Component
public class TrainingRecordingStore {

  private static final Map<String, String> EXTENSIONS =
      Map.of(
          "audio/webm", "webm",
          "audio/ogg", "ogg",
          "audio/mp4", "mp4",
          "audio/wav", "wav",
          "audio/x-wav", "wav",
          "audio/mpeg", "mp3");
  private static final Pattern KEY_PATTERN =
      Pattern.compile("[0-9a-f-]{36}\\.(webm|ogg|mp4|wav|mp3)");

  private final Path directory;

  public TrainingRecordingStore(@Value("${elsehere.training-recording.dir}") String directory) {
    this.directory = Path.of(directory);
  }

  /** "audio/webm;codecs=opus"처럼 매개변수가 붙어도 형식만 돌려준다. */
  public static String mediaType(String contentType) {
    return contentType.split(";", 2)[0].trim().toLowerCase(Locale.ROOT);
  }

  /** 녹음을 저장하고 파일 키(training_recordings.audio_key)를 돌려준다. */
  public String save(byte[] audio, String contentType) {
    String extension = EXTENSIONS.get(mediaType(contentType));
    if (extension == null) {
      throw new BusinessException(ErrorCode.AUDIO_FORMAT_UNSUPPORTED);
    }
    String key = UUID.randomUUID() + "." + extension;
    try {
      Files.createDirectories(directory);
      Files.write(directory.resolve(key), audio);
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
    return key;
  }

  /** 녹음 파일을 지운다. 이미 없으면 그대로 둔다. */
  public void delete(String key) {
    // 키는 서버가 만든 값만 받는다. 경로 조작을 막기 위해 형식을 확인한다.
    if (key == null || !KEY_PATTERN.matcher(key).matches()) {
      return;
    }
    try {
      Files.deleteIfExists(directory.resolve(key));
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
  }
}
