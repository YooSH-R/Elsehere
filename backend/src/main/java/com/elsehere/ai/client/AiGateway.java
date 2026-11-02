package com.elsehere.ai.client;

import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.util.List;
import java.util.function.Supplier;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

/**
 * AI 워커를 부르는 유일한 입구. 호출마다 AI 호출 기록(ai_requests)을 남긴다. 계약: docs/api/ai-worker.md
 *
 * <p>워커 오류로 끝난 호출은 FAILED로 남기고 오류를 그대로 던진다. 응답은 왔지만 쓸 수 없는 결과(NPC 대답 오류, 빈 힌트·추천 단어)도 FAILED로 남기고
 * 오류로 던지므로, 돌려받은 결과는 그대로 쓰면 된다. 모델을 부르지 않는 관계 갱신과 음성 파일 받기는 기록하지 않는다.
 *
 * <p>워커 클라이언트({@link AiWorkerClient})와 기록기({@link AiRequestRecorder})는 이 패키지 안에서만 쓸 수 있어, 다른 패키지는 이
 * 클래스로만 워커를 부른다.
 */
@Component
@RequiredArgsConstructor
public class AiGateway {

  private final AiWorkerClient aiWorkerClient;
  private final AiRequestRecorder aiRequestRecorder;

  /** 의도·번역 판정. */
  public JudgeResult judge(
      WorkerTrace trace, WorkerNpc npc, WorkerState state, List<WorkerLine> history, String text) {
    JudgeResult result =
        send(
            trace,
            AiRequestKind.JUDGE,
            () -> aiWorkerClient.judge(trace, npc, state, history, text));
    record(trace, AiRequestKind.JUDGE, result.usage(), true);
    return result;
  }

  /** 무례 판정. 판정하지 못해도(판정값 null) 워커가 답했으므로 OK로 남긴다. */
  public GuardResult guard(
      WorkerTrace trace, WorkerNpc npc, List<WorkerLine> history, String text) {
    GuardResult result =
        send(trace, AiRequestKind.GUARD, () -> aiWorkerClient.guard(trace, npc, history, text));
    record(trace, AiRequestKind.GUARD, result.usage(), true);
    return result;
  }

  /**
   * NPC 대답. 대답 생성이 실패하면 그때까지 쓴 사용량으로 FAILED를 남기고 워커 오류 코드에 맞는 오류로 던진다.
   *
   * @param text 학습자 원문(한국어가 섞여도 그대로)
   * @param instruction 이번 대답에서 할 일. 없으면 빈 문자열
   * @param learner 대답 상대인 학습자. 캐릭터에 맞는 호칭을 쓰게 한다
   */
  public NpcReply reply(
      WorkerTrace trace,
      WorkerNpc npc,
      WorkerMemory memory,
      List<WorkerLine> history,
      String text,
      String instruction,
      int level,
      WorkerLearner learner) {
    NpcReply reply =
        send(
            trace,
            AiRequestKind.NPC,
            () ->
                aiWorkerClient.reply(
                    trace, npc, memory, history, text, instruction, level, learner));
    record(trace, AiRequestKind.NPC, reply.usage(), reply.succeeded());
    if (!reply.succeeded()) {
      throw new BusinessException(AiWorkerClient.toErrorCode(reply.errorCode()));
    }
    return reply;
  }

  /** NPC 대답 스트림이 만든 음성 파일. 모델을 부르지 않으므로 기록하지 않는다. 워커 메모리에 10분만 남으므로 대답을 받은 직후 부른다. */
  public byte[] audio(WorkerTrace trace, String audioId) {
    return aiWorkerClient.audio(trace, audioId);
  }

  /** 녹음 전사. */
  public SttResult transcribe(WorkerTrace trace, byte[] audio, String contentType) {
    SttResult result =
        send(trace, AiRequestKind.STT, () -> aiWorkerClient.transcribe(trace, audio, contentType));
    record(trace, AiRequestKind.STT, result.usage(), true);
    return result;
  }

  /** NPC 대사 한 줄의 한국어 뜻과 단어 풀이(CONV-04 뜻 보기). */
  public MeaningResult meaning(WorkerTrace trace, WorkerNpc npc, String text, int level) {
    MeaningResult result =
        send(trace, AiRequestKind.MEANING, () -> aiWorkerClient.meaning(trace, npc, text, level));
    record(trace, AiRequestKind.MEANING, result.usage(), true);
    return result;
  }

