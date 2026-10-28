package com.elsehere;

import static org.assertj.core.api.Assertions.assertThat;

import jakarta.persistence.EntityManagerFactory;
import java.util.List;
import org.hibernate.SessionFactory;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.jdbc.core.JdbcTemplate;

// 스키마는 db/migration의 Flyway 마이그레이션이 만들고, Hibernate는 엔티티와 맞는지 검사(validate)만 한다.
@DataJpaTest
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import(TestcontainersConfiguration.class)
class EntitySchemaTest {

  private static final String MIGRATED_SCHEMA = "public";

  // 엔티티 매핑으로 만든 스키마를 마이그레이션 결과와 비교하려고 따로 만드는 스키마
  private static final String ENTITY_SCHEMA = "entity_mapping";

  @Autowired private EntityManagerFactory entityManagerFactory;

  @Autowired private JdbcTemplate jdbcTemplate;

  @AfterEach
  void dropEntitySchema() {
    jdbcTemplate.execute("drop schema if exists " + ENTITY_SCHEMA + " cascade");
  }

  @Test
  @DisplayName("마이그레이션이 엔티티 테이블을 만들고 없앤 테이블은 만들지 않는다")
  void migrations_createEntityTables() {
    List<String> tableNames =
        jdbcTemplate.queryForList(
            "select table_name from information_schema.tables where table_schema = ?",
            String.class,
            MIGRATED_SCHEMA);

    assertThat(tableNames)
        .contains(
            "users",
            "user_consents",
            "user_language_profiles",
            "level_tests",
            "level_test_answers",
            "scenes",
            "npcs",
            "scene_words",
            "user_word_prefs",
            "vocabularies",
            "user_vocab",
            "missions",
            "user_missions",
            "first_day_missions",
            "conversations",
            "turns",
            "ai_requests",
            "conversation_feedback",
            "turn_feedback",
            "user_npc_relations")
        // 장소별 사물 단어로 바꾸면서 없앤 사물 단위 테이블은 다시 만들지 않는다.
        .doesNotContain("world_objects", "user_object_prefs");
  }

  // validate는 테이블·칸·타입만 보므로 CHECK(enum 허용 값)·NOT NULL·기본값·유니크·외래 키·인덱스는 여기서 비교한다.
  // 유니크 제약 이름은 비교하지 않는다. 기존 DB에 맞춘 마이그레이션의 이름(uk...)과
  // Hibernate가 새 스키마에 붙이는 이름(<테이블>_<칸>_key)이 다르다.
  @Test
  @DisplayName("마이그레이션으로 만든 스키마가 엔티티 매핑으로 만든 스키마와 같다")
  void migrations_matchEntityMapping() {
    entityManagerFactory
        .unwrap(SessionFactory.class)
        .getSchemaManager()
        .forSchema(ENTITY_SCHEMA)
        .exportMappedObjects(true);

    assertThat(columns(MIGRATED_SCHEMA))
        .containsExactlyInAnyOrderElementsOf(columns(ENTITY_SCHEMA));
    assertThat(constraints(MIGRATED_SCHEMA))
        .containsExactlyInAnyOrderElementsOf(constraints(ENTITY_SCHEMA));
    assertThat(indexes(MIGRATED_SCHEMA))
        .containsExactlyInAnyOrderElementsOf(indexes(ENTITY_SCHEMA));
  }

  private List<String> columns(String schema) {
    return jdbcTemplate.queryForList(
        """
        select table_name || '.' || column_name || ' ' || data_type
               || coalesce('(' || character_maximum_length || ')', '')
               || case when is_nullable = 'NO' then ' not null' else '' end
               || coalesce(' default ' || column_default, '')
               || case when is_identity = 'YES' then ' identity' else '' end
        from information_schema.columns
        where table_schema = ? and table_name <> 'flyway_schema_history'
        """,
        String.class,
        schema);
  }

  private List<String> constraints(String schema) {
    List<String> definitions =
        jdbcTemplate.queryForList(
            """
            select t.relname || ' ' || case c.contype when 'u' then 'unique' else c.conname end
                   || ' ' || pg_get_constraintdef(c.oid)
            from pg_constraint c
            join pg_class t on t.oid = c.conrelid
            join pg_namespace n on n.oid = t.relnamespace
            where n.nspname = ? and t.relname <> 'flyway_schema_history'
            """,
            String.class,
            schema);
    return withoutSchemaName(definitions, schema);
  }

  // 기본 키·유니크 제약이 만든 인덱스는 제약 비교에 포함되므로 직접 만든 인덱스만 비교한다.
  private List<String> indexes(String schema) {
    List<String> definitions =
        jdbcTemplate.queryForList(
            """
            select i.indexdef
            from pg_indexes i
            where i.schemaname = ? and i.tablename <> 'flyway_schema_history'
              and not exists (
                select 1 from pg_constraint c
                join pg_namespace n on n.oid = c.connamespace
                where n.nspname = i.schemaname and c.conname = i.indexname)
            """,
            String.class,
            schema);
    return withoutSchemaName(definitions, schema);
  }

  // 외래 키와 인덱스 정의에 붙는 스키마 이름을 빼서 두 스키마를 같은 기준으로 비교한다.
  private List<String> withoutSchemaName(List<String> definitions, String schema) {
    return definitions.stream().map(definition -> definition.replace(schema + ".", "")).toList();
  }
}
