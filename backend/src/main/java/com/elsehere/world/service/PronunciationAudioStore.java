package com.elsehere.world.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.io.IOException;
import java.util.Optional;
import java.util.regex.Pattern;
import org.springframework.core.io.ClassPathResource;
import org.springframework.stereotype.Component;

/**
 * 미리 만든 사물 단어 발음 음성(WAV) 파일. 파일은 저장소의 {@code content/pronunciation/}에 두고 서버와 함께 배포한다.
 *
 * <p>요청할 때 음성을 만들지 않으므로 AI 워커가 꺼져 있거나 AI 사용량이 소진돼도 들을 수 있다.
 */
@Component
public class PronunciationAudioStore {

  public static final String DIRECTORY = "content/pronunciation/";

  /** 발음 키는 공용 단어 키에 .wav를 붙인 파일 이름이다. 폴더 밖 파일을 읽지 않도록 형식을 확인한다. */
  private static final Pattern KEY_PATTERN = Pattern.compile("[a-z0-9_]+\\.wav");

  public Optional<byte[]> load(String key) {
    if (key == null || !KEY_PATTERN.matcher(key).matches()) {
      return Optional.empty();
    }
    ClassPathResource file = new ClassPathResource(DIRECTORY + key);
    if (!file.exists()) {
      return Optional.empty();
    }
    try {
      return Optional.of(file.getContentAsByteArray());
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.INTERNAL_ERROR, exception);
    }
  }
}
