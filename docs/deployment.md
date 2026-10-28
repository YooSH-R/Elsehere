# 단일 서버의 개발·운영 구성

하나의 Ubuntu 서버에서 개발과 운영을 서로 다른 Docker Compose 프로젝트로 실행합니다. `dev`와 `main`은 배포할 코드 버전의 기준이고, 실행 환경은 프로젝트 이름·포트·네트워크·환경 파일로 구분합니다.

## 적용 범위

현재 개발 환경은 PostgreSQL·백엔드·AI 워커·Unity WebGL 웹 컨테이너를 실행하며, 운영 환경은 환경 확인용 서비스를 유지합니다. 호스트 Nginx가 도메인에 따라 각 환경으로 전달하고 기존 SSAFY 경로도 호환 제공합니다. 운영 확인용 응답은 실제 애플리케이션의 기동이나 통합 동작을 보장하지 않습니다.

백엔드·AI 워커·웹 이미지와 앱용 Compose는 저장소에서 관리합니다. Unity 테스트·WebGL 빌드는 [Windows 노트북 전용 Runner](unity/windows-runner.md)에서 실행합니다. 개발은 [dev 자동 배포](dev-auto-deployment.md)의 변경 영역 검사·이미지 전달·배포 후 점검을 사용합니다. GPU 서버 연결은 제공 조건과 AI 실행 방식이 정해진 뒤 반영합니다.

## 서버와 환경 구분

| 항목 | 구성 |
| --- | --- |
| 앱 도메인 | 개발 `dev.elsehere.kr`, 운영 `elsehere.kr`·`www.elsehere.kr` |
| 서버 관리·호환 주소 | `k15a305.p.ssafy.io` |
| 접속 | PEM 인증키를 사용하는 `ubuntu` 계정의 SSH |
| 운영체제·아키텍처 | Ubuntu 24.04 LTS, `x86_64` |
| 서버 자원 | 논리 CPU 4개, 메모리 약 15GiB, 루트 디스크 약 309GiB, Swap 없음 |
| 외부 진입점 | 호스트 Nginx, HTTP 80·HTTPS 443 |
| 방화벽 | UFW 수신 기본 차단, SSH 22·HTTP 80·HTTPS 443 허용 |

| 항목 | 개발 | 운영 |
| --- | --- | --- |
| 배포 기준 브랜치 | `dev` | `main` |
| Compose 프로젝트 | `a305-dev` | `a305-prod` |
| Docker 기본 네트워크 | `a305-dev_default` | `a305-prod_default` |
| 소스 폴더 | `~/apps/a305/dev/source` | `~/apps/a305/prod/source` |
| 확인용 환경 파일 | `~/apps/a305/dev/bootstrap.env` | `~/apps/a305/prod/bootstrap.env` |
| 앱용 환경 파일 위치 | `~/apps/a305/dev/deploy.env` | `~/apps/a305/prod/deploy.env` |
| 웹 호스트 포트 | `127.0.0.1:8081` | `127.0.0.1:8080` |
| 도메인별 API 경로 | `https://dev.elsehere.kr/api/` | `https://elsehere.kr/api/` |
| 기존 SSAFY API 경로 | `/dev/api/` | `/api/` |
| 확인용 응답의 환경 이름 | `dev` | `prod` |

`deploy.env`는 앱 설정을 둘 위치입니다. 백엔드는 PostgreSQL을 사용하며 연결 변수는 `DB_URL`, `DB_USERNAME`, `DB_PASSWORD`입니다. 서버에 앱과 DB를 배포할 때 환경별 연결값을 이 파일에 설정합니다. 확인용 컨테이너는 소스 폴더나 Git 브랜치를 읽지 않습니다.

## 요청 전달 구조

```text
클라이언트 → 호스트 Nginx (TLS 종료)
              ├─ dev.elsehere.kr /     → 127.0.0.1:8081 → 개발 웹·API
              └─ elsehere.kr /, www / → 127.0.0.1:8080 → 운영 확인용 서비스
```

신규 개발 주소에는 `/dev/`를 붙이지 않습니다. 기존 SSAFY 주소의 개발 경로에서만 `/dev`를 제거해 내부로 전달합니다. 두 환경은 호스트의 루프백 주소에만 포트를 게시합니다. Nginx를 컨테이너로 옮기면 `127.0.0.1`의 대상이 달라지므로 연결 방식도 함께 바꿔야 합니다.

개발 API도 주소로 직접 접근할 수 있습니다. 도메인·경로 구분은 접근 인증을 대신하지 않으므로 개발·운영 자격 증명과 데이터를 분리합니다. 인증서·리다이렉트·실제 브라우저 검증은 [HTTPS 운영](https.md)을 따릅니다.

## 설정 파일의 책임

