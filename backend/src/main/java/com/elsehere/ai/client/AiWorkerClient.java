package com.elsehere.ai.client;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.net.http.HttpTimeoutException;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.TimeUnit;
import java.util.stream.Collectors;
import java.util.stream.Stream;
import lombok.extern.slf4j.Slf4j;
import org.slf4j.MDC;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;
import tools.jackson.core.JacksonException;
import tools.jackson.databind.DeserializationFeature;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.PropertyNamingStrategies;
import tools.jackson.databind.json.JsonMapper;

/**
 * AI 워커(FastAPI) 호출. 같은 서버의 내부 주소로 부르며 모든 요청에 내부 토큰을 붙인다. 계약: docs/api/ai-worker.md
 *
 * <p>워커의 칸 이름은 snake_case라 전용 JSON 변환기를 쓴다. 워커 오류는 API 오류 코드로 바꿔 {@link BusinessException}으로 던진다.
 *
 * <p>호출 기록이 빠지지 않도록 패키지 밖에서는 쓸 수 없다. 다른 패키지는 {@link AiGateway}로 부른다.
 */
@Slf4j
@Component
class AiWorkerClient {

  private static final Duration CONNECT_TIMEOUT = Duration.ofSeconds(3);
  // 의도·무례 판정은 워커가 5초 안에 끝낸다(JUDGE_BUDGET_S·GUARD_BUDGET_S). NPC 대답은 응답 헤더까지의 상한이며, 스트림은 워커의 시간 상한을
  // 따른다.
  private static final Duration JUDGE_TIMEOUT = Duration.ofSeconds(15);
  private static final Duration REPLY_TIMEOUT = Duration.ofSeconds(30);
  private static final Duration AUDIO_TIMEOUT = Duration.ofSeconds(10);
  private static final Duration STT_TIMEOUT = Duration.ofSeconds(45);
  private static final Duration TTS_TIMEOUT = Duration.ofSeconds(30);
  // 워커의 피드백 전체 상한(FEEDBACK_TIMEOUT_S, 기본 60초)보다 조금 길게 기다린다.
  private static final Duration FEEDBACK_TIMEOUT = Duration.ofSeconds(75);
  private static final Duration MEMORY_TIMEOUT = Duration.ofSeconds(45);
  // 관계 갱신은 모델을 부르지 않는 계산이다.
  private static final Duration RELATIONSHIP_TIMEOUT = Duration.ofSeconds(10);
  // 뜻 보기·힌트는 짧은 모델 호출 한 번이다. 재시도·대체 모델까지 기다리도록 NPC 대답과 같게 둔다.
  private static final Duration HELP_TIMEOUT = Duration.ofSeconds(30);

