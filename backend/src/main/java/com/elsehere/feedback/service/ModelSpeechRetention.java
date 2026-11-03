package com.elsehere.feedback.service;

import com.elsehere.common.config.ClockConfig;
import com.elsehere.feedback.service.ModelSpeechStore.StoredSpeech;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;

/**
 * 모범 음성(FB-04) 보관 정책(docs/npc-audio-storage.md). 마지막 재생(파일 수정 시각) 뒤 보관 기간이 지난 음성을 지우고, 상한을 넘었으면 오래
 * 재생하지 않은 음성부터 지운다.
 *
 * <p>DB 키는 그대로 둔다. 지운 음성은 다음 만들기 요청 때 다시 합성한다.
 */
@Service
public class ModelSpeechRetention {

  private final ModelSpeechStore store;
  private final Clock clock;
  private final Duration retention;
  private final long maxBytes;

  public ModelSpeechRetention(
      ModelSpeechStore store,
      Clock clock,
      @Value("${elsehere.model-speech.retention-days}") int retentionDays,
      @Value("${elsehere.model-speech.max-bytes}") long maxBytes) {
    this.store = store;
    this.clock = clock;
    this.retention = Duration.ofDays(retentionDays);
    this.maxBytes = maxBytes;
  }

  @Scheduled(cron = "${elsehere.npc-audio.cleanup-cron}", zone = ClockConfig.SERVICE_ZONE_ID)
  public void cleanUp() {
    Instant expiredBefore = clock.instant().minus(retention);
    List<StoredSpeech> kept = new ArrayList<>();
    for (StoredSpeech speech : store.files()) {
      if (speech.lastPlayedAt().isBefore(expiredBefore)) {
        store.delete(speech.key());
      } else {
        kept.add(speech);
      }
    }
    freeSpace(kept);
  }

  private void freeSpace(List<StoredSpeech> speeches) {
    long bytesToFree = speeches.stream().mapToLong(StoredSpeech::size).sum() - maxBytes;
    List<StoredSpeech> leastRecentlyPlayedFirst =
        speeches.stream().sorted(Comparator.comparing(StoredSpeech::lastPlayedAt)).toList();
    for (StoredSpeech speech : leastRecentlyPlayedFirst) {
      if (bytesToFree <= 0) {
        return;
      }
      store.delete(speech.key());
      bytesToFree -= speech.size();
    }
  }
}
