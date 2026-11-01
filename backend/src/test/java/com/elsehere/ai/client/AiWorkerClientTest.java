package com.elsehere.ai.client;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.groups.Tuple.tuple;

import ch.qos.logback.classic.Level;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.logging.LogCapture;
import com.sun.net.httpserver.HttpServer;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.net.ServerSocket;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

// 테스트 안에서 띄운 가짜 워커(이 PC의 루프백 주소)로 요청 형식과 응답 해석을 확인한다. 실제 AI 워커와 외부 네트워크는 부르지 않는다.
class AiWorkerClientTest {

  private static final String TOKEN = "test-token";
  private static final WorkerNpc NPC =
      new WorkerNpc("CAFE_STAFF", "Mia", "the barista", "", "The counter", "af_heart", 1.0);
  private static final UUID REQUEST_ID = UUID.fromString("00000000-0000-0000-0000-000000000001");
  private static final WorkerTrace TRACE = new WorkerTrace(REQUEST_ID, 7L, 11L);

  private final Map<String, FakeResponse> responses = new ConcurrentHashMap<>();
  private final Map<String, ReceivedRequest> received = new ConcurrentHashMap<>();
  private HttpServer server;
  private AiWorkerClient client;

  @BeforeEach
  void startFakeWorker() throws IOException {
    server = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
    server.createContext(
        "/",
        exchange -> {
          String path = exchange.getRequestURI().getPath();
          received.put(
              path,
              new ReceivedRequest(
                  exchange.getRequestHeaders().getFirst("X-Internal-Token"),
                  exchange.getRequestHeaders().getFirst("X-Request-Id"),
                  exchange.getRequestHeaders().getFirst("X-User-Id"),
                  exchange.getRequestHeaders().getFirst("X-Conv-Id"),
                  exchange.getRequestHeaders().getFirst("Content-Type"),
                  new String(
                      exchange.getRequestBody().readAllBytes(), StandardCharsets.ISO_8859_1)));
          FakeResponse response =
              responses.getOrDefault(
                  path,
                  new FakeResponse(
                      404, "application/json", "{\"error\":{\"code\":\"not_found\"}}"));
          byte[] body = response.body().getBytes(StandardCharsets.UTF_8);
          exchange.getResponseHeaders().set("Content-Type", response.contentType());
          exchange.sendResponseHeaders(response.status(), body.length);
          exchange.getResponseBody().write(body);
          exchange.close();
        });
    server.start();
    client = new AiWorkerClient("http://127.0.0.1:" + server.getAddress().getPort() + "/", TOKEN);
  }

  @AfterEach
  void stopFakeWorker() {
    server.stop(0);
  }

  @Test
  @DisplayName("NPC 대답 스트림을 끝까지 읽어 대답 전문·턴 전체 음성 ID·추천 단어·사용량을 모은다")
  void reply_collectsDoneEventWordsAndUsage() {
    respond(
        "/v1/npc/reply",
        200,
        "text/event-stream",
        """
        event: delta
        data: {"text": "Hi"}

        event: audio
        data: {"seq": 0, "text": "Hi! What can I get?", "audio_id": "a-0", "duration_s": 1.0}

        event: words
        data: {"words": ["latte", "iced"], "error": null}

        event: done
        data: {"text": "Hi! What can I get?", "audio_full_id": "full-1", "usage": [{"step": "npc", "model": "m1", "input_tokens": 120, "output_tokens": 8}]}

        """);

    NpcReply reply = client.reply(TRACE, NPC, null, List.of(), "Hello", "", 1, null);

    assertThat(reply.succeeded()).isTrue();
    assertThat(reply.text()).isEqualTo("Hi! What can I get?");
    assertThat(reply.audioFullId()).isEqualTo("full-1");
    assertThat(reply.suggestWords()).containsExactly("latte", "iced");
    assertThat(reply.usage())
        .extracting(WorkerUsage::model, WorkerUsage::inputTokens)
        .containsExactly(tuple("m1", 120));
  }

