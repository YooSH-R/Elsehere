package com.elsehere.admin.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.admin.dto.AdminNpcDetail;
import com.elsehere.admin.dto.AdminNpcSummary;
import com.elsehere.admin.dto.PersonaRequest;
import com.elsehere.admin.dto.PersonaResponse;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.service.PronunciationAudioStore;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.autoconfigure.ImportAutoConfiguration;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jackson.autoconfigure.JacksonAutoConfiguration;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 고친 페르소나는 대화가 AI 워커에 보내는 NPC 정보(WorkerNpcs)로 다시 읽어 확인한다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  AdminNpcService.class,
  WorkerNpcs.class
})
@ImportAutoConfiguration(JacksonAutoConfiguration.class)
class AdminNpcServiceTest {

  @Autowired private AdminNpcService adminNpcService;

  @Autowired private WorkerNpcs workerNpcs;

  @Autowired private NpcRepository npcRepository;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("NPC 목록은 시드의 NPC를 코드·이름·역할 이름·장소 이름과 함께 넣은 순서로 보여 준다")
  void npcs_listsNpcsWithScene() {
    assertThat(adminNpcService.npcs())
        .extracting(
            AdminNpcSummary::npcCode,
            AdminNpcSummary::name,
            AdminNpcSummary::roleLabel,
            AdminNpcSummary::sceneName)
        .containsExactly(
            tuple("TOWN_PASSERBY", "Ben", "행인", "마을"),
            tuple("CAFE_STAFF", "Mia", "카페 직원", "모퉁이 카페"),
            tuple("HOTEL_STAFF", "Jake", "호텔 직원", "호텔"),
            tuple("SHOP_STAFF", "Alex", "드럭스토어 직원", "드럭스토어"),
            tuple("TAXI_DRIVER", "Joe", "택시 기사", "마을"),
            tuple("POLICE_OFFICER", "Dan", "경찰관", "마을"),
            tuple("POST_OFFICE_STAFF", "Linda", "우체국 직원", "마을"),
            tuple("DINER_STAFF", "Rosa", "식당 종업원", "마을"),
            tuple("BANK_STAFF", "Henry", "은행원", "마을"),
            tuple("LAUNDROMAT_STAFF", "Gus", "빨래방 주인", "마을"),
            tuple("FLOWER_SHOP_STAFF", "Lily", "꽃집 주인", "마을"),
            tuple("BARBER_STAFF", "Tony", "이발사", "마을"),
            tuple("BAKERY_STAFF", "Grace", "제빵사", "마을"),
            tuple("ARCADE_STAFF", "Max", "오락실 직원", "마을"),
            tuple("HOME_STORE_STAFF", "Nina", "매장 직원", "마을"),
            tuple("CAFE_BARISTA", "Zoe", "카페 직원", "모퉁이 카페"),
            tuple("HOTEL_RECEPTIONIST", "Owen", "호텔 직원", "호텔"),
            tuple("HOTEL_NIGHT_MANAGER", "Clara", "호텔 야간 매니저", "호텔"),
            tuple("SHOP_PHARMACIST", "Hannah", "약사", "드럭스토어"),
            tuple("SHOP_NIGHT_CLERK", "Samuel", "드럭스토어 직원", "드럭스토어"),
            tuple("TOWN_RETIREE", "Ruby", "우체국 직원", "마을"),
            tuple("TOWN_STUDENT", "Ethan", "오락실 직원", "마을"),
            tuple("TOWN_EVENING_WALKER", "Maya", "은행원", "마을"),
            tuple("CAFE_SERVER", "Leo", "카페 직원", "모퉁이 카페"),
            tuple("HOTEL_CONCIERGE", "Walter", "꽃집 직원", "마을"),
            tuple("HOTEL_BELLHOP", "Diego", "벨보이", "호텔"),
            tuple("TOWN_SHOPPER", "Helen", "은행원", "마을"),
            tuple("TOWN_HANDYMAN", "Frank", "생활용품점 직원", "마을"),
            tuple("POLICE_SERGEANT", "Victor", "경사", "마을"),
            tuple("POLICE_PATROL_OFFICER", "Kevin", "베이커리 직원", "마을"),
            tuple("POLICE_ROOKIE", "Taylor", "신입 경찰관", "마을"),
            tuple("FIRE_CAPTAIN", "Ray", "식당 직원", "마을"),
            tuple("FIRE_FIGHTER", "Rachel", "빨래방 직원", "마을"),
            tuple("FIRE_ROOKIE", "Andre", "이발소 직원", "마을"),
            tuple("TOWN_GRANDFATHER", "Arthur", "행인", "마을"),
            tuple("TOWN_GRANDMOTHER", "Margaret", "행인", "마을"),
            tuple("TOWN_JOHN_PARK", "John", "뮤지션", "마을"));
  }

