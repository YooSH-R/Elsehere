package com.elsehere.feedback.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyDouble;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.BDDMockito.willThrow;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.service.DialogueServiceTest;
import com.elsehere.feedback.dto.ModelSpeechResponse;
import com.elsehere.feedback.entity.TurnFeedback;
import com.elsehere.feedback.repository.TurnFeedbackRepository;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.stream.Stream;
import org.assertj.core.api.ThrowableAssert.ThrowingCallable;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.mockito.AdditionalAnswers;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.transaction.support.TransactionTemplate;

// 상세 평가 항목의 모범 음성(FB-04). 워커는 Mock이고 음성 파일은 테스트 폴더에 저장한다.
@DialogueServiceTest
class ModelSpeechServiceTest {

  private static final String BETTER_TEXT = "I'd like an iced latte, please.";
  private static final byte[] WAV = {82, 73, 70, 70};

  @Autowired private ModelSpeechService modelSpeechService;

  @Autowired private AiGateway aiGateway;

  @Autowired private EntityManager entityManager;

  @Autowired private TurnFeedbackRepository turnFeedbackRepository;

  @Autowired private ConversationLookup conversationLookup;

  @Autowired private TransactionTemplate transactionTemplate;

  // 남은 파일을 세는 테스트는 테스트마다 빈 폴더에 저장하는 서비스를 쓴다.
  @TempDir private Path speechDirectory;

