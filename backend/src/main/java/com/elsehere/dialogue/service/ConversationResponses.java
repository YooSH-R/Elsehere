package com.elsehere.dialogue.service;

import com.elsehere.common.config.ClockConfig;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.dialogue.dto.ConversationDetailResponse;
import com.elsehere.dialogue.dto.ConversationStateResponse;
import com.elsehere.dialogue.dto.NpcTurnResponse;
import com.elsehere.dialogue.dto.SpeechResponse;
import com.elsehere.dialogue.dto.TranslationResponse;
import com.elsehere.dialogue.dto.TurnDetailResponse;
import com.elsehere.dialogue.dto.UserTurnResponse;
import com.elsehere.memory.service.NpcAffinityService;
import java.time.Clock;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.json.JsonMapper;

/** 대화 API 응답을 만든다. 턴에 JSON으로 저장하는 추천 단어도 여기서 읽고 쓴다. 엔티티를 읽으므로 호출하는 쪽의 트랜잭션 안에서 부른다. */
@Component
@RequiredArgsConstructor
class ConversationResponses {

  private static final ZoneId ZONE = ClockConfig.SERVICE_ZONE;
  private static final TypeReference<List<String>> STRING_LIST = new TypeReference<>() {};

  private final TurnRepository turnRepository;
  private final DialogueContexts dialogueContexts;
  private final NpcAffinityService npcAffinityService;
  private final JsonMapper jsonMapper;
  private final Clock clock;

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
        Conversation.RUDE_LIMIT,
        npcAffinityService.affinity(conversation.getUserId(), conversation.getNpcId()),
        null,
        null,
        false,
        0);
  }

  ConversationDetailResponse detail(Conversation conversation) {
    NpcContext npc = dialogueContexts.npcById(conversation.getNpcId());
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
        Conversation.RUDE_LIMIT,
        turns,
        conversation.getReviewCardId());
  }

  static OffsetDateTime toOffset(Instant instant) {
    return instant == null ? null : instant.atZone(ZONE).toOffsetDateTime();
  }

  String writeWords(List<String> words) {
    return words == null ? null : jsonMapper.writeValueAsString(words);
  }

  List<String> readWords(String json) {
    return json == null ? null : jsonMapper.readValue(json, STRING_LIST);
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
}
