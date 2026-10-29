package com.elsehere;

import static org.assertj.core.api.Assertions.assertThat;

import java.net.URI;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.boot.autoconfigure.AutoConfigurations;
import org.springframework.boot.jdbc.autoconfigure.JdbcConnectionDetails;
import org.springframework.boot.test.context.runner.ApplicationContextRunner;
import org.springframework.boot.testcontainers.lifecycle.TestcontainersLifecycleApplicationContextInitializer;
import org.springframework.boot.testcontainers.service.connection.ServiceConnectionAutoConfiguration;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.datasource.DriverManagerDataSource;

class TestcontainersConfigurationTest {

  private final ApplicationContextRunner contextRunner =
      new ApplicationContextRunner()
          .withInitializer(new TestcontainersLifecycleApplicationContextInitializer())
          .withConfiguration(AutoConfigurations.of(ServiceConnectionAutoConfiguration.class))
          .withUserConfiguration(TestcontainersConfiguration.class);

  @Test
  @DisplayName("서로 다른 Spring 테스트 환경이 같은 PostgreSQL 서버를 사용한다")
  void separateContexts_sharePostgresServer() {
    contextRunner.run(
        firstContext -> {
          JdbcConnectionDetails firstDatabase = firstContext.getBean(JdbcConnectionDetails.class);

          contextRunner.run(
              secondContext -> {
                JdbcConnectionDetails secondDatabase =
                    secondContext.getBean(JdbcConnectionDetails.class);

                URI firstAddress =
                    URI.create(firstDatabase.getJdbcUrl().substring("jdbc:".length()));
                URI secondAddress =
                    URI.create(secondDatabase.getJdbcUrl().substring("jdbc:".length()));
                assertThat(secondAddress.getAuthority()).isEqualTo(firstAddress.getAuthority());
              });
        });
  }

  @Test
  @DisplayName("서로 다른 Spring 테스트 환경의 테이블과 데이터가 섞이지 않는다")
  void separateContexts_keepTablesAndDataIsolated() {
    contextRunner.run(
        firstContext -> {
          JdbcTemplate firstDatabase = database(firstContext.getBean(JdbcConnectionDetails.class));
          firstDatabase.execute("CREATE TABLE isolation_probe (value integer NOT NULL)");
          firstDatabase.update("INSERT INTO isolation_probe VALUES (1)");

          contextRunner.run(
              secondContext -> {
                JdbcTemplate secondDatabase =
                    database(secondContext.getBean(JdbcConnectionDetails.class));
                secondDatabase.execute("CREATE TABLE isolation_probe (value integer NOT NULL)");
                secondDatabase.update("INSERT INTO isolation_probe VALUES (2)");

                assertThat(
                        secondDatabase.queryForObject(
                            "SELECT value FROM isolation_probe", Integer.class))
                    .isEqualTo(2);
                secondDatabase.execute("DROP TABLE isolation_probe");
                assertThat(
                        firstDatabase.queryForObject(
                            "SELECT value FROM isolation_probe", Integer.class))
                    .isEqualTo(1);
              });

          assertThat(
                  firstDatabase.queryForObject("SELECT value FROM isolation_probe", Integer.class))
              .isEqualTo(1);
        });
  }

  private JdbcTemplate database(JdbcConnectionDetails connectionDetails) {
    return new JdbcTemplate(
        new DriverManagerDataSource(
            connectionDetails.getJdbcUrl(),
            connectionDetails.getUsername(),
            connectionDetails.getPassword()));
  }
}
