package com.elsehere.conversation.service;

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
 * NPC 턴 음성(WAV) 파일 보관. 워커는 음성을 10분만 들고 있으므로 받은 즉시 이 폴더에 저장한다.
 *
 * <p>보관 위치·기간은 [미정]이라 서버 폴더에 둔다. 배포에서는 컨테이너 밖 볼륨으로 연결해야 재시작 뒤에도 남는다.
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
    // 키는 서버가 만든 값만 받는다. 경로 조작을 막기 위해 형식을 확인한다.
    if (key == null || !KEY_PATTERN.matcher(key).matches()) {
      return Optional.empty();
    }
    Path file = directory.resolve(key);
    if (!Files.exists(file)) {
      return Optional.empty();
    }
    try {
      return Optional.of(Files.readAllBytes(file));
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
  }
}
