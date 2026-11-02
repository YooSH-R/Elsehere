package com.elsehere.dialogue.service;

import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.service.WorkerLines;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

/** 대화에 필요한 NPC·학습자·앞선 대화를 읽는다. 엔티티를 읽으므로 호출하는 쪽의 트랜잭션 안에서 부른다. */
@Component
@RequiredArgsConstructor
class DialogueContexts {

  // 워커가 받는 앞선 대화의 최대 줄 수
  private static final int HISTORY_LIMIT = 40;

  private final NpcRepository npcRepository;
  private final UserRepository userRepository;
  private final TurnRepository turnRepository;
  private final WorkerNpcs workerNpcs;
  private final WorkerLines workerLines;

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

  /** 워커에 보낼 앞선 대화. 최근 줄만 보낸다. */
  List<WorkerLine> history(Long conversationId) {
    return workerLines.recent(
        turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId), HISTORY_LIMIT);
  }

  /** 워커에 보낼 앞선 대화 중 seq번째 턴까지. 힌트처럼 지난 NPC 대사에 대답할 말을 만들 때 쓴다. */
  List<WorkerLine> historyUntil(Long conversationId, int seq) {
    return workerLines.recent(
        turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId).stream()
            .filter(turn -> turn.getSeq() <= seq)
            .toList(),
        HISTORY_LIMIT);
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
