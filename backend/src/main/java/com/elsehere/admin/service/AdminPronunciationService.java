package com.elsehere.admin.service;

import com.elsehere.admin.dto.PronunciationProgress;
import com.elsehere.admin.dto.PronunciationRun;
import com.elsehere.admin.dto.PronunciationStatusResponse;
import java.util.concurrent.atomic.AtomicBoolean;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.core.task.TaskExecutor;
import org.springframework.stereotype.Service;

/**
 * 관리자가 실행하는 사물 단어 발음 만들기. 단어 수에 따라 몇 분 걸리므로 요청은 바로 돌려주고 백그라운드에서 만든다.
 *
 * <p>한 번에 하나만 실행한다. 실행 상태와 마지막 결과는 서버 메모리에만 두므로 서버를 다시 시작하면 사라진다. 백엔드는 환경마다 컨테이너 하나로 실행한다.
 */
@Slf4j
@Service
public class AdminPronunciationService {

  private final PronunciationGenerator pronunciationGenerator;
  private final TaskExecutor taskExecutor;
  private final AtomicBoolean running = new AtomicBoolean();
  private volatile PronunciationRun lastRun;

  public AdminPronunciationService(
      PronunciationGenerator pronunciationGenerator,
      @Qualifier("applicationTaskExecutor") TaskExecutor taskExecutor) {
    this.pronunciationGenerator = pronunciationGenerator;
    this.taskExecutor = taskExecutor;
  }

  public PronunciationStatusResponse status() {
    PronunciationProgress progress = pronunciationGenerator.progress();
    return new PronunciationStatusResponse(
        progress.total(),
        progress.ready(),
        progress.total() - progress.ready(),
        running.get(),
        lastRun);
  }

  /** 발음 파일이 없는 단어의 발음 만들기를 시작한다. 이미 만드는 중이면 새로 시작하지 않고 진행 상황만 돌려준다. */
  public PronunciationStatusResponse start() {
    if (running.compareAndSet(false, true)) {
      try {
        taskExecutor.execute(this::generate);
      } catch (RuntimeException exception) {
        running.set(false);
        throw exception;
      }
    }
    return status();
  }

  private void generate() {
    try {
      lastRun = pronunciationGenerator.generateMissing();
    } catch (RuntimeException exception) {
      log.error("pronunciation_generation_stopped", exception);
    } finally {
      running.set(false);
    }
  }
}
