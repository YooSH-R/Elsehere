package com.elsehere.dialogue.service;

import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.NpcReplyListener;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.mission.service.QuestTalk;
import com.elsehere.mission.service.QuestTalkService;
import com.elsehere.world.service.PoliceOfficers;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

/**
 * 대화를 여는 NPC의 첫마디를 고르고 만든다(CONV-01).
 *
 * <p>John은 저장된 랩으로 연다. 건넬 것이 준비됐으면 건네는 말로(경찰관이면 출동 결과를 알리는 말로) 연다. 행인에게 다가갔으면 거리 인사를, 말걸기 대사가 있는
 * NPC가 먼저 말을 걸었으면 그 대사를 쓴다. 그 밖에는 AI가 상황(복습·퀘스트·근무 시간이 아님)에 맞는 인사를 만든다.
 */
@Component
@RequiredArgsConstructor
class OpeningLines {
  private static final String JOHN_ORDER_RAP =
      """
      Cheeseburger, fries, lemonade on ice!
      Add an apple pie, yeah, that sounds nice!
      Make that two, one for me, one for you.
      That's my order, now tell me yours!""";
  private static final String JOHN_ORDER_RAP_KOREAN =
      """
      치즈버거, 감자튀김, 얼음 넣은 레모네이드!
      애플파이도 하나, 그래 딱 좋아!
      두 개로 해줘, 하나는 내 것, 하나는 네 것.
      내 주문은 이거야, 네 것도 말해줘!""";

  // 직원의 첫 인사는 AI로 만든다(API 명세 [미정]). 학습자는 아직 말하지 않았으므로 다가온 상황만 알려 준다.
  private static final String GREETING_TEXT = "(The learner walks up to you.)";
  private static final String GREETING_INSTRUCTION =
      "The learner just walked up to you. Open in your own voice, following your personality, "
          + "first-meeting behavior, familiarity and current situation. If you are a passerby, react "
          + "as a local approached by someone, not a shop assistant: you may ask what brings them "
          + "over, cautiously or warmly as fits your character. Only offer service if you are "
          + "actually serving customers now. Give them an easy conversational opening. 1 or 2 short sentences.";

  // NPC가 먼저 말을 걸어 부탁하는 첫마디(FavorService.pendingCall). 학습자는 아직 말하지 않았다.
  private static final String FAVOR_CALL_TEXT = "(You walk up to the learner to ask a favor.)";

  // 미션 지시에는 학습자가 할 일과 NPC가 해 줄 수 있는 일이 담겨 있다. 첫 인사에서 이를 먼저 다 말하면 학습자가 요청할 일이 사라진다.
  private static final String MISSION_GREETING =
      " Welcome the learner in your own voice, following your personality and first-meeting behavior. Invite their request in one short sentence for this situation."
          + " Do not mention the learner's tasks, your offers or directions yet; wait for the learner to ask."
          + " Do not assume their task is complete.";

  // 근무 시간이 아닌 NPC는 손님을 맞지 않으므로 무엇을 도와줄지 묻지 않고 쉬는 중에 만난 사람처럼 인사한다.
  private static final String OFF_DUTY_GREETING_INSTRUCTION =
      "The learner just walked up to you while you are off work. Greet them casually, the way"
          + " you would in your free time, following your personality, familiarity and first-meeting "
          + "behavior. You may share a small observation or ask what brings them over. "
          + "Do not offer to help or serve them. 1 or 2 short sentences.";
  // 준비된 것을 건네는 첫마디. 학습자는 아직 말하지 않았으므로 NPC가 먼저 부른다.
  private static final String HANDOVER_TEXT = "(The learner is waiting nearby for their order.)";
  private static final String HANDOVER_INSTRUCTION =
      "The %s the learner asked you for is ready now. Call them and hand it over warmly in your own"
          + " way, in one or two short sentences. Do not ask them to order again.";
  // 경찰관이 신고를 받아 직접 순찰차로 다녀온 결과(PoliceDesk, FollowUpService). 학습자의 물건을 찾았으면 돌려준다.
  private static final String POLICE_CALL_TEXT =
      "(The learner who reported the trouble is back at the police station.)";
  private static final String POLICE_CALL_INSTRUCTION =
      "You are back from the call the learner reported, and this is how it ended: %s. Tell them in"
          + " your own way in one or two short sentences, and if you found something of theirs,"
          + " hand it back. Do not ask them to report it again.";

