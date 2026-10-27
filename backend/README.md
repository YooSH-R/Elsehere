# 백엔드

ELSEHERE 서비스 API 서버입니다. API는 [MVP API 명세](../docs/api/mvp-api.md), 데이터 구조는 [ERD](../docs/erd.dbml)를 따릅니다. 코드 작성 기준은 [공통 코드 규약](../docs/code-conventions.md)과 [백엔드 개발 지침](AGENTS.md)을 따릅니다.

## 기술 구성

| 항목 | 버전·구성 |
| --- | --- |
| 언어 | Java 21 (Gradle 툴체인으로 고정) |
| 프레임워크 | Spring Boot 4.1.1 |
| 빌드 | Gradle 9.8.2 Wrapper, Groovy DSL |
| 주요 의존성 | Spring Web MVC, Validation, Security, OAuth2 Client, Spring Data JPA, Lombok, PostgreSQL 드라이버 |
| 테스트 DB | Testcontainers PostgreSQL 16 (Docker 필요) |
| 포맷 | Spotless 8.9.0 + google-java-format 1.28.0 (Google Java Style, 들여쓰기 2칸) |
| 규칙 검사 | Checkstyle 14.3.0 ([설정](config/checkstyle/checkstyle.xml)) |
| 설정 파일 | `src/main/resources/application.properties` |

- 스키마 마이그레이션 도구는 아직 넣지 않았습니다.
- google-java-format은 Spotless가 검증한 1.28.0으로 고정했습니다. 최신 1.37.0은 이 구성에서 포맷 실행이 실패합니다.

## 패키지 구조

기능 영역(도메인)별로 패키지를 나누고, 그 아래에 계층별 패키지를 둡니다.

```
com.elsehere
├── common          여러 도메인이 함께 쓰는 코드
│   ├── auth        로그인 사용자(LoginUser), 로그인 세션 저장, 요청 위조 방지 헤더 검사
│   ├── config      보안·Clock·JPA Auditing 설정
│   ├── entity      BaseEntity
│   ├── logging     요청 ID와 민감 정보를 제외한 요청 요약
│   ├── error       공통 오류 응답(ErrorCode, BusinessException)
│   └── seed        장소·NPC·사물·어휘·미션 시드 실행(WorldSeedLoader)
└── <도메인>
    ├── controller
    ├── service
    ├── repository
    └── entity      엔티티와 엔티티가 쓰는 enum·복합 키
```

| 도메인 | 테이블 |
| --- | --- |
| `auth` | 없음 (로그인·가입·로그아웃 처리, 개발용 로그인) |
| `bootstrap` | 없음 (여러 기능의 상태를 모아 접속 직후 첫 화면을 정하는 BOOT-01) |
| `user` | users, user_consents |
| `world` | scenes, npcs, world_objects, user_object_prefs |
| `vocab` | vocabularies, user_vocab |
| `mission` | missions, user_missions |
| `onboarding` | user_language_profiles (학습 언어·대화 난이도 설정), level_tests, level_test_answers (레벨 테스트와 문항 응답) |

- 계층 패키지는 그 계층의 코드를 처음 작성할 때 만듭니다. 빈 패키지를 미리 만들지 않습니다.
- 요청·응답 객체(DTO)는 `<도메인>/dto`에 둡니다. 컨트롤러 하나에서만 쓰는 작은 요청은 컨트롤러 안의 record로 둘 수 있습니다.
- Spring Security가 호출하는 로그인 성공·실패 처리기와 인증 진입점은 `auth/handler`에 둡니다.

## 준비

