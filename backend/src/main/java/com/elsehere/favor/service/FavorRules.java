package com.elsehere.favor.service;

import java.io.IOException;
import java.io.InputStream;
import java.io.UncheckedIOException;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import org.springframework.core.io.ClassPathResource;
import org.springframework.stereotype.Component;
import tools.jackson.databind.DeserializationFeature;
import tools.jackson.databind.json.JsonMapper;

/**
 * 대화 중 돌발 부탁 규칙(content/npc-favors.json). AI 규칙 원본(ai/npcs/rules/requests.json, world-rules.json의
 * 장소 제공 목록· 부탁 상수)과 NPC 근무지·일과를 ai/tools/npc_life_export.py가 내보낸 파일이라 직접 고치지
 * 않는다(docs/npc-life-design.md 7절).
 */
@Component
public class FavorRules {

  static final String SOURCE = "content/npc-favors.json";
  static final int MINUTES_PER_DAY = 24 * 60;

  private final Source source;

  public FavorRules() {
    this(read());
  }

  FavorRules(Source source) {
    this.source = source;
  }

  public Constants constants() {
    return source.constants();
  }

  /** 부탁 인상이 호감으로 바뀌는 비율(relationship.affinityFollowRate). */
  public double affinityFollowRate() {
    return source.affinityFollowRate();
  }

  public List<Request> requests() {
    return source.requests();
  }

  public Map<String, Place> places() {
    return source.places();
  }

  public Optional<Request> request(String id) {
    return source.requests().stream().filter(request -> request.id().equals(id)).findFirst();
  }

  public Optional<Npc> npc(String npcCode) {
    return source.npcs().stream().filter(npc -> npc.npcId().equals(npcCode)).findFirst();
  }

  public Optional<Offer> offer(String placeCode, String key) {
    return Optional.ofNullable(source.places().get(placeCode)).stream()
        .flatMap(place -> place.offers().stream())
        .filter(offer -> offer.key().equals(key))
        .findFirst();
  }

  private static Source read() {
    try (InputStream input = new ClassPathResource(SOURCE).getInputStream()) {
      return JsonMapper.builder()
          .disable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES)
          .build()
          .readValue(input, Source.class);
    } catch (IOException exception) {
      throw new UncheckedIOException(SOURCE + "을 읽지 못했습니다.", exception);
    }
  }

  record Source(
      Constants constants,
      double affinityFollowRate,
      Map<String, Place> places,
      List<Request> requests,
      List<Npc> npcs) {}

  /**
   * 빈도·기한 상수.
   *
   * @param checkAfterLearnerLines 학습자의 이 번째 정상 발화 뒤에 부탁을 볼지 정한다
   * @param learnerGapMinutes 대화 속 부탁이 끝난 뒤 새 대화 속 부탁을 꺼내지 않는 게임 분
   * @param callGapMinutes NPC가 말 거는 부탁이 끝난 뒤 다음 말 걸기까지의 게임 분. 사용자별 게임 시각은 플레이하는 동안만 흘러 플레이어 기준 간격이다
   * @param errandExpiryMinutes 심부름 기한(게임 분)
   * @param minShiftLeftMinutes 근무가 이만큼 남은 NPC만 심부름을 부탁한다
   * @param dislikedBelow 호감이 이보다 낮으면 부탁하지 않는다
   */
  public record Constants(
      int checkAfterLearnerLines,
      int learnerGapMinutes,
      int callGapMinutes,
      int errandExpiryMinutes,
      int minShiftLeftMinutes,
      int dislikedBelow) {}

  public record Place(Name name, List<Offer> offers) {}

  public record Name(String en, String ko) {}

  /** 학습자가 이 장소에서 남을 대신해 사거나 부탁할 수 있는 것. fills가 없으면 상황 계기가 찾는 서비스다. */
  public record Offer(
      String key,
      String en,
      String ko,
      String taskKo,
      String stepKo,
      Map<String, Integer> fills,
      int price,
      List<String> languageFocus) {

    public Map<String, Integer> fills() {
      return fills == null ? Map.of() : fills;
    }
  }

  /** 계기 틀. 욕구 계기(need·below) 또는 상황 계기(places·windows)와 해결 방식(TALK·ERRAND). */
  public record Request(
      String id,
      List<String> opens,
      String titleKo,
      Trigger trigger,
      String notice,
      List<String> roles,
      List<String> activities,
      String solve,
      Goal goal,
      OfferRef offer,
      String reason,
      String ko,
      Reward reward) {

    public boolean errand() {
      return "ERRAND".equals(solve);
    }

    public boolean needTrigger() {
      return trigger.need() != null;
    }

    /** 부탁이 시작될 수 있는 곳(TALK·CALL)인지. */
    public boolean opensIn(String opening) {
      return opens.contains(opening);
    }
  }

  public record Trigger(
      String need,
      Integer below,
      List<String> places,
      List<Window> windows,
      Boolean firstTalkOfDay) {

    public boolean onlyFirstTalkOfDay() {
      return Boolean.TRUE.equals(firstTalkOfDay);
    }
  }

  /** 게임 시각 범위. to가 from보다 이르면 자정을 넘긴다. */
  public record Window(String from, String to) {

    public boolean covers(int minuteOfDay) {
      int start = minuteOf(from);
      int length = Math.floorMod(minuteOf(to) - start, MINUTES_PER_DAY);
      return Math.floorMod(minuteOfDay - start, MINUTES_PER_DAY)
          < (length == 0 ? MINUTES_PER_DAY : length);
    }
  }

  public record Goal(String intent, String desc, List<String> languageFocus) {}

  public record OfferRef(String place, String key) {}

  public record Reward(int coins, int impression, Map<String, Integer> fills) {}

  /** NPC 한 명의 역할·근무지·일과(npc_life_export가 계산한 것). */
  public record Npc(String npcId, String role, String workplace, List<Block> schedule) {

    /** 그 시각의 활동(WORK·BREAK·FREE·OFF). */
    public String activityAt(int minuteOfDay) {
      for (Block block : schedule) {
        int start = minuteOf(block.from());
        int length = Math.floorMod(minuteOf(block.to()) - start, MINUTES_PER_DAY);
        if (Math.floorMod(minuteOfDay - start, MINUTES_PER_DAY)
            < (length == 0 ? MINUTES_PER_DAY : length)) {
          return block.activity();
        }
      }
      return "OFF";
    }

    /** 지금 활동이 바뀔 때까지 남은 게임 분. 하루 내내 같으면 하루. */
    public int minutesLeft(int minuteOfDay) {
      String now = activityAt(minuteOfDay);
      for (int passed = 10; passed < MINUTES_PER_DAY; passed += 10) {
        if (!activityAt(Math.floorMod(minuteOfDay + passed, MINUTES_PER_DAY)).equals(now)) {
          return passed;
        }
      }
      return MINUTES_PER_DAY;
    }
  }

  public record Block(String from, String to, String activity) {}

  static int minuteOf(String clock) {
    String[] parts = clock.split(":");
    return Integer.parseInt(parts[0]) * 60 + Integer.parseInt(parts[1]);
  }
}
