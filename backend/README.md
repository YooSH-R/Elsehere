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

## 준비

- JDK 21을 설치하고 `JAVA_HOME`을 설정합니다. Gradle은 Wrapper가 내려받으므로 따로 설치하지 않습니다.
- 테스트는 Testcontainers로 PostgreSQL 컨테이너를 띄우므로 Docker Desktop을 실행한 상태에서 실행합니다.
- 로컬 실행(`bootRun`)은 PostgreSQL이 필요합니다. 접속 정보는 환경변수 `DB_URL`, `DB_USERNAME`, `DB_PASSWORD`로 지정하며, 없으면 `jdbc:postgresql://localhost:5432/elsehere`, 사용자 `elsehere`를 씁니다.
- IDE에서 프로젝트를 열 때는 `backend/` 폴더를 Gradle 프로젝트로 엽니다.

## 명령

`backend/` 폴더에서 실행합니다. Windows PowerShell·명령 프롬프트에서는 `./gradlew` 대신 `.\gradlew.bat`을 사용합니다.

| 목적 | 명령 |
| --- | --- |
| 실행 | `./gradlew bootRun` |
| 포맷 적용 | `./gradlew spotlessApply` |
| 포맷·규칙 검사·테스트 | `./gradlew check` |
| 테스트만 실행 | `./gradlew test` |

MR을 만들기 전에 `spotlessApply`로 포맷을 적용하고 `check`가 통과하는지 확인합니다. 포맷이 맞지 않으면 `check`가 실패합니다.