- JDK 21을 설치하고 `JAVA_HOME`을 설정합니다. Gradle은 Wrapper가 내려받으므로 따로 설치하지 않습니다.
- 테스트는 Testcontainers로 PostgreSQL 컨테이너를 띄우므로 Docker Desktop을 실행한 상태에서 실행합니다.
- 테스트 JVM 안에서는 PostgreSQL 서버 하나를 공유하고 Spring 테스트 환경마다 별도 DB를 만듭니다. 각 환경의 테이블 생성·삭제와 데이터는 서로 격리되며 컨테이너는 JVM 종료 후 Testcontainers가 정리합니다. 여러 환경이 DB 연결을 미리 점유하지 않도록 테스트용 Hikari 풀의 최소 유휴 연결은 `0`으로 둡니다. 로컬 앱이나 개발·운영 DB를 테스트에 사용하지 않습니다.
- 로컬 실행(`bootRun`)은 PostgreSQL이 필요합니다. 접속 정보는 환경변수 `DB_URL`, `DB_USERNAME`, `DB_PASSWORD`로 지정하며, 없으면 `jdbc:postgresql://localhost:5432/elsehere`, 사용자 `elsehere`를 씁니다. 스키마 생성 방식은 `JPA_DDL_AUTO`로 지정하며 기본값은 `update`입니다.
- 로컬에서는 환경변수 대신 `backend/.env`나 저장소 루트 `.env`에 `GOOGLE_CLIENT_ID=값`처럼 한 줄에 하나씩 적어도 됩니다. 값에 따옴표나 `export`를 붙이지 않으며, 같은 이름의 환경변수가 있으면 환경변수가 우선합니다. `.env`는 커밋하지 않습니다. 서버 컨테이너에는 `.env`가 없으므로 값은 Compose의 `environment`로 넘깁니다.
- IDE에서 프로젝트를 열 때는 `backend/` 폴더를 Gradle 프로젝트로 엽니다.

## 스키마 관리

- 스키마는 [ERD](../docs/erd.dbml)를 기준으로 작성한 엔티티에서 JPA(`ddl-auto`)가 만듭니다. 엔티티를 바꾸면 ERD도 함께 수정합니다. 운영 환경에서는 `JPA_DDL_AUTO`를 `validate` 또는 `none`으로 지정합니다.
- `update`는 없는 테이블·컬럼·인덱스를 추가할 뿐, 기존 컬럼의 타입과 제약은 바꾸지 않습니다. enum 컬럼에는 허용 값 CHECK 제약이 생성되므로, enum에 값을 추가하거나 컬럼 정의를 바꾸면 로컬 DB에서 해당 테이블을 지우고 다시 만들어야 반영됩니다.
- ERD 상단에 "DDL에서 직접 만들 것"으로 적은 항목(pgvector 확장, 부분 인덱스)은 JPA가 만들지 않습니다. 해당 기능을 구현할 때 생성 방법을 정합니다.
- `EntitySchemaTest`가 모든 엔티티의 DDL이 PostgreSQL에서 오류 없이 실행되는지 확인합니다.

## 시드 데이터