| 파일 | 역할 |
| --- | --- |
| `infra/compose/compose.bootstrap.yaml` | 환경 확인용 서비스, 루프백 포트, 상태 검사, 자원·로그 상한 |
| `infra/compose/bootstrap.dev.env.example` | 개발 환경 이름과 포트 예제 |
| `infra/compose/bootstrap.prod.env.example` | 운영 환경 이름과 포트 예제 |
| `infra/nginx/bootstrap-response.conf.template` | 확인용 JSON 응답 |
| `infra/nginx/a305.bootstrap.conf` | 최초 HTTP 연결용 프록시 예제 |
| `/etc/nginx/sites-available/a305-elsehere` | 개발·운영·www 도메인별 프록시와 Certbot TLS 설정 |
| `/etc/nginx/sites-available/a305` | 기존 SSAFY 경로 호환과 해당 인증서 설정 |
| `~/apps/a305/bootstrap/` | 확인용 Compose와 응답 템플릿을 함께 두는 서버 실행 경로 |

저장소의 HTTP 예제를 이미 HTTPS가 적용된 서버 설정 위에 다시 복사하지 않습니다. 인증서와 개인 키, 실제 환경 파일은 서버에서 관리하고 저장소에 넣지 않습니다.

## 환경 분리와 자원 운영 기준

