package com.elsehere;

import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.SQLException;
import java.sql.Statement;
import java.util.UUID;
import org.springframework.boot.jdbc.autoconfigure.JdbcConnectionDetails;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.PropertySource;
import org.testcontainers.postgresql.PostgreSQLContainer;
import org.testcontainers.utility.DockerImageName;

/**
 * 테스트 JVM에서 PostgreSQL 서버 하나를 공유하고 Spring 환경마다 별도 DB를 만든다. Docker가 필요하다.
 *
 * <p>pgvector를 쓰는 기능을 만들 때 pgvector 이미지로 바꾼다.
 */
@TestConfiguration(proxyBeanMethods = false)
@PropertySource("classpath:test-database.properties")
public class TestcontainersConfiguration {

  private static final PostgreSQLContainer POSTGRES =
      new PostgreSQLContainer(DockerImageName.parse("postgres:16"));

  static {
    // Spring 환경이 닫혀도 다른 테스트가 서버를 사용한다. JVM 종료 후 정리는 Testcontainers의 Ryuk가 맡는다.
    POSTGRES.start();
  }

  @Bean
  JdbcConnectionDetails postgresConnectionDetails() throws SQLException {
    String databaseName = "test_" + UUID.randomUUID().toString().replace("-", "");
    try (Connection connection =
            DriverManager.getConnection(
                POSTGRES.getJdbcUrl(), POSTGRES.getUsername(), POSTGRES.getPassword());
        Statement statement = connection.createStatement()) {
      // DB를 나눠 각 환경의 스키마 생성(Flyway 마이그레이션, create-drop)과 트랜잭션 밖의 쓰기를 서로 격리한다.
      statement.execute("CREATE DATABASE " + databaseName);
    }

    String jdbcUrl =
        "jdbc:postgresql://"
            + POSTGRES.getHost()
            + ":"
            + POSTGRES.getMappedPort(PostgreSQLContainer.POSTGRESQL_PORT)
            + "/"
            + databaseName;
    return new JdbcConnectionDetails() {
      @Override
      public String getJdbcUrl() {
        return jdbcUrl;
      }

      @Override
      public String getUsername() {
        return POSTGRES.getUsername();
      }

      @Override
      public String getPassword() {
        return POSTGRES.getPassword();
      }
    };
  }
}
