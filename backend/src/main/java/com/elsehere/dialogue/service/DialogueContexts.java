package com.elsehere.dialogue.service;

import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.service.WorkerLines;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.service.GameClockService;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Limit;
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
  private final GameClockService gameClockService;

  /** NPC 대답의 상대인 학습자. NPC가 캐릭터에 맞는 호칭을 쓰도록 닉네임과 캐릭터 기본형을, 대답 음성에 쓸 수 있게 학습자가 고른 말 빠르기를 보낸다. */
  WorkerLearner learner(User user, double speechSpeed) {
    return new WorkerLearner(
        user.getNickname(),
        user.getAvatarType() == null ? null : user.getAvatarType().name(),
        speechSpeed);
  }

  WorkerLearner learner(Long userId, double speechSpeed) {
    return learner(
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND)),
        speechSpeed);
  }

  /** 대화를 시작할 NPC. 근무 시간이 아니면 워커에 일상 상황을 보낸다. */
  NpcContext npcByCode(Long userId, String npcCode, boolean npcOffDuty) {
    NpcContext npc =
        npcRepository
            .findByCode(npcCode)
            .map(this::toContext)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    return atCurrentTime(npcOffDuty ? npc.offDuty() : npc, userId);
  }

  /** 대화 상대인 NPC. 워커를 부를 때는 대화를 시작할 때 정한 근무 여부를 따르도록 이 메서드로 읽는다. */
  NpcContext npcOf(Conversation conversation) {
    NpcContext npc = npcById(conversation.getNpcId());
    return atCurrentTime(
            conversation.isNpcOffDuty() ? npc.offDuty() : npc, conversation.getUserId())
        .withTravelSituation(conversation.getNpcTravelSituation())
        .withNeeds(conversation.getNpcNeeds());
  }

  // 대화 중에는 게임 시계가 멈춘다. PC 시각이나 인격의 평소 일과 대신 사용자별 게임 시각을 매 요청에 싣는다.
  private NpcContext atCurrentTime(NpcContext npc, Long userId) {
    var time = gameClockService.currentTime(userId);
    String scene =
        npc.workerNpc().scene()
            + " Current game time: day %d, %02d:%02d (24-hour clock)."
                .formatted(time.day(), time.minuteOfDay() / 60, time.minuteOfDay() % 60);
    return new NpcContext(
        npc.npcId(),
        npc.summary(),
        npc.sceneCode(),
        npc.sceneOpen(),
        npc.workerNpc().withScene(scene),
        npc.voiceId(),
        npc.approachLine(),
        npc.approachAudioKey());
  }

  /** 근무 여부와 관계없는 NPC 정보(이름·장소·목소리). 대화의 워커 호출에는 {@link #npcOf}를 쓴다. */
  NpcContext npcById(Long npcId) {
    return npcRepository
        .findById(npcId)
        .map(this::toContext)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  /** 워커에 보낼 앞선 대화. 최근 줄만 보낸다. */
  List<WorkerLine> history(Long conversationId) {
    return historyUntil(conversationId, Integer.MAX_VALUE);
  }

  /** 워커에 보낼 앞선 대화 중 seq번째 턴까지. 힌트처럼 지난 NPC 대사에 대답할 말을 만들 때 쓴다. */
  List<WorkerLine> historyUntil(Long conversationId, int seq) {
    return workerLines.recent(
        turnRepository
            .findByConversationIdAndSeqLessThanEqualOrderBySeqDesc(
                conversationId, seq, Limit.of(HISTORY_LIMIT))
            .reversed(),
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
