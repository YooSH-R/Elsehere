package com.elsehere.conversation.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.NpcAudioStore.StoredAudio;
import com.elsehere.world.repository.NpcRepository;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.concurrent.locks.ReentrantLock;
import java.util.stream.Collectors;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * NPC 턴 음성 보관 정책(docs/npc-audio-storage.md). 환경 전체 음성 용량이 상한을 넘지 않게 저장하고, 지운 음성을 쓰던 턴에 삭제 시각을 남긴다.
 *
 * <p>저장과 정리는 한 잠금 안에서 해 상한을 지킨다. 환경마다 백엔드가 하나라는 전제다.
 */
@Slf4j
@Service
public class NpcAudioRetention {

  private final NpcAudioStore store;
  private final TurnRepository turnRepository;
  private final NpcRepository npcRepository;
  private final TransactionTemplate transactionTemplate;
  private final Clock clock;
  private final Duration retention;
  private final long maxBytes;
  private final ReentrantLock lock = new ReentrantLock();

  public NpcAudioRetention(
      NpcAudioStore store,
      TurnRepository turnRepository,
      NpcRepository npcRepository,
      TransactionTemplate transactionTemplate,
      Clock clock,
      @Value("${elsehere.npc-audio.retention-days}") int retentionDays,
      @Value("${elsehere.npc-audio.max-bytes}") long maxBytes) {
    this.store = store;
    this.turnRepository = turnRepository;
    this.npcRepository = npcRepository;
    this.transactionTemplate = transactionTemplate;
    this.clock = clock;
    this.retention = Duration.ofDays(retentionDays);
    this.maxBytes = maxBytes;
  }

  /** 상한 안에 들어가도록 오래된 음성부터 정리한 뒤 저장한다. 저장할 수 없으면 null이며, 호출한 쪽은 자막만 보여 준다. */
  public String save(byte[] wav) {
    if (wav.length > maxBytes) {
      log.warn("NPC 음성이 보관 상한보다 커서 저장하지 않습니다 bytes={}", wav.length);
      return null;
    }
    lock.lock();
    try {
      Set<String> shared = sharedKeys();
      deleteExpired(clock.instant(), shared);
      freeSpace(usedBytes(shared) + wav.length - maxBytes, shared);
      if (usedBytes(shared) + wav.length > maxBytes) {
        log.warn("NPC 음성 저장 공간을 확보하지 못했습니다 bytes={}", wav.length);
        return null;
      }
      return store.save(wav);
    } catch (BusinessException exception) {
      log.warn("NPC 음성을 저장하지 못했습니다", exception);
      return null;
    } finally {
      lock.unlock();
    }
  }

  /** 소유자를 확인한 턴을 호출한 쪽의 트랜잭션 안에서 읽고 만료·파일 없음 상태를 맞춘다. */
  public Optional<byte[]> load(Turn turn) {
    lock.lock();
    try {
      String key = turn.getNpcAudioKey();
      if (key == null || turn.getNpcAudioDeletedAt() != null) {
        return Optional.empty();
      }
      Instant now = clock.instant();
      Instant createdAt = audioCreatedAt(turn);
      if (!sharedKeys().contains(key) && !createdAt.isAfter(now.minus(retention))) {
        deleteAudio(turn, now);
        return Optional.empty();
      }
      Optional<byte[]> wav = store.load(key);
      if (wav.isEmpty()) {
        turn.deleteNpcAudio(now);
      }
      return wav;
    } finally {
      lock.unlock();
    }
  }

  /**
   * 매일 정리한다. 보관 기간이 지난 음성, 파일이 없어진 키(DB 복원·이전 실패), 어떤 턴도 쓰지 않는 오래된 파일을 지우고, 상한을 넘었으면 오래된 파일부터 지운다.
   * NPC 공용 음성(말걸기)은 지우지 않는다.
   */
  @Scheduled(cron = "${elsehere.npc-audio.cleanup-cron}", zone = "Asia/Seoul")
  public void cleanUp() {
    lock.lock();
    try {
      Instant now = clock.instant();
      Set<String> shared = sharedKeys();
      deleteExpired(now, shared);
      markMissingFiles(now);
      deleteUnreferencedFiles(now, shared);
      freeSpace(usedBytes(shared) - maxBytes, shared);
    } finally {
      lock.unlock();
    }
  }

  /** 지운 턴이 쓰던 음성 파일을 지운다(게스트 내용 정리). NPC 공용 음성(말걸기)은 다른 사용자도 쓰므로 남긴다. */
  public void deleteAudio(Collection<String> keys) {
    lock.lock();
    try {
      Set<String> shared = sharedKeys();
      keys.stream().filter(key -> !shared.contains(key)).forEach(store::delete);
    } finally {
      lock.unlock();
    }
  }

