# 검사 파이프라인과 Runner 연결

검사 설정은 `.gitlab-ci.yml`과 `.gitlab/ci/`에서 관리합니다. 일반 Linux 검사는 `a305-linux-ci` 태그로 EC2와 노트북의 Docker Runner가 나눠 받습니다. 경량 검사는 EC2의 `a305-linux-light` Runner에서 실행하며, 배포 계획·전달은 `a305-ec2` 태그도 요구해 EC2로 제한합니다. 설정 파일 작성, Runner 등록, 실제 작업 성공은 각각 확인합니다.

## 자동 검사 범위

백엔드·Unity 포맷·AI 작업은 `needs: ["ci:yaml"]`로 YAML 검사 성공 후 실행할 수 있습니다. MR의 Unity 실행 테스트는 YAML과 Unity 포맷 검사 성공 후 시작합니다. 각 작업이 별도 환경을 사용하므로 Runner의 동시 실행 한도 안에서 인프라 검사와 겹쳐 실행합니다. 파이프라인 성공에는 선택된 모든 필수 작업의 통과가 필요합니다.

| 작업 | 실행 조건 | 검증·산출물 |
| --- | --- | --- |
| `ci:yaml` | 생성된 모든 파이프라인 | CI YAML 문법·형식 검사 |
| `backend:check-and-build` | 백엔드, API 명세, ERD, 앱 실행 설정 또는 공통 CI·작성 지침 변경 | Java 21·Gradle Wrapper의 `check bootJar`, Testcontainers PostgreSQL 테스트, JUnit 결과·`app.jar`·커밋 식별자 |
| `unity:format` | 프론트, API 명세, Unity CI·검사 스크립트·규약 또는 공통 작성 지침 변경 | 고정된 .NET SDK와 CSharpier로 직접 작성한 C# 포맷 검사 |
| `unity:editor-tests` | 같은 프로젝트의 Unity 관련 MR | Windows Runner에서 컴파일·EditMode·PlayMode 필수 검사, NUnit XML·로그. WebGL 빌드·서버 배포 없음 |
| `unity:external-mr` | 다른 프로젝트에서 보낸 Unity 관련 MR | 노트북에서 외부 코드를 실행하지 않고 필수 검사 실패로 병합 차단. 검토한 변경을 프로젝트 작업 브랜치로 옮겨 검사 |
| `unity:editor-and-webgl` | Unity 검사를 선택한 보호된 dev 배포 자식, 모든 운영 배포 자식 또는 보호된 ref의 웹 실행에서 `RUN_UNITY_BUILD=true` | Windows 전용 Runner의 EditMode·PlayMode 테스트, 필요한 경우 WebGL 빌드, NUnit XML·로그·커밋 식별자가 포함된 정적 산출물 |
| `ai:check-and-test` | AI, API 명세 또는 공통 CI·작성 지침 변경 | Python 3.12·Ruff 검사, 피드백·워커 테스트, 구문 컴파일, JUnit·소스 아카이브·커밋 식별자 |
| `ai:panel-tests` | AI 피드백, API 명세 또는 공통 CI·작성 지침 변경 | Node.js 22의 피드백 패널 테스트 |
| `infra:compose-and-nginx` | 확인용 Compose가 존재하고 관련 설정·공통 CI 변경 | 개발·운영 Compose 해석, 호스트 프록시와 두 환경 응답 템플릿의 Nginx 문법 검사 |
| `infra:deployment-plan-tests` | 인프라 스크립트 또는 공통 CI 변경 | Ruff·임시 Git 저장소 기반 누적 배포 범위 판정 테스트 |
| `infra:app-runtime` | 앱용 Dockerfile·Compose·환경 예제·웹 설정·실행 점검 코드, API 명세 또는 공통 CI 변경 | 검사된 JAR를 받아 실제 앱 이미지를 빌드하고, 일회용 Compose에서 DB·백엔드 health·AI 인증·TTS 모델 적재·웹 gzip/MIME 검증 |
| `infra:observability` | 모니터링·Nginx·관측 검증 스크립트 또는 공통 CI 변경 | 요청 ID·로그의 민감 정보 제외, Prometheus·Grafana 구성, 경보 발동·복구와 가짜 수신기 전송 검사 |
| `prod:plan`·`prod:child`·`prod:deliver` | 보호된 `main` Push에서 자동 시작. 수동 재실행은 웹 실행과 `RUN_PROD_DEPLOY=true` 및 검증 변수 사용 | 직렬화된 자식에서 모든 영역 검사 후 누적 이미지 빌드·운영 자동 전달·상태 점검과 버전 보관. 설치·DB·병합 전 통합 검증 조건은 [운영 배포 문서](prod-deployment.md) 참조 |