- Compose 명령에는 프로젝트 이름과 해당 환경 파일을 항상 함께 지정합니다. 개발 작업에서 `a305-prod`를 중지하거나 교체하지 않습니다.
- PostgreSQL을 서버에 배포할 때 환경별 컨테이너·계정·볼륨을 나눕니다. 운영 DB를 개발 테스트에 연결하거나 두 환경에서 같은 외부 볼륨을 공유하지 않습니다.
- AI 워커는 Spring과 같은 서버에서 별도 프로세스로 실행하고 Spring만 호출합니다. 환경별 연결 주소와 `AI_WORKER_TOKEN`을 구분하며, DB와 AI 워커 포트는 외부에 게시하지 않습니다. 실행 변수와 내부 인증은 [AI 워커 문서](../ai/worker/README.md)를 따릅니다.
- 확인용 서비스는 각각 CPU 0.25개·메모리 128MiB로 제한하고, 컨테이너 로그를 파일당 10MiB·최대 3개로 순환합니다. 실제 앱의 제한값은 부하 측정으로 정합니다.
- 서버 자원과 Nginx는 공유합니다. 개발 부하, 디스크 부족, 재부팅과 서버 장애는 운영에도 영향을 줍니다. 빌드는 가능하면 별도 Runner에서 수행합니다.
- SSH 22번 규칙과 공개키를 유지합니다. `/home`과 시스템 디렉터리 권한을 일괄 변경하지 않습니다.
- Docker 게시 포트는 UFW만으로 차단된다고 가정하지 않습니다. 외부 진입점 외 서비스는 루프백 또는 환경 내부 네트워크로 제한합니다. [Docker 방화벽 안내](https://docs.docker.com/engine/network/packet-filtering-firewalls/#docker-and-ufw)

## 실제 앱으로 교체하는 기준

### 앱 실행 구성

`infra/compose/compose.app.yaml`은 `database`(PostgreSQL 16), `ai-worker`, `backend`, `web`을 같은 환경의 내부 네트워크에서 실행합니다. 외부로 게시하는 포트는 웹 서버의 루프백 주소 하나입니다. API 요청은 웹 서버에서 `backend:8080`으로 전달하고 DB·AI 포트는 게시하지 않습니다. 프로젝트 이름으로 네트워크와 DB·NPC 음성 볼륨을 분리합니다.

NPC 음성은 `npc-audio-data` 볼륨을 백엔드의 `/app/data/npc-audio`에 연결해 보존합니다. 이미지 교체와 롤백에서도 같은 프로젝트·볼륨을 유지하며, 권한·저장 검증과 보관 정책은 [NPC 음성 저장 운영 문서](npc-audio-storage.md)를 따릅니다.

```text
호스트 Nginx dev.elsehere.kr /
  → 127.0.0.1:8081 (web)
      ├─ 정적 WebGL 파일
      └─ /api/ → backend:8080 → database:5432
                              → ai-worker:8020 (연동 주소)
```

기능 API와 게임의 서버 연동은 각 기능의 구현 상태를 따릅니다. 컨테이너 기동만으로 학습·대화 기능이 완성되었다고 판단하지 않습니다. 기본 AI 이미지는 원격 모델 호출과 STT에 필요한 실행 의존성을 포함합니다. Kokoro·로컬 판정 모델과 관련 선택 의존성은 포함하지 않으므로 TTS·로컬 판정 활성화는 모델 준비와 별도 검증이 필요합니다.

| 설정 | 역할 |
| --- | --- |
| `infra/docker/backend.Dockerfile` | 검사에 통과한 `backend/build/libs/app.jar` 실행. 서버에서 다시 빌드하지 않음 |
| `infra/docker/ai-worker.Dockerfile` | 워커와 공유 피드백 모듈 실행 |
| `infra/docker/web.Dockerfile` | WebGL 산출물과 내부 API 프록시 제공 |
| `infra/nginx/web.conf` | WebAssembly MIME·gzip 헤더, 백엔드 교체 후 DNS 재조회 |
| `infra/compose/deploy.*.env.example` | 환경별 DB·Google OAuth·AI·포트·이미지 변수 예제 |
| `infra/nginx/a305.dev-webgl.locations.conf` | 기존 HTTPS 서버에 수동으로 추가할 개발 WebGL 경로 예제 |

실제 `deploy.env`에는 DB 비밀번호, `GOOGLE_CLIENT_ID`·`GOOGLE_CLIENT_SECRET`, 내부 토큰, GMS·Groq 키와 검증한 이미지 참조가 필요합니다. 예제의 빈 값은 사용자가 서버에서 채우며 저장소에 넣지 않습니다. 공통 앱 Compose는 Google 두 값을 백엔드의 `environment`에 명시적으로 전달하며 값이 없거나 비어 있으면 실행 전에 실패합니다. `--env-file`로 치환할 값을 읽는 것과 컨테이너에 전달하는 것은 별개입니다. 운영 앱을 활성화할 때도 운영용 Google 값을 운영 `deploy.env`에 준비합니다. DB 스키마는 백엔드가 시작할 때 Flyway 마이그레이션이 만들고 바꾸며, 모든 환경에서 `JPA_DDL_AUTO=validate`로 엔티티와 맞는지 검사합니다. 새 운영 DB도 첫 시작 때 마이그레이션으로 만들어집니다. 앱을 이전 이미지로 되돌려도 적용된 마이그레이션은 되돌아가지 않습니다. 작성 기준은 [백엔드 README](../backend/README.md#스키마-관리)를 따릅니다.

개발 서비스의 초기 상한은 DB 0.5 CPU·512MiB, AI 0.5 CPU·512MiB, 백엔드 0.75 CPU·1GiB, 웹 0.25 CPU·128MiB입니다. 부하 측정 전의 시작값이며 Unity 빌드 자원과는 별도로 관리합니다. 백엔드 `/api/health`는 DB 상태를 포함한 `UP`/`DOWN`을 반환하고 세부 연결 정보는 노출하지 않습니다. 웹 `/version.json`은 WebGL 산출물의 커밋을 표시합니다.

### 전환 순서

1. 영역별 실행·검사·빌드 명령, 상태 확인 API, 필수 환경변수와 이미지 버전을 정합니다.
2. 앱용 Compose를 작성하고 개발·운영 설정 조합을 모두 검사합니다. DB·AI 실행 방식은 확정된 기술 결정에 맞춥니다.
3. 개발 확인용 서비스만 중지한 뒤 개발 포트를 앱에 연결합니다. 개발에서 Nginx 경로와 앱 통합 동작을 검증합니다.
4. 운영 후보의 코드·이미지·설정을 고정하고 검증 결과와 복구 절차를 릴리스 MR에 기록합니다.
5. 운영 확인용 서비스를 앱으로 교체하고 HTTPS 응답과 대표 요청을 확인합니다. 변경된 앱과 호환되는 이전 이미지·설정을 보관합니다.

개발 WebGL은 `dev.elsehere.kr/`에서 제공합니다. 기존 SSAFY `/dev/`의 호환 경로를 수정할 때만 `a305.dev-webgl.locations.conf`를 참고하며, 하위 파일 요청에서 `/dev`가 제거되는지 확인합니다. location 예제로 Certbot이 수정한 전체 서버 설정을 덮어쓰지 않습니다. 운영 도메인의 루트는 실제 운영 앱 전환 전까지 확인용 서비스이며, 해당 서비스의 `healthy`를 앱 배포 성공으로 사용하지 않습니다.

`infra:app-runtime` 검사는 격리된 일회용 Compose에서 실제 백엔드·PostgreSQL·AI 워커와 웹 설정을 확인합니다. 외부 모델 호출은 하지 않으며, 작은 정적 fixture로 확인한 HTTP 압축·MIME 검증을 Unity 게임 실행 검증과 구분합니다.

일상적인 명령과 장애 점검은 [서버 운영 방법](server-operations.md)을 따릅니다. 브랜치·리뷰·릴리스는 [협업 규칙](development.md), 개발 자동화는 [dev 자동 배포](dev-auto-deployment.md)를 따릅니다. main 운영 배포·복구 절차는 개발 자동 배포와 구분해 구성·검증합니다.
