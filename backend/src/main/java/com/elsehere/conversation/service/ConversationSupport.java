package com.elsehere.conversation.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.WorkerLearner;
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
import com.elsehere.conversation.dto.TranslationResponse;
import com.elsehere.conversation.dto.TurnDetailResponse;
import com.elsehere.conversation.dto.UserTurnResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
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

  /** 대화 하나에서 무례로 셀 수 있는 발화 수(서버 설정값). 여기에 닿으면 NPC가 대화를 끝낸다. */
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
  private final UserRepository userRepository;
  private final JsonMapper jsonMapper;

  /** NPC 대답의 상대인 학습자. NPC가 캐릭터에 맞는 호칭을 쓰도록 닉네임과 캐릭터 기본형을 보낸다. */
  WorkerLearner learner(User user) {
    return new WorkerLearner(
        user.getNickname(), user.getAvatarType() == null ? null : user.getAvatarType().name());
  }

  WorkerLearner learner(Long userId) {
    return learner(
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND)));
  }

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

  /** 다른 사용자의 턴과 사용자 턴은 존재 여부를 드러내지 않도록 NOT_FOUND로 응답한다. */
  Turn ownedNpcTurn(Long userId, Long turnId) {
    Turn turn =
        turnRepository
            .findById(turnId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (turn.getSpeaker() != Speaker.NPC || !turn.getConversation().getUserId().equals(userId)) {
      throw new BusinessException(ErrorCode.NOT_FOUND);
    }
    return turn;
  }

  /** 워커에 보낼 앞선 대화. 최근 줄만 보낸다. */
  List<WorkerLine> history(Long conversationId) {
    return recentLines(turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId));
  }

  /** 워커에 보낼 앞선 대화 중 seq번째 턴까지. 힌트처럼 지난 NPC 대사에 대답할 말을 만들 때 쓴다. */
  List<WorkerLine> historyUntil(Long conversationId, int seq) {
    return recentLines(
        turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId).stream()
            .filter(turn -> turn.getSeq() <= seq)
            .toList());
  }

  // 학습자 줄에는 의도 판정이 고른 대화 사건을 함께 보낸다. NPC의 대화 중 기분 계산에 쓰인다.
  private List<WorkerLine> recentLines(List<Turn> turns) {
    return turns.subList(Math.max(0, turns.size() - HISTORY_LIMIT), turns.size()).stream()
        .map(
            turn ->
                turn.getSpeaker() == Speaker.NPC
                    ? WorkerLine.npc(turn.getText())
                    : WorkerLine.learner(
                        turn.getText(),
                        turn.getTranslationEn(),
                        turn.isRude(),
                        readWords(turn.getEvents())))
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

  UserTurnResponse userTurn(Turn turn) {
    return new UserTurnResponse(
        turn.getId(),
        turn.getSeq(),
        Speaker.USER,
        turn.getText(),
        turn.isRude(),
        translation(turn));
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
        turn.isRude(),
        translation(turn),
        readWords(turn.getSuggestWords()),
        turn.getMeaningKo(),
        turn.getHintText(),
        npcTurn
            ? SpeechResponse.of(turn.getId(), turn.getNpcAudioKey(), turn.getNpcAudioDeletedAt())
            : null);
  }

  private static TranslationResponse translation(Turn turn) {
    String english = turn.shownTranslation();
    return english == null ? null : new TranslationResponse(english);
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
