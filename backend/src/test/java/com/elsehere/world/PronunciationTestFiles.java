package com.elsehere.world;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.file.Files;
import java.nio.file.Path;

/** 테스트용 발음 폴더(elsehere.pronunciation.dir). 공용 단어 sofa의 발음 파일(sofa.wav)만 있다. 테스트 실행마다 한 번 만든다. */
public final class PronunciationTestFiles {

  public static final byte[] SOFA_WAV = {82, 73, 70, 70, 36, 0, 0, 0, 87, 65, 86, 69};

  private static final Path DIRECTORY = create();

  private PronunciationTestFiles() {}

  public static String directory() {
    return DIRECTORY.toString();
  }

  private static Path create() {
    try {
      Path directory = Files.createTempDirectory("pronunciation-test");
      Path sofa = Files.write(directory.resolve("sofa.wav"), SOFA_WAV);
      // 종료할 때는 등록한 반대 순서로 지우므로 폴더를 먼저 등록한다.
      directory.toFile().deleteOnExit();
      sofa.toFile().deleteOnExit();
      return directory;
    } catch (IOException exception) {
      throw new UncheckedIOException(exception);
    }
  }
}
