package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.reply;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import com.elsehere.ai.client.FollowUpResult;
import com.elsehere.ai.client.WorkerFollowUp;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.EndConversationResponse;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.mission.entity.ShowcaseJourney;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.mission.repository.ShowcaseJourneyRepository;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.test.context.TestPropertySource;

@DialogueServiceTest
@TestPropertySource(properties = "elsehere.showcase.enabled=true")
class ShowcaseConversationTest {
  @Autowired private ConversationService service;
  @Autowired private AiGateway gateway;
  @Autowired private EntityManager entityManager;
  @Autowired private ShowcaseJourneyRepository journeys;
  private Long userId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "showcase-test");
  }

  @Test
  void returningToHotelAsksWhetherTheLearnerStayedDryInsteadOfOfferingNewService() {
    ShowcaseJourney journey = ShowcaseJourney.start(userId);
    journey.accept(0, "HOTEL_STAFF", ShowcaseStage.HOTEL.goalIds());
    journey.chooseTaxiDestination("TAXI_DRIVER", "DINER");
    journey.arriveByTaxi("DINER");
    journey.accept(2, "DINER_STAFF", ShowcaseStage.ORDER.goalIds());
    journeys.saveAndFlush(journey);
    given(gateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Welcome back! Did you stay dry?", null));

    service.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "HOTEL_STAFF", InitiatedBy.USER));

    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(gateway)
        .should()
        .reply(any(), any(), any(), any(), anyString(), instruction.capture(), anyInt(), any());
    assertThat(instruction.getValue())
        .contains("stay dry", "umbrella")
        .doesNotContain("Invite their request");
  }

  @Test
  void burgerDemoKeepsFeedbackButNeverCreatesAWaitingItem() {
    Conversation conversation = conversation(entityManager, userId, "DINER_STAFF");
    conversation.attachShowcaseStage(ShowcaseStage.ORDER.ordinal());
    npcTurn(entityManager, conversation, 1, "Welcome to Maple Burger.");
    textTurn(entityManager, conversation, 2, "A cheeseburger, fries and Coke, to go please.");
    given(gateway.followUp(any(), any(), any()))
        .willReturn(new FollowUpResult(new WorkerFollowUp("burger", "햄버거", "QUICK"), List.of()));
    entityManager.flush();

    EndConversationResponse ended =
        service.end(
            userId, conversation.getId(), new EndConversationRequest(UUID.randomUUID(), true));

    assertThat(ended.followUp()).isNull();
    assertThat(ended.userTurnCount()).isEqualTo(1);
    then(gateway).should(never()).followUp(any(), any(), any());
  }
}