  private Long userId;
  private Conversation conversation;
  private TurnFeedback feedback;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    conversation = conversation(entityManager, userId, "CAFE_STAFF");
    Turn userTurn = textTurn(entityManager, conversation, 2, "I want 아이스 라떼.");
    feedback = saveFeedback(userTurn, BETTER_TEXT);
  }

  @Test
  @DisplayName("고친 문장을 고정 목소리로 합성해 저장하고 받을 주소를 돌려준다. 워커 요청에는 요청 ID와 대화를 붙인다")
  void create_synthesizesBetterTextWithFixedVoice() {
    given(aiGateway.synthesize(any(), eq(BETTER_TEXT), eq("af_heart"), eq(1.0))).willReturn(WAV);
    UUID requestId = UUID.randomUUID();

    ModelSpeechResponse response = modelSpeechService.create(userId, feedback.getId(), requestId);

    assertThat(response.url())
        .isEqualTo("/api/v1/turn-feedback/" + feedback.getId() + "/model-speech");
    assertThat(modelSpeechService.load(userId, feedback.getId())).isEqualTo(WAV);
    then(aiGateway)
        .should()
        .synthesize(
            eq(new WorkerTrace(requestId, userId, conversation.getId())),
            eq(BETTER_TEXT),
            eq("af_heart"),
            eq(1.0));
  }

  @Test
  @DisplayName("한 번 만든 모범 음성은 다시 쓰고 새로 합성하지 않는다")
  void create_twice_reusesSavedSpeech() {
    given(aiGateway.synthesize(any(), anyString(), anyString(), anyDouble())).willReturn(WAV);

    ModelSpeechResponse first = modelSpeechService.create(userId, feedback.getId(), uuid());
    ModelSpeechResponse second = modelSpeechService.create(userId, feedback.getId(), uuid());

    assertThat(second).isEqualTo(first);
    then(aiGateway).should(times(1)).synthesize(any(), anyString(), anyString(), anyDouble());
  }

  @Test
  @DisplayName("저장해 둔 파일이 없어졌으면(볼륨 교체 등) 다시 합성한다")
  void create_whenSavedFileIsGone_synthesizesAgain() {
    turnFeedbackRepository.attachModelAudio(
        feedback.getId(), null, "00000000-0000-0000-0000-000000000000.wav");
    given(aiGateway.synthesize(any(), anyString(), anyString(), anyDouble())).willReturn(WAV);

    modelSpeechService.create(userId, feedback.getId(), uuid());

    assertThat(modelSpeechService.load(userId, feedback.getId())).isEqualTo(WAV);
  }

  @Test
  @DisplayName("다른 사용자의 피드백이면 NOT_FOUND로 거절하고 합성하지 않는다")
  void create_otherUsersFeedback_notFound() {
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");

    assertNotFound(() -> modelSpeechService.create(otherUserId, feedback.getId(), uuid()));
    then(aiGateway).should(never()).synthesize(any(), any(), any(), anyDouble());
  }

  @Test
  @DisplayName("없는 피드백이면 NOT_FOUND로 거절한다")
  void create_missingFeedback_notFound() {
    assertNotFound(() -> modelSpeechService.create(userId, 999_999L, uuid()));
  }

  @Test
  @DisplayName("고칠 표현이 없는 항목은 모범 문장이 없어 NOT_FOUND로 거절하고 합성하지 않는다")
  void create_withoutBetterText_notFound() {
    TurnFeedback fine =
        saveFeedback(textTurn(entityManager, conversation, 4, "A medium, please."), null);

    assertNotFound(() -> modelSpeechService.create(userId, fine.getId(), uuid()));
    then(aiGateway).should(never()).synthesize(any(), any(), any(), anyDouble());
  }

  @Test
  @DisplayName("음성 합성이 실패하면 워커 오류를 그대로 알리고 음성을 남기지 않는다")
  void create_whenSynthesisFails_keepsNoSpeech() {
    given(aiGateway.synthesize(any(), anyString(), anyString(), anyDouble()))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    assertThatThrownBy(() -> modelSpeechService.create(userId, feedback.getId(), uuid()))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.AI_TEMPORARILY_UNAVAILABLE);
    assertNotFound(() -> modelSpeechService.load(userId, feedback.getId()));
  }

  @Test
  @DisplayName("합성하는 사이 피드백이 지워지면(게스트 정리·탈퇴) 만든 음성 파일을 남기지 않는다")
  void create_whenFeedbackDeletedDuringSynthesis_keepsNoFile() throws IOException {
    given(aiGateway.synthesize(any(), anyString(), anyString(), anyDouble()))
        .willAnswer(
            invocation -> {
              turnFeedbackRepository.deleteById(feedback.getId());
              entityManager.flush();
              return WAV;
            });

    serviceIn(turnFeedbackRepository).create(userId, feedback.getId(), uuid());

    assertThat(savedFiles()).isEmpty();
  }

  @Test
  @DisplayName("같은 피드백의 모범 음성을 거의 동시에 두 번 만들면 먼저 붙은 음성 하나만 남기고 나중 파일은 지운다")
  void create_concurrently_keepsOnlyAttachedFile() throws IOException {
    ModelSpeechService service = serviceIn(turnFeedbackRepository);
    AtomicBoolean firstSynthesis = new AtomicBoolean(true);
    given(aiGateway.synthesize(any(), anyString(), anyString(), anyDouble()))
        .willAnswer(
            invocation -> {
              // 먼저 온 요청이 합성하는 사이 두 번째 요청이 끝까지 실행된다.
              if (firstSynthesis.getAndSet(false)) {
                service.create(userId, feedback.getId(), uuid());
              }
              return WAV;
            });

    service.create(userId, feedback.getId(), uuid());

    entityManager.flush();
    entityManager.clear();
    String attachedKey =
        turnFeedbackRepository.findById(feedback.getId()).orElseThrow().getModelAudioKey();
    assertThat(savedFiles()).containsExactly(attachedKey);
  }

  @Test
  @DisplayName("음성 파일을 저장한 뒤 DB 반영이 실패하면 오류를 알리고 만든 파일을 지운다")
  void create_whenAttachFails_deletesSavedFile() throws IOException {
    TurnFeedbackRepository failingRepository =
        mock(TurnFeedbackRepository.class, AdditionalAnswers.delegatesTo(turnFeedbackRepository));
    willThrow(new IllegalStateException("Simulated database update failure"))
        .given(failingRepository)
        .attachModelAudio(anyLong(), any(), anyString());
    given(aiGateway.synthesize(any(), anyString(), anyString(), anyDouble())).willReturn(WAV);

    assertThatThrownBy(() -> serviceIn(failingRepository).create(userId, feedback.getId(), uuid()))
        .isInstanceOf(IllegalStateException.class);
    assertThat(savedFiles()).isEmpty();
  }

  @Test
  @DisplayName("모범 음성을 만들기 전에 받으려 하면 NOT_FOUND로 거절한다")
  void load_beforeCreated_notFound() {
    assertNotFound(() -> modelSpeechService.load(userId, feedback.getId()));
  }

  @Test
  @DisplayName("다른 사용자는 만들어 둔 모범 음성을 받을 수 없다")
  void load_otherUsersSpeech_notFound() {
    given(aiGateway.synthesize(any(), anyString(), anyString(), anyDouble())).willReturn(WAV);
    modelSpeechService.create(userId, feedback.getId(), uuid());
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");

    assertNotFound(() -> modelSpeechService.load(otherUserId, feedback.getId()));
  }

  private TurnFeedback saveFeedback(Turn userTurn, String betterText) {
    TurnFeedback saved = TurnFeedback.of(userTurn.getId(), betterText, "이유", null, null);
    entityManager.persist(saved);
    entityManager.flush();
    return saved;
  }

  private ModelSpeechService serviceIn(TurnFeedbackRepository repository) {
    return new ModelSpeechService(
        repository,
        conversationLookup,
        aiGateway,
        new ModelSpeechStore(speechDirectory.toString()),
        transactionTemplate,
        "af_heart");
  }

  private List<String> savedFiles() throws IOException {
    try (Stream<Path> files = Files.list(speechDirectory)) {
      return files.map(file -> file.getFileName().toString()).toList();
    }
  }

  private static void assertNotFound(ThrowingCallable callable) {
    assertThatThrownBy(callable)
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.NOT_FOUND);
  }

  private static UUID uuid() {
    return UUID.randomUUID();
  }
}