  private final HttpClient httpClient =
      HttpClient.newBuilder()
          .connectTimeout(CONNECT_TIMEOUT)
          .version(HttpClient.Version.HTTP_1_1)
          .build();
  private final JsonMapper jsonMapper =
      JsonMapper.builder()
          .propertyNamingStrategy(PropertyNamingStrategies.SNAKE_CASE)
          .disable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES)
          .build();
  private final String baseUrl;
  private final String token;

  AiWorkerClient(
      @Value("${elsehere.ai-worker.url}") String baseUrl,
      @Value("${elsehere.ai-worker.token}") String token) {
    this.baseUrl = baseUrl.endsWith("/") ? baseUrl.substring(0, baseUrl.length() - 1) : baseUrl;
    this.token = token;
  }

  /** 의도·번역 판정. 무례 판정(/v1/guard)은 따로 부른다. */
  public JudgeResult judge(
      WorkerTrace trace, WorkerNpc npc, WorkerState state, List<WorkerLine> history, String text) {
    HttpRequest request =
        jsonRequest("/v1/judge", trace, JUDGE_TIMEOUT, new JudgeBody(npc, state, history, text));
    return readJson(send(request, HttpResponse.BodyHandlers.ofString()), JudgeResult.class);
  }

  /** 무례 판정. 판정 모델이 실패해도 워커는 200에 판정값 null로 돌려준다. */
  public GuardResult guard(
      WorkerTrace trace, WorkerNpc npc, List<WorkerLine> history, String text) {
    HttpRequest request =
        jsonRequest("/v1/guard", trace, JUDGE_TIMEOUT, new GuardBody(npc, history, text));
    return readJson(send(request, HttpResponse.BodyHandlers.ofString()), GuardResult.class);
  }

  /**
   * NPC 대답. 스트림을 끝까지 받아 대답 전문·턴 전체 음성 ID를 모은다. 추천 단어는 {@link #suggest}로 따로 받는다. 생성이 실패해도 그때까지 쓴
   * 사용량을 담아 돌려주며, 실패 여부는 {@link NpcReply#succeeded()}로 확인한다.
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
    ReplyBody body =
        new ReplyBody(
            npc,
            memory,
            memory == null ? null : memory.relationship(),
            history,
            text,
            instruction,
            true,
            level,
            false,
            "",
            learner);
    HttpResponse<Stream<String>> response =
        send(
            jsonRequest("/v1/npc/reply", trace, REPLY_TIMEOUT, body),
            HttpResponse.BodyHandlers.ofLines());
    try (Stream<String> lines = response.body()) {
      if (response.statusCode() != 200) {
        throw toError(response.statusCode(), lines.collect(Collectors.joining("\n")));
      }
      return readReplyStream(lines);
    }
  }

  /** 스트림이 만든 음성. 워커 메모리에 10분만 남으므로 대답을 받은 직후 부른다. */
  public byte[] audio(WorkerTrace trace, String audioId) {
    HttpRequest request = request("/v1/audio/" + audioId, trace, AUDIO_TIMEOUT).GET().build();
    return readBytes(send(request, HttpResponse.BodyHandlers.ofByteArray()));
  }

  /** 녹음 전사. 파일 부분의 형식이 워커가 받는 녹음 형식이어야 한다. */
  public SttResult transcribe(WorkerTrace trace, byte[] audio, String contentType) {
    String boundary = "elsehere-" + UUID.randomUUID();
    ByteArrayOutputStream body = new ByteArrayOutputStream();
    body.writeBytes(
        ("--"
                + boundary
                + "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"speech\""
                + "\r\nContent-Type: "
                + contentType
                + "\r\n\r\n")
            .getBytes(StandardCharsets.UTF_8));
    body.writeBytes(audio);
    body.writeBytes(("\r\n--" + boundary + "--\r\n").getBytes(StandardCharsets.UTF_8));
    HttpRequest request =
        request("/v1/stt", trace, STT_TIMEOUT)
            .header("Content-Type", "multipart/form-data; boundary=" + boundary)
            .POST(HttpRequest.BodyPublishers.ofByteArray(body.toByteArray()))
            .build();
    return readJson(send(request, HttpResponse.BodyHandlers.ofString()), SttResult.class);
  }

  /** NPC 대사 한 줄의 한국어 뜻과 단어 풀이(CONV-04 뜻 보기). */
  public MeaningResult meaning(WorkerTrace trace, WorkerNpc npc, String text, int level) {
    HttpRequest request =
        jsonRequest("/v1/meaning", trace, HELP_TIMEOUT, new MeaningBody(npc, text, level));
    return readJson(send(request, HttpResponse.BodyHandlers.ofString()), MeaningResult.class);
  }

  /**
   * 학습자가 다음에 할 만한 말(CONV-05 힌트).
   *
   * @param history 대답할 NPC 줄까지의 앞선 대화
   * @param goal 지금 학습자가 할 일. 없으면 빈 문자열
   */
  public HintResult hint(
      WorkerTrace trace, WorkerNpc npc, List<WorkerLine> history, String goal, int level) {
    // 장소(scene)를 비우면 워커가 NPC의 장소를 쓴다.
    HttpRequest request =
        jsonRequest("/v1/hint", trace, HELP_TIMEOUT, new HintBody(npc, "", goal, history, level));
    return readJson(send(request, HttpResponse.BodyHandlers.ofString()), HintResult.class);
  }

  /**
   * 학습자가 다음 대답에 쓸 만한 단어(CONV-12 추천 단어).
   *
   * @param history 대답할 NPC 줄까지의 앞선 대화
   * @param goal 지금 학습자가 할 일. 없으면 빈 문자열
   */
  public SuggestResult suggest(
      WorkerTrace trace, WorkerNpc npc, List<WorkerLine> history, String goal, int level) {
    // 장소(scene)를 비우면 워커가 NPC의 장소를 쓴다.
    HttpRequest request =
        jsonRequest(
            "/v1/suggest", trace, HELP_TIMEOUT, new SuggestBody(npc, "", goal, history, level));
    return readJson(send(request, HttpResponse.BodyHandlers.ofString()), SuggestResult.class);
  }

  /** 한 문장 음성 합성. WAV를 돌려준다. */
  public byte[] synthesize(WorkerTrace trace, String text, String voice, double speed) {
    HttpRequest request =
        jsonRequest("/v1/tts", trace, TTS_TIMEOUT, new TtsBody(text, voice, speed));
    return readBytes(send(request, HttpResponse.BodyHandlers.ofByteArray()));
  }

  /**
   * NPC 기억 카드 갱신. 이번 대화와 이전 카드로 새 카드 전체를 만든다. 실패(429·502·503)하면 오류로 던지며, 호출한 쪽은 이전 카드를 그대로 둔다.
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
    HttpRequest request =
        jsonRequest(
            "/v1/memory",
            trace,
            MEMORY_TIMEOUT,
            new MemoryBody(npc, outcome, history, previousCard));
    HttpResponse<String> response = send(request, HttpResponse.BodyHandlers.ofString());
    if (response.statusCode() != 200) {
      throw toError(response.statusCode(), response.body());
    }
    try {
      JsonNode card = jsonMapper.readTree(response.body()).path("memory_card");
      if (!card.isObject()) {
        throw new BusinessException(ErrorCode.AI_RESPONSE_INVALID);
      }
      MemoryEnvelope envelope = jsonMapper.readValue(response.body(), MemoryEnvelope.class);
      return new MemoryCardResult(jsonMapper.writeValueAsString(card), usageOf(envelope.usage()));
    } catch (JacksonException exception) {
      throw new BusinessException(ErrorCode.AI_RESPONSE_INVALID, exception);
    }
  }

  /**
   * 대화 뒤 관계 갱신. 저장된 관계로 알아봄 변화량을 계산한다(호감은 대화 중에 발화마다 이미 바꿨다). 모델을 부르지 않는다. 실패하면 오류로 던지며, 호출한 쪽은 관계를
   * 그대로 둔다.
   *
   * @param relationship 저장된 관계와 마지막 대화 뒤 지난 게임 날 수
   * @param history 대화 전체(최대 200줄). 워커는 학습자가 말한 대화인지 확인한다
   */
  public RelationshipChange relationship(
      WorkerTrace trace, WorkerNpc npc, WorkerRelationship relationship, List<WorkerLine> history) {
    HttpRequest request =
        jsonRequest(
            "/v1/npc/relationship",
            trace,
            RELATIONSHIP_TIMEOUT,
            new RelationshipBody(npc, relationship, history));
    return readJson(send(request, HttpResponse.BodyHandlers.ofString()), RelationshipChange.class);
  }

  /** 대화 종료 피드백. 생성 실패·평가할 발화 없음도 HTTP 200 안의 상태로 온다. 요청 형식이 계약과 다르면(400) 오류로 던진다. */
  public WorkerFeedback feedback(WorkerTrace trace, FeedbackInput input) {
    HttpResponse<String> response =
        send(
            jsonRequest("/v1/feedback", trace, FEEDBACK_TIMEOUT, input),
            HttpResponse.BodyHandlers.ofString());
    if (response.statusCode() != 200) {
      throw toError(response.statusCode(), response.body());
    }
    try {
      FeedbackEnvelope envelope = jsonMapper.readValue(response.body(), FeedbackEnvelope.class);
      FeedbackBody body = envelope.feedback();
      if (body == null) {
        throw new BusinessException(ErrorCode.AI_RESPONSE_INVALID);
      }
      JsonNode result = jsonMapper.readTree(response.body()).path("feedback").path("result");
      return new WorkerFeedback(
          body.status(),
          body.problem() == null ? null : body.problem().code(),
          body.result(),
          result.isObject() ? jsonMapper.writeValueAsString(result) : null,
          usageOf(envelope.usage()));
    } catch (JacksonException exception) {
      throw new BusinessException(ErrorCode.AI_RESPONSE_INVALID, exception);
    }
  }

  /** 워커 오류 코드를 API 오류 코드로 바꾼다. 스트림 안의 오류 코드에도 쓴다. 명세 0절의 대응을 따른다. */
  public static ErrorCode toErrorCode(String workerCode) {
    if (workerCode == null) {
      return ErrorCode.AI_TEMPORARILY_UNAVAILABLE;
    }
    return switch (workerCode) {
      case "provider_limit" -> ErrorCode.AI_RATE_LIMITED;
      case "provider_unavailable", "timeout", "tts_unavailable", "internal_error" ->
          ErrorCode.AI_TEMPORARILY_UNAVAILABLE;
      case "invalid_output" -> ErrorCode.AI_RESPONSE_INVALID;
      case "audio_too_large" -> ErrorCode.AUDIO_TOO_LARGE;
      case "audio_format_unsupported" -> ErrorCode.AUDIO_FORMAT_UNSUPPORTED;
      // unauthorized·invalid_request·not_found는 호출 쪽 설정·코드 오류다.
      default -> ErrorCode.INTERNAL_ERROR;
    };
  }

  // SSE는 "event: 이름", "data: JSON", 빈 줄 순서로 이벤트 하나가 온다. delta·audio 조각은 스트리밍 재생용이라 쓰지 않는다.
  private NpcReply readReplyStream(Stream<String> lines) {
    ReplyCollector collector = new ReplyCollector();
    String event = null;
    StringBuilder data = new StringBuilder();
    for (String line : (Iterable<String>) lines::iterator) {
      if (line.isEmpty()) {
        collector.accept(event, data.toString());
        event = null;
        data.setLength(0);
      } else if (line.startsWith("event:")) {
        event = line.substring("event:".length()).trim();
      } else if (line.startsWith("data:")) {
        data.append(line.substring("data:".length()).trim());
      }
    }
    collector.accept(event, data.toString());
    return collector.result();
  }

  private final class ReplyCollector {

    private DoneEvent done;
    private ErrorEvent error;

    void accept(String event, String data) {
      if (event == null || data.isEmpty()) {
        return;
      }
      switch (event) {
        case "done" -> done = parse(data, DoneEvent.class);
        case "error" -> error = parse(data, ErrorEvent.class);
        default -> {
          // delta·audio
        }
      }
    }

    NpcReply result() {
      if (done != null) {
        return new NpcReply(done.text(), done.audioFullId(), null, usageOf(done.usage()));
      }
      if (error != null) {
        log.warn("AI 워커 NPC 대답 실패 code={}", error.code());
        return new NpcReply(null, null, error.code(), usageOf(error.usage()));
      }
      // done·error 없이 끊긴 스트림
      log.warn("AI 워커 NPC 대답 스트림이 끝나지 않고 끊겼습니다");
      return new NpcReply(null, null, "provider_unavailable", List.of());
    }

    private <T> T parse(String data, Class<T> type) {
      try {
        return jsonMapper.readValue(data, type);
      } catch (JacksonException exception) {
        throw new BusinessException(ErrorCode.AI_RESPONSE_INVALID, exception);
      }
    }
  }

  private static List<WorkerUsage> usageOf(List<WorkerUsage> usage) {
    return usage == null ? List.of() : usage;
  }

  private HttpRequest jsonRequest(String path, WorkerTrace trace, Duration timeout, Object body) {
    return request(path, trace, timeout)
        .header("Content-Type", "application/json")
        .POST(HttpRequest.BodyPublishers.ofString(jsonMapper.writeValueAsString(body)))
        .build();
  }

  // 워커 로그를 Spring 요청 로그와 같은 ID로 찾도록 HTTP 추적 ID를 우선 전달한다.
  private HttpRequest.Builder request(String path, WorkerTrace trace, Duration timeout) {
    String httpRequestId = MDC.get("requestId");
    HttpRequest.Builder builder =
        HttpRequest.newBuilder(URI.create(baseUrl + path))
            .timeout(timeout)
            .header("X-Internal-Token", token)
            .header(
                "X-Request-Id",
                httpRequestId != null ? httpRequestId : trace.requestId().toString())
            .header("X-User-Id", String.valueOf(trace.userId()));
    if (trace.conversationId() != null) {
      builder.header("X-Conv-Id", String.valueOf(trace.conversationId()));
    }
    return builder;
  }

  // 호출 수·시간 통계는 ai_requests와 지표로 보고, 개발 서버(DEBUG)에서 문제를 따라갈 때만 호출마다 남긴다. 본문은 남기지 않는다.
  private <T> HttpResponse<T> send(HttpRequest request, HttpResponse.BodyHandler<T> handler) {
    long startedAt = System.nanoTime();
    try {
      HttpResponse<T> response = httpClient.send(request, handler);
      log.debug(
          "ai_worker_call path={} status={} duration_ms={}",
          request.uri().getPath(),
          response.statusCode(),
          TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - startedAt));
      return response;
    } catch (HttpTimeoutException exception) {
      log.warn("AI 워커 응답 시간 초과 path={}", request.uri().getPath());
      throw new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE, exception);
    } catch (IOException exception) {
      log.warn("AI 워커에 연결하지 못했습니다 path={}", request.uri().getPath());
      throw new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE, exception);
    } catch (InterruptedException exception) {
      Thread.currentThread().interrupt();
      throw new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE, exception);
    }
  }

  private <T> T readJson(HttpResponse<String> response, Class<T> type) {
    if (response.statusCode() != 200) {
      throw toError(response.statusCode(), response.body());
    }
    try {
      return jsonMapper.readValue(response.body(), type);
    } catch (JacksonException exception) {
      throw new BusinessException(ErrorCode.AI_RESPONSE_INVALID, exception);
    }
  }

  private byte[] readBytes(HttpResponse<byte[]> response) {
    if (response.statusCode() != 200) {
      throw toError(response.statusCode(), new String(response.body(), StandardCharsets.UTF_8));
    }
    return response.body();
  }

  // 오류 본문은 {"error": {"code", "message"}}다. 메시지에는 사용자 입력이 섞일 수 있어 코드만 남긴다.
  private BusinessException toError(int status, String body) {
    String code = null;
    try {
      ErrorBody error = jsonMapper.readValue(body, ErrorBody.class);
      code = error.error() == null ? null : error.error().code();
    } catch (JacksonException exception) {
      // 오류 형식이 아닌 본문(프록시 오류 등)
    }
    log.warn("AI 워커 오류 status={} code={}", status, code);
    return new BusinessException(toErrorCode(code));
  }

  record JudgeBody(WorkerNpc npc, WorkerState state, List<WorkerLine> history, String text) {}

  record GuardBody(WorkerNpc npc, List<WorkerLine> history, String text) {}

  record ReplyBody(
      WorkerNpc npc,
      WorkerMemory memory,
      WorkerRelationship relationship,
      List<WorkerLine> history,
      String text,
      String instruction,
      boolean tts,
      int level,
      boolean suggestWords,
      String goal,
      WorkerLearner learner) {}

  record MeaningBody(WorkerNpc npc, String text, int level) {}

  record HintBody(WorkerNpc npc, String scene, String goal, List<WorkerLine> history, int level) {}

  record SuggestBody(
      WorkerNpc npc, String scene, String goal, List<WorkerLine> history, int level) {}

  record TtsBody(String text, String voice, double speed) {}

  record MemoryBody(WorkerNpc npc, String outcome, List<WorkerLine> history, Object memoryCard) {}

  record RelationshipBody(
      WorkerNpc npc, WorkerRelationship relationship, List<WorkerLine> history) {}

  record MemoryEnvelope(List<WorkerUsage> usage) {}

  record DoneEvent(String text, String audioFullId, List<WorkerUsage> usage) {}

  record ErrorEvent(String code, List<WorkerUsage> usage) {}

  record FeedbackEnvelope(FeedbackBody feedback, List<WorkerUsage> usage) {}

  record FeedbackBody(String status, WorkerFeedback.Result result, Problem problem) {}

  record Problem(String code) {}

  record ErrorBody(ErrorDetail error) {}

  record ErrorDetail(String code) {}
}
