package com.elsehere.onboarding.service;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

/**
 * 레벨 테스트 듣기 문항 음성(ONB-10). 문항 대사를 그 NPC 목소리로 만든다.
 *
 * <p>대사는 모든 사용자에게 같으므로 문항마다 한 번만 만들어 메모리에 두고 다시 쓴다. 서버를 다시 띄우면 처음 요청할 때 다시 만든다.
 */
@Service
@RequiredArgsConstructor
public class LevelTestSpeechService {

  // 게임 대화의 NPC 음성과 같은 빠르기다(SpeechService).
  private static final double NPC_SPEECH_SPEED = 1.1;

  private final LevelTestCatalog levelTestCatalog;
  private final NpcRepository npcRepository;
  private final AiGateway aiGateway;

  private final Map<String, byte[]> speechByItem = new ConcurrentHashMap<>();

  /** 듣기 문항의 WAV. 말하기 문항이나 없는 문항이면 NOT_FOUND다. */
  public byte[] speech(Long userId, String itemCode) {
    LevelTestItem item =
        levelTestCatalog
            .findItem(itemCode)
            .filter(LevelTestItem::isListening)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    byte[] cached = speechByItem.get(itemCode);
    if (cached != null) {
      return cached;
    }
    byte[] wav = synthesize(userId, item);
    speechByItem.putIfAbsent(itemCode, wav);
    return wav;
  }

  private byte[] synthesize(Long userId, LevelTestItem item) {
    String voice =
        npcRepository
            .findByCode(item.npcCode())
            .map(Npc::getVoiceId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    return aiGateway.synthesize(
        new WorkerTrace(UUID.randomUUID(), userId, null), item.script(), voice, NPC_SPEECH_SPEED);
  }
}