  @Test
  @DisplayName("NPC 하나를 열면 기본 정보와 지금 쓰는 페르소나(role·facts·scene)를 보여 준다")
  void npc_showsCurrentPersona() {
    AdminNpcDetail detail = adminNpcService.npc("SHOP_STAFF");

    assertThat(detail.npcCode()).isEqualTo("SHOP_STAFF");
    assertThat(detail.name()).isEqualTo("Alex");
    assertThat(detail.sceneName()).isEqualTo("드럭스토어");
    assertThat(detail.voiceId()).isEqualTo("am_michael");
    assertThat(detail.role()).isEqualTo("the helpful clerk at the drugstore.");
    assertThat(detail.facts()).startsWith("The drugstore sells umbrellas");
    assertThat(detail.scene()).isEqualTo("A small drugstore on the town main street.");
  }

  @Test
  @DisplayName("페르소나를 고치면 고친 값을 돌려주고, 다음 대화에 AI 워커로 보내는 NPC 정보도 그 값을 쓴다")
  void updatePersona_isUsedForNextConversation() {
    PersonaResponse response =
        adminNpcService.updatePersona(
            "CAFE_STAFF",
            new PersonaRequest(
                "a cheerful barista who loves latte art.",
                "Soy milk is free today.",
                "A sunny corner cafe."));
    entityManager.flush();
    entityManager.clear();

    assertThat(response)
        .isEqualTo(
            new PersonaResponse(
                "CAFE_STAFF",
                "a cheerful barista who loves latte art.",
                "Soy milk is free today.",
                "A sunny corner cafe."));
    WorkerNpc workerNpc = workerNpcs.of(npcRepository.findByCode("CAFE_STAFF").orElseThrow());
    assertThat(workerNpc.role()).isEqualTo("a cheerful barista who loves latte art.");
    assertThat(workerNpc.facts()).isEqualTo("Soy milk is free today.");
    assertThat(workerNpc.scene()).isEqualTo("A sunny corner cafe.");
  }

  @Test
  @DisplayName("facts·scene을 보내지 않으면 빈 값으로 저장한다")
  void updatePersona_withoutFactsAndScene_savesEmpty() {
    PersonaResponse response =
        adminNpcService.updatePersona(
            "CAFE_STAFF", new PersonaRequest("the barista at the corner cafe.", null, null));
    entityManager.flush();
    entityManager.clear();

    assertThat(response.facts()).isEmpty();
    assertThat(response.scene()).isEmpty();
    WorkerNpc workerNpc = workerNpcs.of(npcRepository.findByCode("CAFE_STAFF").orElseThrow());
    assertThat(workerNpc.facts()).isEmpty();
    assertThat(workerNpc.scene()).isEmpty();
  }

  @Test
  @DisplayName("없는 NPC 코드면 NOT_FOUND다")
  void updatePersona_unknownNpc_throwsNotFound() {
    assertThatThrownBy(
            () ->
                adminNpcService.updatePersona(
                    "NO_SUCH_NPC", new PersonaRequest("someone.", null, null)))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }
}
