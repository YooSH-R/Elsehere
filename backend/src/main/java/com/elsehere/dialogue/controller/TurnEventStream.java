package com.elsehere.dialogue.controller;

import com.elsehere.common.error.ErrorResponse;
import com.elsehere.dialogue.dto.ConversationStartResponse;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.dialogue.service.TurnEvents;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.concurrent.TimeUnit;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import tools.jackson.databind.json.JsonMapper;

/**
 * 발화 전송(CONV-03)과 대화 시작(CONV-01)의 스트리밍 응답. 대화 시작의 첫 인사도 대화의 첫 턴이라 같은 이벤트로 보낸다. 이벤트 형식은
 * docs/api/mvp-api.md CONV-01·CONV-03 "스트리밍"을 따른다.
 *
 * <p>첫 이벤트를 쓸 때 200과 SSE 헤더를 보낸다. 그 전에 난 오류는 공통 오류 처리기가 지금처럼 HTTP 오류로 응답한다. 클라이언트가 연결을 끊어도 쓰기 실패를
 * 무시해 턴 처리를 끝까지 마치게 한다. 저장된 턴·대화는 같은 요청 ID로 다시 보내면 받는다.
 */
@Slf4j
final class TurnEventStream implements TurnEvents {

  private final HttpServletResponse response;
  private final JsonMapper jsonMapper;
  // 대화 시작 스트림인지. 끝날 때 남기는 시간 로그 이름만 다르다.
  private final boolean opening;
  private final long createdAt = System.nanoTime();
  private OutputStream output;
  private boolean clientGone;
  private Long firstDeltaAt;
  private Long firstAudioAt;
  private int chunks;
  private int silentChunks;

  /** 발화 전송(CONV-03) 스트림. */
  TurnEventStream(HttpServletResponse response, JsonMapper jsonMapper) {
    this(response, jsonMapper, false);
  }

  private TurnEventStream(HttpServletResponse response, JsonMapper jsonMapper, boolean opening) {
    this.response = response;
    this.jsonMapper = jsonMapper;
    this.opening = opening;
  }

  /** 대화 시작(CONV-01) 스트림. */
  static TurnEventStream forOpening(HttpServletResponse response, JsonMapper jsonMapper) {
    return new TurnEventStream(response, jsonMapper, true);
  }

  /** 첫 이벤트를 보냈는지. 보낸 뒤의 오류는 HTTP 상태로 알릴 수 없어 {@link #error}로 보낸다. */
  boolean isStarted() {
    return output != null;
  }

  @Override
  public void delta(String text) {
    if (firstDeltaAt == null) {
      firstDeltaAt = System.nanoTime();
    }
    send("delta", new Delta(text));
  }

  @Override
  public void audio(int seq, String text, byte[] wav) {
    chunks++;
    if (wav == null) {
      silentChunks++;
    } else if (firstAudioAt == null) {
      firstAudioAt = System.nanoTime();
    }
    send(
        "audio",
        new Audio(seq, text, wav == null ? null : Base64.getEncoder().encodeToString(wav)));
  }

  /** 저장한 턴. REST 응답과 같은 본문이며 마지막 이벤트다. */
  void turn(TurnResponse turn) {
    send("turn", turn);
    logTiming("turn");
  }

  /** 시작한 대화. REST 응답과 같은 본문이며 마지막 이벤트다. */
  void conversation(ConversationStartResponse started) {
    send("conversation", started);
    logTiming("conversation");
  }

  /**
   * 대사를 보내기 시작한 뒤 난 오류. 응답 상태는 이미 200이라, 원래 보냈을 HTTP 상태를 공통 오류 본문과 함께 보내고 끝낸다. 클라이언트는 이 상태로 다시 시도할
   * 수 있는 오류인지 가린다.
   */
  void error(int status, ErrorResponse error) {
    send("error", new StreamError(status, error.error()));
    logTiming(error.error().code());
  }

  private void send(String event, Object data) {
    if (clientGone) {
      return;
    }
    try {
      if (output == null) {
        response.setStatus(HttpStatus.OK.value());
        response.setContentType(MediaType.TEXT_EVENT_STREAM_VALUE);
        response.setCharacterEncoding(StandardCharsets.UTF_8.name());
        response.setHeader("Cache-Control", "no-cache");
        // nginx가 응답을 모아 두지 않고 바로 넘기게 한다.
        response.setHeader("X-Accel-Buffering", "no");
        output = response.getOutputStream();
      }
      String frame = "event: " + event + "\ndata: " + jsonMapper.writeValueAsString(data) + "\n\n";
      output.write(frame.getBytes(StandardCharsets.UTF_8));
      output.flush();
    } catch (IOException exception) {
      // 사용자가 화면을 닫았거나 연결이 끊겼다. 턴은 끝까지 처리해 저장한다.
      clientGone = true;
      log.info("turn_stream_client_gone event={}", event);
    }
  }

  // 스트리밍으로 첫 글자·첫 음성이 얼마나 빨라졌는지 보도록 남긴다. 대사는 남기지 않는다.
  private void logTiming(String result) {
    if (opening) {
      log.info(
          "conversation_start_timing result={} first_delta_ms={} first_audio_ms={} total_ms={}"
              + " chunks={} silent_chunks={} client_gone={}",
          result,
          sinceCreated(firstDeltaAt),
          sinceCreated(firstAudioAt),
          sinceCreated(System.nanoTime()),
          chunks,
          silentChunks,
          clientGone);
      return;
    }
    log.info(
        "turn_stream_timing result={} first_delta_ms={} first_audio_ms={} total_ms={} chunks={}"
            + " silent_chunks={} client_gone={}",
        result,
        sinceCreated(firstDeltaAt),
        sinceCreated(firstAudioAt),
        sinceCreated(System.nanoTime()),
        chunks,
        silentChunks,
        clientGone);
  }

  private Long sinceCreated(Long at) {
    return at == null ? null : TimeUnit.NANOSECONDS.toMillis(at - createdAt);
  }

  private record Delta(String text) {}

  private record Audio(int seq, String text, String wav) {}

  private record StreamError(int status, ErrorResponse.Error error) {}
}
