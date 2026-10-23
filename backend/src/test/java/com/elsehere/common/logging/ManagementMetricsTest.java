package com.elsehere.common.logging;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.server.LocalServerPort;
import org.springframework.context.annotation.Import;

@SpringBootTest(
    webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT,
    properties = {"management.server.port=0", "monitoring.password=metrics-test-password"})
@Import(TestcontainersConfiguration.class)
class ManagementMetricsTest {

  @LocalServerPort private int applicationPort;

  @Value("${local.management.port:0}")
  private int managementPort;

  @Test
  void metrics_areCollectedOnSeparateManagementPort() throws Exception {
    assertThat(managementPort).isPositive().isNotEqualTo(applicationPort);

    assertThat(get(applicationPort, "/api/v1/bootstrap?token=private-token").statusCode())
        .isEqualTo(200);
    HttpResponse<String> metrics = get(managementPort, "/actuator/prometheus");

    assertThat(metrics.statusCode()).isEqualTo(200);
    assertThat(metrics.body())
        .contains(
            "http_server_requests_seconds_count",
            "http_server_requests_seconds_bucket",
            "uri=\"/api/v1/bootstrap\"")
        .doesNotContain("private-token");
    assertThat(get(applicationPort, "/actuator/prometheus").statusCode()).isEqualTo(401);
  }

  @Test
  void managementEndpoints_requireSeparateCredentials() throws Exception {
    assertThat(managementPort).isPositive();
    assertThat(get(managementPort, "/actuator/prometheus", false).statusCode()).isEqualTo(401);
    assertThat(get(managementPort, "/actuator/health", false).statusCode()).isEqualTo(401);
    assertThat(get(managementPort, "/actuator/env").statusCode()).isNotEqualTo(200);
  }

  @Test
  void scrapes_doNotPolluteApplicationRequestMetrics() throws Exception {
    assertThat(managementPort).isPositive();
    get(managementPort, "/actuator/prometheus");
    HttpResponse<String> metrics = get(managementPort, "/actuator/prometheus");

    assertThat(metrics.statusCode()).isEqualTo(200);
    assertThat(metrics.body()).doesNotContain("uri=\"/actuator/prometheus\"");
  }

  @Test
  void authenticationFailures_collectHistogramWithoutRawIdentifiers() throws Exception {
    assertThat(get(applicationPort, "/api/v1/private/person-one?token=private-query").statusCode())
        .isEqualTo(401);
    assertThat(get(applicationPort, "/api/v1/private/person-two").statusCode()).isEqualTo(401);

    HttpResponse<String> metrics = get(managementPort, "/actuator/prometheus");

    assertThat(metrics.statusCode()).isEqualTo(200);
    assertThat(metrics.body())
        .contains(
            "http_server_requests_seconds_count",
            "http_server_requests_seconds_bucket",
            "status=\"401\"")
        .doesNotContain("person-one", "person-two", "private-query", "requestId", "request_id");
  }

  private HttpResponse<String> get(int port, String path) throws Exception {
    return get(port, path, true);
  }

  private HttpResponse<String> get(int port, String path, boolean authenticate) throws Exception {
    HttpRequest.Builder builder =
        HttpRequest.newBuilder(URI.create("http://127.0.0.1:" + port + path))
            .timeout(Duration.ofSeconds(10))
            .GET();
    if (authenticate && port == managementPort) {
      String credentials =
          java.util.Base64.getEncoder()
              .encodeToString(
                  "monitoring:metrics-test-password"
                      .getBytes(java.nio.charset.StandardCharsets.UTF_8));
      builder.header("Authorization", "Basic " + credentials);
    }
    HttpRequest request = builder.build();
    return HttpClient.newHttpClient().send(request, HttpResponse.BodyHandlers.ofString());
  }
}