- 장소·NPC·사물·어휘·미션처럼 모든 환경에 같이 있어야 하는 기본 데이터는 [`src/main/resources/db/seed/world.sql`](src/main/resources/db/seed/world.sql)에 있습니다.
- 사물 단어·한국어 뜻·영문 예문·예문 해석·발음 파일 키의 편집 원본은 [Unity 공용 단어 에셋](../frontend/UnityClient/Assets/Data/ObjectHover/Words/)입니다. Unity의 **Tools → A305 → ObjectHover → Export Backend Data**로 [단어 JSON](src/main/resources/content/object-words.json), [씬·프리팹 연결 JSON](src/main/resources/content/object-word-bindings.json), SQL의 `GENERATED OBJECT WORDS` 구간을 생성합니다. 발음 파일 키가 빈 항목은 DB에 NULL로 적재합니다.
- Editor가 프로젝트를 닫은 상태에서는 저장소 루트의 `./infra/scripts/sync-object-words.ps1`로 내보내고 `-Check`로 에셋과 생성 파일의 일치를 검사합니다. 프로젝트 버전의 Unity Editor가 필요하며 다른 설치 위치는 `-UnityPath` 또는 `UNITY_EDITOR_PATH`로 지정합니다. 생성 JSON·SQL 구간은 직접 편집하지 않습니다.
- `WorldSeedLoader`가 애플리케이션이 시작될 때(로컬·개발·운영 모두) 이 SQL을 실행합니다. 고유 키(`code`, `unity_key`, `(language_code, text)`)가 같은 행이 있으면 시드 값으로 갱신하므로 여러 번 실행해도 행이 늘지 않습니다. DB에서 직접 고친 값은 다음 시작 때 시드 값으로 돌아가므로, 사물 어휘는 Unity 에셋에서, 그 밖의 기본 데이터는 SQL 파일에서 고칩니다. 언어 코드나 영어 표현을 바꾸면 현재 DB에서는 새 어휘 행이 생기며 기존 행은 삭제하지 않습니다. 기존 사용자 학습 기록을 새 표현으로 옮겨야 한다면 별도 DB 마이그레이션이 필요합니다.
- `code`, `unity_key`는 Unity 씬·빌더에 쓴 값과 같아야 합니다. 행을 추가하거나 고치면 `WorldSeedLoaderTest`의 기대값도 함께 고칩니다.
- 공용 목록의 어휘 전체를 `vocabularies`에 적재합니다. 연결 JSON에는 씬별 공용 키·사물 수와 프리팹 GUID·경로·공용 키를 담습니다. 기존 `world_objects`의 8개 시드와 WORLD-02는 아직 이 JSON을 읽지 않습니다. 공용 키 하나를 여러 씬에서 사용하는 서버 연결과 Unity의 서버 조회는 별도 연동 작업이며, 현재 씬 검수는 Unity 에셋을 직접 읽습니다. 사용자별 저장·숨김 상태는 공용 에셋에 넣지 않습니다.
- Spring의 SQL 초기화(`spring.sql.init`)는 `@DataJpaTest`에서도 실행되어 테스트 데이터와 겹치므로 쓰지 않습니다. DB 테스트에서 시드가 필요하면 `WorldSeedLoader`를 `@Import`합니다.

## 인증과 오류 응답

- 로그인 상태는 서버 세션에 두고 세션 쿠키(HttpOnly, `SameSite=Lax`)로 유지합니다. 로그인 없이 `/api/v1` API를 호출하면 `401 UNAUTHENTICATED`, Google 로그인만 하고 가입 정보를 내지 않았으면 `403 SIGNUP_REQUIRED`입니다.
- 가입(AUTH-03·04)은 Google 로그인이 세션에 남긴 가입 대기 정보로 진행합니다. 받는 약관과 현재 버전은 `ConsentCatalog`에 있으며, 약관 문서를 바꾸면 이 클래스의 버전과 주소를 함께 바꿉니다.
- POST·PUT·PATCH·DELETE 요청에는 `X-Requested-With` 헤더(값은 자유)를 붙여야 합니다. 없으면 `403 CSRF_HEADER_REQUIRED`입니다.
- 오류 응답 형식과 코드는 [MVP API 명세](../docs/api/mvp-api.md)의 1.5절을 따릅니다.

### Google 로그인

[MVP API 명세](../docs/api/mvp-api.md)의 AUTH-01·02입니다. 브라우저에서 `/api/v1/auth/login/google`을 열면 Google 로그인 후 `/api/v1/auth/callback/google`로 돌아오고, 서버가 로그인 또는 가입 대기 상태를 만든 뒤 클라이언트 주소로 보냅니다.

| 환경변수 | 기본값 | 설명 |
| --- | --- | --- |
| `GOOGLE_CLIENT_ID` | `not-configured` | Google OAuth 클라이언트 ID |
| `GOOGLE_CLIENT_SECRET` | `not-configured` | Google OAuth 클라이언트 보안 비밀번호. 코드와 Git에 넣지 않습니다. |
| `CLIENT_URL` | `/` | 로그인을 마친 뒤 돌아갈 클라이언트 주소 |

- 값이 없어도 서버는 실행되지만 Google 로그인은 실패합니다. 개발용 로그인만 쓸 때는 지정하지 않아도 됩니다.
- Google 콘솔의 OAuth 클라이언트에 환경별 리디렉션 URI(`http://localhost:8080/api/v1/auth/callback/google` 등)를 등록하고, 앱이 테스트 상태이면 로그인할 계정을 테스트 사용자로 추가합니다.
- nginx 뒤에서는 `X-Forwarded-Proto`, `X-Forwarded-Host` 헤더로 원래 주소를 알아내 콜백 주소를 만듭니다(`server.forward-headers-strategy=native`).

