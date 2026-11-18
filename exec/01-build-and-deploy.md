# ELSEHERE 빌드·배포 매뉴얼

새 환경에서 ELSEHERE를 실행하려면 **소스 받기 → 빌드 → 환경 파일 작성 → 빈 DB 복원 → 앱 기동** 순서로 진행합니다. 이미 배포된 앱에서 시연만 하려면 [시연 시나리오](04-demo-scenario.md)부터 보면 됩니다.

## 1. 준비물과 소스 받기

- 빌드 PC에 Git, JDK 21, Unity Editor와 Web Build Support, Docker를 준비합니다. Unity 빌드 명령은 Windows PowerShell에서 실행합니다.
- 앱을 띄울 서버에 Docker와 Compose v2를 준비합니다. 서버 기동 명령은 Bash 기준입니다.
- Google OAuth, SSAFY GMS, Groq에서 발급받을 값은 [외부 서비스 설정](02-external-services.md)을 따라 준비합니다.

| 구성 | 버전과 설정 |
| --- | --- |
| 백엔드 빌드·IDE | JDK 21, Gradle Wrapper 9.8.2. IntelliJ IDEA 2026.1.4에서 `backend/`를 Gradle 프로젝트로 열 수 있습니다. 명령줄 빌드에는 IDE가 필요하지 않습니다. |
| 백엔드 JVM·WAS | Eclipse Temurin 21 JRE, Spring Boot 4.1.1 내장 Tomcat 11.0.24. 컨테이너 내부 포트 `8080`. |
| DB | PostgreSQL 16 (`postgres:16-alpine`), 내부 포트 `5432`. Flyway 12.4.0이 변경 SQL을 적용하고 JPA는 `validate`로 검사합니다. |
| 클라이언트 | Unity 6.3 LTS `6000.3.26f1`, Web Build Support, URP 17.3.0. Unity 프로젝트는 `frontend/UnityClient`입니다. |
| AI 워커 | Python 3.12, FastAPI·Uvicorn. 컨테이너 내부 포트 `8020`. 패키지 버전은 `ai/worker/requirements*.txt`와 `ai/feedback/requirements.txt`를 따릅니다. |
| 웹서버 | 컨테이너 Nginx `1.30.6-alpine`, 내부 포트 `80`. WebGL 정적 파일을 제공하고 `/api/`를 백엔드로 전달합니다. |
| 배포 호스트 | Ubuntu 24.04 LTS x86_64, Docker Engine 및 Compose v2. 호스트 Nginx가 `80`·`443`에서 TLS와 환경별 프록시를 처리합니다. 호스트 Nginx 버전은 대상 서버에서 `nginx -v`로 확인합니다. |

GitLab 접근 권한이 있는 계정으로 제출 브랜치를 클론합니다. 아래는 릴리스 브랜치 `main`을 받는 예입니다. 제출 주소가 `dev`를 가리키면 `--branch main`을 `--branch dev`로 바꿉니다. 제출 커밋이 별도로 지정되어 있으면 그 커밋을 체크아웃합니다.

```sh
git clone --branch main https://lab.ssafy.com/s15-final/S15P31A305.git
cd S15P31A305
git rev-parse HEAD
```

## 2. 백엔드와 WebGL 빌드

`JAVA_HOME`을 JDK 21 설치 경로로 설정합니다. 저장소 루트의 Windows PowerShell에서 아래 명령을 순서대로 실행합니다. `check`의 PostgreSQL 통합 테스트가 Docker를 사용하므로 Docker를 먼저 시작합니다. Bash에서 JAR를 빌드할 때는 `backend/`에서 `./gradlew check bootJar`를 실행합니다.

```powershell
Push-Location backend
./gradlew.bat check bootJar
Pop-Location
$env:CI_COMMIT_SHA = (git rev-parse HEAD).Trim()
./infra/scripts/run-unity-ci.ps1 -Action build
```

