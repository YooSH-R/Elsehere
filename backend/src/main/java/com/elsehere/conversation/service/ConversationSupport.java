package com.elsehere.conversation.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.ConversationDetailResponse;
import com.elsehere.conversation.dto.ConversationStateResponse;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.dto.NpcTurnResponse;
import com.elsehere.conversation.dto.SpeechResponse;
import com.elsehere.conversation.dto.TurnDetailResponse;
import com.elsehere.conversation.dto.UserTurnResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.util.List;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.json.JsonMapper;

/** 대화 API들이 함께 쓰는 조회·변환. 엔티티를 읽는 메서드는 호출하는 쪽의 트랜잭션 안에서 부른다. */
@Slf4j
@Component
@RequiredArgsConstructor
class ConversationSupport {

  /** 대화 하나에서 무례로 셀 수 있는 발화 수(서버 설정값). 무례 판정을 붙이기 전까지 응답 표시용이다. */
  static final int RUDE_LIMIT = 3;

  private static final ZoneId ZONE = ZoneId.of("Asia/Seoul");
  // 워커가 받는 앞선 대화의 최대 줄 수
  private static final int HISTORY_LIMIT = 40;
  private static final TypeReference<List<String>> STRING_LIST = new TypeReference<>() {};

  private final NpcRepository npcRepository;
  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final AiWorkerClient aiWorkerClient;
  private final NpcAudioRetention npcAudioRetention;
  private final WorkerNpcs workerNpcs;
  private final JsonMapper jsonMapper;

  NpcContext npcByCode(String npcCode) {
    return npcRepository
        .findByCode(npcCode)
        .map(this::toContext)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  NpcContext npcById(Long npcId) {
    return npcRepository
        .findById(npcId)
        .map(this::toContext)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  /** 다른 사용자의 대화는 존재 여부를 드러내지 않도록 NOT_FOUND로 응답한다. */
  Conversation ownedConversation(Long userId, Long conversationId) {
    return conversationRepository
        .findByIdAndUserId(conversationId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  /** 워커에 보낼 앞선 대화. 최근 줄만 보낸다. */
  List<WorkerLine> history(Long conversationId) {
    List<Turn> turns = turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId);
    return turns.subList(Math.max(0, turns.size() - HISTORY_LIMIT), turns.size()).stream()
        .map(
            turn ->
                turn.getSpeaker() == Speaker.NPC
                    ? WorkerLine.npc(turn.getText())
                    : WorkerLine.learner(turn.getText(), turn.getTranslationEn()))
        .toList();
  }

  /** 스트림이 만든 턴 전체 음성을 받아 보관 상한 안에 저장한다. 받지 못하거나 저장하지 못하면 자막만 보여 주도록 null을 돌려준다. */
  String saveNpcAudio(WorkerTrace trace, String audioId) {
    if (audioId == null) {
      return null;
    }
    try {
      return npcAudioRetention.save(aiWorkerClient.audio(trace, audioId));
    } catch (BusinessException exception) {
      log.warn("NPC 음성을 받지 못했습니다 code={}", exception.getErrorCode());
      return null;
    }
  }

  String writeWords(List<String> words) {
    return words == null ? null : jsonMapper.writeValueAsString(words);
  }

  NpcTurnResponse npcTurn(Turn turn) {
    return new NpcTurnResponse(
        turn.getId(),
        turn.getSeq(),
        Speaker.NPC,
        turn.getText(),
        readWords(turn.getSuggestWords()),
        SpeechResponse.of(turn.getId(), turn.getNpcAudioKey(), turn.getNpcAudioDeletedAt()));
  }

  // 번역 카드는 무례가 아니라고 확정된 말에만 보여 준다. 무례 판정을 붙이기 전까지는 확정할 수 없어 보여 주지 않는다.
  UserTurnResponse userTurn(Turn turn) {
    return new UserTurnResponse(
        turn.getId(), turn.getSeq(), Speaker.USER, turn.getText(), false, null);
  }

  ConversationStateResponse state(Conversation conversation) {
    return new ConversationStateResponse(
        conversation.getStatus(),
        conversation.getEndReason(),
        conversation.getRudeCount(),
        RUDE_LIMIT);
  }

  ConversationDetailResponse detail(Conversation conversation) {
    NpcContext npc = npcById(conversation.getNpcId());
    List<TurnDetailResponse> turns =
        turnRepository.findAllByConversationIdOrderBySeqAsc(conversation.getId()).stream()
            .map(this::turnDetail)
            .toList();
    return new ConversationDetailResponse(
        conversation.getId(),
        conversation.getStatus(),
        conversation.getEndReason(),
        conversation.getLevelApplied(),
        npc.summary(),
        npc.sceneCode(),
        toOffset(conversation.getStartedAt()),
        toOffset(conversation.getEndedAt()),
        conversation.getRudeCount(),
        RUDE_LIMIT,
        turns);
  }

  static OffsetDateTime toOffset(Instant instant) {
    return instant == null ? null : instant.atZone(ZONE).toOffsetDateTime();
  }

  private TurnDetailResponse turnDetail(Turn turn) {
    boolean npcTurn = turn.getSpeaker() == Speaker.NPC;
    return new TurnDetailResponse(
        turn.getId(),
        turn.getSeq(),
        turn.getSpeaker(),
        turn.getText(),
        false,
        null,
        readWords(turn.getSuggestWords()),
        turn.getMeaningKo(),
        turn.getHintText(),
        npcTurn
            ? SpeechResponse.of(turn.getId(), turn.getNpcAudioKey(), turn.getNpcAudioDeletedAt())
            : null);
  }

  private List<String> readWords(String json) {
    return json == null ? null : jsonMapper.readValue(json, STRING_LIST);
  }

  private NpcContext toContext(Npc npc) {
    return new NpcContext(
        npc.getId(),
        new NpcSummaryResponse(npc.getCode(), npc.getName(), npc.getRoleLabel()),
        npc.getScene().getCode(),
        npc.getScene().isOpen(),
        workerNpcs.of(npc),
        npc.getVoiceId(),
        npc.getApproachLine(),
        npc.getApproachAudioKey());
  }
}
