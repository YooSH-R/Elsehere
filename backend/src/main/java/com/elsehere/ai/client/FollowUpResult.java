package com.elsehere.ai.client;

import java.util.List;

/**
 * 건넬 것 판정(/v1/npc/follow-up) 결과.
 *
 * @param followUp NPC가 곧 건네기로 한 것. 없으면 null
 */
public record FollowUpResult(WorkerFollowUp followUp, List<WorkerUsage> usage) {}
