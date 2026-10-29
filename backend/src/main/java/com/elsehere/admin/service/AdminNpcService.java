package com.elsehere.admin.service;

import com.elsehere.admin.dto.AdminNpcDetail;
import com.elsehere.admin.dto.AdminNpcSummary;
import com.elsehere.admin.dto.PersonaRequest;
import com.elsehere.admin.dto.PersonaResponse;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.NpcPersona;
import com.elsehere.world.repository.NpcRepository;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.databind.json.JsonMapper;

/**
 * 운영자 어드민의 NPC 페르소나 관리. 이름·장소·목소리는 시드가 관리하므로 고치지 않는다. 시드는 페르소나를 NPC를 처음 넣을 때만 쓰므로 여기서 고친 값이 배포 뒤에도
 * 남는다.
 */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AdminNpcService {

  private final NpcRepository npcRepository;
  private final JsonMapper jsonMapper;

  /** 시드에 넣은 순서대로 보여 준다. */
  public List<AdminNpcSummary> npcs() {
    return npcRepository.findAll(Sort.by("id")).stream()
        .map(
            npc ->
                new AdminNpcSummary(
                    npc.getCode(), npc.getName(), npc.getRoleLabel(), npc.getScene().getName()))
        .toList();
  }

  public AdminNpcDetail npc(String npcCode) {
    Npc npc = find(npcCode);
    NpcPersona persona = jsonMapper.readValue(npc.getPersona(), NpcPersona.class);
    return new AdminNpcDetail(
        npc.getCode(),
        npc.getName(),
        npc.getRoleLabel(),
        npc.getScene().getName(),
        npc.getVoiceId(),
        persona.role(),
        persona.facts(),
        persona.scene());
  }

  /** 페르소나를 바꾼다. 다음 대화 요청부터 AI 워커에 바뀐 값을 보낸다. 앞뒤 공백은 지운다. */
  @Transactional
  public PersonaResponse updatePersona(String npcCode, PersonaRequest request) {
    Npc npc = find(npcCode);
    NpcPersona persona =
        new NpcPersona(request.role().strip(), strip(request.facts()), strip(request.scene()));
    npc.changePersona(jsonMapper.writeValueAsString(persona));
    return new PersonaResponse(npc.getCode(), persona.role(), persona.facts(), persona.scene());
  }

  private Npc find(String npcCode) {
    return npcRepository
        .findByCode(npcCode)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  private static String strip(String value) {
    return value == null ? null : value.strip();
  }
}
