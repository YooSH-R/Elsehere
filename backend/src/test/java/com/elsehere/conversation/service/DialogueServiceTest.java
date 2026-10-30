package com.elsehere.conversation.service;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.service.AiRequestRecorder;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.feedback.service.FeedbackService;
import com.elsehere.feedback.service.ModelSpeechService;
import com.elsehere.feedback.service.ModelSpeechStore;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.user.service.GuestContentCleanup;
import com.elsehere.user.service.OptionalConsentService;
import com.elsehere.user.service.UserContentEraser;
import com.elsehere.user.service.WithdrawalService;
import com.elsehere.world.service.PronunciationAudioStore;
import java.lang.annotation.Documented;
import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;
import org.springframework.boot.autoconfigure.ImportAutoConfiguration;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jackson.autoconfigure.JacksonAutoConfiguration;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.bean.override.mockito.MockitoBean;

/**
 * 대화·피드백·기억 서비스를 실제 DB(PostgreSQL 컨테이너)와 함께 검증하는 테스트. AI 워커 호출만 Mock으로 바꾼다.
 *
 * <p>NPC는 시드(WorldSeedLoader가 시작 때 넣음)를 쓴다. 비동기 실행 설정(@EnableAsync)을 불러오지 않으므로 기억 카드 갱신도 호출한 스레드에서
 * 바로 끝난다. AI 호출 기록은 별도 트랜잭션으로 커밋되어 테스트가 끝나도 남으므로 요청 ID로 찾는다.
 */
@Target(ElementType.TYPE)
@Retention(RetentionPolicy.RUNTIME)
@Documented
@DataJpaTest(
    properties = {
      "spring.jpa.hibernate.ddl-auto=create-drop",
      "elsehere.npc-audio.dir=build/test-npc-audio",
      "elsehere.training-recording.dir=" + DialogueServiceTest.TRAINING_RECORDING_DIR,
      "elsehere.guest.conversation-limit=2"
    })
@AutoConfigureTestDatabase(replace = Replace.NONE)
@ImportAutoConfiguration(JacksonAutoConfiguration.class)
@Import({
  TestcontainersConfiguration.class,
  ClockConfig.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  ConversationService.class,
  ConversationHistoryService.class,
  TurnService.class,
  SpeechService.class,
  TurnHelpService.class,
  ConversationSupport.class,
  GuestConversationLimit.class,
  NpcAudioStore.class,
  NpcAudioRetention.class,
  WorkerNpcs.class,
  AiRequestRecorder.class,
  FeedbackService.class,
  ModelSpeechService.class,
  ModelSpeechStore.class,
  TrainingRecordingService.class,
  TrainingRecordingStore.class,
  OptionalConsentService.class,
  ConsentCatalog.class,
  MemoryService.class,
  NpcAffinityService.class,
  FirstDayMissionService.class,
  GuestContentCleanup.class,
  UserContentEraser.class,
  WithdrawalService.class
})
@MockitoBean(types = AiWorkerClient.class)
public @interface DialogueServiceTest {

  String TRAINING_RECORDING_DIR = "build/test-training-recordings";
}
