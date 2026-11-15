package com.elsehere.favor.service;

import com.elsehere.favor.service.FavorRules.Offer;
import com.elsehere.favor.service.FavorRules.Request;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.TreeMap;

/**
 * 돌발 부탁 고르기(docs/npc-life-design.md 7.4·7.6). 기준 아래인 욕구 계기와 지금 때가 맞는 상황 계기를 모두 후보로 두고, 급할수록 무겁게(욕구
 * 계기 1 + 2 × (기준 − 값) / 기준, 상황 계기 1) npcId+게임 날짜 씨앗으로 하나를 고른다. 확률을 쓰지 않아 같은 날 같은 NPC는 같은 부탁을 하고 날마다
 * 바뀐다. 심부름 해결처도 그 욕구를 채우는 제공 가운데 자기 근무지가 아닌 것에서 씨앗으로 고른다. 저장은 하지 않는다.
 */
public final class FavorPicker {

  private FavorPicker() {}

  /**
   * 부탁을 고를 때 보는 지금 상황.
   *
   * @param needs 대화를 시작한 순간 NPC의 욕구. 생활하지 않는 NPC면 빈 맵
   * @param firstTalkOfDay 그 게임 날 이 학습자와 이 NPC의 첫 대화인지
   * @param shiftLeftMinutes 지금 활동(근무·여가)이 끝날 때까지 남은 게임 분
   * @param situationsUsedToday 이 학습자가 그 게임 날 이미 겪은 상황 계기 id
   */
  public record Situation(
      String npcCode,
      String role,
      String workplace,
      String activity,
      Map<String, Double> needs,
      int day,
      int minuteOfDay,
      boolean firstTalkOfDay,
      int shiftLeftMinutes,
      Set<String> situationsUsedToday) {}

  /**
   * 고른 부탁.
   *
   * @param offerPlace 심부름 해결처 장소 코드. 그 대화 안에서 끝나는 부탁이면 null
   * @param offer 심부름 해결처의 제공. 그 대화 안에서 끝나는 부탁이면 null
   */
  public record Pick(Request request, String offerPlace, Offer offer) {}

  public static Optional<Pick> pick(FavorRules rules, Situation situation) {
    boolean canSendOnErrand =
        situation.shiftLeftMinutes() >= rules.constants().minShiftLeftMinutes();
    List<Candidate> candidates = new ArrayList<>();
    for (Request request : rules.requests()) {
      if (!request.roles().contains(situation.role())
          || !request.activities().contains(situation.activity())) {
        continue;
      }
      if (request.needTrigger()) {
        needCandidate(rules, situation, request, canSendOnErrand).ifPresent(candidates::add);
      } else {
        situationCandidate(rules, situation, request, canSendOnErrand).ifPresent(candidates::add);
      }
    }
    return weighted(candidates, situation.npcCode() + "|" + situation.day() + "|pick");
  }

  private static Optional<Candidate> needCandidate(
      FavorRules rules, Situation situation, Request request, boolean canSendOnErrand) {
    String need = request.trigger().need();
    Double value = situation.needs().get(need);
    int below = request.trigger().below();
    if (value == null || value >= below) {
      return Optional.empty();
    }
    double weight = 1 + 2 * (below - value) / below;
    if (!request.errand()) {
      return Optional.of(new Candidate(weight, new Pick(request, null, null)));
    }
    if (!canSendOnErrand) {
      return Optional.empty();
    }
    // 장소 코드 순으로 늘어놓아 씨앗이 같으면 늘 같은 해결처가 된다.
    List<Pick> errands = new ArrayList<>();
    new TreeMap<>(rules.places())
        .forEach(
            (placeCode, place) -> {
              if (placeCode.equals(situation.workplace())) {
                return;
              }
              place.offers().stream()
                  .filter(offer -> offer.fills().containsKey(need))
                  .forEach(offer -> errands.add(new Pick(request, placeCode, offer)));
            });
    if (errands.isEmpty()) {
      return Optional.empty();
    }
    Pick errand =
        errands.get(seed(situation.npcCode() + "|" + situation.day() + "|" + need, errands.size()));
    return Optional.of(new Candidate(weight, errand));
  }

  private static Optional<Candidate> situationCandidate(
      FavorRules rules, Situation situation, Request request, boolean canSendOnErrand) {
    var trigger = request.trigger();
    if (trigger.places() != null && !trigger.places().contains(situation.workplace())) {
      return Optional.empty();
    }
    if (trigger.windows().stream().noneMatch(window -> window.covers(situation.minuteOfDay()))
        || (trigger.onlyFirstTalkOfDay() && !situation.firstTalkOfDay())
        || situation.situationsUsedToday().contains(request.id())) {
      return Optional.empty();
    }
    if (!request.errand()) {
      return Optional.of(new Candidate(1, new Pick(request, null, null)));
    }
    var ref = request.offer();
    if (!canSendOnErrand || ref.place().equals(situation.workplace())) {
      return Optional.empty();
    }
    return rules
        .offer(ref.place(), ref.key())
        .map(offer -> new Candidate(1, new Pick(request, ref.place(), offer)));
  }

  private static Optional<Pick> weighted(List<Candidate> candidates, String key) {
    if (candidates.isEmpty()) {
      return Optional.empty();
    }
    double total = candidates.stream().mapToDouble(Candidate::weight).sum();
    double roll = unit(key) * total;
    for (Candidate candidate : candidates) {
      roll -= candidate.weight();
      if (roll < 0) {
        return Optional.of(candidate.pick());
      }
    }
    return Optional.of(candidates.getLast().pick());
  }

  private static int seed(String key, int size) {
    return (int) (unit(key) * size);
  }

  // 0 이상 1 미만의 정해진 수. 같은 글자면 늘 같은 값이다.
  private static double unit(String key) {
    try {
      byte[] digest =
          MessageDigest.getInstance("SHA-256").digest(key.getBytes(StandardCharsets.UTF_8));
      long value = 0;
      for (int i = 0; i < 4; i++) {
        value = (value << 8) | (digest[i] & 0xff);
      }
      return value / (double) (1L << 32);
    } catch (NoSuchAlgorithmException exception) {
      throw new IllegalStateException(exception);
    }
  }

  private record Candidate(double weight, Pick pick) {}
}