일반 문서 변경은 빠른 YAML 검사만 수행합니다. API 명세는 일반 문서와 구분하여 백엔드·프론트·AI 검사를 함께 실행합니다. 작업 브랜치의 자동 검사는 Draft를 포함한 MR 파이프라인으로 통일합니다. MR을 만들기 전의 작업 브랜치 Push에는 파이프라인을 생성하지 않으며, 필요하면 웹에서 수동 실행합니다. 같은 MR에 새 커밋이 올라오면 이전 커밋의 취소 가능한 검사만 자동 취소하고 최신 커밋에 필요한 검사는 모두 수행합니다. `dev`·`main`의 커밋별 검사와 배포는 보존합니다. 웹에서 수동 실행한 파이프라인은 사용 가능한 백엔드·Unity 포맷·AI·관측 검사를 모두 수행하고, 확인용 Compose가 있으면 Compose·Nginx 검사도 수행합니다.

자동 취소는 `.gitlab-ci.yml`의 MR 전용 `workflow:rules:auto_cancel`에서 `on_new_commit: interruptible`로 설정합니다. 소스 브랜치가 `dev`·`main`인 MR, Push·웹 수동 실행·배포 자식 파이프라인은 기본값 `none`을 유지합니다. 배포 연결과 실제 전달 작업의 `interruptible: false`도 유지하므로 진행 중인 배포를 중단하지 않습니다. 최신 MR 검사의 실패·미실행은 계속 병합을 차단하며 다른 MR의 검사는 이 정책으로 취소하지 않습니다.

이 정책은 변경된 CI 설정을 포함해 생성한 파이프라인부터 적용됩니다. 기존 작업 브랜치는 최신 `dev`를 반영해야 하며 이미 생성된 작업의 설정은 소급 변경하지 않습니다. GitLab 프로젝트의 **Auto-cancel redundant pipelines**도 활성화되어 있어야 합니다.

