package com.elsehere;

import static org.assertj.core.api.Assertions.assertThat;

import jakarta.persistence.EntityManager;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 마이그레이션 도구를 정하기 전까지 스키마는 엔티티에서 만든다.
// halt_on_error를 켜서 DDL 실행 오류가 경고 로그로 묻히지 않고 테스트 실패로 드러나게 한다.
@DataJpaTest(
    properties = {
      "spring.jpa.hibernate.ddl-auto=create-drop",
      "spring.jpa.properties.hibernate.hbm2ddl.halt_on_error=true"
    })
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import(TestcontainersConfiguration.class)
class EntitySchemaTest {

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("엔티티 매핑으로 만든 테이블이 PostgreSQL에 오류 없이 생성된다")
  void createSchema_createsEntityTables() {
    @SuppressWarnings("unchecked")
    List<String> tableNames =
        entityManager
            .createNativeQuery(
                "select table_name from information_schema.tables where table_schema = 'public'")
            .getResultList();

    assertThat(tableNames)
        .contains(
            "users",
            "user_consents",
            "user_language_profiles",
            "scenes",
            "npcs",
            "world_objects",
            "user_object_prefs",
            "vocabularies",
            "user_vocab",
            "missions",
            "user_missions");
  }
}
