# 백엔드

ELSEHERE 서비스 API 서버입니다. API는 [MVP API 명세](../docs/api/mvp-api.md), 데이터 구조는 [ERD](../docs/erd.dbml)를 따릅니다. 코드 작성 기준은 [공통 코드 규약](../docs/code-conventions.md)과 [백엔드 개발 지침](AGENTS.md)을 따릅니다.

## 기술 구성

| 항목 | 버전·구성 |
| --- | --- |
| 언어 | Java 21 (Gradle 툴체인으로 고정) |
| 프레임워크 | Spring Boot 4.1.1 |
| 빌드 | Gradle 9.8.2 Wrapper, Groovy DSL |
| 주요 의존성 | Spring Web MVC, Validation, Security, OAuth2 Client, Spring Data JPA, Flyway, Actuator, Lombok, PostgreSQL 드라이버 |
| 테스트 DB | Testcontainers PostgreSQL 16 (Docker 필요) |
| 포맷 | Spotless 8.9.0 + google-java-format 1.28.0 (Google Java Style, 들여쓰기 2칸) |
| 규칙 검사 | Checkstyle 14.3.0 ([설정](config/checkstyle/checkstyle.xml)) |
| 설정 파일 | `src/main/resources/application.properties` |

- DB 스키마는 Flyway 마이그레이션으로 관리합니다. 작성 기준은 [스키마 관리](#스키마-관리)를 따릅니다.
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
| `ai` | ai_requests (AI 호출 기록). AI 워커 호출과 AI 이용 상태(AI-01) |
| `user` | users, user_consents (가입 약관과 AI 개선 선택 동의) |
| `conversation` | conversations, turns, training_recordings (AI 개선에 동의한 사용자의 녹음) |
| `world` | scenes, npcs, scene_words, user_word_prefs |
| `vocab` | vocabularies, user_vocab |
| `mission` | missions, user_missions, first_day_missions |
| `onboarding` | user_language_profiles (학습 언어·대화 난이도 설정), level_tests, level_test_answers (레벨 테스트와 문항 응답) |
| `admin` | 없음 (운영자 어드민 화면·관리 API. 다른 도메인의 테이블을 고친다. [운영자 어드민](../docs/admin.md)) |

- 계층 패키지는 그 계층의 코드를 처음 작성할 때 만듭니다. 빈 패키지를 미리 만들지 않습니다.
- 요청·응답 객체(DTO)는 `<도메인>/dto`에 둡니다. 컨트롤러 하나에서만 쓰는 작은 요청은 컨트롤러 안의 record로 둘 수 있습니다.
- Spring Security가 호출하는 로그인 성공·실패 처리기와 인증 진입점은 `auth/handler`에 둡니다.

## 준비

- JDK 21을 설치하고 `JAVA_HOME`을 설정합니다. Gradle은 Wrapper가 내려받으므로 따로 설치하지 않습니다.
- 테스트는 Testcontainers로 PostgreSQL 컨테이너를 띄우므로 Docker Desktop을 실행한 상태에서 실행합니다.
- 테스트 JVM 안에서는 PostgreSQL 서버 하나를 공유하고 Spring 테스트 환경마다 별도 DB를 만듭니다. 각 환경의 테이블 생성·삭제와 데이터는 서로 격리되며 컨테이너는 JVM 종료 후 Testcontainers가 정리합니다. 여러 환경이 DB 연결을 미리 점유하지 않도록 테스트용 Hikari 풀의 최소 유휴 연결은 `0`으로 둡니다. 로컬 앱이나 개발·운영 DB를 테스트에 사용하지 않습니다.
- 로컬 실행(`bootRun`)은 PostgreSQL이 필요합니다. 접속 정보는 환경변수 `DB_URL`, `DB_USERNAME`, `DB_PASSWORD`로 지정하며, 없으면 `jdbc:postgresql://localhost:5432/elsehere`, 사용자 `elsehere`를 씁니다. 스키마는 앱이 시작할 때 Flyway가 만들고 바꾸며, `JPA_DDL_AUTO`(기본값 `validate`)는 엔티티와 스키마가 맞는지 검사합니다.
- 로컬에서는 환경변수 대신 `backend/.env`나 저장소 루트 `.env`에 `GOOGLE_CLIENT_ID=값`처럼 한 줄에 하나씩 적어도 됩니다. 값에 따옴표나 `export`를 붙이지 않으며, 같은 이름의 환경변수가 있으면 환경변수가 우선합니다. `.env`는 커밋하지 않습니다. 서버 컨테이너에는 `.env`가 없으므로 값은 Compose의 `environment`로 넘깁니다.
- NPC 대화·피드백·기억 카드는 AI 워커([계약](../docs/api/ai-worker.md))를 부릅니다. 워커 주소는 `AI_WORKER_BASE_URL`(기본 `http://localhost:8020`), 내부 토큰은 `AI_WORKER_TOKEN`으로 지정하며 워커의 `AI_WORKER_TOKEN`과 같은 값이어야 합니다. 토큰은 비밀값이므로 저장소에 넣지 않습니다.
- NPC 턴 음성(WAV)은 `NPC_AUDIO_DIR`(기본 `./data/npc-audio`) 폴더에 저장합니다. 컨테이너로 실행하면 재시작 뒤에도 남도록 컨테이너 밖 볼륨으로 연결합니다.
- IDE에서 프로젝트를 열 때는 `backend/` 폴더를 Gradle 프로젝트로 엽니다.

## 상태 확인과 배포 산출물

`GET /api/health`는 인증 없이 앱과 DB 상태를 확인합니다. 정상 응답은 `{"status":"UP"}`이며 세부 연결 정보와 다른 관리 엔드포인트는 노출하지 않습니다. 다른 API 경로의 기존 인증 요구와 CSRF 설정은 유지합니다. 기능 담당자가 Security 설정을 추가할 때도 이 접근 경계를 보존하고 `HealthEndpointTest`로 확인합니다.

`./gradlew check bootJar`가 검사·테스트와 실행 파일 `build/libs/app.jar`를 생성합니다. Docker 이미지는 이 파일을 받아 실행하며 이미지 생성 단계와 서버에서 JAR를 다시 빌드하지 않습니다.

## NPC 음성 파일 저장

NPC가 생성한 WAV는 `NPC_AUDIO_DIR`에 저장하며 로컬 기본 경로는 `./data/npc-audio`입니다. 상세 평가의 모범 음성(FB-04)은 같은 폴더의 `model-speech/`에, AI 개선에 동의한 사용자의 녹음은 `training-recordings/`(`TRAINING_RECORDING_DIR`)에 저장합니다([AI 개선 데이터 활용 기준](../docs/ai-training-data.md)). 앱 Compose는 `/app/data/npc-audio`에 환경별 영구 볼륨을 연결합니다. NPC 턴 WAV는 최대 30일·환경별 합계 5GB까지 보관하고, 만료·용량 정리로 삭제한 음성은 다시 합성하지 않습니다. 공용 음성과 하위 폴더는 이 정책에서 제외합니다. 정책 원문, 저장 권한, 재배포·백업·복원 기준은 [NPC 음성 저장 운영 문서](../docs/npc-audio-storage.md)를 따릅니다.

## 스키마 관리

- 스키마는 Flyway가 [`src/main/resources/db/migration`](src/main/resources/db/migration)의 SQL 파일로 만들고 바꿉니다. 앱이 시작할 때 아직 적용하지 않은 파일만 버전 순서대로 실행하고 DB의 `flyway_schema_history`에 기록합니다. JPA는 `ddl-auto=validate`로 엔티티와 스키마가 맞는지만 검사하며 스키마를 바꾸지 않습니다.
- 엔티티를 바꾸면 같은 MR에 다음 순번의 마이그레이션 파일(`V<번호>__<설명>.sql`, 밑줄 두 개)을 추가하고 [ERD](../docs/erd.dbml)도 함께 수정합니다.
- 번호는 순번입니다. 다른 MR과 번호가 겹치면 병합 뒤 앱이 시작하지 않으므로, 병합 전에 최신 `dev`를 반영해 마지막 번호를 확인합니다.
- `dev`에 병합된 마이그레이션 파일은 고치지 않습니다. 적용된 파일의 내용이 바뀌면 체크섬이 맞지 않아 앱이 시작하지 않습니다. 고칠 내용은 새 파일로 추가합니다.
- 앱 이미지를 이전 버전으로 되돌려도 DB는 되돌아가지 않습니다. 컬럼 이름 바꾸기·삭제처럼 이전 버전 앱이 깨지는 변경은 추가 → 데이터 옮기기 → 삭제처럼 나눠서 적용합니다.
- V1은 Flyway 도입 전까지 엔티티 매핑이 만들던 스키마입니다. 유니크 제약 이름은 기존 DB와 같도록 Hibernate가 붙이던 `uk...` 이름을 씁니다.
- Flyway 도입 전에 `ddl-auto=update`로 만든 DB(이력 테이블 없음)는 `spring.flyway.baseline-on-migrate=true`로 V1 상태로 인정하고 V2부터 적용합니다. V2는 `update`가 고치지 않은 차이(enum CHECK 제약, 없앤 사물 테이블)를 맞춥니다. 로컬 DB가 V1보다 오래되어 앱 시작 때 검사에서 실패하면(예: `scene_words` 테이블 없음) 로컬 DB를 지우고 다시 만듭니다.
- ERD 상단에 "DDL에서 직접 만들 것"으로 적은 항목(pgvector 확장, 부분 인덱스)은 해당 기능을 구현할 때 마이그레이션 파일에 넣습니다.
- `EntitySchemaTest`는 마이그레이션으로 만든 스키마가 엔티티 매핑으로 만든 스키마와 같은지(컬럼·타입·NOT NULL·기본값, CHECK·유니크·외래 키, 인덱스) 비교합니다. `validate`가 보지 않는 enum CHECK 제약도 여기서 비교하므로, 마이그레이션을 빠뜨리거나 엔티티와 다르게 쓰면 이 테스트가 실패합니다.
- 엔티티 필드와 DB 컬럼의 타입이 다르면 `validate`가 실패합니다. `Integer` 필드를 `smallint` 컬럼에 두려면 `columnDefinition` 대신 `@JdbcTypeCode(SqlTypes.SMALLINT)`를 씁니다.
- 테스트 전용 엔티티의 테이블은 [`src/test/resources/db/test-migration`](src/test/resources/db/test-migration)에서 만듭니다. 테스트 설정이 운영 마이그레이션과 이 위치를 함께 읽습니다.
- 대화·피드백·기억 서비스 테스트(`DialogueServiceTest`를 붙인 테스트)는 AI 워커를 Mock으로 바꾸고 실제 DB와 함께 실행합니다. `AiWorkerClientTest`는 테스트 안에서 띄운 가짜 워커로 요청 형식을 확인하며 실제 AI 워커를 부르지 않습니다.

## 시드 데이터

- 장소·NPC·사물·어휘·미션처럼 모든 환경에 같이 있어야 하는 기본 데이터는 [`src/main/resources/db/seed/world.sql`](src/main/resources/db/seed/world.sql)에 있습니다.
- 사물 단어·한국어 뜻·영문 예문·예문 해석·발음 파일 키의 편집 원본은 [Unity 공용 단어 에셋](../frontend/UnityClient/Assets/Data/ObjectHover/Words/)입니다. Unity의 **Tools → A305 → ObjectHover → Export Backend Data**로 [단어 JSON](src/main/resources/content/object-words.json), [씬·프리팹 연결 JSON](src/main/resources/content/object-word-bindings.json), SQL의 `GENERATED OBJECT WORDS` 구간을 생성합니다. 발음 파일 키가 빈 항목은 DB에 NULL로 적재합니다.
- Editor가 프로젝트를 닫은 상태에서는 저장소 루트의 `./infra/scripts/sync-object-words.ps1`로 내보내고 `-Check`로 에셋과 생성 파일의 일치를 검사합니다. 프로젝트 버전의 Unity Editor가 필요하며 다른 설치 위치는 `-UnityPath` 또는 `UNITY_EDITOR_PATH`로 지정합니다. 생성 JSON·SQL 구간은 직접 편집하지 않습니다.
- `WorldSeedLoader`가 애플리케이션이 시작될 때(로컬·개발·운영 모두) 이 SQL을 실행합니다. 고유 키(`code`, `(language_code, text)`)가 같은 행이 있으면 시드 값으로 갱신하므로 여러 번 실행해도 행이 늘지 않습니다. DB에서 직접 고친 값은 다음 시작 때 시드 값으로 돌아가므로, 사물 어휘는 Unity 에셋에서, 그 밖의 기본 데이터는 SQL 파일에서 고칩니다. 예외로 NPC 페르소나(`npcs.persona`)는 NPC를 처음 넣을 때만 시드 값을 쓰고, 이후에는 [운영자 어드민](../docs/admin.md)에서 고친 값을 유지합니다. 언어 코드나 영어 표현을 바꾸면 현재 DB에서는 새 어휘 행이 생기며 기존 행은 삭제하지 않습니다. 기존 사용자 학습 기록을 새 표현으로 옮겨야 한다면 별도 DB 마이그레이션이 필요합니다.
- NPC 인격 카드(`npcs.profile`)의 원본은 AI 쪽 `ai/npcs/<npcId>.json`이고, AI 도구(`ai/tools/npc_configs.py`)가 [NPC 설정 JSON](src/main/resources/content/npc-configurations.json)으로 내보냅니다. `WorldSeedLoader`가 SQL을 실행한 뒤 이 파일의 `npcProfile`을 카드가 없는 NPC에만 넣고, 이미 카드가 있는 NPC는 바꾸지 않습니다(페르소나처럼 처음만 씀). 카드는 AI 워커 요청의 `npc.profile`에 칸 이름(camelCase) 그대로 실리며, 카드가 없는 NPC는 페르소나의 역할 한 줄로 대답합니다. 카드가 있는 NPC의 `name`은 카드의 이름과 같게 둡니다.
- `code`, `unity_key`는 Unity 씬·빌더에 쓴 값과 같아야 합니다. 행을 추가하거나 고치면 `WorldSeedLoaderTest`의 기대값도 함께 고칩니다.
- 공용 목록의 어휘 전체를 `vocabularies`에 적재합니다. SQL을 실행한 뒤 `WorldSeedLoader`가 단어 JSON의 공용 키를 `vocabularies.word_key`에 넣고, 연결 JSON의 씬별 공용 키 목록으로 `scene_words`(장소별 단어)를 맞춥니다. 연결 JSON에서 빠진 단어는 그 장소의 `scene_words`에서 지웁니다. WORLD-02 단어 카드, WORLD-03 숨김, VOCAB-02 사물 저장은 모두 이 공용 키(`wordKey`)를 씁니다. 사용자별 저장·숨김 상태(`user_vocab`, `user_word_prefs`)는 공용 에셋에 넣지 않습니다.
- 사물 단어 발음 음성(WORLD-04)은 음성 볼륨의 하위 폴더(`elsehere.pronunciation.dir`, 기본 `${NPC_AUDIO_DIR}/pronunciation`)에 `{공용 키}.wav`로 둡니다. 관리자가 발음 생성 API(`POST /api/v1/admin/pronunciations`)로 발음 파일이 없는 단어만 AI 워커 TTS로 만듭니다([운영자 어드민](../docs/admin.md)). 시드 SQL이 발음 키를 Unity 에셋 값(대부분 비어 있음)으로 되돌리므로, `WorldSeedLoader`가 서버를 시작할 때마다 Unity 에셋에 발음 키가 없는 단어 중 이 폴더에 파일이 있는 단어에 파일 이름을 `pronunciation_audio_key`로 다시 넣습니다. 볼륨은 환경마다 따로라 개발·운영에서 각각 한 번 만듭니다.
- Spring의 SQL 초기화(`spring.sql.init`)는 `@DataJpaTest`에서도 실행되어 테스트 데이터와 겹치므로 쓰지 않습니다. DB 테스트에서 시드가 필요하면 `WorldSeedLoader`를 `@Import`합니다.

## 인증과 오류 응답

- 로그인 상태는 서버 세션에 두고 세션 쿠키(HttpOnly, `SameSite=Lax`)로 유지합니다. 로그인 없이 `/api/v1` API를 호출하면 `401 UNAUTHENTICATED`, Google 로그인만 하고 가입 정보를 내지 않았으면 `403 SIGNUP_REQUIRED`입니다.
- 가입자 세션은 요청이 24시간 없으면 끝나고(`server.servlet.session.timeout`), 쿠키도 24시간 유지해 브라우저를 닫았다 열어도 이어집니다. 게스트는 로그인할 때 세션을 2시간으로 줄입니다(`elsehere.guest.session-timeout`). 세션은 서버 메모리에 있어 백엔드를 다시 시작하면 모두 로그아웃되므로, 운영 배포는 사용자가 적은 시간에 합니다. 서버를 늘리거나 무중단 배포를 하게 되면 세션을 DB 등 외부 저장소로 옮깁니다.
- 가입(AUTH-03·04)은 Google 로그인이 세션에 남긴 가입 대기 정보로 진행합니다. 받는 약관과 현재 버전은 `ConsentCatalog`에 있으며, 약관 문서를 바꾸면 이 클래스의 버전과 주소를 함께 바꿉니다.
- POST·PUT·PATCH·DELETE 요청에는 `X-Requested-With` 헤더(값은 자유)를 붙여야 합니다. 없으면 `403 CSRF_HEADER_REQUIRED`입니다.
- 운영자 어드민 화면(`/admin/**`)과 관리 API(`/api/v1/admin/**`)는 `users.role`이 `ADMIN`인 사용자만 씁니다. 보안 설정이 요청마다 `AdminAccess`로 DB의 역할을 확인하며, 관리자가 아니면 `403 ADMIN_REQUIRED`입니다. 로그인하지 않고 어드민 화면을 열면 Google 로그인으로 보냅니다. 관리자 지정 방법은 [운영자 어드민](../docs/admin.md)을 따릅니다.
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

### 게스트로 시작하기

[MVP API 명세](../docs/api/mvp-api.md)의 AUTH-07입니다. `POST /api/v1/auth/guest`에 고른 캐릭터(`avatarType`)를 보내면 Google 로그인과 가입 화면 없이 새 사용자(`provider=GUEST`)를 만들고 그 사용자로 로그인합니다. 닉네임은 `guest-` 뒤에 여섯 자리 숫자를 붙이고 약관 동의 기록은 남기지 않습니다. 학습 언어·대화 난이도는 영어·초급으로 정해 튜토리얼부터 시작합니다. 모든 환경에서 열려 있습니다.

| 환경변수 | 기본값 | 설명 |
| --- | --- | --- |
| `GUEST_CONVERSATION_LIMIT` | `3` | 게스트 계정 하나가 시작할 수 있는 NPC 대화 수(CONV-01). 다 쓰면 `AI_QUOTA_EXHAUSTED`이며 다시 채워지지 않습니다. |

- 게스트는 세션이 끝나면 다시 들어올 수 없는 일회용 계정입니다. `GuestContentCleanup`이 매시 30분에 마지막 활동 뒤 24시간(`elsehere.guest.content-retention`)이 지난 게스트의 **내용만** 지웁니다. 지우는 것은 발화 원문(`turns`)과 그 NPC 음성 파일(NPC 공용 말걸기 음성 제외), 대화 피드백, 기억 카드·관계, 저장 단어·숨김, 레벨 테스트, 학습용 녹음입니다. 사용자·대화·AI 호출 기록·미션 진행·언어 프로필·게임 시계 행은 남겨, 남은 행이 가리키는 곳이 사라지지 않고 게스트 수·대화 수·사용량을 집계할 수 있게 합니다.
- 사용자·대화를 가리키는 칸(`user_id`, `…conversation_id`)이 있는 테이블을 새로 만들면 `GuestContentCleanup`의 지움(`CLEARED_TABLES`)·남김(`RETAINED_TABLES`) 중 한쪽에 넣습니다. 빠뜨리면 `GuestContentCleanupTest`가 실패합니다.

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
