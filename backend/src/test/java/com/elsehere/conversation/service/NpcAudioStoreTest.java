package com.elsehere.conversation.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.groups.Tuple.tuple;

import com.elsehere.conversation.service.NpcAudioStore.StoredAudio;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

// NPC 턴 음성 파일 보관. 테스트마다 빈 임시 폴더를 쓴다.
class NpcAudioStoreTest {

  @TempDir private Path directory;

  private NpcAudioStore store;

  @BeforeEach
  void setUp() {
    store = new NpcAudioStore(directory.toString());
  }

  @Test
  @DisplayName("음성을 지우면 목록과 사용량에서 빠지고, 없는 키나 형식이 다른 키를 지워도 오류가 나지 않는다")
  void delete_removesFileFromListAndUsage() {
    String kept = store.save(new byte[3]);
    String removed = store.save(new byte[5]);

    store.delete(removed);
    store.delete(UUID.randomUUID() + ".wav");
    store.delete("../outside.wav");

    assertThat(store.load(removed)).isEmpty();
    assertThat(store.files()).extracting(StoredAudio::key).containsExactly(kept);
    assertThat(store.usedBytes()).isEqualTo(3);
  }

  @Test
  @DisplayName("파일 목록과 사용량은 서버가 만든 UUID.wav 파일만 센다")
  void files_countOnlyServerAudioFiles() throws IOException {
    String key = store.save(new byte[4]);
    // 배포 검사가 같은 폴더에 쓰기 확인 파일을 만들 수 있다.
    Files.write(directory.resolve(".ci-write-check.abc123"), new byte[100]);
    Files.write(directory.resolve("readme.txt"), new byte[50]);

    assertThat(store.files())
        .extracting(StoredAudio::key, StoredAudio::size)
        .containsExactly(tuple(key, 4L));
    assertThat(store.usedBytes()).isEqualTo(4);
  }
}
