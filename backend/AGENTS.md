# 백엔드 개발 지침

루트 [AGENTS.md](../AGENTS.md)와 [공통 코드 규약](../docs/code-conventions.md)을 기본으로 적용합니다. 이 문서는 Java + Spring Boot 백엔드의 테스트 우선 개발 기준을 정합니다. 진행 절차는 `tdd` 스킬을 따릅니다.

## 현재 상태

- 기술 구성과 실행 방법은 [백엔드 README](README.md)에 있습니다. Java 21, Spring Boot 4.1.1, Gradle(Groovy DSL)이며 기본 패키지는 `com.elsehere`입니다.
- 포함된 의존성: Spring Web MVC, Validation, Security, OAuth2 Client, Spring Data JPA, Actuator, Lombok, PostgreSQL 드라이버
- 아직 넣지 않은 것: 스키마 마이그레이션 도구. 정하기 전까지 테이블은 엔티티를 기준으로 Hibernate `ddl-auto`가 만듭니다.
- 엔티티는 `com.elsehere.common.entity.BaseEntity`를 상속합니다. 생성·수정 시각(`created_at`, `updated_at`)은 JPA Auditing이 `Clock` 빈 기준으로 기록하며, 식별자는 각 엔티티에서 선언합니다.
- 데이터 구조는 [ERD](../docs/erd.dbml), API는 [MVP API 명세](../docs/api/mvp-api.md)를 따릅니다.
- 정하지 않은 기술을 코드 작성 중에 임의로 확정하지 않습니다. 필요하면 선택지와 영향을 설명하고 사용자 확인을 받습니다.

## 패키지와 엔티티

- 패키지는 `com.elsehere.<도메인>` 아래에 `controller`, `service`, `repository`, `entity`를 둡니다. 도메인별 테이블은 [백엔드 README](README.md#패키지-구조)에 있습니다.
- 엔티티의 테이블·컬럼 이름, 길이, NOT NULL, 기본값, 유니크·인덱스를 ERD와 맞추고, 엔티티를 바꾸면 ERD도 함께 수정합니다.
- 다른 담당 영역의 테이블(users, conversations, turns)은 연관관계 없이 `Long` ID로만 참조합니다.
- `saved_at`, `started_at`처럼 업무에 쓰는 시각은 `created_at`과 별도 컬럼으로 두고, 값을 넣는 코드에서 `Clock` 기준으로 채웁니다.
- 새 테이블의 엔티티를 만들면 `EntitySchemaTest`의 테이블 목록에 추가합니다.

## 테스트 구성

| 대상 | 테스트 방식 | 확인할 내용 |
| --- | --- | --- |
| 도메인 규칙·서비스 로직 | Spring 컨텍스트 없는 JUnit 5 단위 테스트 | 미션 판정, 상태 변경, 사용량 차단 등 핵심 규칙 |
| 컨트롤러 | `@WebMvcTest` + `MockMvc` | 요청 검증, 응답 형식, 상태 코드, 오류 응답 |
| 리포지토리·엔티티 매핑 | `@DataJpaTest` + Testcontainers PostgreSQL | 직접 작성한 쿼리, 매핑, 시각 자동 기록 |
| 여러 계층을 잇는 흐름 | `@SpringBootTest` | 핵심 사용자 흐름에 한해 소수만 작성 |

- Spring Boot 테스트 스타터에 포함된 JUnit 5, AssertJ, Mockito를 사용합니다. 다른 테스트 라이브러리는 필요성을 설명하고 확인을 받은 뒤 추가합니다.
- 가장 가벼운 방식부터 선택합니다. 단위 테스트로 검증할 수 있는 규칙을 `@SpringBootTest`로 검증하지 않습니다.
- Mock은 외부 AI 서비스, 시간, 난수처럼 테스트에서 통제해야 하는 경계에 사용합니다. 협력 객체를 모두 Mock으로 바꿔 호출 여부만 검증하지 않습니다.
- Spring 빈을 대체할 때는 `@MockitoBean`을 사용합니다. `@MockBean`은 Spring Boot 3.4에서 지원 중단되었고 4.0에서 제거되었습니다.
- DB는 PostgreSQL입니다. H2로는 `jsonb`, 부분 인덱스, `vector` 타입을 검증할 수 없으므로 Testcontainers로 실제 PostgreSQL에서 테스트합니다. 테스트를 실행하는 PC에 Docker가 필요합니다.
- DB가 필요한 테스트는 `com.elsehere.TestcontainersConfiguration`을 `@Import`합니다. `@DataJpaTest`는 `@AutoConfigureTestDatabase(replace = Replace.NONE)`과 함께 씁니다.

## 테스트 작성 기준

- 테스트 클래스는 `src/test/java`에서 대상 클래스와 같은 패키지에 `<대상 클래스>Test` 이름으로 둡니다.
- 테스트 메서드 이름은 시나리오와 결과를 드러냅니다. 필요하면 `@DisplayName`에 한국어 설명을 적습니다.
- 본문은 준비(given), 실행(when), 검증(then) 순서로 작성합니다.
- 테스트 하나는 동작 하나를 검증합니다. 같은 규칙에 입력만 바뀌면 `@ParameterizedTest`를 사용합니다.
- 현재 시각은 `Clock`을 주입받아 사용하고 테스트에서 고정합니다. 실행 시점에 따라 결과가 달라지지 않게 합니다.
- 오류 응답 테스트는 상태 코드와 함께 응답 본문의 오류 형식을 검증합니다.

## 실행 명령

`backend/` 폴더에서 실행합니다. Windows PowerShell·명령 프롬프트에서는 `./gradlew` 대신 `.\gradlew.bat`을 사용합니다.

| 목적 | 명령 |
| --- | --- |
| 테스트 클래스 하나 실행 | `./gradlew test --tests "<패키지>.<클래스>"` |
| 전체 테스트 | `./gradlew test` |
| 포맷 적용 | `./gradlew spotlessApply` |
| 포맷·규칙 검사·테스트 | `./gradlew check` |

Red-Green 반복 중에는 작성 중인 테스트를 실행하고, 작업을 마치기 전에 `spotlessApply`로 포맷을 적용한 뒤 `check`를 실행합니다.
