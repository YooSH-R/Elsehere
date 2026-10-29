package com.elsehere.admin.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;

import com.elsehere.admin.dto.PronunciationProgress;
import com.elsehere.admin.dto.PronunciationRun;
import com.elsehere.admin.dto.PronunciationStatusResponse;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

// 백그라운드 실행기는 넘겨받은 작업을 모아 두기만 하고, 테스트가 원하는 때에 실행한다.
class AdminPronunciationServiceTest {

  private final PronunciationGenerator pronunciationGenerator = mock(PronunciationGenerator.class);
  private final List<Runnable> submittedTasks = new ArrayList<>();
  private final AdminPronunciationService adminPronunciationService =
      new AdminPronunciationService(pronunciationGenerator, submittedTasks::add);

  @BeforeEach
  void setUp() {
    given(pronunciationGenerator.progress()).willReturn(new PronunciationProgress(106, 1));
  }

  @Test
  @DisplayName("만드는 중이 아니면 백그라운드로 발음 만들기를 시작하고, 만드는 중과 남은 단어 수를 보여 준다")
  void start_submitsGenerationAndShowsRunning() {
    PronunciationStatusResponse status = adminPronunciationService.start(1L);

    assertThat(submittedTasks).hasSize(1);
    assertThat(status.running()).isTrue();
    assertThat(status.total()).isEqualTo(106);
    assertThat(status.ready()).isEqualTo(1);
    assertThat(status.missing()).isEqualTo(105);
    then(pronunciationGenerator).should(never()).generateMissing(any());
  }

  @Test
  @DisplayName("이미 만드는 중이면 새로 시작하지 않는다")
  void start_whileRunning_doesNotSubmitAgain() {
    adminPronunciationService.start(1L);

    PronunciationStatusResponse status = adminPronunciationService.start(1L);

    assertThat(submittedTasks).hasSize(1);
    assertThat(status.running()).isTrue();
  }

  @Test
  @DisplayName("다 만들면 만드는 중이 아니고, 마지막 결과(만든 수·실패한 단어)를 보여 준다")
  void finishedGeneration_showsLastRun() {
    PronunciationRun run = new PronunciationRun(104, List.of("sofa"));
    given(pronunciationGenerator.generateMissing(1L)).willReturn(run);
    adminPronunciationService.start(1L);

    submittedTasks.getFirst().run();
    PronunciationStatusResponse status = adminPronunciationService.status();

    assertThat(status.running()).isFalse();
    assertThat(status.lastRun()).isEqualTo(run);
  }

  @Test
  @DisplayName("만들다가 예상하지 못한 오류가 나도 다시 시작할 수 있다")
  void failedGeneration_canStartAgain() {
    given(pronunciationGenerator.generateMissing(1L)).willThrow(new IllegalStateException("db"));
    adminPronunciationService.start(1L);
    submittedTasks.getFirst().run();

    PronunciationStatusResponse status = adminPronunciationService.start(1L);

    assertThat(submittedTasks).hasSize(2);
    assertThat(status.running()).isTrue();
  }

  @Test
  @DisplayName("한 번도 만들지 않았으면 마지막 결과가 없다")
  void status_beforeAnyRun_hasNoLastRun() {
    PronunciationStatusResponse status = adminPronunciationService.status();

    assertThat(status.running()).isFalse();
    assertThat(status.lastRun()).isNull();
  }
}
