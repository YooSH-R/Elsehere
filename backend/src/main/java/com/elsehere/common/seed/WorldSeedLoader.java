package com.elsehere.common.seed;

import java.nio.charset.StandardCharsets;
import javax.sql.DataSource;
import lombok.RequiredArgsConstructor;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.core.io.ClassPathResource;
import org.springframework.jdbc.datasource.init.ResourceDatabasePopulator;
import org.springframework.stereotype.Component;

/**
 * 장소·NPC·사물·어휘·미션 시드를 애플리케이션이 시작될 때 넣는다.
 *
 * <p>시드 SQL은 고유 키가 겹치면 시드 값으로 갱신하므로 여러 번 실행해도 행이 늘지 않는다.
 *
 * <p>Spring의 SQL 초기화({@code spring.sql.init})를 쓰지 않는 이유: 그 기능은 {@code @DataJpaTest}에서도 실행되어, 테스트가
 * 직접 넣는 장소 데이터와 겹친다. 이 클래스는 빈으로 등록된 컨텍스트에서만 실행된다.
 */
@Component
@RequiredArgsConstructor
public class WorldSeedLoader implements ApplicationRunner {

  static final String SCRIPT = "db/seed/world.sql";

  private final DataSource dataSource;

  @Override
  public void run(ApplicationArguments args) {
    load();
  }

  public void load() {
    ResourceDatabasePopulator populator =
        new ResourceDatabasePopulator(new ClassPathResource(SCRIPT));
    populator.setSqlScriptEncoding(StandardCharsets.UTF_8.name());
    populator.execute(dataSource);
  }
}