  /**
   * 학습자가 다음에 할 만한 말(CONV-05 힌트). 추천 문장이 하나도 없으면 FAILED를 남기고 AI_RESPONSE_INVALID로 던진다.
   *
   * @param history 대답할 NPC 줄까지의 앞선 대화
   * @param goal 지금 학습자가 할 일. 없으면 빈 문자열
   */
  public HintResult hint(
      WorkerTrace trace, WorkerNpc npc, List<WorkerLine> history, String goal, int level) {
    HintResult result =
        send(
            trace, AiRequestKind.HINT, () -> aiWorkerClient.hint(trace, npc, history, goal, level));
    String sentence = result.firstSentence();
    requireUsable(
        trace, AiRequestKind.HINT, result.usage(), sentence != null && !sentence.isBlank());
    return result;
  }

  /**
   * 학습자가 다음 대답에 쓸 만한 단어(CONV-12 추천 단어). 단어가 하나도 없으면 FAILED를 남기고 AI_RESPONSE_INVALID로 던진다.
   *
   * <p>추천 단어는 자유 대화 힌트의 첫 단계(CONV-05)라 힌트 호출로 기록한다. 사용량의 step은 suggest로 남는다.
   *
   * @param history 대답할 NPC 줄까지의 앞선 대화
   * @param goal 지금 학습자가 할 일. 없으면 빈 문자열
   */
  public SuggestResult suggest(
      WorkerTrace trace, WorkerNpc npc, List<WorkerLine> history, String goal, int level) {
    SuggestResult result =
        send(
            trace,
            AiRequestKind.HINT,
            () -> aiWorkerClient.suggest(trace, npc, history, goal, level));
    requireUsable(
        trace,
        AiRequestKind.HINT,
        result.usage(),
        result.words() != null && !result.words().isEmpty());
    return result;
  }

  /** 한 문장 음성 합성. WAV를 돌려준다. 워커가 사용량을 알리지 않아 호출만 남는다. */
  public byte[] synthesize(WorkerTrace trace, String text, String voice, double speed) {
    byte[] wav =
        send(trace, AiRequestKind.TTS, () -> aiWorkerClient.synthesize(trace, text, voice, speed));
    record(trace, AiRequestKind.TTS, List.of(), true);
    return wav;
  }

  /**
   * NPC 기억 카드 갱신. 이번 대화와 이전 카드로 새 카드 전체를 만든다.
   *
   * @param outcome 대화 결과 한 줄(영어)
   * @param history 대화 전체(최대 200줄)
   * @param previousCard 이전 카드. 없으면 null
   */
  public MemoryCardResult memoryCard(
      WorkerTrace trace,
      WorkerNpc npc,
      String outcome,
      List<WorkerLine> history,
      Object previousCard) {
    MemoryCardResult result =
        send(
            trace,
            AiRequestKind.MEMORY,
            () -> aiWorkerClient.memoryCard(trace, npc, outcome, history, previousCard));
    record(trace, AiRequestKind.MEMORY, result.usage(), true);
    return result;
  }

  /**
   * 대화 뒤 관계 갱신. 저장된 관계와 대화 전체로 알아봄·호감 변화량을 계산한다. 모델을 부르지 않으므로 기록하지 않는다(워커 문서 2.7.2).
   *
   * @param relationship 저장된 관계와 마지막 대화 뒤 지난 게임 날 수
   * @param history 대화 전체(최대 200줄). 학습자 줄의 무례·사건을 담는다
   */
  public RelationshipChange relationship(
      WorkerTrace trace, WorkerNpc npc, WorkerRelationship relationship, List<WorkerLine> history) {
    return aiWorkerClient.relationship(trace, npc, relationship, history);
  }

  /** 대화 종료 피드백. 평가할 발화 부족·생성 실패는 결과의 상태로 돌려주고, completed가 아니면 FAILED로 남긴다. */
  public WorkerFeedback feedback(WorkerTrace trace, FeedbackInput input) {
    WorkerFeedback result =
        send(trace, AiRequestKind.FEEDBACK, () -> aiWorkerClient.feedback(trace, input));
    record(trace, AiRequestKind.FEEDBACK, result.usage(), result.completed());
    return result;
  }

  // 워커 오류(연결 실패·시간 초과·오류 응답)로 끝나면 FAILED로 남기고 그대로 던진다.
  private <T> T send(WorkerTrace trace, AiRequestKind kind, Supplier<T> call) {
    try {
      return call.get();
    } catch (RuntimeException exception) {
      aiRequestRecorder.record(trace, kind, List.of(), AiRequestStatus.FAILED);
      throw exception;
    }
  }

  private void requireUsable(
      WorkerTrace trace, AiRequestKind kind, List<WorkerUsage> usage, boolean usable) {
    record(trace, kind, usage, usable);
    if (!usable) {
      throw new BusinessException(ErrorCode.AI_RESPONSE_INVALID);
    }
  }

  private void record(
      WorkerTrace trace, AiRequestKind kind, List<WorkerUsage> usage, boolean succeeded) {
    aiRequestRecorder.record(
        trace, kind, usage, succeeded ? AiRequestStatus.OK : AiRequestStatus.FAILED);
  }
}