빌드 결과는 `backend/build/libs/app.jar`와 `dist/webgl/`입니다. `dist/webgl/index.html`이 있고 `version.json`의 `commit`이 `git rev-parse HEAD` 결과와 같은지 확인합니다. Unity Editor 설치 경로가 기본값과 다르면 `-UnityEditorPath`로 `Unity.exe`를 지정합니다. [Unity 빌드 절차](../docs/unity/setup.md#자동-빌드-진입점)

## 3. 배포 이미지 빌드

JAR와 WebGL 결과가 있는 저장소 루트에서 세 이미지를 빌드합니다. Docker는 Linux 컨테이너 모드로 실행하고, 모델과 패키지 다운로드에 필요한 네트워크를 연결합니다. 다음 명령은 PowerShell 기준입니다. Bash에서는 첫 줄을 `commit=$(git rev-parse HEAD)`로 바꿉니다.

```powershell
$commit = (git rev-parse HEAD).Trim()
docker build -f infra/docker/backend.Dockerfile -t "a305/backend:$commit" .
docker build -f infra/docker/ai-worker.Dockerfile -t "a305/ai:$commit" .
docker build -f infra/docker/web.Dockerfile -t "a305/web:$commit" .
```

빌드 PC와 서버가 다르면 세 이미지를 서버로 전달합니다. 예를 들어 빌드 PC에서 `docker save -o a305-images.tar "a305/backend:$commit" "a305/ai:$commit" "a305/web:$commit"`을 실행한 뒤 파일을 복사하고, 서버에서 `docker load -i a305-images.tar`를 실행합니다. 다음 단계의 이미지 태그에 `git rev-parse HEAD`에서 확인한 커밋 값을 씁니다.

| 빌드 변수·인자 | 설정 내용 |
| --- | --- |
| `JAVA_HOME` | JDK 21 설치 경로. Gradle Wrapper와 컴파일러가 사용합니다. |
| `UNITY_EDITOR_PATH` | 기본 경로와 다른 Unity Editor를 사용하는 경우 `Unity.exe`의 경로를 지정합니다. |
| `CI_COMMIT_SHA` | 빌드할 커밋의 전체 SHA. Unity 빌드가 `version.json`에 기록합니다. |
| `WEBGL_BUILD_DIRECTORY`, `WEBGL_DIRECTORY` | Unity 스크립트는 `WEBGL_BUILD_DIRECTORY`를 저장소의 `dist/webgl`로 지정합니다. 웹 이미지의 `WEBGL_DIRECTORY` 빌드 인자 기본값도 `dist/webgl`입니다. |
| `KOKORO_RELEASE` | AI 이미지 빌드의 모델 다운로드 경로입니다. 기본값은 Dockerfile에 지정된 `model-files-v1.1` 배포이며, 받은 파일의 체크섬을 확인합니다. |

## 4. 서버 환경 파일 작성

서버에서 클론한 저장소 루트로 이동해 아래 파일을 준비합니다. 이 예시는 GPU 설정 없이 기본 CPU 음성 합성을 사용합니다. `DB_PASSWORD`와 `AI_WORKER_TOKEN`은 이 환경 전용 값으로 만들고, OAuth와 모델 키는 발급받은 값을 넣습니다. [외부 서비스 값 준비](02-external-services.md)

```bash
mkdir -p "$HOME/a305-porting"
PORTING_ENV="$HOME/a305-porting/deploy.env"
```

위 경로에 `deploy.env`를 만들고 다음 예시에서 `REPLACE_`로 시작하는 값을 바꿉니다. 세 이미지 태그에는 3단계에서 빌드한 **같은 커밋 SHA**를 넣습니다.

```dotenv
BACKEND_HOST_PORT=8098
DB_NAME=elsehere_porting
DB_USERNAME=elsehere_porting
DB_PASSWORD=REPLACE_DB_PASSWORD
JPA_DDL_AUTO=validate
BACKEND_IMAGE=a305/backend:REPLACE_COMMIT_SHA
AI_IMAGE=a305/ai:REPLACE_COMMIT_SHA
WEB_IMAGE=a305/web:REPLACE_COMMIT_SHA
GOOGLE_CLIENT_ID=REPLACE_GOOGLE_CLIENT_ID
GOOGLE_CLIENT_SECRET=REPLACE_GOOGLE_CLIENT_SECRET
AI_WORKER_TOKEN=REPLACE_AI_WORKER_TOKEN
GMS_KEY=REPLACE_GMS_KEY
GROQ_API_KEY=REPLACE_GROQ_API_KEY
```

환경 파일을 저장한 뒤 `chmod 600 "$PORTING_ENV"`로 접근을 제한합니다. 각 값의 의미와 선택 설정은 아래 표를 참고합니다.

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

## 5. 빈 DB 복원과 기동

[제출용 덤프](elsehere-db.dump)는 `b2d6e79ef`의 백엔드로 빈 PostgreSQL 16을 초기화해 만든 custom 형식입니다. Flyway V46까지의 스키마와 공용 장소·NPC·미션·어휘가 들어 있습니다. 계정·대화·학습·음성 수집·서비스 이용 기록 테이블은 비어 있습니다.

서버의 저장소 루트에서 실행합니다. **새 `a305-porting` 프로젝트에 DB만 먼저 띄운 뒤** 복원합니다. `pg_restore`가 성공한 뒤에 앱 전체를 기동합니다.

```bash
PORTING_ENV="$HOME/a305-porting/deploy.env"
compose=(docker compose -p a305-porting --env-file "$PORTING_ENV" -f infra/compose/compose.app.yaml)

"${compose[@]}" config --quiet &&
"${compose[@]}" up -d --wait database &&
"${compose[@]}" cp exec/elsehere-db.dump database:/tmp/elsehere-db.dump &&
"${compose[@]}" exec -T database sh -ec '
  pg_restore --exit-on-error --single-transaction --no-owner --no-privileges --no-tablespaces \
    -U "$POSTGRES_USER" -d "$POSTGRES_DB" /tmp/elsehere-db.dump
' &&
"${compose[@]}" up -d --wait
```

## 6. 실행 확인

```bash
curl -fsS http://127.0.0.1:8098/api/health
curl -fsS http://127.0.0.1:8098/version.json
```

`/api/health`에서 `{"status":"UP"}`을, `/version.json`에서 빌드 커밋을 확인합니다. 서버와 같은 PC의 브라우저에서는 `http://127.0.0.1:8098`을 열고 게스트로 입장합니다. 별도 서버에서 실행했다면 해당 서버에 연결된 HTTPS 주소를 사용합니다. 기동에 실패하면 같은 셸에서 `"${compose[@]}" ps`와 `"${compose[@]}" logs --tail=100 database backend ai-worker web`으로 실패한 서비스를 확인합니다.

## 배포 시 확인

- 기존 개발 배포는 `dev`·`a305-dev`·`dev.elsehere.kr`, 운영은 `main`·`a305-prod`·`elsehere.kr`을 기준으로 합니다. 기존 환경을 업데이트할 때는 팀의 GitLab CI와 배포 수신기 절차를 따릅니다. 환경별 DB·계정·볼륨을 구분하고 운영 데이터에는 기존 Flyway 업그레이드 절차를 적용합니다. [개발 배포](../docs/dev-auto-deployment.md) · [운영 배포](../docs/prod-deployment.md)
- WebGL과 API를 같은 주소에서 제공하고, 호스트 Nginx에서 `Host`와 HTTPS 전달 헤더를 유지합니다. 공개 환경은 HTTPS를 사용한 뒤 브라우저의 마이크 권한, Google 콜백, 대화 스트리밍, gzip 파일과 Wasm 응답 형식을 확인합니다. 호스트 Nginx 설정 변경 시 `nginx -t`를 통과한 후 다시 불러옵니다. [HTTPS 및 브라우저 확인](../docs/https.md)
- 백엔드, AI, WebGL 이미지 태그와 덤프의 기준을 최종 제출 커밋에 맞춥니다. 기존 배포의 DB와 음성 볼륨은 해당 환경에 유지합니다. [서버 구성](../docs/deployment.md)