### 개발용 로그인

소셜 로그인 없이 사용자별 데이터를 개발할 때 씁니다. local 프로필에서만 켜지며, 다른 환경에는 이 API가 없습니다.

1. local 프로필로 서버를 실행합니다. PowerShell에서는 `$env:SPRING_PROFILES_ACTIVE="local"`을 지정한 뒤 같은 창에서 `.\gradlew.bat bootRun`을 실행합니다.
2. `POST /api/v1/dev/login`에 `X-Requested-With` 헤더와 본문 `{ "userId": 1 }`을 보내면 그 사용자로 로그인한 세션 쿠키를 받습니다.
3. 이후 요청은 같은 세션 쿠키로 보냅니다.

사용자가 실제로 있는지는 확인하지 않습니다.

## 명령

`backend/` 폴더에서 실행합니다. Windows PowerShell·명령 프롬프트에서는 `./gradlew` 대신 `.\gradlew.bat`을 사용합니다.

| 목적 | 명령 |
| --- | --- |
| 실행 | `./gradlew bootRun` |
| 포맷 적용 | `./gradlew spotlessApply` |
| 포맷·규칙 검사·테스트 | `./gradlew check` |
| 테스트만 실행 | `./gradlew test` |
| 테스트 클래스 하나 실행 | `./gradlew test --tests "<패키지>.<클래스>"` |
| 실행 JAR 빌드 | `./gradlew bootJar` |

MR을 만들기 전에 `spotlessApply`로 포맷을 적용하고 `check`가 통과하는지 확인합니다. `check`는 `spotlessCheck`, `checkstyleMain`, `checkstyleTest`와 테스트를 실행하며 포맷이 맞지 않으면 실패합니다.

검사 대상은 `src/main/java`와 `src/test/java`의 직접 작성한 Java 소스입니다. `build/`의 생성 코드·산출물은 대상에서 제외합니다. 소스는 UTF-8·LF를 사용하며 `.gitattributes`가 Java 파일의 LF를 유지합니다.

IDE의 프로젝트 JDK·Gradle JVM은 Java 21, 파일 인코딩은 UTF-8, 줄바꿈은 LF로 맞춥니다. IDE 기본 포맷과 결과가 다르면 저장 후 `spotlessApply`를 실행하고 `spotlessCheck`로 확인합니다. CI에서는 `check`가 같은 포맷·규칙을 검사하며 파일을 자동 수정하지 않습니다.

## 요청 추적과 관리 지표

모든 요청은 응답의 `X-Request-ID`와 로그의 `request_id`로 연결합니다. 이 값은 API 본문의 중복 방지용 `requestId`와 구분합니다. 요청 요약에는 경로 패턴·상태·처리 시간만 남기며, 쿼리·인증값·본문과 원본 예외 메시지는 기록하지 않습니다.

Actuator·Micrometer Prometheus를 사용하며, histogram으로 p95를 조회합니다. 기본 관리 주소는 `127.0.0.1:9090`이고 공개 앱 포트와 분리합니다. `MONITORING_PASSWORD`를 설정해야 사용자 `monitoring`으로 관리용 HTTP Basic 인증을 사용할 수 있습니다. 값이 없으면 관리 접근을 거절합니다. 컨테이너 내부 수집에만 `MANAGEMENT_ADDRESS=0.0.0.0`을 사용하고 관리 포트를 호스트에 게시하지 않습니다.

관리 경로는 `/actuator/health`, `/actuator/prometheus`만 노출합니다. Nginx 전달, 보관 정책, Prometheus·Grafana 실행과 검증은 [관측성 운영 문서](../docs/observability.md)를 따릅니다.

기존 컨테이너와 프록시의 `GET /api/health`는 앱 포트에서 유지합니다. 내부 연결 상세 없이 `status`만 반환하며, 필수 연결이 비정상이면 HTTP 503으로 응답합니다. 이 공개 상태 확인과 인증이 필요한 관리 지표를 구분합니다.
