# ELSEHERE 빌드·배포 매뉴얼

빌드와 DB 복원 절차는 `dev`의 `f925fa353`을 기준으로 정리했습니다.

## 실행 환경

| 구성 | 버전과 설정 |
| --- | --- |
| 백엔드 빌드·IDE | JDK 21, Gradle Wrapper 9.8.2. IntelliJ IDEA 2026.1.4에서 `backend/`를 Gradle 프로젝트로 열 수 있습니다. 명령줄 빌드에는 IDE가 필요하지 않습니다. |
| 백엔드 JVM·WAS | Eclipse Temurin 21 JRE, Spring Boot 4.1.1 내장 Tomcat 11.0.24. 컨테이너 내부 포트 `8080`. |
| DB | PostgreSQL 16 (`postgres:16-alpine`), 내부 포트 `5432`. Flyway 12.4.0이 변경 SQL을 적용하고 JPA는 `validate`로 검사합니다. |
| 클라이언트 | Unity 6.3 LTS `6000.3.26f1`, Web Build Support, URP 17.3.0. Unity 프로젝트는 `frontend/UnityClient`입니다. |
| AI 워커 | Python 3.12, FastAPI·Uvicorn. 컨테이너 내부 포트 `8020`. 패키지 버전은 `ai/worker/requirements*.txt`와 `ai/feedback/requirements.txt`를 따릅니다. |
| 웹서버 | 컨테이너 Nginx `1.30.6-alpine`, 내부 포트 `80`. WebGL 정적 파일을 제공하고 `/api/`를 백엔드로 전달합니다. |
| 배포 호스트 | Ubuntu 24.04 LTS x86_64, Docker Engine 및 Compose v2. 호스트 Nginx가 `80`·`443`에서 TLS와 환경별 프록시를 처리합니다. 호스트 Nginx 버전은 대상 서버에서 `nginx -v`로 확인합니다. |

## 소스 클론과 빌드

GitLab 프로젝트에 접근할 수 있는 계정으로 저장소를 클론합니다. 최종 제출 커밋을 확인한 다음 백엔드와 WebGL을 같은 커밋에서 빌드합니다.

```sh
git clone https://lab.ssafy.com/s15-final/S15P31A305.git
cd S15P31A305
git rev-parse HEAD
```

`backend/`에서 `JAVA_HOME`을 JDK 21로 설정하고 `./gradlew check bootJar`를 실행합니다. Windows PowerShell에서는 `./gradlew.bat check bootJar`를 실행합니다. `check`의 PostgreSQL 통합 테스트는 Docker가 필요합니다. 실행 파일은 `backend/build/libs/app.jar`에 생성됩니다. JAR만 다시 만들 때는 `check`를 생략하고 `bootJar`를 실행할 수 있습니다.

Windows 빌드 PC에서는 Unity Hub로 위 버전의 Editor와 Web Build Support를 설치한 뒤, 저장소 루트의 PowerShell에서 다음을 실행합니다. 기본 경로 외의 Editor를 사용하면 `-UnityEditorPath`로 `Unity.exe`를 지정합니다.

```powershell
$env:CI_COMMIT_SHA = (git rev-parse HEAD).Trim()
./infra/scripts/run-unity-ci.ps1 -Action build
```

