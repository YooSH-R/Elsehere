package com.elsehere.mission.service;

import com.elsehere.world.service.GameTime;
import jakarta.persistence.EntityManager;
import java.sql.Timestamp;
import java.time.Clock;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

/**
 * 시연을 처음부터 시작할 때 5분 시연 대본의 출발 상태를 맞춘다(docs/demo-rainy-evening.md "시연 계정"). 같은 계정으로 몇 번을 해도 같은 조건에서
 * 시작하도록, 대본에 나오는 NPC 관계와 게임 시각만 덮어쓴다. 대화·피드백·복습 기록은 건드리지 않는다.
 *
 * <ul>
 *   <li>Owen(호텔 프런트 보조): 지난번 무례했던 손님으로 기억해 사이가 나쁘다. 무례 한마디에 호감이 깎이는 장면을 보인다.
 *   <li>Rosa(Maple Burger): 단골을 알아보고 늘 시키던 메뉴를 묻는다. 메뉴는 시연 주문(치즈버거·감자튀김·콜라)과 일부러 다르다.
 *   <li>Jake(호텔 프런트)·택시 기사: 처음 만나는 상태로 되돌린다.
 *   <li>게임 시각: 해가 지기 전 저녁(누적 플레이 237초 = 1일차 오후 5시 28분).
 * </ul>
 */
@Component
@RequiredArgsConstructor
public class ShowcaseScene {
  static final String OWEN_MEMORY = "Was rude to me at the front desk last time.";
  public static final String ROSA_USUAL_MEMORY =
      "A regular who comes in most evenings and usually orders a Crispy Chicken burger with a lemonade.";
  // 오전 8시 시작에서 237초 = 게임 9시간 28분. 일몰 오후 7시까지 이동·탐색 시간을 남긴다.
  static final long EVENING_PLAY_SECONDS = 237;

  private final EntityManager entityManager;
  private final Clock clock;

  @Transactional
  public void setUp(Long userId) {
    int gameDay = GameTime.ofPlaySeconds(EVENING_PLAY_SECONDS).day();
    remember(userId, "HOTEL_RECEPTIONIST", -40, 20, 1, OWEN_MEMORY, "wary", gameDay);
    remember(userId, "DINER_STAFF", 100, 60, 6, ROSA_USUAL_MEMORY, "friendly", gameDay);
    entityManager
        .createNativeQuery(
            """
            update user_npc_relations
            set affinity = 0, recognition = 0, talk_count = 0, last_talked_at = null,
                last_talk_game_day = null, memory_card = null, memory_conversation_id = null,
                updated_at = ?2
            where user_id = ?1
              and npc_id in (select id from npcs where code in ('HOTEL_STAFF', 'TAXI_DRIVER'))
            """)
        .setParameter(1, userId)
        .setParameter(2, now())
        .executeUpdate();
    // 마지막 신호 시각을 비워 두면 다음 첫 신호는 시계를 시작만 하고 시간을 더하지 않는다.
    entityManager
        .createNativeQuery(
            """
            insert into user_game_clocks (user_id, play_seconds, last_beat_at, created_at, updated_at)
            values (?1, ?2, null, ?3, ?3)
            on conflict (user_id) do update
            set play_seconds = excluded.play_seconds, last_beat_at = null, updated_at = excluded.updated_at
            """)
        .setParameter(1, userId)
        .setParameter(2, EVENING_PLAY_SECONDS)
        .setParameter(3, now())
        .executeUpdate();
  }

  // 기억 카드 이름은 계정 닉네임이다. NPC가 시연자를 이름으로 부른다.
  private void remember(
      Long userId,
      String npcCode,
      int affinity,
      int recognition,
      int talkCount,
      String memory,
      String attitude,
      int gameDay) {
    entityManager
        .createNativeQuery(
            """
            insert into user_npc_relations
              (user_id, npc_id, affinity, recognition, talk_count, last_talked_at,
               last_talk_game_day, memory_card, memory_conversation_id, created_at, updated_at)
            select ?1, n.id, ?2, ?3, ?4, ?5, ?6,
                   jsonb_build_object('name', coalesce(u.nickname, ''), 'claims', '[]'::jsonb,
                                      'events', jsonb_build_array(cast(?7 as text)),
                                      'attitude', cast(?8 as text), 'struggled', '[]'::jsonb),
                   null, ?5, ?5
            from npcs n cross join users u
            where n.code = ?9 and u.id = ?1
            on conflict (npc_id, user_id) do update
            set affinity = excluded.affinity, recognition = excluded.recognition,
                talk_count = excluded.talk_count, last_talked_at = excluded.last_talked_at,
                last_talk_game_day = excluded.last_talk_game_day, memory_card = excluded.memory_card,
                memory_conversation_id = null, updated_at = excluded.updated_at
            """)
        .setParameter(1, userId)
        .setParameter(2, affinity)
        .setParameter(3, recognition)
        .setParameter(4, talkCount)
        .setParameter(5, now())
        .setParameter(6, gameDay)
        .setParameter(7, memory)
        .setParameter(8, attitude)
        .setParameter(9, npcCode)
        .executeUpdate();
  }

  private Timestamp now() {
    return Timestamp.from(clock.instant());
  }
}
