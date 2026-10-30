package com.elsehere.conversation;

import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerUsage;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

/** 테스트용 대화 데이터. AI를 거치지 않고 대화와 턴을 바로 저장한다. NPC는 시드에 있는 것을 쓴다. */
public final class DialogueTestData {

  public static final Instant STARTED_AT = Instant.parse("2026-10-27T01:00:00Z");

  private DialogueTestData() {}

  public static Long npcId(EntityManager entityManager, String npcCode) {
    return entityManager
        .createQuery("select npc.id from Npc npc where npc.code = :code", Long.class)
        .setParameter("code", npcCode)
        .getSingleResult();
  }

  public static Conversation conversation(
      EntityManager entityManager, Long userId, String npcCode) {
    Conversation conversation =
        Conversation.start(userId, npcId(entityManager, npcCode), InitiatedBy.NPC, 1, STARTED_AT);
    entityManager.persist(conversation);
    return conversation;
  }

  public static Turn npcTurn(
      EntityManager entityManager, Conversation conversation, int seq, String text) {
    Turn turn = Turn.npc(conversation, seq, text, null, null);
    entityManager.persist(turn);
    return turn;
  }

  public static Turn textTurn(
      EntityManager entityManager, Conversation conversation, int seq, String text) {
    Turn turn =
        Turn.user(
            conversation,
            seq,
            UUID.randomUUID(),
            InputType.TEXT,
            text,
            null,
            null,
            null,
            null,
            null);
    entityManager.persist(turn);
    return turn;
  }

  public static Turn voiceTurn(
      EntityManager entityManager,
      Conversation conversation,
      int seq,
      String text,
      String sttRawText) {
    Turn turn =
        Turn.user(
            conversation,
            seq,
            UUID.randomUUID(),
            InputType.VOICE,
            text,
            sttRawText,
            null,
            null,
            null,
            null);
    entityManager.persist(turn);
    return turn;
  }

  /** 대답이 끝난 NPC 스트림. 음성 ID가 없으면 음성을 받지 않는다. */
  public static NpcReply reply(String text, String audioFullId, List<String> suggestWords) {
    return new NpcReply(text, audioFullId, suggestWords, null, List.of(usage("npc")));
  }

  public static WorkerUsage usage(String step) {
    return new WorkerUsage(step, "test-model", 100, 10, null, 0.1, false);
  }
}