  @Test
  @DisplayName("모든 요청에 내부 토큰과 추적 헤더를 붙이고, 본문은 워커 형식(snake_case)으로 보낸다")
  void reply_sendsTokenTraceHeadersAndSnakeCaseBody() {
    respond("/v1/npc/reply", 200, "text/event-stream", "event: done\ndata: {\"text\": \"Hi\"}\n\n");

    client.reply(
        TRACE,
        NPC,
        new WorkerMemory(2, null, Map.of("attitude", "friendly")),
        List.of(WorkerLine.npc("Hi")),
        "Hello",
        "",
        2,
        null);

    ReceivedRequest request = received.get("/v1/npc/reply");
    assertThat(request.token()).isEqualTo(TOKEN);
    assertThat(request.requestId()).isEqualTo(REQUEST_ID.toString());
    assertThat(request.userId()).isEqualTo("7");
    assertThat(request.conversationId()).isEqualTo("11");
    assertThat(request.contentType()).isEqualTo("application/json");
    assertThat(request.body())
        .contains("\"suggest_words\":true")
        .contains("\"level\":2")
        .contains("\"tts_voice\":\"af_heart\"")
        .contains(
            "\"memory\":{\"visits\":2,\"last_order\":null,\"card\":{\"attitude\":\"friendly\"}}");
  }

  @Test
  @DisplayName("대답 상대인 학습자의 닉네임과 캐릭터 기본형을 learner로 보낸다")
  void reply_sendsLearnerNicknameAndAvatarType() {
    respond("/v1/npc/reply", 200, "text/event-stream", "event: done\ndata: {\"text\": \"Hi\"}\n\n");

    client.reply(TRACE, NPC, null, List.of(), "Hello", "", 2, new WorkerLearner("Sun", "F"));

    assertThat(received.get("/v1/npc/reply").body())
        .contains("\"learner\":{\"nickname\":\"Sun\",\"avatar_type\":\"F\"}");
  }

  @Test
  @DisplayName("NPC의 인격 카드는 snake_case로 바꾸지 않고 카드의 칸 이름(camelCase) 그대로 보낸다")
  void reply_sendsProfileWithCamelCaseKeys() {
    respond("/v1/npc/reply", 200, "text/event-stream", "event: done\ndata: {\"text\": \"Hi\"}\n\n");
    WorkerNpc npcWithProfile =
        new WorkerNpc(
            "CAFE_STAFF",
            "Mia",
            "the barista",
            "",
            "The counter",
            "af_heart",
            1.0,
            Map.of("schemaVersion", "2.0", "speechStyle", Map.of("tone", "bright")));

    client.reply(TRACE, npcWithProfile, null, List.of(), "Hello", "", 2, null);

    assertThat(received.get("/v1/npc/reply").body())
        .contains("\"tts_voice\":\"af_heart\"")
        .contains("\"schemaVersion\":\"2.0\"")
        .contains("\"speechStyle\":{\"tone\":\"bright\"}")
        .doesNotContain("schema_version");
  }

  @Test
  @DisplayName("대화가 없는 요청에는 대화 ID 헤더를 붙이지 않는다")
  void request_withoutConversation_omitsConversationHeader() {
    respond("/v1/npc/reply", 200, "text/event-stream", "event: done\ndata: {\"text\": \"Hi\"}\n\n");

    client.reply(new WorkerTrace(REQUEST_ID, 7L, null), NPC, null, List.of(), "Hello", "", 1, null);

    assertThat(received.get("/v1/npc/reply").conversationId()).isNull();
  }

  @Test
  @DisplayName("스트림이 오류 이벤트로 끝나면 실패한 대답으로 오류 코드와 그때까지의 사용량을 돌려준다")
  void reply_errorEvent_returnsFailedReply() {
    respond(
        "/v1/npc/reply",
        200,
        "text/event-stream",
        "event: error\ndata: {\"code\": \"provider_limit\", \"usage\": [{\"step\": \"npc\", \"model\": \"m1\"}]}\n\n");

    NpcReply reply = client.reply(TRACE, NPC, null, List.of(), "Hello", "", 1, null);

    assertThat(reply.succeeded()).isFalse();
    assertThat(reply.errorCode()).isEqualTo("provider_limit");
    assertThat(reply.usage()).hasSize(1);
  }

  @Test
  @DisplayName("done·error 없이 끊긴 스트림은 provider_unavailable 실패로 본다")
  void reply_streamCut_returnsProviderUnavailable() {
    respond(
        "/v1/npc/reply", 200, "text/event-stream", "event: delta\ndata: {\"text\": \"Hi\"}\n\n");

    NpcReply reply = client.reply(TRACE, NPC, null, List.of(), "Hello", "", 1, null);

    assertThat(reply.succeeded()).isFalse();
    assertThat(reply.errorCode()).isEqualTo("provider_unavailable");
  }

