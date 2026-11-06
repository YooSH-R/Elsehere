package com.elsehere.memory.service;

import com.elsehere.memory.repository.UserNpcRelationRepository;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

/**
 * 사용자별 NPC 호감도(-100~100). 대화 중 발화마다 바꾸고(무례면 깎고, 칭찬·인사 같은 사건이면 올림) 대화 화면에 지금 값을 보여 준다. 대화가 끝난 뒤 좋은
 * 대화 몫은 기억 카드를 저장하면서 올린다(MemoryService).
 */
@Service
public class NpcAffinityService {

  private final UserNpcRelationRepository userNpcRelationRepository;
  private final int rudeAffinityDelta;

  public NpcAffinityService(
      UserNpcRelationRepository userNpcRelationRepository,
      @Value("${elsehere.relationship.rude-affinity-delta}") int rudeAffinityDelta) {
    this.userNpcRelationRepository = userNpcRelationRepository;
    this.rudeAffinityDelta = rudeAffinityDelta;
  }

  /**
   * 무례로 센 말 하나만큼 호감도를 깎는다. 무례 판정이 NPC 성격을 반영한 변화량을 주면 그 값을, 없으면 기본값을 쓴다. 무례 횟수와 함께 확정되도록 발화를 저장하는
   * 트랜잭션 안에서 부른다.
   */
  public void deductForRudeness(Long userId, Long npcId, Integer workerDelta) {
    userNpcRelationRepository.addAffinity(
        userId, npcId, workerDelta != null ? workerDelta : rudeAffinityDelta);
  }

  /**
   * 무례가 아닌 말의 대화 사건(칭찬·인사 등)으로 의도 판정이 준 변화량만큼 호감도를 올린다. 변화량이 없거나(이 칸을 모르는 이전 워커) 0이면 그대로 둔다. 발화를
   * 저장하는 트랜잭션 안에서 부른다.
   */
  public void raiseForEvents(Long userId, Long npcId, Integer workerDelta) {
    if (workerDelta != null && workerDelta != 0) {
      userNpcRelationRepository.addAffinity(userId, npcId, workerDelta);
    }
  }

  /** 지금 호감도. 관계 행이 없으면(처음 만난 NPC) 0이다. */
  public int affinity(Long userId, Long npcId) {
    return userNpcRelationRepository.findAffinity(userId, npcId).orElse(0);
  }
}
