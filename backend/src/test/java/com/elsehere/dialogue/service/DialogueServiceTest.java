package com.elsehere.dialogue.service;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.service.GuestConversationLimit;
import com.elsehere.ai.service.WorkerLines;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.conversation.service.NpcAudioRetention;
import com.elsehere.conversation.service.NpcAudioStore;
import com.elsehere.conversation.service.TrainingRecordingService;
import com.elsehere.conversation.service.TrainingRecordingStore;
import com.elsehere.feedback.service.FeedbackService;
import com.elsehere.feedback.service.ModelSpeechService;
import com.elsehere.feedback.service.ModelSpeechStore;
import com.elsehere.followup.service.FollowUpService;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.mission.service.MissionDialogueService;
import com.elsehere.mission.service.TownStoryService;
import com.elsehere.user.service.GuestContentCleanup;
import com.elsehere.user.service.OptionalConsentService;
import com.elsehere.user.service.UserContentEraser;
import com.elsehere.user.service.WithdrawalService;
import com.elsehere.world.service.GameClockService;
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
 * 대화·피드백·기억 서비스를 실제 DB(PostgreSQL 컨테이너)와 함께 검증하는 테스트. AI 워커 호출({@link AiGateway})만 Mock으로 바꾼다. AI
 * 호출 기록은 AiGatewayTest가 확인한다.
 *
 * <p>NPC는 시드(WorldSeedLoader가 시작 때 넣음)를 쓴다. 비동기 실행 설정(@EnableAsync)을 불러오지 않으므로 기억 카드 갱신도 호출한 스레드에서
 * 바로 끝난다.
 */
@Target(ElementType.TYPE)
@Retention(RetentionPolicy.RUNTIME)
@Documented
@DataJpaTest(
    properties = {
      "spring.jpa.hibernate.ddl-auto=create-drop",
      "elsehere.npc-audio.dir=build/test-npc-audio",
      "elsehere.training-recording.dir=" + DialogueServiceTest.TRAINING_RECORDING_DIR,
      "elsehere.conversation.daily-limit=2"
    })
@AutoConfigureTestDatabase(replace = Replace.NONE)
@ImportAutoConfiguration(JacksonAutoConfiguration.class)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  ConversationService.class,
  ConversationHistoryService.class,
  TurnService.class,
  DialogueRequestLocks.class,
  SpeechService.class,
  TurnHelpService.class,
  ConversationLookup.class,
  DialogueContexts.class,
  ConversationResponses.class,
  NpcAudioReceiver.class,
  WorkerLines.class,
  GuestConversationLimit.class,
  NpcAudioStore.class,
  NpcAudioRetention.class,
  WorkerNpcs.class,
  FeedbackService.class,
  ModelSpeechService.class,
  ModelSpeechStore.class,
  TrainingRecordingService.class,
  TrainingRecordingStore.class,
  OptionalConsentService.class,
  ConsentCatalog.class,
  MemoryService.class,
  FollowUpService.class,
  NpcAffinityService.class,
  FirstDayMissionService.class,
  TownStoryService.class,
  MissionDialogueService.class,
  GameClockService.class,
  GuestContentCleanup.class,
  UserContentEraser.class,
  WithdrawalService.class
})
@MockitoBean(types = AiGateway.class)
public @interface DialogueServiceTest {

  String TRAINING_RECORDING_DIR = "build/test-training-recordings";
}