자동 취소의 기준은 같은 MR ref의 새 커밋입니다. 같은 커밋의 수동 재실행을 이전 커밋 검사 정리로 취급하지 않습니다. 설정과 취소 가능 작업의 의미는 [GitLab 자동 취소 설정](https://docs.gitlab.com/ci/yaml/#workflowauto_cancelon_new_commit), [interruptible 기준](https://docs.gitlab.com/ci/yaml/#interruptible)을 따릅니다.

자동 취소는 이전 검사의 실행·대기를 중단하며 이미 완료된 결과와 취소 이력은 남깁니다. 이전 커밋에서 검사가 성공했더라도 최신 커밋의 필수 검사 통과를 대신하지 않습니다.

`dev → main` 릴리스 MR은 같은 소스 SHA의 dev 파이프라인 결과를 사용하며 별도 MR 파이프라인을 생성하지 않습니다. 일반 기능 MR과 `main → dev` 동기화 MR의 자동 검사는 유지합니다. 릴리스 검토 시 dev의 해당 SHA 검사 결과와 별도 통합 검증을 확인하고 main 병합 후 운영 전체 검사도 유지합니다.

MR의 `rules:changes:compare_to`에 실제 대상 브랜치를 명시하여 대상과의 최종 차이로 작업을 선택합니다. 같은 MR에서 문서만 추가로 고쳐도 백엔드 파일이 대상과 다르면 백엔드 검사가 다시 실행되고, 백엔드 변경을 완전히 되돌리면 선택 대상에서 빠집니다. 각 Push의 최종 커밋을 검사하며, 한 번의 Push에 포함된 중간 커밋마다 별도 파이프라인을 만들지는 않습니다. [GitLab 비교 대상 지정](https://docs.gitlab.com/ci/yaml/#ruleschangescompare_to)

### 변경 영역별 작업 선택 기준

| 대상 브랜치와 다른 경로 | 선택할 작업 |
| --- | --- |
| 일반 문서만 변경 | `ci:yaml` |
| `backend/` | `ci:yaml`, `backend:check-and-build` |
| `frontend/` | `ci:yaml`, `unity:format`, `unity:editor-tests` |
| `ai/worker/` 또는 `ai/tools/` | `ci:yaml`, `ai:check-and-test` |
| `ai/feedback/` | `ci:yaml`, `ai:check-and-test`, `ai:panel-tests` |
| `docs/api/` | `ci:yaml`, `backend:check-and-build`, `unity:format`, `unity:editor-tests`, 두 AI 작업, `infra:app-runtime` |
| 확인용 `infra/compose/` | `ci:yaml`, `infra:compose-and-nginx` |
| 앱용 Dockerfile·Compose·환경 예제·웹 응답 설정 | `ci:yaml`, `backend:check-and-build`, `infra:app-runtime`, 해당 경로의 인프라 문법 검사 |
| `infra/scripts/` | `ci:yaml`, `infra:deployment-plan-tests`. 앱 실행 점검 코드와 공통 이미지 빌더 `docker_build.py`는 백엔드 빌드·실행 검사도 선택 |
| `infra/nginx/` | `ci:yaml`, `infra:observability`, 확인용 Compose가 있으면 `infra:compose-and-nginx` |
| `infra/monitoring/`, `check_observability.py`, `check_alerting.py` | `ci:yaml`, `infra:observability` |
| `.gitlab-ci.yml` 또는 Unity 이외의 `.gitlab/ci/` 파일 | Unity를 제외한 적용 가능한 작업 |
| `.gitlab/ci/unity.yml` | Unity를 포함한 적용 가능한 모든 작업 |
| 웹에서 수동 실행 | 적용 가능한 모든 작업 |

`infra:compose-and-nginx`만 확인용 Compose의 존재를 전제로 합니다. `infra:observability`는 해당 파일 없이 독립 실행합니다. ERD 변경은 백엔드 검사, Unity 규약·검사 스크립트 변경은 Unity 검사, 공통 작성 지침 변경은 백엔드·Unity·AI 검사를 선택합니다. 여러 영역을 함께 바꾸면 해당 작업을 모두 실행합니다. AI 공통 설정·의존성 변경도 Python 검사를 실행하며 로컬 명령은 [AI 검사 문서](../ai/README.md)를 따릅니다.

Unity 검사 스크립트 `run-unity-ci.ps1`·`prepare-unity-test-inputs.ps1`와 `infra/tests/tripo/` 변경도 MR의 포맷·실행 테스트를 선택합니다. 실행 테스트는 Draft를 포함한 MR의 각 Push에서 대상 브랜치와 남은 차이를 기준으로 다시 실행합니다. 외부 프로젝트 MR에는 같은 변경 경로에서 `unity:editor-tests` 대신 실패하는 `unity:external-mr`을 선택하므로 필수 검사를 생략한 성공 결과로 병합하지 않습니다.

MR의 Unity 작업은 공통 workflow나 다른 영역의 CI 파일 변경만으로 선택하지 않습니다. 자동 취소 정책을 담은 `.gitlab-ci.yml`과 백엔드·AI·인프라 CI 파일은 YAML·설정 회귀 검사와 각 영역의 작업으로 검증합니다. Unity CI 파일인 `.gitlab/ci/unity.yml`을 바꾸거나 같은 MR에 Unity 관련 변경이 함께 있으면 포맷·실행 테스트를 모두 선택합니다. 이 기준은 MR의 작업 선택 범위이며, dev 배포 자식의 누적 검사 판정과 운영 배포의 전체 검사는 별도 기준을 유지합니다.

작업 선택을 검증할 때는 CI 설정이 있는 버전을 대상으로 별도 Draft MR을 만듭니다. 대상과의 차이가 위 표의 한 영역에만 남도록 변경한 뒤, 파이프라인의 작업 목록과 결과를 확인합니다. 다음 사례로 바꿀 때 이전 검증용 변경을 복원합니다. 검증용 변경은 실제 CI MR이나 `dev`에 병합하지 않으며, 파이프라인·커밋별 결과는 검증 MR과 Jira에 기록합니다. 열린 MR에 Push한 커밋에서 MR 파이프라인만 생성되는지도 확인합니다. `ci:yaml` 로그의 실행 원인·커밋·대상 브랜치·MR 비교 기준을 작업 목록과 함께 확인할 수 있습니다.

Unity 관련 MR은 **포맷 검사와 실제 EditMode·PlayMode 실행 테스트를 모두 통과해야 합니다.** `unity:editor-tests`는 실패를 허용하지 않으며, 실패·취소·대기 중인 최신 파이프라인은 [병합 검사 설정](gitlab-setup.md#병합-검사-설정)에 따라 병합을 차단합니다. dev 병합 후에는 병합된 커밋의 누적 통합 검사와 필요한 WebGL 빌드·배포를 별도로 수행합니다. 보호된 후보의 수동 연결 시험에는 `unity:editor-and-webgl`을 사용하며 실행 조건과 노트북 유지 방법은 [Windows Unity Runner](unity/windows-runner.md)를 따릅니다. Runner Online 상태만으로 테스트·빌드 성공으로 판단하지 않습니다. AI 단위 테스트는 모의 공급자로 실행하며 실제 모델 호출·GPU·음성 모델 기동은 검증 범위에 포함하지 않습니다.

일반 MR 파이프라인은 검사를 수행하며 서버 앱을 교체하지 않습니다. dev 자동 배포의 보호 조건, 자식 파이프라인, 서버 설치와 연결 시험은 [dev 자동 배포 운영](dev-auto-deployment.md)을 따릅니다. 운영 통합 테스트는 별도 릴리스 검증으로 관리합니다. 마지막 성공 배포 이후 누적 변경 판정은 배포 구현의 책임이며 CI의 `rules:changes`만으로 해결했다고 간주하지 않습니다.

`infra:app-runtime`은 Runner의 rootless Docker 안에 `a305-check-*` 프로젝트를 만들고 종료 시 그 프로젝트의 컨테이너·네트워크·DB 볼륨만 정리합니다. 테스트용 네트워크의 외부 통신을 차단하고 공급자 키는 가짜 값을 사용하므로 실제 모델을 호출하지 않습니다. 웹 응답에는 작은 HTTP fixture를 사용하며 실제 Unity 게임 화면이나 외부 HTTPS 경로를 검증한 것으로 표시하지 않습니다. 실행 구성은 [서버 구성](deployment.md), Unity 실행 환경은 [Windows Unity Runner](unity/windows-runner.md)를 따릅니다.

## 동일 서버의 실행 구조와 제한

```text
GitLab → EC2 Runner 관리 컨테이너 (최대 3개)
          ├─ a305-linux-light + a305-ec2 (최대 1개) → 경량 검사·배포 계획
          └─ a305-linux-ci + a305-ec2 (최대 2개) → 일반 검사·이미지 전달
       → 노트북 Linux Runner (최대 1개, a305-linux-ci) → 일반 검사

호스트 Docker → a305-dev / a305-prod (서비스 실행)
```

Runner 관리 컨테이너만 호스트 Docker 소켓을 사용합니다. 일반 검사 컨테이너에는 소켓이나 앱의 환경 파일·인증서를 마운트하지 않습니다. Testcontainers는 검사 작업 전용 rootless Docker 서비스에 연결합니다. 프로젝트 Runner는 서버의 Docker를 제어하므로 팀 저장소의 신뢰하는 작업에만 사용합니다. [Runner 컨테이너 운영](https://docs.gitlab.com/runner/install/docker/)

EC2의 동시 실행 상한은 세 개이며, 경량 검사 한 자리를 빌드 대기열과 분리합니다. 노트북 Linux Runner는 별도로 한 개만 실행합니다. Windows Unity Runner의 동시 실행 수와는 별도 제한입니다.

| 태그 | 동시 작업 | 자원 상한 | 작업 |
| --- | --- | --- | --- |
| EC2 `a305-linux-light` | 1개 | CPU 1개·메모리 1GiB. Docker 서비스 실행 불가 | YAML, Unity 포맷, AI 패널, 인프라 스크립트 검사, 외부 Unity MR 차단. 배포 계획은 `a305-ec2`도 요구 |
| EC2 `a305-linux-ci` | 2개 | 작업당 CPU 2개·메모리 3GiB. Docker 서비스는 CPU 1개·메모리 2GiB | 일반 검사. 이미지 전달은 `a305-ec2`도 요구 |
| 노트북 `a305-linux-ci` | 1개 | 작업당 CPU 2개·메모리 3GiB. Docker 서비스는 CPU 1개·메모리 2GiB | 백엔드·AI 검사, Compose·Nginx·관측 검사, 앱 실행 검사 |

동시 실행 수는 작업을 받을 수 있는 상한이며 CPU 4개·메모리 16GiB 서버에서 모든 작업의 최대 자원을 보장하는 값은 아닙니다. 앱·모니터링과 자원을 공유하므로 CPU 사용률·가용 메모리·앱 응답을 함께 확인합니다. 빌드 부하가 크면 일반 Runner의 `limit`를 먼저 낮춰 경량 검사 자리를 유지합니다. Runner 자체의 컨테이너 제한과 작업·서비스 제한은 별도로 적용합니다. 변경 전에 생성된 작업은 기존 태그를 유지하므로 이전 경량 작업도 일반 Runner에서 실행될 수 있습니다.

백엔드는 Gradle 의존성·Wrapper 캐시와 `--build-cache`의 작업 결과 캐시를 함께 사용합니다. 입력이 달라진 작업은 다시 실행하며 캐시가 없어도 검사·빌드를 수행합니다. 캐시 복원·저장 시간도 작업 실행 시간에 포함해 확인합니다.

제한값과 허용 서비스는 EC2의 `infra/runner/config.template.toml`, 노트북의 `infra/runner/laptop.config.template.toml`을 따릅니다. 두 파일에는 토큰이 없으며 등록 완료 파일을 대체하는 완성본이 아닙니다. 등록된 항목의 인증 정보는 보존하고 제한값을 해당 항목에 병합합니다. [Runner 고급 설정](https://docs.gitlab.com/runner/configuration/advanced-configuration/)

### Docker 이미지와 AI 모델 계층 캐시

앱 실행 검사와 dev·prod 이미지 빌드는 `infra/scripts/docker_build.py`를 사용합니다. CI의 `A305_DOCKER_CACHE_DIR=/cache/a305-buildkit` 설정으로 BuildKit의 로컬 계층 캐시를 불러오고 내보냅니다. AI 이미지에 포함된 Kokoro 모델·음성 파일과 Python 의존성도 변경되지 않은 계층으로 재사용합니다. 모델 URL·SHA256, 의존성 또는 소스가 달라지면 BuildKit이 해당 입력과 그 뒤의 계층을 다시 빌드합니다. 모델 다운로드의 SHA256 검증은 Dockerfile에 유지합니다.

`/cache`는 Runner가 동시 실행 번호별로 관리하는 지속 볼륨입니다. 일반·경량 Runner와 보호 여부가 다른 작업의 볼륨은 분리하며, 같은 일반 Runner 슬롯의 다음 작업이 이전 캐시를 재사용합니다. 프로젝트의 보호 브랜치 캐시 분리 설정을 유지합니다. 캐시가 없거나 로컬 실행에서 `A305_DOCKER_CACHE_DIR`를 설정하지 않으면 일반 빌드를 수행합니다. [Docker executor의 지속 저장소](https://docs.gitlab.com/runner/executors/docker/#persistent-storage)

Docker 데몬·테스트 컨테이너·네트워크·DB 볼륨은 작업마다 새로 준비합니다. 데몬의 데이터 디렉터리나 호스트 소켓을 캐시로 공유하지 않습니다. 빌드 성공 후 완성한 캐시만 교체하고 이전 캐시 디렉터리는 정리하므로, 실패한 빌드가 이전 캐시를 덮어쓰거나 이전 이미지 계층이 계속 누적되지 않습니다. 캐시는 `backend`·`ai`·`web`별로 분리하고 GitLab 산출물로 업로드하지 않습니다. [BuildKit 로컬 캐시](https://docs.docker.com/build/cache/backends/local/)

Docker 27 이후 기본 빌드 드라이버의 캐시 내보내기에는 containerd 이미지 저장소가 필요합니다. CI에 고정한 Docker 환경의 `docker info`에서 `driver-type: io.containerd.snapshotter.v1`을 확인합니다. 캐시 사용 여부는 빌드 로그의 로컬 캐시 가져오기와 `CACHED` 계층으로 확인합니다. 새 Docker 데몬에서 같은 입력을 다시 빌드해 모델·의존성 계층을 재사용하고, 소스를 바꾸면 필요한 계층이 다시 생성되는지도 확인합니다. 캐시가 있어도 테스트·커밋 식별자·배포 전 검증은 계속 수행합니다. [GitLab Docker 계층 캐시 조건](https://docs.gitlab.com/ci/docker/docker_layer_caching/)

## GitLab에서 프로젝트 Runner 생성

프로젝트 **Settings → CI/CD → Runners → Create project runner**에서 생성합니다.

- 플랫폼: Linux
- 태그: EC2 일반 Runner는 `a305-linux-ci,a305-ec2,a305-linux`, EC2 경량 Runner는 `a305-linux-light,a305-ec2`, 노트북 Linux Runner는 `a305-linux-ci`만 지정
- Run untagged jobs: 해제
- 설명: 일반 Runner는 `a305-linux-ci`, 경량 Runner는 `a305-linux-light-ci`
- Lock to current projects: 활성화
- 처음에는 Paused 상태로 생성하고 자원 제한 반영 후 활성화합니다.

등록 화면의 `glrt-` 인증 토큰은 서버의 등록 프롬프트에만 입력합니다. 저장소·채팅·터미널 출력 공유에 포함하지 않습니다. MR 검사에 사용하므로 protected 브랜치 전용으로 제한하는 옵션은 선택하지 않습니다. 배포용 권한은 이 검사 Runner에 추가하지 않습니다.

### 노트북 Linux Runner

Docker Desktop의 WSL2 Linux 엔진에서 Runner 관리 컨테이너를 실행합니다. Windows Unity Runner와 등록·설정 볼륨을 분리하고 `laptop.config.template.toml`의 `concurrent=1`, `limit=1`과 자원 제한을 적용합니다. 관리 컨테이너는 CPU 0.25개·메모리 256MiB, 로그는 파일당 10MiB·3개로 제한합니다.

관리 컨테이너의 Docker 소켓 연결은 노트북 Docker의 컨테이너·볼륨을 제어할 권한을 줍니다. 일반 검사 컨테이너에는 이 소켓이나 Windows 사용자 폴더를 마운트하지 않으며, PostgreSQL·이미지 빌드는 작업별 rootless Docker 서비스에서 실행합니다. 노트북의 Docker에 중요한 개인 서비스를 함께 운영한다면 별도 Linux VM 등 독립된 Docker 엔진을 준비합니다.

GitLab에서 설명 `a305-laptop-linux-ci`, 태그 `a305-linux-ci`, Run untagged jobs 해제, 프로젝트 잠금 활성화, 최초 Paused로 생성합니다. 다음과 같이 전용 설정 볼륨을 사용하는 공식 `gitlab/gitlab-runner:alpine-v18.11.4` 컨테이너를 준비하고 등록 프롬프트로 인증 토큰을 입력합니다.

```powershell
docker desktop start --detach
docker volume create a305-laptop-runner-config
docker run -d --name a305-laptop-linux-runner --restart unless-stopped `
  --cpus 0.25 --memory 256m `
  --log-driver json-file --log-opt max-size=10m --log-opt max-file=3 `
  -v a305-laptop-runner-config:/etc/gitlab-runner `
  -v /var/run/docker.sock:/var/run/docker.sock `
  gitlab/gitlab-runner:alpine-v18.11.4
docker exec -it a305-laptop-linux-runner gitlab-runner register
```

등록 URL은 `https://lab.ssafy.com`, executor는 `docker`, 기본 이미지는 `alpine:3.22`입니다. 생성된 인증 정보는 유지하고 노트북 템플릿의 한 작업 제한·Docker 설정을 적용한 뒤 `verify`와 실제 일반 검사 성공을 확인하고 Paused를 해제합니다. 토큰이나 설정 전체를 로그로 출력하지 않습니다.

노트북은 `DOCKERD_ROOTLESS_ROOTLESSKIT_NET=slirp4netns`로 작업별 rootless Docker의 네트워크를 지정합니다. 기본 `vpnkit`의 `192.168.65.0/24`는 Docker Desktop 내부 DNS 주소와 겹쳐 중첩 빌드의 이름 해석이 실패할 수 있습니다. `slirp4netns`의 별도 네트워크를 사용하고 실제 중첩 컨테이너에서 DNS 조회·패키지 설치·이미지 빌드를 확인합니다. EC2 설정은 바꾸지 않습니다. [Docker rootless 네트워크 설정](https://docs.docker.com/engine/security/rootless/troubleshoot/)

EC2 일반 Runner에는 기존 `a305-linux` 태그를 남겨 이전 파이프라인을 계속 처리하게 합니다. 노트북에는 이 이전 태그와 `a305-ec2`를 부여하지 않으므로, 변경 전의 배포도 노트북으로 넘어가지 않습니다. 새 설정은 병합 후 생성한 파이프라인과 해당 설정을 반영한 작업 브랜치부터 적용됩니다.

공통 태그의 작업은 여유가 있는 Runner가 나눠 받으며 정확한 반반 배분이나 EC2 우선순위를 보장하지 않습니다. 노트북이 꺼졌거나 Docker Desktop이 중지되면 새 일반 검사는 EC2에서 처리할 수 있습니다. 이미 노트북에서 실행 중이던 작업은 자동 이전되지 않으므로 중단 시 상태를 확인하고 재시도합니다.

설정 회귀 검사는 `python -m unittest discover -s infra/runner/tests -v`로 실행하며 `ci:yaml`에도 연결합니다. 일반 검사 태그, 배포 계획·전달의 EC2 제한, 노트북의 한 작업 제한과 Docker 소켓 비공유를 확인합니다.

## Ubuntu 서버에 Runner 설치·등록

Runner 이미지는 확인한 GitLab의 18.11 계열과 맞춘 `gitlab/gitlab-runner:alpine-v18.11.4`를 사용합니다. 버전 변경 시 실제 GitLab 버전과 이미지 제공 여부를 다시 확인합니다.

```bash
sudo install -d -m 700 /srv/gitlab-runner/config
sudo docker pull gitlab/gitlab-runner:alpine-v18.11.4
sudo docker run -d --name gitlab-runner --restart unless-stopped \
  --cpus 0.25 --memory 256m \
  --log-driver json-file --log-opt max-size=10m --log-opt max-file=3 \
  -v /srv/gitlab-runner/config:/etc/gitlab-runner \
  -v /var/run/docker.sock:/var/run/docker.sock \
  gitlab/gitlab-runner:alpine-v18.11.4
sudo docker exec -it gitlab-runner gitlab-runner register
```

기존 `gitlab-runner` 컨테이너나 등록이 있는 서버에서는 먼저 확인하고 중복 생성하지 않습니다. 두 프로젝트 Runner를 같은 관리 컨테이너에 각각 등록합니다. 등록 프롬프트에는 아래 값을 입력합니다.

| 항목 | 값 |
| --- | --- |
| GitLab instance URL | `https://lab.ssafy.com` |
| Authentication token | 생성 화면의 실제 `glrt-` 토큰 |
| Runner name | 일반 `a305-linux-ci`, 경량 `a305-linux-light-ci` |
| Executor | `docker` |
| Default Docker image | `alpine:3.22` |

등록 후 `/srv/gitlab-runner/config/config.toml`을 서버 안에 백업하고 편집합니다. 토큰을 공유하지 않도록 파일 전체를 출력하지 않습니다.

```bash
sudo cp -a /srv/gitlab-runner/config/config.toml /srv/gitlab-runner/config/config.toml.before-limits
sudo nano /srv/gitlab-runner/config/config.toml
```

최상위 `concurrent`는 `3`, 일반 Runner의 `limit`는 `2`, 경량 Runner의 `limit`는 `1`로 맞추고 각각의 id·token·URL·이름·토큰 시각을 유지합니다. 템플릿의 `environment`와 `[runners.docker]` 설정을 해당 항목에 적용합니다. 등록 명령이 만든 두 항목에 제한값만 반영하며, 같은 인증 정보로 항목을 복제하거나 템플릿으로 파일 전체를 덮어쓰지 않습니다. 기존 Runner의 동시 실행 수만 조정할 때는 설정 자동 다시 읽기를 사용하여 진행 중인 작업을 재시작하지 않습니다.

프로젝트 전용 Docker Runner의 `pull_policy`는 `["always", "if-not-present"]`를 사용합니다. 새 이미지를 먼저 확인하고, 레지스트리 다운로드가 실패하면 로컬에 저장된 이미지로 작업을 시작합니다. Docker Hub의 `429 Too Many Requests`처럼 다운로드가 제한된 경우에도 기존 이미지가 있으면 코드 검사와 빌드를 계속할 수 있습니다. 로컬에 없는 이미지는 다운로드에 성공해야 사용할 수 있으며, 레지스트리가 정상화되면 다시 새 이미지를 확인합니다. [GitLab의 다중 이미지 가져오기 정책](https://docs.gitlab.com/runner/executors/docker/#set-multiple-pull-policies)

```bash
sudo docker restart gitlab-runner
sudo docker exec gitlab-runner gitlab-runner verify
sudo docker logs --tail=30 gitlab-runner
```

로그를 외부에 공유할 때는 인증 정보가 없는 부분만 전달합니다. 등록 상태와 제한 설정을 확인한 뒤 GitLab에서 Paused를 해제합니다. 별도의 UFW 수신 포트는 필요하지 않으며 Runner가 GitLab으로 작업을 요청합니다.

## Rootless Docker와 Testcontainers 연결

백엔드 검사 컨테이너는 `DOCKER_HOST=tcp://docker:2375`로 작업 전용 Docker 서비스에 연결합니다. Testcontainers의 정리 컨테이너인 Ryuk는 그 서비스 내부의 Unix 소켓도 사용합니다. `docker:29.1.3-dind-rootless`는 UID 1000으로 실행하므로 `.gitlab/ci/backend.yml`에 `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/run/user/1000/docker.sock`을 지정합니다. 이 경로는 테스트 전용 서비스 내부의 소켓이며 호스트 Docker 소켓이 아닙니다. [Docker rootless 실행 경로](https://docs.docker.com/engine/security/rootless/tips/), [Testcontainers 소켓 설정](https://java.testcontainers.org/features/configuration/#customizing-docker-host-detection)

작업마다 새로 만드는 검사용 Docker는 호스트의 이미지 캐시를 공유하지 않습니다. `.docker-job`의 서비스 시작 명령에 `--registry-mirror=https://mirror.gcr.io`를 지정해 Google이 관리하는 공개 Docker Hub 이미지 캐시를 먼저 확인합니다. 캐시에 없는 이미지는 Docker Hub에서 받습니다. 이 설정은 검사용 서비스에만 적용하고, 호스트 Docker 소켓·운영 앱 파일·인증 정보는 검사 컨테이너에 연결하지 않습니다. [Google 공개 이미지 캐시](https://docs.cloud.google.com/artifact-registry/docs/pull-cached-dockerhub-images)

### 테스트용 Docker의 임시 포트 범위

Docker 29의 rootless 환경에서는 서비스 바깥쪽 네트워크의 임시 포트와 안쪽 Docker가 공개하는 동적 포트가 겹치면 `RootlessKit PortManager.AddPort(): ... bind: address already in use` 오류가 발생할 수 있습니다. `.gitlab-ci.yml`의 `.docker-job`은 테스트용 Docker 서비스가 시작될 때 바깥쪽 임시 포트 범위를 `15000 25000`으로 설정합니다. [Docker 포트 충돌 이슈](https://github.com/moby/moby/issues/53153)

서비스 시작 명령은 UID 0으로 해당 컨테이너의 `sysctl` 값을 설정한 다음 `su`로 `rootless` 사용자에게 전환해 기본 `dockerd-entrypoint.sh`를 실행합니다. Docker 데몬은 기존 rootless 방식으로 동작하며 검사 컨테이너와 서비스의 자원 제한도 유지합니다. 서비스의 사용자·시작 명령은 GitLab의 [services:docker](https://docs.gitlab.com/ci/yaml/#servicesdocker)와 [services:entrypoint](https://docs.gitlab.com/ci/yaml/#servicesentrypoint) 설정을 사용합니다.

매 작업에서 새로 만드는 서비스에 자동 적용하므로 서버에서 수동 `docker exec` 명령을 반복할 필요가 없습니다. 호스트의 `/etc/sysctl.conf`나 Runner 등록 파일을 수정하지 않습니다. 이 설정이 포함된 커밋의 새 파이프라인으로 검증합니다. 이전 커밋 작업의 Retry는 이전 시작 명령을 다시 사용합니다.

### Ubuntu 24.04의 AppArmor 프로필

Ubuntu 24.04에서는 비특권 사용자 네임스페이스 제한으로 rootless Docker가 시작되지 않을 수 있습니다. 서비스에 `Privileged=true`, `AppArmor=unconfined`가 적용돼도 커널 기록에 `rootlesskit`의 `unprivileged_userns` 프로필에서 `sys_admin` 권한 차단이 나타날 수 있습니다. 이 경우 실행 경로 `/usr/local/bin/rootlesskit`에 `userns` 허용 프로필을 추가합니다. 다른 경로의 바이너리를 사용하면 실제 실행 경로에 맞춰 프로필을 수정합니다. [RootlessKit AppArmor 설정](https://rootlesscontaine.rs/getting-started/common/apparmor/)

```bash
if sudo test -f /etc/apparmor.d/usr.local.bin.rootlesskit; then
  sudo cp -a /etc/apparmor.d/usr.local.bin.rootlesskit "$HOME/rootlesskit.apparmor.backup"
fi

sudo tee /etc/apparmor.d/usr.local.bin.rootlesskit >/dev/null <<'EOF'
abi <abi/4.0>,
include <tunables/global>

/usr/local/bin/rootlesskit flags=(unconfined) {
  userns,
}
EOF

sudo apparmor_parser -r /etc/apparmor.d/usr.local.bin.rootlesskit
```

해당 프로필을 적용한 뒤 새 백엔드 작업에서 Docker 서비스가 정상 기동하는지 확인합니다. Docker 서비스가 준비됐는데 테스트가 실패하면 작업의 **Artifacts → Browse → backend/build/test-results/test/** 또는 파이프라인의 **Tests → View details**에서 실패한 컨테이너와 예외 원인을 확인합니다. Ryuk 시작 실패와 PostgreSQL 시작 실패는 구분해서 진단합니다.

## 자동 실행 검증과 병합 조건

1. CI 설정이 포함된 작업 브랜치의 MR에서 파이프라인이 생성되는지 확인합니다. dev 병합 전에도 검사할 수 있습니다.
2. `a305-linux-ci` 작업이 EC2 또는 노트북에서 Pending에서 Running으로 바뀌는지 확인합니다. 계획·전달 작업은 EC2에만 배정돼야 합니다.
3. 백엔드 DB 테스트·품질 검사·빌드가 성공하고 JUnit 보고서·JAR·커밋 식별자가 보관되는지 확인합니다.
4. Unity 포맷·AI 검사와 적용 가능한 인프라 검사를 확인합니다. 인프라 설정 MR과 통합한 버전에서도 검사합니다.
5. 일반 문서·백엔드·프론트·AI·API 명세 변경에 따라 작업 선택이 달라지고 열린 MR의 Push가 중복 파이프라인을 만들지 않는지 확인합니다.
6. 실제 필수 검사가 동작하는 것을 확인하고 아래 병합 조건을 적용합니다. 작성만 끝난 설정이나 Pending 상태를 성공으로 기록하지 않습니다.

### CI 성공을 요구하는 병합 조건

검사 성공을 요구하는 프로젝트 설정은 [GitLab 운영 정책의 병합 검사 설정](gitlab-setup.md#병합-검사-설정)을 따릅니다.

검사 작업에는 `allow_failure`를 지정하지 않고, CI에서 자동 포맷이나 수정 커밋을 만들지 않습니다. 리뷰·논의 해결과 최종 병합 판단은 [협업 규칙](development.md#병합-조건과-방식)을 따릅니다.

오류 검증은 기존 검증 MR을 재사용할 수 있습니다. 의도한 포맷·규칙·테스트·빌드 오류는 검증 브랜치에만 넣고 해당 작업의 실패 로그와 병합 차단을 확인합니다. 결과를 기록한 뒤 오류를 제거하고 병합 없이 종료합니다.

GitLab CI Lint의 성공은 설정 검증이며 서비스 테스트 성공과 구분합니다. Unity Editor 검사·실제 AI 모델 평가·배포·운영 통합 검증은 준비 조건과 실행 결과를 별도로 추적합니다.
