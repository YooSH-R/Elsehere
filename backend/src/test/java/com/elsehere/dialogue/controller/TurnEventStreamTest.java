package com.elsehere.dialogue.controller;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatCode;

import jakarta.servlet.ServletOutputStream;
import jakarta.servlet.WriteListener;
import java.io.IOException;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.mock.web.MockHttpServletResponse;
import tools.jackson.databind.json.JsonMapper;

class TurnEventStreamTest {

  @Test
  @DisplayName("클라이언트가 연결을 끊어 쓰기에 실패해도 오류를 던지지 않고 남은 이벤트는 쓰지 않는다")
  void send_afterClientGone_ignoresFailuresAndStopsWriting() {
    DisconnectedResponse response = new DisconnectedResponse();
    TurnEventStream stream = new TurnEventStream(response, JsonMapper.builder().build());

    assertThatCode(
            () -> {
              stream.delta("Hello");
              stream.audio(0, "Hello", new byte[] {1});
              stream.delta(" there");
            })
        .doesNotThrowAnyException();
    assertThat(stream.isStarted()).isTrue();
    assertThat(response.writes).isEqualTo(1);
  }

  // 첫 쓰기에서 연결이 끊긴 것처럼 실패하는 응답
  private static final class DisconnectedResponse extends MockHttpServletResponse {

    private int writes;

    @Override
    public ServletOutputStream getOutputStream() {
      return new ServletOutputStream() {
        @Override
        public void write(int b) throws IOException {
          write(new byte[] {(byte) b}, 0, 1);
        }

        @Override
        public void write(byte[] bytes, int offset, int length) throws IOException {
          writes++;
          throw new IOException("Broken pipe");
        }

        @Override
        public boolean isReady() {
          return true;
        }

        @Override
        public void setWriteListener(WriteListener listener) {}
      };
    }
  }
}