  @Test
  @DisplayName("워커가 오류 응답을 주면 워커 오류 코드를 API 오류 코드로 바꿔 던진다")
  void reply_errorResponse_throwsMappedCode() {
    respond(
        "/v1/npc/reply", 429, "application/json", "{\"error\": {\"code\": \"provider_limit\"}}");

    assertThatThrownBy(() -> client.reply(TRACE, NPC, null, List.of(), "Hello", "", 1, null))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.AI_RATE_LIMITED));
  }

  @Test
  @DisplayName("의도·번역 판정 결과에서 판정한 문장·버림 여부·영어 번역을 읽는다")
  void judge_readsTextDroppedAndTranslation() {
    respond(
        "/v1/judge",
        200,
        "application/json",
        """
        {"text": "라떼 주세요", "dropped": false, "notice": null,
         "intent": {"id": "chat"}, "translation": {"english": "A latte, please.", "error": null},
         "usage": [{"step": "intent", "model": "m1"}]}
        """);

    JudgeResult result =
        client.judge(TRACE, NPC, new WorkerState("free_talk", "", List.of()), List.of(), "라떼 주세요");

    assertThat(result.text()).isEqualTo("라떼 주세요");
    assertThat(result.dropped()).isFalse();
    assertThat(result.english()).isEqualTo("A latte, please.");
  }

  @Test
  @DisplayName("무례 판정은 앞선 대화의 무례 표시와 함께 보내고 판정값·사용량을 읽는다")
  void guard_sendsHistoryAndReadsAbusive() {
    respond(
        "/v1/guard",
        200,
        "application/json",
        """
        {"text": "you idiot", "dropped": false,
         "guard": {"abusive": true, "error": null, "error_code": null},
         "usage": [{"step": "guard", "model": "m1", "input_tokens": 230, "output_tokens": 1}]}
        """);

    GuardResult result =
        client.guard(TRACE, NPC, List.of(new WorkerLine("me", "shut up", null, true)), "you idiot");

    assertThat(result.abusive()).isTrue();
    assertThat(result.errorCode()).isNull();
    assertThat(result.usage()).extracting(WorkerUsage::step).containsExactly("guard");
    assertThat(received.get("/v1/guard").body())
        .contains("\"text\":\"you idiot\"")
        .contains("\"rude\":true");
  }

  @Test
  @DisplayName("무례 판정이 호감도 변화량(affinity_delta)을 주면 함께 읽고, 주지 않으면 null이다")
  void guard_readsAffinityDeltaWhenGiven() {
    respond(
        "/v1/guard",
        200,
        "application/json",
        """
        {"guard": {"abusive": true, "error_code": null}, "affinity_delta": -7, "usage": []}
        """);
    GuardResult withDelta = client.guard(TRACE, NPC, List.of(), "you idiot");
    respond(
        "/v1/guard",
        200,
        "application/json",
        """
        {"guard": {"abusive": true, "error_code": null}, "usage": []}
        """);
    GuardResult withoutDelta = client.guard(TRACE, NPC, List.of(), "you idiot");

    assertThat(withDelta.affinityDelta()).isEqualTo(-7);
    assertThat(withoutDelta.affinityDelta()).isNull();
  }

  @Test
  @DisplayName("판정하지 못한 무례 판정은 판정값 null과 이유 코드로 읽는다")
  void guard_undecided_readsNullAndErrorCode() {
    respond(
        "/v1/guard",
        200,
        "application/json",
        """
        {"text": "hmm", "dropped": false,
         "guard": {"abusive": null, "error": "timeout", "error_code": "timeout"}, "usage": []}
        """);

    GuardResult result = client.guard(TRACE, NPC, List.of(), "hmm");

    assertThat(result.abusive()).isNull();
    assertThat(result.errorCode()).isEqualTo("timeout");
  }

  @Test
  @DisplayName("추천 단어를 끄면 NPC 대답 요청에 suggest_words false를 보낸다")
  void reply_withoutSuggestWords_sendsFalse() {
    respond(
        "/v1/npc/reply", 200, "text/event-stream", "event: done\ndata: {\"text\": \"Bye\"}\n\n");

    client.reply(TRACE, NPC, null, List.of(), "Hello", "", 1, false, null);

    assertThat(received.get("/v1/npc/reply").body()).contains("\"suggest_words\":false");
  }

  @Test
  @DisplayName("뜻 보기는 NPC·대사·난이도를 보내고 한국어 뜻·단어 풀이·사용량을 읽는다")
  void meaning_sendsLineAndReadsMeaningAndGlossary() {
    respond(
        "/v1/meaning",
        200,
        "application/json",
        """
        {"meaning_ko": "좋아요! 매장에서 드실 건가요, 가져가실 건가요?",
         "glossary": [{"text": "to go", "meaning_ko": "포장"}],
         "usage": [{"step": "translate", "model": "m1"}]}
        """);

    MeaningResult result = client.meaning(TRACE, NPC, "Great! For here or to go?", 1);

    assertThat(result.meaningKo()).isEqualTo("좋아요! 매장에서 드실 건가요, 가져가실 건가요?");
    assertThat(result.glossary())
        .extracting(MeaningResult.Glossary::text, MeaningResult.Glossary::meaningKo)
        .containsExactly(tuple("to go", "포장"));
    assertThat(result.usage()).hasSize(1);
    assertThat(received.get("/v1/meaning").body())
        .contains("\"text\":\"Great! For here or to go?\"")
        .contains("\"level\":1")
        .contains("\"tts_voice\":\"af_heart\"");
  }

  @Test
  @DisplayName("힌트는 앞선 대화·목표·난이도를 보내고 첫 추천 문장과 사용량을 읽는다")
  void hint_sendsHistoryAndReadsFirstSentence() {
    respond(
        "/v1/hint",
        200,
        "application/json",
        """
        {"say_this": [{"en": "For here, please.", "ko": "여기서 먹을게요."},
                      {"en": "I'll have it here.", "ko": "여기서 마실게요."}],
         "keywords": [{"en": "for here", "ko": "매장에서"}],
         "usage": [{"step": "hint", "model": "m1"}]}
        """);

    HintResult result =
        client.hint(TRACE, NPC, List.of(WorkerLine.npc("For here or to go?")), "", 1);

    assertThat(result.firstSentence()).isEqualTo("For here, please.");
    assertThat(result.usage()).hasSize(1);
    assertThat(received.get("/v1/hint").body())
        .contains("\"goal\":\"\"")
        .contains("\"history\":[{\"who\":\"npc\",\"text\":\"For here or to go?\"")
        .contains("\"level\":1");
  }

  @Test
  @DisplayName("녹음은 file 부분에 받은 형식을 붙인 multipart로 보내고 전사 문장과 길이를 읽는다")
  void transcribe_sendsMultipartAndReadsText() {
    respond(
        "/v1/stt",
        200,
        "application/json",
        "{\"text\": \"I want a latte\", \"audio_s\": 1.2, \"usage\": [{\"step\": \"stt\"}]}");

    SttResult result = client.transcribe(TRACE, new byte[] {1, 2, 3}, "audio/wav");

    assertThat(result.text()).isEqualTo("I want a latte");
    assertThat(result.audioS()).isEqualTo(1.2);
    ReceivedRequest request = received.get("/v1/stt");
    assertThat(request.contentType()).startsWith("multipart/form-data; boundary=");
    assertThat(request.body())
        .contains("Content-Disposition: form-data; name=\"file\"")
        .contains("Content-Type: audio/wav");
  }

  @Test
  @DisplayName("피드백 결과에서 상태·결과·검증된 결과 원본·사용량을 읽는다")
  void feedback_completed_readsResultAndRawJson() {
    respond(
        "/v1/feedback",
        200,
        "application/json",
        """
        {"feedback": {"status": "completed", "problem": null,
          "result": {"conversation_summary": {"text_ko": "요약"}, "review_tags": [],
                     "turn_reviews": [], "summary_suggestion_id": null, "spoken_usage_notes": []}},
         "usage": [{"step": "feedback", "model": "m2"}]}
        """);

    WorkerFeedback feedback = client.feedback(TRACE, input());

    assertThat(feedback.completed()).isTrue();
    assertThat(feedback.result().conversationSummary().textKo()).isEqualTo("요약");
    assertThat(feedback.resultJson()).contains("\"text_ko\":\"요약\"");
    assertThat(feedback.usage()).extracting(WorkerUsage::model).containsExactly("m2");
    assertThat(received.get("/v1/feedback").body()).contains("\"schema_version\":\"0.3.0\"");
  }

  @Test
  @DisplayName("평가할 근거가 부족하다는 결과는 오류가 아니라 상태와 이유 코드로 돌려준다")
  void feedback_insufficientEvidence_readsProblemCode() {
    respond(
        "/v1/feedback",
        200,
        "application/json",
        "{\"feedback\": {\"status\": \"insufficient_evidence\", \"result\": null,"
            + " \"problem\": {\"code\": \"NO_USABLE_USER_TURNS\"}}, \"usage\": []}");

    WorkerFeedback feedback = client.feedback(TRACE, input());

    assertThat(feedback.insufficientEvidence()).isTrue();
    assertThat(feedback.problemCode()).isEqualTo("NO_USABLE_USER_TURNS");
    assertThat(feedback.resultJson()).isNull();
  }

  @Test
  @DisplayName("피드백 응답에 feedback이 없으면 AI_RESPONSE_INVALID다")
  void feedback_withoutBody_throwsInvalid() {
    respond("/v1/feedback", 200, "application/json", "{\"usage\": []}");

    assertThatThrownBy(() -> client.feedback(TRACE, input()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.AI_RESPONSE_INVALID));
  }

  @Test
  @DisplayName("기억 카드 응답의 카드 전체를 JSON 그대로 돌려준다")
  void memoryCard_returnsCardJson() {
    respond(
        "/v1/memory",
        200,
        "application/json",
        "{\"memory_card\": {\"name\": \"Sun\", \"claims\": []}, \"usage\": [{\"step\": \"memory\"}]}");

    MemoryCardResult result =
        client.memoryCard(
            TRACE, NPC, "The learner ended the conversation normally.", List.of(), null);

    assertThat(result.cardJson()).isEqualTo("{\"name\":\"Sun\",\"claims\":[]}");
    assertThat(result.usage()).hasSize(1);
    assertThat(received.get("/v1/memory").body()).contains("\"memory_card\":null");
  }

  @Test
  @DisplayName("기억 카드가 객체가 아니면 AI_RESPONSE_INVALID, 워커 오류면 오류 코드로 던진다")
  void memoryCard_invalidOrError_throws() {
    respond("/v1/memory", 200, "application/json", "{\"memory_card\": null, \"usage\": []}");
    assertThatThrownBy(() -> client.memoryCard(TRACE, NPC, "outcome", List.of(), null))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.AI_RESPONSE_INVALID));

    respond("/v1/memory", 503, "text/html", "<html>Bad gateway</html>");
    assertThatThrownBy(() -> client.memoryCard(TRACE, NPC, "outcome", List.of(), null))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode())
                    .isEqualTo(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
  }

  @Test
  @DisplayName("기억에 담긴 관계는 memory 안이 아니라 요청 최상위의 relationship으로 보낸다")
  void reply_sendsRelationshipAtTopLevel() {
    respond("/v1/npc/reply", 200, "text/event-stream", "event: done\ndata: {\"text\": \"Hi\"}\n\n");

    client.reply(
        TRACE,
        NPC,
        new WorkerMemory(2, null, null, new WorkerRelationship(23, 3, 0)),
        List.of(),
        "Hello",
        "",
        2,
        null);

    assertThat(received.get("/v1/npc/reply").body())
        .contains("\"memory\":{\"visits\":2,\"last_order\":null,\"card\":null}")
        .contains(
            "\"relationship\":{\"recognition\":23,\"affinity\":3,\"days_since_last_talk\":0}");
  }

  @Test
  @DisplayName("관계 갱신은 저장된 관계와 학습자 줄의 무례·사건이 담긴 대화를 보내고 변화량과 센 사건을 읽는다")
  void relationship_sendsRelationshipAndHistoryAndReadsDeltas() {
    respond(
        "/v1/npc/relationship",
        200,
        "application/json",
        "{\"recognition_delta\": 8, \"affinity_delta\": 1, \"events\": [\"GREETING\", \"LEAVE\"]}");

    RelationshipChange change =
        client.relationship(
            TRACE,
            NPC,
            new WorkerRelationship(15, 2, 0),
            List.of(
                WorkerLine.npc("Hi! What can I get you?"),
                WorkerLine.learner("Hi! Bye!", null, false, List.of("GREETING", "LEAVE"))));

    assertThat(change.recognitionDelta()).isEqualTo(8);
    assertThat(change.affinityDelta()).isEqualTo(1);
    assertThat(change.events()).containsExactly("GREETING", "LEAVE");
    assertThat(received.get("/v1/npc/relationship").body())
        .contains("\"relationship\":{\"recognition\":15,\"affinity\":2,\"days_since_last_talk\":0}")
        .contains(
            "{\"who\":\"npc\",\"text\":\"Hi! What can I get you?\",\"english\":null,\"rude\":false,\"events\":[]}")
        .contains("\"rude\":false,\"events\":[\"GREETING\",\"LEAVE\"]");
  }

  @Test
  @DisplayName("관계 갱신에 실패하면 워커 오류 코드로 던진다")
  void relationship_error_throwsMappedCode() {
    respond("/v1/npc/relationship", 503, "text/html", "<html>Bad gateway</html>");

    assertThatThrownBy(
            () -> client.relationship(TRACE, NPC, new WorkerRelationship(0, 0, 0), List.of()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode())
                    .isEqualTo(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
  }

  @Test
  @DisplayName("워커에 연결하지 못하면 AI_TEMPORARILY_UNAVAILABLE이다")
  void unreachableWorker_throwsTemporarilyUnavailable() throws IOException {
    int closedPort;
    try (ServerSocket socket = new ServerSocket(0)) {
      closedPort = socket.getLocalPort();
    }
    AiWorkerClient unreachable = new AiWorkerClient("http://127.0.0.1:" + closedPort, TOKEN);

    assertThatThrownBy(() -> unreachable.audio(TRACE, "a-1"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode())
                    .isEqualTo(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
  }

  @ParameterizedTest
  @CsvSource({
    "provider_limit, AI_RATE_LIMITED",
    "provider_unavailable, AI_TEMPORARILY_UNAVAILABLE",
    "timeout, AI_TEMPORARILY_UNAVAILABLE",
    "invalid_output, AI_RESPONSE_INVALID",
    "audio_too_large, AUDIO_TOO_LARGE",
    "audio_format_unsupported, AUDIO_FORMAT_UNSUPPORTED",
    "unauthorized, INTERNAL_ERROR",
    "invalid_request, INTERNAL_ERROR"
  })
  @DisplayName("워커 오류 코드를 API 명세의 오류 코드로 바꾼다")
  void toErrorCode_mapsWorkerCodes(String workerCode, ErrorCode expected) {
    assertThat(AiWorkerClient.toErrorCode(workerCode)).isEqualTo(expected);
  }

  @Test
  @DisplayName("오류 코드가 없으면 일시적인 AI 오류로 본다")
  void toErrorCode_null_isTemporarilyUnavailable() {
    assertThat(AiWorkerClient.toErrorCode(null)).isEqualTo(ErrorCode.AI_TEMPORARILY_UNAVAILABLE);
  }

  @Test
  @DisplayName("개발 서버(DEBUG)에서는 워커 호출마다 경로·상태·걸린 시간만 남기고 보낸 말은 남기지 않는다")
  void everyCall_logsPathStatusAndDurationAtDebug() {
    respond(
        "/v1/guard",
        200,
        "application/json",
        """
        {"guard": {"abusive": false, "error_code": null}, "usage": []}
        """);

    try (LogCapture logs = LogCapture.atDebug(AiWorkerClient.class)) {
      client.guard(TRACE, NPC, List.of(), "my secret plan");

      assertThat(logs.messages(Level.DEBUG))
          .singleElement()
          .asString()
          .matches("ai_worker_call path=/v1/guard status=200 duration_ms=\\d+")
          .doesNotContain("my secret plan");
    }
  }

  @Test
  @DisplayName("운영(INFO)에서는 성공한 워커 호출을 남기지 않는다")
  void successfulCall_isNotLoggedAtInfo() {
    respond(
        "/v1/guard",
        200,
        "application/json",
        """
        {"guard": {"abusive": false, "error_code": null}, "usage": []}
        """);

    try (LogCapture logs = LogCapture.atInfo(AiWorkerClient.class)) {
      client.guard(TRACE, NPC, List.of(), "hello");

      assertThat(logs.messages(Level.DEBUG)).isEmpty();
      assertThat(logs.messages(Level.INFO)).isEmpty();
    }
  }

  private void respond(String path, int status, String contentType, String body) {
    responses.put(path, new FakeResponse(status, contentType, body));
  }

  private static FeedbackInput input() {
    return new FeedbackInput(
        "0.3.0",
        REQUEST_ID.toString(),
        new FeedbackInput.Scope("7", "CAFE_STAFF", "11"),
        1,
        "elsehere-feedback-v1",
        new FeedbackInput.Context("CAFE", "free_chat", "카페", "en-US", "ko-KR", "user_end", null),
        List.of(),
        List.of());
  }

  private record FakeResponse(int status, String contentType, String body) {}

  private record ReceivedRequest(
      String token,
      String requestId,
      String userId,
      String conversationId,
      String contentType,
      String body) {}
}
