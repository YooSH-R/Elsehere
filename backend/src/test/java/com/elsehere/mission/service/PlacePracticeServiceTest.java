package com.elsehere.mission.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.mission.dto.PracticePlaceListResponse;
import com.elsehere.mission.dto.PracticeProgressResponse;
import com.elsehere.mission.entity.PracticeScenario;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.service.PronunciationAudioStore;
import jakarta.persistence.EntityManager;
import java.util.List;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.EnumSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 장소 연습 목록(PRC-01)과 연습 진행(PRC-03). NPC는 시드(WorldSeedLoader가 시작 때 넣음)를 쓴다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  PlacePracticeService.class
})
class PlacePracticeServiceTest {
  @Autowired private PlacePracticeService practice;
  @Autowired private EntityManager entityManager;

  private Long userId;
  private Long otherUserId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "practice-owner");
    otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "practice-other", "Other");
  }

  @Test
  @DisplayName("장소 연습 목록은 12곳을 정한 순서로, 가게마다 목표 두 개짜리 상황 세 개와 직원 정보를 보여 준다")
  void places_listsTwelvePlacesWithThreeScenariosEach() {
    PracticePlaceListResponse response = practice.places(userId);

    assertThat(response.places())
        .extracting(PracticePlaceListResponse.Place::placeCode)
        .containsExactly(
            "HOTEL",
            "CAFE",
            "DINER",
            "LAUNDRY",
            "BANK",
            "BARBER",
            "FLORIST",
            "BAKERY",
            "DRUGSTORE",
            "HOME_STORE",
            "ARCADE",
            "POST_OFFICE");
    assertThat(response.places())
        .allSatisfy(
            place -> {
              assertThat(place.totalCount()).isEqualTo(3);
              assertThat(place.scenarios())
                  .extracting(PracticePlaceListResponse.Scenario::number)
                  .containsExactly(1, 2, 3);
              assertThat(place.scenarios()).allSatisfy(s -> assertThat(s.goals()).hasSize(2));
            });
    PracticePlaceListResponse.Place diner = response.places().get(2);
    assertThat(diner)
        .extracting(
            PracticePlaceListResponse.Place::name,
            PracticePlaceListResponse.Place::summary,
            PracticePlaceListResponse.Place::npcCode,
            PracticePlaceListResponse.Place::npcName,
            PracticePlaceListResponse.Place::roleLabel,
            PracticePlaceListResponse.Place::sceneCode)
        .containsExactly(
            "햄버거 가게", "자리 잡기 · 못 먹는 재료 말하기", "DINER_STAFF", "Rosa", "햄버거 가게 직원", "TOWN");
    PracticePlaceListResponse.Scenario table = diner.scenarios().getFirst();
    assertThat(table.scenarioId()).isEqualTo("DINER_TABLE");
    assertThat(table.description()).isEqualTo("몇 명인지 말하고 창가 자리를 부탁해요. 자리가 없으면 얼마나 기다리는지 물어요.");
    assertThat(table.goals())
        .extracting(PracticePlaceListResponse.Goal::goalId, PracticePlaceListResponse.Goal::title)
        .containsExactly(
            tuple("say_party_window", "인원 말하고 창가 자리 부탁하기"), tuple("ask_wait_time", "기다리는 시간 묻기"));
  }

  @Test
  @DisplayName("모든 목표를 이룬 연습만 그 사용자의 완료로 보이고, 끝내지 못한 연습과 다른 사용자의 완료는 세지 않는다")
  void places_marksOnlyThisUsersFinishedScenariosDone() {
    finish(practiceConversation(userId, "DINER_STAFF", "DINER_TABLE"), "DINER_TABLE");
    finish(practiceConversation(userId, "DINER_STAFF", "DINER_TABLE"), "DINER_TABLE");
    practice.accept(
        practiceConversation(userId, "DINER_STAFF", "DINER_ALLERGY"), recognized("say_allergy"));
    finish(practiceConversation(otherUserId, "CAFE_STAFF", "CAFE_RECOMMEND"), "CAFE_RECOMMEND");
    entityManager.flush();

    PracticePlaceListResponse response = practice.places(userId);

    PracticePlaceListResponse.Place diner = response.places().get(2);
    assertThat(diner.doneCount()).isEqualTo(1);
    assertThat(diner.scenarios())
        .extracting(PracticePlaceListResponse.Scenario::done)
        .containsExactly(true, false, false);
    assertThat(response.places().get(1).doneCount()).isZero();
  }

  @Test
  @DisplayName("연습 진행은 그 대화에서 이룬 목표와 완료 여부를 보여 주고, 이 대화의 목표가 아닌 판정은 반영하지 않는다")
  void progress_showsGoalsCompletedInThatConversation() {
    Conversation conversation = practiceConversation(userId, "HOTEL_STAFF", "HOTEL_CHANGE_ROOM");
    practice.accept(conversation, recognized("ask_other_room", "order_drink"));
    entityManager.flush();

    PracticeProgressResponse progress =
        practice.progress(userId, "HOTEL_CHANGE_ROOM", conversation.getId());

    assertThat(progress.scenarioId()).isEqualTo("HOTEL_CHANGE_ROOM");
    assertThat(progress.goals())
        .extracting(PracticeProgressResponse.Goal::goalId, PracticeProgressResponse.Goal::completed)
        .containsExactly(tuple("describe_room_problem", false), tuple("ask_other_room", true));
    assertThat(progress.goals().getFirst().hint()).isNotBlank();
    assertThat(progress.done()).isFalse();
  }

  @Test
  @DisplayName("모든 목표를 이루면 그 연습을 완료로 남긴다")
  void progress_isDoneOnceEveryGoalIsCompleted() {
    Conversation conversation = practiceConversation(userId, "HOTEL_STAFF", "HOTEL_CHANGE_ROOM");
    practice.accept(conversation, recognized("describe_room_problem"));
    practice.accept(conversation, recognized("ask_other_room"));
    entityManager.flush();

    PracticeProgressResponse progress =
        practice.progress(userId, "HOTEL_CHANGE_ROOM", conversation.getId());

    assertThat(progress.goals()).allSatisfy(goal -> assertThat(goal.completed()).isTrue());
    assertThat(progress.done()).isTrue();
    assertThat(conversation.getPracticeDoneAt()).isEqualTo(TestClockConfig.NOW);
  }

  @Test
  @DisplayName("상황의 목표를 모두 이룬 연습 대화는 판정할 목표 없이 새 요청을 꺼내지 말고 마무리하라는 지시만 준다")
  void context_afterEveryGoal_onlyGuidesToClose() {
    PracticeScenario scenario = practice.scenario("HOTEL_CHANGE_ROOM");
    List<String> everyGoal = scenario.goals().stream().map(PracticeScenario.Goal::id).toList();

    QuestTalk context = practice.context(scenario, everyGoal);

    assertThat(context).isInstanceOf(QuestTalk.Guide.class);
    assertThat(context.instruction()).contains("already done everything", "close politely");
  }

  // 학습자가 첫 목표만 말했는데 직원이 남은 목표의 답(예: 늦은 체크아웃의 추가 요금)까지 말하면 그 목표를 말할 이유가 사라진다.
  // 지시문은 AI 워커가 받는 길이(3000자, ai/worker/app/schemas.py ReplyRequest) 안이어야 한다.
  @ParameterizedTest
  @EnumSource(PracticeScenario.class)
  @DisplayName("첫 목표만 이룬 턴의 직원 지시는 남은 목표의 답을 학습자가 물을 때까지 미루게 한다")
  void replyInstruction_afterFirstGoal_holdsAnswersToRemainingGoals(PracticeScenario scenario) {
    QuestTalk context = practice.context(scenario, List.of());
    String firstGoal = scenario.goals().get(0).id();

    String reply = context.replyInstruction(recognized(firstGoal));

    assertThat(reply)
        .contains("Answer only what the learner said or asked in this turn")
        .contains("until the learner asks");
    assertThat(reply.length()).isLessThanOrEqualTo(3000);
  }

  @Test
  @DisplayName("다른 사용자의 대화, 다른 상황의 대화, 없는 상황의 진행은 NOT_FOUND다")
  void progress_ofSomeoneElsesOrAnotherScenarioIsNotFound() {
    Conversation conversation = practiceConversation(userId, "HOTEL_STAFF", "HOTEL_CHANGE_ROOM");
    entityManager.flush();

    assertNotFound(() -> practice.progress(otherUserId, "HOTEL_CHANGE_ROOM", conversation.getId()));
    assertNotFound(() -> practice.progress(userId, "HOTEL_LATE_CHECKOUT", conversation.getId()));
    assertNotFound(() -> practice.progress(userId, "NO_SUCH_SCENARIO", conversation.getId()));
  }

  private Conversation practiceConversation(Long owner, String npcCode, String scenarioId) {
    Conversation conversation = conversation(entityManager, owner, npcCode);
    conversation.attachPracticeScenario(scenarioId);
    return conversation;
  }

  private void finish(Conversation conversation, String scenarioId) {
    var goals =
        practice.places(conversation.getUserId()).places().stream()
            .flatMap(place -> place.scenarios().stream())
            .filter(scenario -> scenario.scenarioId().equals(scenarioId))
            .flatMap(scenario -> scenario.goals().stream())
            .map(PracticePlaceListResponse.Goal::goalId)
            .toArray(String[]::new);
    practice.accept(conversation, recognized(goals));
  }

  private static JudgeResult.Intent recognized(String... goalIds) {
    return new JudgeResult.Intent(goalIds[0], List.of(goalIds), null);
  }

  private static void assertNotFound(org.assertj.core.api.ThrowableAssert.ThrowingCallable call) {
    assertThatThrownBy(call)
        .isInstanceOfSatisfying(
            BusinessException.class,
            error -> assertThat(error.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }
}
