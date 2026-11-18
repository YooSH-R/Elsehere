package com.elsehere.mission.service;

import com.elsehere.user.entity.User;
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
 * <p>덮어쓰기 전 값은 보관해 두었다가 시연을 벗어나면(끝냄·멈춤·처음으로) 그대로 돌려놓는다. 시연 동안 바뀐 네 NPC와의 관계와 흐른 게임 시간은 일반 진행에 남기지
 * 않는다. 멈춘 시연은 멈춘 때의 상태를 따로 보관해 이어 할 때 되살린다.
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

  // 보관 종류. 시작 전 계정 상태와, 멈춘 시연의 상태.
  private static final String ORIGINAL = "ORIGINAL";
  private static final String SHOWCASE = "SHOWCASE";
  private static final String SCRIPTED_NPCS =
      "(select id from npcs"
          + " where code in ('HOTEL_RECEPTIONIST', 'DINER_STAFF', 'HOTEL_STAFF', 'TAXI_DRIVER'))";

  private final EntityManager entityManager;
  private final Clock clock;

  /** 대본의 출발 상태로 맞춘다. 시작 전 값을 아직 보관하지 않았으면 먼저 보관한다. */
  @Transactional
  public void setUp(Long userId) {
    drop(userId, SHOWCASE);
    save(userId, ORIGINAL);
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

  /** 시연을 멈춘다. 지금의 시연 상태를 보관하고 시작 전 상태로 돌려놓는다. 시연 상태가 아니면 아무것도 하지 않는다. */
  @Transactional
  public void pause(Long userId) {
    if (!has(userId, ORIGINAL)) {
      return;
    }
    drop(userId, SHOWCASE);
    save(userId, SHOWCASE);
    load(userId, ORIGINAL);
  }

  /** 멈춘 시연을 이어 한다. 지금 상태를 시작 전 값으로 보관하고 멈춘 때의 시연 상태를 되살린다. 멈춘 시연이 없으면 아무것도 하지 않는다. */
  @Transactional
  public void resume(Long userId) {
    if (!has(userId, SHOWCASE)) {
      return;
    }
    save(userId, ORIGINAL);
    load(userId, SHOWCASE);
  }

  /**
   * 시연을 벗어난다. 시작 전 상태로 돌려놓고 보관한 것을 지운다.
   *
   * @return 돌려놓았는지. 이미 시작 전 상태면 false
   */
  @Transactional
  public boolean leave(Long userId) {
    drop(userId, SHOWCASE);
    if (!has(userId, ORIGINAL)) {
      return false;
    }
    load(userId, ORIGINAL);
    return true;
  }

  private boolean has(Long userId, String kind) {
    return !entityManager
        .createNativeQuery("select 1 from showcase_saved_states where user_id = ?1 and kind = ?2")
        .setParameter(1, userId)
        .setParameter(2, kind)
        .getResultList()
        .isEmpty();
  }

  private void drop(Long userId, String kind) {
    entityManager
        .createNativeQuery("delete from showcase_saved_states where user_id = ?1 and kind = ?2")
        .setParameter(1, userId)
        .setParameter(2, kind)
        .executeUpdate();
  }

  // 대본이 덮어쓰는 네 NPC와의 관계 행 전체와 게임 시계를 보관한다. 같은 종류를 이미 보관했으면 먼저 보관한 것을 둔다.
  private void save(Long userId, String kind) {
    entityManager
        .createNativeQuery(
            """
            insert into showcase_saved_states (user_id, kind, play_seconds, relations, created_at)
            select ?1, ?2,
                   (select play_seconds from user_game_clocks where user_id = ?1),
                   coalesce(
                       (select jsonb_agg(to_jsonb(r)) from user_npc_relations r
                        where r.user_id = ?1 and r.npc_id in %s),
                       '[]'::jsonb),
                   ?3
            on conflict (user_id, kind) do nothing
            """
                .formatted(SCRIPTED_NPCS))
        .setParameter(1, userId)
        .setParameter(2, kind)
        .setParameter(3, now())
        .executeUpdate();
  }

  // 보관한 관계와 게임 시계로 바꿔 놓고 보관한 것을 지운다. 보관할 때 없던 관계·시계는 지운다.
  private void load(Long userId, String kind) {
    entityManager
        .createNativeQuery(
            "delete from user_npc_relations where user_id = ?1 and npc_id in " + SCRIPTED_NPCS)
        .setParameter(1, userId)
        .executeUpdate();
    entityManager
        .createNativeQuery(
            """
            insert into user_npc_relations
            select r.*
            from showcase_saved_states s
                 cross join lateral
                 jsonb_populate_recordset(null::user_npc_relations, s.relations) r
            where s.user_id = ?1 and s.kind = ?2
            """)
        .setParameter(1, userId)
        .setParameter(2, kind)
        .executeUpdate();
    entityManager
        .createNativeQuery(
            """
            delete from user_game_clocks
            where user_id = ?1
              and exists (select 1 from showcase_saved_states s
                          where s.user_id = ?1 and s.kind = ?2 and s.play_seconds is null)
            """)
        .setParameter(1, userId)
        .setParameter(2, kind)
        .executeUpdate();
    // 마지막 신호 시각을 비워 두면 다음 첫 신호는 시계를 시작만 하고 시간을 더하지 않는다.
    entityManager
        .createNativeQuery(
            """
            insert into user_game_clocks (user_id, play_seconds, last_beat_at, created_at, updated_at)
            select ?1, s.play_seconds, null, ?3, ?3
            from showcase_saved_states s
            where s.user_id = ?1 and s.kind = ?2 and s.play_seconds is not null
            on conflict (user_id) do update
            set play_seconds = excluded.play_seconds, last_beat_at = null, updated_at = excluded.updated_at
            """)
        .setParameter(1, userId)
        .setParameter(2, kind)
        .setParameter(3, now())
        .executeUpdate();
    drop(userId, kind);
  }

  // 기억 카드 이름은 NPC가 부를 이름(회원은 닉네임, 게스트는 kimssafy)이다. NPC가 시연자를 이름으로 부른다.
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
                   jsonb_build_object('name', cast(?10 as text), 'claims', '[]'::jsonb,
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
        .setParameter(10, entityManager.getReference(User.class, userId).nameForNpcs())
        .executeUpdate();
  }

  private Timestamp now() {
    return Timestamp.from(clock.instant());
  }
}
