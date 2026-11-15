package com.elsehere.favor.service;

import com.elsehere.ai.client.WorkerState;
import com.elsehere.favor.entity.Favor;
import com.elsehere.favor.entity.FavorStatus;
import com.elsehere.favor.service.FavorRules.Offer;
import com.elsehere.favor.service.FavorRules.Request;
import java.util.Collection;
import java.util.List;
import java.util.Optional;

/**
 * 돌발 부탁의 판정 의도·대답 지시문·카드 문구(docs/npc-life-design.md 7.7·7.8·7.11). 무엇을 왜 하는지는 규칙 데이터(계기의 이유, 제공의
 * 영어·한국어 이름)에서 넣고, 말투와 문장은 LLM이 인격대로 만든다. 저장하지 않는다.
 */
public final class FavorScript {

  static final String ACCEPT = "accept_favor";
  static final String DECLINE = "decline_favor";
  static final String GET_FOR_REQUESTER = "get_for_requester";
  static final String HAND_OVER = "hand_over";

  /** 학습자가 NPC가 내비친 욕구에 반응했다(대화 속 욕구 부탁을 꺼내는 계기). */
  static final String NOTICE = "notice_need";

  private FavorScript() {}

  /** 지금 대화하는 NPC가 이 부탁에서 맡은 자리. */
  public enum Role {
    /** 부탁한 NPC */
    REQUESTER,
    /** 수락한 심부름의 해결처 가게 직원 */
    SHOP,
    NONE
  }

  /** 부탁 카드의 단계 한 줄. */
  public record Step(String placeCode, String npcCode, String textKo, boolean done) {}

  public static Role roleOf(FavorRules rules, Favor favor, String npcCode) {
    if (favor.getRequesterNpcCode().equals(npcCode)) {
      return Role.REQUESTER;
    }
    boolean atTheShop =
        favor.errand()
            && favor.getStatus() == FavorStatus.ACCEPTED
            && rules
                .npc(npcCode)
                .map(npc -> favor.getOfferPlace().equals(npc.workplace()))
                .orElse(false);
    return atTheShop ? Role.SHOP : Role.NONE;
  }

  /** 이 대화의 판정 상태에 더할 의도. */
  public static List<WorkerState.Intent> intents(FavorRules rules, Favor favor, Role role) {
    Request request = request(rules, favor);
    String requester = favor.getRequesterName();
    return switch (role) {
      case REQUESTER ->
          switch (favor.getStatus()) {
            case OFFERED ->
                favor.errand()
                    ? List.of(
                        new WorkerState.Intent(
                            ACCEPT,
                            "The learner agrees to "
                                + task(rules, favor)
                                + " for "
                                + requester
                                + "."),
                        new WorkerState.Intent(
                            DECLINE,
                            "The learner says they cannot or will not "
                                + task(rules, favor)
                                + " now."))
                    : List.of(
                        new WorkerState.Intent(request.goal().intent(), request.goal().desc()));
            case GOT ->
                List.of(
                    new WorkerState.Intent(
                        HAND_OVER,
                        "The learner gives "
                            + offer(rules, favor).en()
                            + " to "
                            + requester
                            + " or tells them the errand is done."));
            default -> List.of();
          };
      case SHOP ->
          List.of(
              new WorkerState.Intent(
                  GET_FOR_REQUESTER,
                  "The learner asks for "
                      + offer(rules, favor).en()
                      + " (to go) for someone else, or orders it to go."));
      case NONE -> List.of();
    };
  }

  /** 대화 속 욕구 부탁: 학습자가 NPC가 내비친 그 욕구에 반응했는지 판정할 의도. 설명은 계기 틀의 notice다. */
  public static WorkerState.Intent noticeIntent(FavorPicker.Pick pick) {
    return new WorkerState.Intent(NOTICE, pick.request().notice());
  }

  /** NPC가 먼저 말을 걸어 꺼낸 부탁의 첫마디 지시문. 학습자는 아직 아무 말도 하지 않았으므로 짧게 인사하고 부탁한다. */
  public static String callInstruction(FavorRules rules, Favor favor) {
    FavorPicker.Pick pick =
        new FavorPicker.Pick(
            request(rules, favor),
            favor.getOfferPlace(),
            favor.errand() ? offer(rules, favor) : null);
    return "You just walked up to the learner (or called them over) because you want to ask a favor;"
        + " they have not said anything yet. Greet them briefly in your own way first. "
        + offerInstruction(rules, pick);
  }

  /** 판정한 의도로 정해지는 다음 상태. 바뀌지 않으면 빈 값이다. */
  public static Optional<FavorStatus> advance(
      FavorRules rules, Favor favor, Role role, Collection<String> intentIds) {
    FavorStatus status = favor.getStatus();
    if (role == Role.REQUESTER && status == FavorStatus.OFFERED) {
      if (!favor.errand()) {
        return intentIds.contains(request(rules, favor).goal().intent())
            ? Optional.of(FavorStatus.DONE)
            : Optional.empty();
      }
      if (intentIds.contains(ACCEPT)) {
        return Optional.of(FavorStatus.ACCEPTED);
      }
      return intentIds.contains(DECLINE) ? Optional.of(FavorStatus.DECLINED) : Optional.empty();
    }
    if (role == Role.SHOP
        && status == FavorStatus.ACCEPTED
        && intentIds.contains(GET_FOR_REQUESTER)) {
      return Optional.of(FavorStatus.GOT);
    }
    if (role == Role.REQUESTER && status == FavorStatus.GOT && intentIds.contains(HAND_OVER)) {
      return Optional.of(FavorStatus.DONE);
    }
    return Optional.empty();
  }