  private final AiGateway aiGateway;
  private final NpcAudioReceiver npcAudioReceiver;
  private final ConversationResponses conversationResponses;
  private final ReviewDialogue reviewDialogue;

  /**
   * 첫마디를 고르고 AI로 만들 첫마디면 만든다. 스트리밍 요청({@code events}가 있음)이면 AI 첫 인사의 글자 조각과 문장 조각 음성을 만들어지는 대로 넘긴다.
   * 이 NPC가 건넬 것이 준비됐으면 건네는 말로 시작한다. 말걸기 대사가 있는 NPC가 먼저 말을 걸었으면 그 대사로 시작한다. 직원처럼 말걸기 대사가 없거나 근무 시간이
   * 아니면 AI로 첫 인사를 만든다.
   */
  Opening open(
      StartContext context,
      StartConversationRequest request,
      WorkerTrace trace,
      TurnEvents events) {
    NpcContext npc = context.npc();
    NpcReplyListener listener = NpcReplyRelay.to(aiGateway, trace, events);
    boolean recordedRap =
        context.mode().story() && "TOWN_JOHN_PARK".equals(npc.summary().npcCode());
    if (recordedRap) {
      // 저장 공연은 원문과 번역을 함께 기록해 뜻 미리 생성에서도 AI를 부르지 않는다.
      return new Opening(JOHN_ORDER_RAP, null, null, JOHN_ORDER_RAP_KOREAN, false);
    }
    return context
        .followUp()
        .map(
            ready ->
                PoliceOfficers.includes(npc.summary().npcCode())
                    ? greetingOpening(
                        trace,
                        listener,
                        npc,
                        context,
                        POLICE_CALL_TEXT,
                        POLICE_CALL_INSTRUCTION.formatted(ready.item()))
                    : greetingOpening(
                        trace,
                        listener,
                        npc,
                        context,
                        HANDOVER_TEXT,
                        HANDOVER_INSTRUCTION.formatted(ready.item())))
        .orElseGet(() -> ordinaryOpening(trace, listener, context, request));
  }

  // 행인 말걸기에 응답하면 이미 보여 준 말걸기 대사를 첫 턴으로 저장하고 AI를 부르지 않는다.
  private Opening approachOpening(NpcContext npc) {
    return new Opening(npc.approachLine(), npc.approachAudioKey(), null, null, false);
  }

  private Opening ordinaryOpening(
      WorkerTrace trace,
      NpcReplyListener listener,
      StartContext context,
      StartConversationRequest request) {
    NpcContext npc = context.npc();
    if (context.favorCall().isPresent()) {
      return greetingOpening(
          trace, listener, npc, context, FAVOR_CALL_TEXT, context.favorCall().get().instruction());
    }
    if (context.streetGreeting() != null) {
      return streetOpening(trace, context);
    }
    if (!context.mode().isReview()
        && context.quest().isEmpty()
        && !request.npcOffDuty()
        && request.initiatedBy() == InitiatedBy.NPC
        && npc.approachLine() != null) {
      return approachOpening(npc);
    }
    return greetingOpening(
        trace,
        listener,
        npc,
        context,
        GREETING_TEXT,
        greetingInstruction(context, request.npcOffDuty()));
  }

