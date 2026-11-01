package com.elsehere.memory.service;

import com.elsehere.memory.repository.UserNpcRelationRepository;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

/** 사용자별 NPC 호감도(-100~100). 무례로 센 말마다 깎고, 대화 화면에 지금 값을 보여 준다. */
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

  /** 지금 호감도. 관계 행이 없으면(처음 만난 NPC) 0이다. */
  public int affinity(Long userId, Long npcId) {
    return userNpcRelationRepository.findAffinity(userId, npcId).orElse(0);
  }
}