  /** NPC가 부탁을 꺼내는 이번 대답의 지시문. */
  public static String offerInstruction(FavorRules rules, FavorPicker.Pick pick) {
    Request request = pick.request();
    String situation = "Something on your mind right now: " + request.reason() + " ";
    if (pick.offer() == null) {
      return situation
          + "Bring it up naturally and briefly in your own words, as a question the learner can"
          + " answer, then wait for their answer.";
    }
    String place = rules.places().get(pick.offerPlace()).name().en();
    // 상황 심부름(잔돈 바꾸기·편지 부치기·꽃)은 물건 사 오기가 아니라 해결처에서 그 일을 대신 봐 주는 것이다.
    String ask =
        pick.request().offer() != null
            ? "Naturally and briefly ask the learner if they could take care of it for you at "
                + place
                + " (it needs "
                + pick.offer().en()
                + ")"
            : "Naturally and briefly ask the learner if they could get you "
                + pick.offer().en()
                + " from "
                + place;
    return situation
        + ask
        + ". Say you will give them the money and a small thank-you. Then wait for their answer.";
  }

  /**
   * 부탁이 걸린 이번 대답의 지시문.
   *
   * @param after 이번 턴의 판정으로 바뀐 상태. 바뀌지 않았으면 빈 값
   */
  public static String instruction(
      FavorRules rules, Favor favor, Role role, Optional<FavorStatus> after) {
    String requester = favor.getRequesterName();
    FavorStatus status = after.orElse(favor.getStatus());
    if (role == Role.SHOP) {
      Offer offer = offer(rules, favor);
      return status == FavorStatus.GOT
          ? "The learner got "
              + offer.en()
              + " for "
              + requester
              + ". Hand it over with a friendly word."
          : "The learner is getting "
              + offer.en()
              + " for "
              + requester
              + ". Serve them as usual; it costs "
              + offer.price()
              + " coins, paid with "
              + requester
              + "'s money.";
    }
    if (role != Role.REQUESTER) {
      return "";
    }
    return switch (status) {
      case OFFERED ->
          "You just asked the learner for a favor. If they have not answered yet, wait for their"
              + " answer without repeating the whole request.";
      case ACCEPTED ->
          after.isPresent()
              ? "The learner agreed to help. Thank them and tell them to go to "
                  + placeName(rules, favor)
                  + "."
              : "You are still waiting for "
                  + offer(rules, favor).en()
                  + " the learner promised to get from "
                  + placeName(rules, favor)
                  + ". You may mention it lightly.";
      case GOT ->
          "The learner went to "
              + placeName(rules, favor)
              + " for you and is bringing "
              + offer(rules, favor).en()
              + ". When they hand it over, thank them warmly.";
      case DONE ->
          "The learner did what you asked. Thank them warmly and do not ask for anything else today.";
      case DECLINED -> "The learner cannot help now. Say it is fine and do not ask again today.";
      default -> "";
    };
  }

  /** 부탁 카드 제목. 예: Jake의 부탁 · 크루아상 사다 주기 */
  public static String title(FavorRules rules, Favor favor) {
    String task = favor.errand() ? offer(rules, favor).taskKo() : request(rules, favor).titleKo();
    return favor.getRequesterName() + "의 부탁 · " + task;
  }

  /** 부탁 카드의 단계. 심부름은 해결처와 돌아가기, 대화 안 부탁은 한 단계다. */
  public static List<Step> steps(FavorRules rules, Favor favor) {
    String requesterPlace =
        rules.npc(favor.getRequesterNpcCode()).map(FavorRules.Npc::workplace).orElse(null);
    FavorStatus status = favor.getStatus();
    if (!favor.errand()) {
      return List.of(
          new Step(
              requesterPlace,
              favor.getRequesterNpcCode(),
              request(rules, favor).titleKo(),
              status == FavorStatus.DONE));
    }
    String shop = rules.places().get(favor.getOfferPlace()).name().ko();
    return List.of(
        new Step(
            favor.getOfferPlace(),
            null,
            shop + "에서 " + offer(rules, favor).stepKo(),
            status == FavorStatus.GOT || status == FavorStatus.DONE),
        new Step(
            requesterPlace,
            favor.getRequesterNpcCode(),
            favor.getRequesterName() + "에게 돌아가기",
            status == FavorStatus.DONE));
  }

  /** 완료하면 부탁한 NPC가 채우는 욕구. 심부름은 제공의 fills, 대화 안 부탁은 계기 틀의 fills. */
  public static java.util.Map<String, Integer> fills(FavorRules rules, Favor favor) {
    if (favor.errand()) {
      return offer(rules, favor).fills();
    }
    var fills = request(rules, favor).reward().fills();
    return fills == null ? java.util.Map.of() : fills;
  }

  private static String task(FavorRules rules, Favor favor) {
    return "get " + offer(rules, favor).en() + " from " + placeName(rules, favor);
  }

  private static String placeName(FavorRules rules, Favor favor) {
    return rules.places().get(favor.getOfferPlace()).name().en();
  }

  private static Request request(FavorRules rules, Favor favor) {
    return rules
        .request(favor.getTemplateId())
        .orElseThrow(() -> new IllegalStateException("없는 계기 틀: " + favor.getTemplateId()));
  }

  private static Offer offer(FavorRules rules, Favor favor) {
    return rules
        .offer(favor.getOfferPlace(), favor.getOfferKey())
        .orElseThrow(
            () ->
                new IllegalStateException(
                    "없는 제공: " + favor.getOfferPlace() + "." + favor.getOfferKey()));
  }
}