  // 거리 인사는 중복을 판별하기 전에는 어떤 글자나 음성도 플레이어에게 보내지 않고, 대화를 저장한 뒤 한 번에 보낸다.
  private Opening streetOpening(WorkerTrace trace, StartContext context) {
    StreetGreeting greeting = context.streetGreeting();
    NpcContext npc = context.npc();
    NpcReply reply =
        aiGateway.reply(
            trace,
            npc.workerNpc(),
            context.memory(),
            List.of(),
            GREETING_TEXT,
            greeting.instruction(),
            context.level(),
            context.learner());
    if (greeting.accepts(reply.text())) {
      return savedOpening(trace, reply, true);
    }
    String alternative = greeting.alternative();
    String audioKey =
        npcAudioReceiver.synthesize(
            trace, alternative, npc.voiceId(), context.learner().speechSpeed());
    return new Opening(alternative, audioKey, null, null, true);
  }

  // 스트리밍 요청이면 첫 인사의 글자 조각과 문장 조각 음성을 받는 대로 listener로 넘긴다. 전체 음성은 다시 듣기용으로 저장한다.
  private Opening greetingOpening(
      WorkerTrace trace,
      NpcReplyListener listener,
      NpcContext npc,
      StartContext context,
      String text,
      String instruction) {
    NpcReply reply =
        listener == NpcReplyListener.NONE
            ? aiGateway.reply(
                trace,
                npc.workerNpc(),
                context.memory(),
                List.of(),
                text,
                instruction,
                context.level(),
                context.learner())
            : aiGateway.reply(
                trace,
                npc.workerNpc(),
                context.memory(),
                List.of(),
                text,
                instruction,
                context.level(),
                context.learner(),
                listener);
    return savedOpening(trace, reply, false);
  }

  private Opening savedOpening(WorkerTrace trace, NpcReply reply, boolean streamAfterSave) {
    return new Opening(
        reply.text(),
        npcAudioReceiver.save(trace, reply.audioFullId()),
        conversationResponses.writeSpeechPhrases(reply.speechPhrases()),
        null,
        streamAfterSave);
  }

  // 첫날 미션 단계의 NPC는 미션 지시대로 인사한다. 미션 NPC가 근무 시간이 아닐 때 미션을 어떻게 할지는 아직 정하지 않아
  // (docs/npc-life-design.md 11절 "교대와 첫날 미션") 지금은 미션 지시를 따른다.
  private String greetingInstruction(StartContext context, boolean npcOffDuty) {
    if (context.mode().isReview()) {
      return reviewDialogue.instruction(context.mode().review())
          + " Open with one brief welcome and let the learner make their request.";
    }
    return context
        .quest()
        .map(quest -> quest.talk().instruction() + missionGreeting(quest))
        .orElse(npcOffDuty ? OFF_DUTY_GREETING_INSTRUCTION : GREETING_INSTRUCTION);
  }

  private String missionGreeting(QuestTalkService.SelectedQuest quest) {
    // 시연 버거 가게는 단골을 알아보는 장면이다. 다른 퀘스트처럼 제안을 숨기게 하면 늘 시키던 메뉴를 묻지 못한다.
    if (quest.source() == QuestTalkService.Source.SHOWCASE
        && quest.talk() instanceof QuestTalk.Goals
        && quest.talk().stage() == ShowcaseStage.ORDER.ordinal()) {
      return " Welcome the learner in your own voice, by name if you know it."
          + " If you remember what this regular usually gets, ask in one short question whether they"
          + " want their usual and name it, for example: The usual? A Crispy Chicken burger and a lemonade?"
          + " If you do not remember their usual, invite their order in one short sentence."
          + " Do not mention the price or the learner's tasks yet.";
    }
    return MISSION_GREETING;
  }

  /**
   * 첫마디.
   *
   * @param npcAudioKey 저장한 전체 음성. 없으면 null
   * @param speechPhrases 문장 조각 음성 정보(JSON). 없으면 null
   * @param meaning 미리 정해 둔 한국어 뜻(저장 공연). 있으면 첫 턴에 붙여 뜻 보기에서 AI를 부르지 않는다
   * @param streamAfterSave 만드는 동안 조각을 보내지 않은 첫마디(거리 인사)라 대화를 저장한 뒤 글자를 한 번에 보내야 하는지
   */
  record Opening(
      String text,
      String npcAudioKey,
      String speechPhrases,
      String meaning,
      boolean streamAfterSave) {}
}