WebGL 결과는 `dist/webgl/`에 생성됩니다. `index.html`과 `version.json`이 있고 `version.json`의 `commit`이 빌드할 커밋과 일치하는지 확인합니다. 배포용 스크립트는 PC용과 모바일용 텍스처를 빌드하고 결과를 합칩니다. [Unity 빌드 절차](../docs/unity/setup.md#자동-빌드-진입점)

JAR와 `dist/webgl/`이 준비된 빌드 환경에서 저장소 루트 기준으로 세 이미지를 만듭니다. 운영 서버와 빌드 장비가 다르면 동일한 커밋의 산출물을 빌드 환경에 준비하고, 세 이미지를 팀의 배포 방식에 따라 전달합니다. Dockerfile의 모델 다운로드와 패키지 설치에는 빌드 시 네트워크 연결이 필요합니다.

```bash
commit=$(git rev-parse HEAD)
docker build -f infra/docker/backend.Dockerfile -t "a305/backend:$commit" .
docker build -f infra/docker/ai-worker.Dockerfile -t "a305/ai:$commit" .
docker build -f infra/docker/web.Dockerfile -t "a305/web:$commit" .
```

| 빌드 변수·인자 | 설정 내용 |
| --- | --- |
| `JAVA_HOME` | JDK 21 설치 경로. Gradle Wrapper와 컴파일러가 사용합니다. |
| `UNITY_EDITOR_PATH` | 기본 경로와 다른 Unity Editor를 사용하는 경우 `Unity.exe`의 경로를 지정합니다. |
| `CI_COMMIT_SHA` | 빌드할 커밋의 전체 SHA. Unity 빌드가 `version.json`에 기록합니다. |
| `WEBGL_BUILD_DIRECTORY`, `WEBGL_DIRECTORY` | Unity 스크립트는 `WEBGL_BUILD_DIRECTORY`를 저장소의 `dist/webgl`로 지정합니다. 웹 이미지의 `WEBGL_DIRECTORY` 빌드 인자 기본값도 `dist/webgl`입니다. |
| `KOKORO_RELEASE` | AI 이미지 빌드의 모델 다운로드 경로입니다. 기본값은 Dockerfile에 지정된 `model-files-v1.1` 배포이며, 받은 파일의 체크섬을 확인합니다. |

## 환경 변수와 설정 파일

`infra/compose/deploy.prod.env.example` 또는 `deploy.dev.env.example`을 참고해 실행할 환경의 `deploy.env`를 서버에 만듭니다. 새 검증 환경에는 전용 DB와 새 접속 값을 사용합니다. 서버의 실제 값은 환경 파일에 보관하고, 이 폴더의 포팅 문서에는 변수명과 설정 위치를 기록합니다. [외부 서비스 값 준비](02-external-services.md)

| 변수 | 설정 내용 |
| --- | --- |
| `DB_NAME`, `DB_USERNAME`, `DB_PASSWORD` | PostgreSQL DB 이름, 앱 접속 계정, 비밀번호. Compose에서 동일한 값으로 DB를 초기화하고 백엔드를 연결합니다. 로컬 JAR 실행 시 `DB_URL=jdbc:postgresql://localhost:5432/elsehere` 형식으로 직접 지정합니다. |
| `JPA_DDL_AUTO` | `validate`. 테이블 변경은 백엔드 시작 시 Flyway가 적용합니다. |
| `BACKEND_IMAGE`, `AI_IMAGE`, `WEB_IMAGE` | 같은 제출 커밋으로 빌드한 이미지 참조. 위 예시를 썼다면 각각 `a305/backend:<커밋>`, `a305/ai:<커밋>`, `a305/web:<커밋>`입니다. |
| `BACKEND_HOST_PORT` | 호스트 루프백에 노출할 **웹** 포트. 기존 개발은 `8081`, 운영은 `8080`입니다. 아래 새 검증 환경 예시는 `8098`을 사용합니다. |
| `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` | Google 웹 OAuth 클라이언트 값. 앱 Compose 기동에 필요합니다. |
| `AI_WORKER_TOKEN`, `AI_WORKER_BASE_URL` | 내부 호출 토큰은 백엔드와 워커가 공유합니다. Compose는 주소를 `http://ai-worker:8020`으로 설정하고, 로컬 백엔드는 기본 `http://localhost:8020`을 사용합니다. |
| `LLM_PROFILE`, `GMS_KEY`, `GROQ_API_KEY` | 모델 프로필 기본값은 `gms-luna`입니다. 두 API 키는 앱 Compose에서 요구합니다. |
| `ADMIN_LOGIN_ID`, `ADMIN_PASSWORD` | 선택 사항. 운영자 화면을 쓰려면 환경 전용 어드민 계정을 설정합니다. 비밀번호는 12자 이상, UTF-8 72바이트 이하입니다. 두 값이 비어 있으면 어드민 로그인이 비활성화됩니다. |
| `STT_CHAIN`, `*_GPU_BASE_URL`, `*_GPU_TOKEN`, `GUARD_GPU_MODEL`, `JUDGE_GPU_MODEL`, `NPC_GPU_MODEL` | 선택 사항. GPU가 있는 환경에서 통신 주소, 토큰, 모델을 지정합니다. GPU 없이 재현할 때는 예제 파일에 채워진 GPU 주소, 모델, `STT_CHAIN`을 비워 기본 STT와 CPU 음성 합성 경로를 사용합니다. |
| `NPC_AUDIO_DIR` | Compose는 `/app/data/npc-audio`에 `npc-audio-data` 볼륨을 연결합니다. PostgreSQL 덤프는 음성 볼륨을 포함하지 않습니다. |

계정·프로퍼티를 확인할 파일은 다음과 같습니다.

| 파일 | 확인할 내용 |
| --- | --- |
| [백엔드 application.properties](../backend/src/main/resources/application.properties) | DB 접속, OAuth, 어드민, AI 워커, Flyway·JPA 기본값. 로컬 실행 시 `backend/.env` 또는 루트 `.env`를 읽습니다. |
| [앱 Compose](../infra/compose/compose.app.yaml)·[운영 환경 예제](../infra/compose/deploy.prod.env.example)·[개발 환경 예제](../infra/compose/deploy.dev.env.example) | 컨테이너 간 주소, 포트, 계정 변수, 데이터 볼륨과 환경별 예제 값. |
| [Flyway 마이그레이션](../backend/src/main/resources/db/migration)·[공용 데이터 시드](../backend/src/main/resources/db/seed/world.sql)·[ERD](../docs/erd.dbml) | DB 구조, 장소·NPC·어휘·미션 기본값, 테이블 관계. NPC 카드와 단어 연결은 [공용 콘텐츠](../backend/src/main/resources/content)에서 함께 로드합니다. |
| [Nginx 웹 설정](../infra/nginx/web.conf)·[HTTPS 운영](../docs/https.md) | WebGL·API 프록시 설정과 호스트에서 발급하는 TLS 인증서. |

## 새 환경에서 DB 복원과 실행

[제출용 덤프](elsehere-db.dump)는 PostgreSQL 16 custom 형식입니다. 빈 DB에 `f925fa353` 백엔드를 처음 실행해 Flyway V45까지 적용하고 장소·NPC·미션·공용 어휘와 장소별 어휘 연결을 넣어 만들었습니다. 사용자 관련 테이블의 구조도 포함되며, 계정·대화·학습·음성 수집·서비스 이용 기록은 비어 있습니다. 새 환경의 DB 접속 값은 위 환경 변수에서 지정합니다.

아래 명령은 Bash 기준입니다. 저장소 루트에서 실행하고 `PORTING_ENV`를 작성한 전용 환경 파일 경로로 바꿉니다. `BACKEND_HOST_PORT=8098`을 예로 사용합니다. **아직 백엔드를 띄운 적 없는 빈 `a305-porting` DB에** 덤프를 복원합니다.

```bash
PORTING_ENV=/path/to/porting/deploy.env
compose=(docker compose -p a305-porting --env-file "$PORTING_ENV" -f infra/compose/compose.app.yaml)

"${compose[@]}" config --quiet
"${compose[@]}" up -d --wait database
"${compose[@]}" cp exec/elsehere-db.dump database:/tmp/elsehere-db.dump
"${compose[@]}" exec -T database sh -ec '
  pg_restore --exit-on-error --single-transaction --no-owner --no-privileges --no-tablespaces \
    -U "$POSTGRES_USER" -d "$POSTGRES_DB" /tmp/elsehere-db.dump
'
"${compose[@]}" up -d --wait

curl -fsS http://127.0.0.1:8098/api/health
curl -fsS http://127.0.0.1:8098/version.json
```

`/api/health`에서 `{"status":"UP"}`을, `/version.json`에서 빌드 커밋을 확인합니다. 초기 사용자 데이터가 없는 상태에서 브라우저의 게스트 시작으로 접속할 수 있습니다. 덤프를 갱신할 때는 새 빈 DB에 해당 커밋의 백엔드를 실행한 뒤, 공용 시드와 Flyway 이력만 행이 있는지 확인하고 같은 형식으로 내보냅니다.

## 배포 시 확인

- 기존 개발 배포는 `dev`·`a305-dev`·`dev.elsehere.kr`, 운영은 `main`·`a305-prod`·`elsehere.kr`을 기준으로 합니다. 기존 환경을 업데이트할 때는 팀의 GitLab CI와 배포 수신기 절차를 따릅니다. 환경별 DB·계정·볼륨을 구분하고 운영 데이터에는 기존 Flyway 업그레이드 절차를 적용합니다. [개발 배포](../docs/dev-auto-deployment.md) · [운영 배포](../docs/prod-deployment.md)
- WebGL과 API를 같은 주소에서 제공하고, 호스트 Nginx에서 `Host`와 HTTPS 전달 헤더를 유지합니다. 공개 환경은 HTTPS를 사용한 뒤 브라우저의 마이크 권한, Google 콜백, 대화 스트리밍, gzip 파일과 Wasm 응답 형식을 확인합니다. 호스트 Nginx 설정 변경 시 `nginx -t`를 통과한 후 다시 불러옵니다. [HTTPS 및 브라우저 확인](../docs/https.md)
- 백엔드, AI, WebGL 이미지 태그와 덤프의 기준을 최종 제출 커밋에 맞춥니다. 기존 배포의 DB와 음성 볼륨은 해당 환경에 유지합니다. [서버 구성](../docs/deployment.md)
