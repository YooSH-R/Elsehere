package com.elsehere.dialogue.service;

import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.favor.service.FavorService;
import com.elsehere.followup.service.FollowUpService;
import com.elsehere.mission.service.QuestTalkService;
import java.util.Optional;

/**
 * 대화 시작에 필요한 정보. {@link DialogueStartService}가 조회해 모으고 {@link OpeningLines}가 첫마디를 고를 때 쓴다.
 *
 * @param learner 대답 상대인 학습자. NPC가 캐릭터에 맞는 호칭을 쓰게 한다
 * @param memory 이 NPC와의 기억. 처음 만나면 null
 * @param quest 퀘스트 대화 목표와 그 목표를 낸 쪽. 장소 연습은 고른 상황의 목표다. 복습이거나 진행 중인 퀘스트가 없으면 빈 값
 * @param followUp 이 NPC가 지금 건넬 수 있는 것
 * @param favorCall 이 NPC가 먼저 말을 걸어 꺼낸 부탁. 있으면 첫마디가 그 부탁이다
 * @param mode 일반 대화·상황 복습·장소 연습 중 어떻게 시작했는지
 * @param streetGreeting 행인에게 다가가 시작하면 고른 거리 인사. 아니면 null
 */
record StartContext(
    NpcContext npc,
    WorkerLearner learner,
    int level,
    WorkerMemory memory,
    Optional<QuestTalkService.SelectedQuest> quest,
    Optional<FollowUpService.ReadyFollowUp> followUp,
    Optional<FavorService.CallOpening> favorCall,
    StartMode mode,
    StreetGreeting streetGreeting) {}
