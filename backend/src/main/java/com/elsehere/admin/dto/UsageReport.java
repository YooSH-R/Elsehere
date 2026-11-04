package com.elsehere.admin.dto;

import java.util.List;

/**
 * 어드민 이용 현황 화면의 공통 부분. 최근 {@code days}일 동안 DB에 남은 활동 흔적을 묶는다. 전체 화면은 모든 사용자를, 사용자별 화면은 그 사용자만 센다.
 *
 * @param byDay 한국 날짜별. 활동이 있는 날만 오래된 날부터
 * @param conversationEnds 기간 안에 시작한 대화의 끝난 방식(conversations.end_reason). 끝내지 않은 대화는 ACTIVE
 * @param conversationsByNpc 기간 안에 시작한 대화의 NPC("이름 · 역할 이름")
 * @param conversationActions 대화 중 학습자의 행동. 음성(VOICE)·글(TEXT)로 보낸 말과 힌트·추천 단어(HINT)·뜻 보기(MEANING) 요청
 * @param wordSaves 단어장에 저장한 곳(user_vocab.source_type)
 * @param feedbacks 대화 피드백 결과(conversation_feedback.status). 사용자가 건너뛴 것은 SKIPPED_BY_USER, 평가할 말이 없는
 *     등 다른 이유로 건너뛴 것은 SKIPPED
 */
public record UsageReport(
    int days,
    Summary summary,
    List<Day> byDay,
    List<Count> conversationEnds,
    List<Count> conversationsByNpc,
    List<Count> conversationActions,
    List<Count> wordSaves,
    List<Count> feedbacks) {

  /**
   * 기간 전체 합계.
   *
   * @param activeUsers 기간 안에 활동 흔적이 있는 사용자. 게스트·가입자 구분은 지금 계정 기준이다
   * @param activeUserDays 활동 사용자마다 활동한 한국 날짜 수를 더한 값
   * @param returningUsers 서로 다른 이틀 이상 활동한 사용자
   * @param newUsers 기간 안에 계정을 만든 사용자
   * @param conversions 기간 안에 게스트에서 Google 가입으로 바꾼 사용자
   * @param conversations 기간 안에 시작한 NPC 대화
   * @param endedConversations 그 가운데 끝낸 대화
   * @param conversingUsers 그 대화를 한 사용자
   * @param missionsCompleted 기간 안에 완료한 오늘의 미션
   * @param wordsSaved 기간 안에 단어장에 저장한 단어
   * @param playSeconds 활동 사용자의 누적 플레이 시간(초) 합. 기간과 관계없이 지금까지 쌓인 값이다
   */
  public record Summary(
      long activeUsers,
      long activeGuests,
      long activeMembers,
      long activeUserDays,
      long returningUsers,
      long newUsers,
      long conversions,
      long conversations,
      long endedConversations,
      long conversingUsers,
      long missionsCompleted,
      long wordsSaved,
      long playSeconds) {

    /** 활동 사용자 가운데 다시 온 사용자 비율(0~1) */
    public double returningRate() {
      return ratio(returningUsers, activeUsers);
    }

    /** 활동 사용자 1인당 활동한 날 수 */
    public double averageActiveDays() {
      return ratio(activeUserDays, activeUsers);
    }

    /** 활동 사용자 1인당 누적 플레이 시간(분) */
    public double averagePlayMinutes() {
      return ratio(playSeconds, activeUsers) / 60;
    }

    /** 시작한 대화 가운데 끝낸 대화 비율(0~1) */
    public double endedRate() {
      return ratio(endedConversations, conversations);
    }

    /** 대화한 사용자 1인당 대화 수 */
    public double conversationsPerUser() {
      return ratio(conversations, conversingUsers);
    }

    // 나눌 수가 0이면(기간 안에 아무도 없으면) 0이다.
    private static double ratio(long part, long whole) {
      return whole == 0 ? 0 : (double) part / whole;
    }
  }

  /**
   * 한국 날짜 하루의 활동.
   *
   * @param day 한국 날짜(yyyy-MM-dd)
   * @param activeUsers 그날 활동 흔적이 있는 사용자. 게스트·가입자 구분은 지금 계정 기준이다
   * @param newUsers 그날 계정을 만든 사용자(게스트 시작·바로 가입)
   * @param conversions 그날 게스트에서 Google 가입으로 바꾼 사용자
   * @param conversations 그날 시작한 NPC 대화
   * @param missionsCompleted 그날 완료한 오늘의 미션
   * @param wordsSaved 그날 단어장에 저장한 단어
   */
  public record Day(
      String day,
      long activeUsers,
      long activeGuests,
      long activeMembers,
      long newUsers,
      long conversions,
      long conversations,
      long missionsCompleted,
      long wordsSaved) {}

  /**
   * 기준 하나로 묶은 횟수. 많은 것부터 보여 준다.
   *
   * @param label 묶은 기준 값(코드 값은 화면에서 이름으로 바꿈)
   * @param users 그렇게 한 사용자 수
   */
  public record Count(String label, long count, long users) {}
}
