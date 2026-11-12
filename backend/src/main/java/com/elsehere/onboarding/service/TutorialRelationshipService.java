package com.elsehere.onboarding.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.memory.entity.UserNpcRelation;
import com.elsehere.memory.entity.UserNpcRelationId;
import com.elsehere.memory.repository.UserNpcRelationRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.service.GameClockService;
import java.time.Clock;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

/** 고정 대본의 Ben 길 안내를 첫 방문과 도움으로 반영한다. 호출자는 사용자 행을 잠근다. */
@Service
public class TutorialRelationshipService {

  private static final String BEN_CARD =
      """
      {"name":"","claims":[],"events":["Introduced themselves and greeted Ben.","Helped Ben find the cafe during the tutorial."],
       "attitude":"grateful","struggled":[]}
      """;

  private final NpcRepository npcRepository;
  private final UserNpcRelationRepository relationRepository;
  private final GameClockService gameClockService;
  private final Clock clock;
  private final int recognitionGain;
  private final int affinityGain;

  public TutorialRelationshipService(
      NpcRepository npcRepository,
      UserNpcRelationRepository relationRepository,
      GameClockService gameClockService,
      Clock clock,
      @Value("${elsehere.relationship.tutorial-recognition:20}") int recognitionGain,
      @Value("${elsehere.relationship.tutorial-affinity:4}") int affinityGain) {
    this.npcRepository = npcRepository;
    this.relationRepository = relationRepository;
    this.gameClockService = gameClockService;
    this.clock = clock;
    this.recognitionGain = recognitionGain;
    this.affinityGain = affinityGain;
  }

  public void recordBenDialogue(Long userId) {
    Npc ben =
        npcRepository
            .findByCode("TOWN_PASSERBY")
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    UserNpcRelation relation =
        relationRepository
            .findById(new UserNpcRelationId(userId, ben.getId()))
            .orElseGet(() -> UserNpcRelation.start(userId, ben.getId()));
    if (relation.recordTutorialVisit(
        clock.instant(),
        gameClockService.currentDay(userId),
        recognitionGain,
        affinityGain,
        BEN_CARD)) {
      relationRepository.save(relation);
    }
  }
}
