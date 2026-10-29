package com.elsehere.common.health;

import java.util.Map;
import lombok.RequiredArgsConstructor;
import org.springframework.boot.health.actuate.endpoint.HealthEndpoint;
import org.springframework.boot.health.actuate.endpoint.HttpCodeStatusMapper;
import org.springframework.boot.health.contributor.Status;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequiredArgsConstructor
public class ApplicationHealthController {

  private final HealthEndpoint healthEndpoint;

  @GetMapping("/api/health")
  public ResponseEntity<Map<String, String>> health() {
    Status status = healthEndpoint.health().getStatus();
    int statusCode = HttpCodeStatusMapper.getDefault().getStatusCode(status);
    return ResponseEntity.status(statusCode).body(Map.of("status", status.getCode()));
  }
}