  // 파일의 수정 시각은 이전·복원으로 달라질 수 있으므로 DB에 기록한 음성 생성 시각부터 센다.
  // 삭제 실패 시 DB 키를 남겨 파일 수정 시각이 바뀌었거나 앱이 재시작되어도 다시 시도한다.
  private void deleteExpired(Instant now, Set<String> shared) {
    transactionTemplate.executeWithoutResult(
        status -> {
          for (Turn turn : turnRepository.findExpiredNpcAudio(now.minus(retention))) {
            if (shared.contains(turn.getNpcAudioKey())) {
              continue;
            }
            deleteAudio(turn, now);
          }
        });
  }

  private void deleteAudio(Turn turn, Instant now) {
    turn.markNpcAudioUnavailable(now);
    try {
      store.delete(turn.getNpcAudioKey());
      turn.deleteNpcAudio(now);
    } catch (BusinessException exception) {
      log.warn("NPC 음성 파일을 지우지 못해 다음 정리에서 재시도합니다 code={}", exception.getErrorCode());
    }
  }

  private void markMissingFiles(Instant now) {
    Set<String> stored = keysOf(store.files());
    transactionTemplate.executeWithoutResult(
        status -> {
          List<String> missing =
              turnRepository.findAllNpcAudioKeys().stream()
                  .filter(key -> !stored.contains(key))
                  .toList();
          if (!missing.isEmpty()) {
            turnRepository.findAllByNpcAudioKeyIn(missing).forEach(t -> t.deleteNpcAudio(now));
          }
        });
  }

  // 음성 파일은 턴보다 먼저 저장하므로, 최근 파일은 아직 턴이 없을 수 있다. 보관 기간이 지난 것만 지운다.
  private void deleteUnreferencedFiles(Instant now, Set<String> shared) {
    Set<String> referenced =
        transactionTemplate.execute(status -> new HashSet<>(turnRepository.findAllNpcAudioKeys()));
    Instant expiredBefore = now.minus(retention);
    for (StoredAudio audio : store.files()) {
      if (!referenced.contains(audio.key())
          && !shared.contains(audio.key())
          && audio.modifiedAt().isBefore(expiredBefore)) {
        store.delete(audio.key());
      }
    }
  }

  // 파일 이전으로 수정 시각이 바뀌어도 원래 생성 순서를 유지한다.
  private void freeSpace(long bytesToFree, Set<String> shared) {
    if (bytesToFree <= 0) {
      return;
    }
    List<String> deleted = new ArrayList<>();
    long freed = 0;
    for (StoredAudio audio : oldestFirst(store.files())) {
      if (freed >= bytesToFree) {
        break;
      }
      if (shared.contains(audio.key())) {
        continue;
      }
      store.delete(audio.key());
      deleted.add(audio.key());
      freed += audio.size();
    }
    markDeleted(deleted);
  }

  // 파일을 먼저 지운다. DB 갱신이 실패하면 파일 없는 키로 남고, 다음 정리가 다시 맞춘다.
  private void markDeleted(Collection<String> keys) {
    if (keys.isEmpty()) {
      return;
    }
    Instant now = clock.instant();
    try {
      transactionTemplate.executeWithoutResult(
          status ->
              turnRepository
                  .findAllByNpcAudioKeyIn(keys)
                  .forEach(turn -> turn.deleteNpcAudio(now)));
    } catch (RuntimeException exception) {
      log.warn("지운 NPC 음성의 턴을 갱신하지 못했습니다 count={}", keys.size(), exception);
    }
  }

  private Set<String> sharedKeys() {
    return transactionTemplate.execute(
        status -> new HashSet<>(npcRepository.findAllApproachAudioKeys()));
  }

  private long usedBytes(Set<String> shared) {
    return store.files().stream()
        .filter(audio -> !shared.contains(audio.key()))
        .mapToLong(StoredAudio::size)
        .sum();
  }

  private List<StoredAudio> oldestFirst(List<StoredAudio> files) {
    Map<String, Instant> createdAtByKey =
        transactionTemplate.execute(
            status -> {
              Map<String, Instant> created = new HashMap<>();
              for (Turn turn :
                  turnRepository.findAllByNpcAudioKeyIn(
                      files.stream().map(StoredAudio::key).toList())) {
                Instant createdAt = audioCreatedAt(turn);
                created.merge(
                    turn.getNpcAudioKey(),
                    createdAt,
                    (first, second) -> first.isBefore(second) ? first : second);
              }
              return created;
            });
    return files.stream()
        .sorted(
            Comparator.comparing(
                audio -> createdAtByKey.getOrDefault(audio.key(), audio.modifiedAt())))
        .toList();
  }

  private static Set<String> keysOf(List<StoredAudio> files) {
    return files.stream().map(StoredAudio::key).collect(Collectors.toSet());
  }

  private static Instant audioCreatedAt(Turn turn) {
    return turn.getNpcAudioCreatedAt() == null ? turn.getCreatedAt() : turn.getNpcAudioCreatedAt();
  }
}
