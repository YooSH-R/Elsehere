# 검사 파이프라인과 Runner 연결

검사 설정은 `.gitlab-ci.yml`과 `.gitlab/ci/`에서 관리합니다. Runner는 GitLab의 작업을 받아 실행하는 프로세스이며, 이 프로젝트에는 `a305-linux` 태그를 사용하는 Docker executor를 연결합니다. 설정 파일 작성, Runner 등록, 실제 작업 성공은 각각 확인합니다.

## 자동 검사 범위

백엔드·Unity·AI 작업은 `needs: ["ci:yaml"]`로 YAML 검사 성공 후 실행할 수 있습니다. 각 작업이 별도 환경을 사용하므로 Runner의 동시 실행 한도 안에서 인프라 검사와 겹쳐 실행합니다. 파이프라인 성공에는 선택된 모든 필수 작업의 통과가 필요합니다.

| 작업 | 실행 조건 | 검증·산출물 |
| --- | --- | --- |
| `ci:yaml` | 생성된 모든 파이프라인 | CI YAML 문법·형식 검사 |
| `backend:check-and-build` | 백엔드, API 명세, ERD, 앱 실행 설정 또는 공통 CI·작성 지침 변경 | Java 21·Gradle Wrapper의 `check bootJar`, Testcontainers PostgreSQL 테스트, JUnit 결과·`app.jar`·커밋 식별자 |
| `unity:format` | 프론트, API 명세, Unity 검사·규약 또는 공통 CI 변경 | 고정된 .NET SDK와 CSharpier로 직접 작성한 C# 포맷 검사 |
| `unity:editor-and-webgl` | Unity 검사를 선택한 보호된 dev 배포 자식, 모든 운영 배포 자식 또는 보호된 ref의 웹 실행에서 `RUN_UNITY_BUILD=true` | Windows 전용 Runner의 EditMode·PlayMode 테스트, 필요한 경우 WebGL 빌드, NUnit XML·로그·커밋 식별자가 포함된 정적 산출물 |
| `ai:check-and-test` | AI, API 명세 또는 공통 CI·작성 지침 변경 | Python 3.12·Ruff 검사, 피드백·워커 테스트, 구문 컴파일, JUnit·소스 아카이브·커밋 식별자 |
| `ai:panel-tests` | AI 피드백, API 명세 또는 공통 CI·작성 지침 변경 | Node.js 22의 피드백 패널 테스트 |
| `infra:compose-and-nginx` | 확인용 Compose가 존재하고 관련 설정·공통 CI 변경 | 개발·운영 Compose 해석, 호스트 프록시와 두 환경 응답 템플릿의 Nginx 문법 검사 |
| `infra:deployment-plan-tests` | 인프라 스크립트 또는 공통 CI 변경 | Ruff·임시 Git 저장소 기반 누적 배포 범위 판정 테스트 |
| `infra:app-runtime` | 앱용 Dockerfile·Compose·환경 예제·웹 설정·실행 점검 코드, API 명세 또는 공통 CI 변경 | 검사된 JAR를 받아 실제 앱 이미지를 빌드하고, 일회용 Compose에서 DB·백엔드 health·AI 인증·TTS 모델 적재·웹 gzip/MIME 검증 |
| `infra:observability` | 모니터링·Nginx·관측 검증 스크립트 또는 공통 CI 변경 | 요청 ID·로그의 민감 정보 제외, Prometheus·Grafana 구성, 경보 발동·복구와 가짜 수신기 전송 검사 |
| `prod:plan`·`prod:child`·`prod:deliver` | 보호된 `main` Push에서 자동 시작. 수동 재실행은 웹 실행과 `RUN_PROD_DEPLOY=true` 및 검증 변수 사용 | 직렬화된 자식에서 모든 영역 검사 후 누적 이미지 빌드·운영 자동 전달·상태 점검과 버전 보관. 설치·DB·병합 전 통합 검증 조건은 [운영 배포 문서](prod-deployment.md) 참조 |

일반 문서 변경은 빠른 YAML 검사만 수행합니다. API 명세는 일반 문서와 구분하여 백엔드·프론트·AI 검사를 함께 실행합니다. 작업 브랜치의 자동 검사는 Draft를 포함한 MR 파이프라인으로 통일합니다. MR을 만들기 전의 작업 브랜치 Push에는 파이프라인을 생성하지 않으며, 필요하면 웹에서 수동 실행합니다. 보호된 `dev`·`main`에 병합한 커밋은 별도 파이프라인으로 검사합니다. 새 커밋이 올라와도 이미 생성된 검사는 자동 취소하지 않습니다. 웹에서 수동 실행한 파이프라인은 사용 가능한 백엔드·Unity 포맷·AI·관측 검사를 모두 수행하고, 확인용 Compose가 있으면 Compose·Nginx 검사도 수행합니다.

MR의 `rules:changes:compare_to`에 실제 대상 브랜치를 명시하여 대상과의 최종 차이로 작업을 선택합니다. 같은 MR에서 문서만 추가로 고쳐도 백엔드 파일이 대상과 다르면 백엔드 검사가 다시 실행되고, 백엔드 변경을 완전히 되돌리면 선택 대상에서 빠집니다. 각 Push의 최종 커밋을 검사하며, 한 번의 Push에 포함된 중간 커밋마다 별도 파이프라인을 만들지는 않습니다. [GitLab 비교 대상 지정](https://docs.gitlab.com/ci/yaml/#ruleschangescompare_to)

### 변경 영역별 작업 선택 기준

| 대상 브랜치와 다른 경로 | 선택할 작업 |
| --- | --- |
| 일반 문서만 변경 | `ci:yaml` |
| `backend/` | `ci:yaml`, `backend:check-and-build` |
| `frontend/` | `ci:yaml`, `unity:format` |
| `ai/worker/` 또는 `ai/tools/` | `ci:yaml`, `ai:check-and-test` |
| `ai/feedback/` | `ci:yaml`, `ai:check-and-test`, `ai:panel-tests` |
| `docs/api/` | `ci:yaml`, `backend:check-and-build`, `unity:format`, 두 AI 작업, `infra:app-runtime` |
| 확인용 `infra/compose/` | `ci:yaml`, `infra:compose-and-nginx` |
| 앱용 Dockerfile·Compose·환경 예제·웹 응답 설정 | `ci:yaml`, `backend:check-and-build`, `infra:app-runtime`, 해당 경로의 인프라 문법 검사 |
| `infra/scripts/` | `ci:yaml`, `infra:deployment-plan-tests`. 앱 실행 점검 코드는 백엔드 빌드·실행 검사도 선택 |
| `infra/nginx/` | `ci:yaml`, `infra:observability`, 확인용 Compose가 있으면 `infra:compose-and-nginx` |
| `infra/monitoring/`, `check_observability.py`, `check_alerting.py` | `ci:yaml`, `infra:observability` |
| `.gitlab-ci.yml` 또는 `.gitlab/ci/` | 적용 가능한 모든 작업 |
| 웹에서 수동 실행 | 적용 가능한 모든 작업 |

`infra:compose-and-nginx`만 확인용 Compose의 존재를 전제로 합니다. `infra:observability`는 해당 파일 없이 독립 실행합니다. ERD 변경은 백엔드 검사, Unity 규약·검사 스크립트 변경은 Unity 검사, 공통 작성 지침 변경은 백엔드·Unity·AI 검사를 선택합니다. 여러 영역을 함께 바꾸면 해당 작업을 모두 실행합니다. AI 공통 설정·의존성 변경도 Python 검사를 실행하며 로컬 명령은 [AI 검사 문서](../ai/README.md)를 따릅니다.

작업 선택을 검증할 때는 CI 설정이 있는 버전을 대상으로 별도 Draft MR을 만듭니다. 대상과의 차이가 위 표의 한 영역에만 남도록 변경한 뒤, 파이프라인의 작업 목록과 결과를 확인합니다. 다음 사례로 바꿀 때 이전 검증용 변경을 복원합니다. 검증용 변경은 실제 CI MR이나 `dev`에 병합하지 않으며, 파이프라인·커밋별 결과는 검증 MR과 Jira에 기록합니다. 열린 MR에 Push한 커밋에서 MR 파이프라인만 생성되는지도 확인합니다. `ci:yaml` 로그의 실행 원인·커밋·대상 브랜치·MR 비교 기준을 작업 목록과 함께 확인할 수 있습니다.

일반 MR의 Unity 작업은 **포맷 검사**입니다. Unity Editor의 컴파일·EditMode·PlayMode·WebGL 빌드 검증을 대신하지 않습니다. 보호된 후보의 수동 CI 연결 시험에는 `unity:editor-and-webgl`을 사용하며 실행 조건과 노트북 유지 방법은 [Windows Unity Runner](unity/windows-runner.md)를 따릅니다. Runner Online 상태만으로 테스트·빌드 성공으로 판단하지 않습니다. AI 단위 테스트는 모의 공급자로 실행하며 실제 모델 호출·GPU·음성 모델 기동은 검증 범위에 포함하지 않습니다.

일반 MR 파이프라인은 검사를 수행하며 서버 앱을 교체하지 않습니다. dev 자동 배포의 보호 조건, 자식 파이프라인, 서버 설치와 연결 시험은 [dev 자동 배포 운영](dev-auto-deployment.md)을 따릅니다. 운영 통합 테스트는 별도 릴리스 검증으로 관리합니다. 마지막 성공 배포 이후 누적 변경 판정은 배포 구현의 책임이며 CI의 `rules:changes`만으로 해결했다고 간주하지 않습니다.

`infra:app-runtime`은 Runner의 rootless Docker 안에 `a305-check-*` 프로젝트를 만들고 종료 시 그 프로젝트의 컨테이너·네트워크·DB 볼륨만 정리합니다. 테스트용 네트워크의 외부 통신을 차단하고 공급자 키는 가짜 값을 사용하므로 실제 모델을 호출하지 않습니다. 웹 응답에는 작은 HTTP fixture를 사용하며 실제 Unity 게임 화면이나 외부 HTTPS 경로를 검증한 것으로 표시하지 않습니다. 실행 구성은 [서버 구성](deployment.md), Unity 실행 환경은 [Windows Unity Runner](unity/windows-runner.md)를 따릅니다.

## 동일 서버의 실행 구조와 제한

```text
GitLab → Runner 관리 컨테이너 → 검사 컨테이너
                              └─ 테스트 전용 Docker 서비스 → 임시 PostgreSQL

호스트 Docker → a305-dev / a305-prod (서비스 실행)
```

Runner 관리 컨테이너만 호스트 Docker 소켓을 사용합니다. 일반 검사 컨테이너에는 소켓이나 앱의 환경 파일·인증서를 마운트하지 않습니다. Testcontainers는 검사 작업 전용 rootless Docker 서비스에 연결합니다. 프로젝트 Runner는 서버의 Docker를 제어하므로 팀 저장소의 신뢰하는 작업에만 사용합니다. [Runner 컨테이너 운영](https://docs.gitlab.com/runner/install/docker/)

동시에 작업 두 개까지 실행하며 각 검사 컨테이너는 CPU 2개·메모리 3GiB, Docker 서비스는 CPU 1개·메모리 2GiB로 제한합니다. CPU 4개·메모리 16GiB 서버에서 앱과 모니터링의 자원을 함께 고려한 상한입니다. Runner 자체의 컨테이너 제한과 작업·서비스 제한은 별도로 적용합니다. 동시 실행 중 CPU 사용률·가용 메모리·앱 응답을 확인하고 서버 여유가 부족하면 `concurrent`와 `limit`를 함께 `1`로 낮춥니다.

백엔드는 Gradle 의존성·Wrapper 캐시와 `--build-cache`의 작업 결과 캐시를 함께 사용합니다. 입력이 달라진 작업은 다시 실행하며 캐시가 없어도 검사·빌드를 수행합니다. 캐시 복원·저장 시간도 작업 실행 시간에 포함해 확인합니다.

제한값과 허용 서비스는 `infra/runner/config.template.toml`을 따릅니다. 이 파일에는 토큰이 없으며 등록 완료 파일을 대체하는 완성본이 아닙니다. 등록된 항목의 인증 정보는 보존하고 제한값을 해당 항목에 병합합니다. [Runner 고급 설정](https://docs.gitlab.com/runner/configuration/advanced-configuration/)

## GitLab에서 프로젝트 Runner 생성

프로젝트 **Settings → CI/CD → Runners → Create project runner**에서 생성합니다.

- 플랫폼: Linux
- 태그: `a305-linux`
- Run untagged jobs: 해제
- 설명: `a305-linux-ci`
- 처음에는 Paused 상태로 생성하고 자원 제한 반영 후 활성화합니다.

등록 화면의 `glrt-` 인증 토큰은 서버의 등록 프롬프트에만 입력합니다. 저장소·채팅·터미널 출력 공유에 포함하지 않습니다. MR 검사에 사용하므로 protected 브랜치 전용으로 제한하는 옵션은 선택하지 않습니다. 배포용 권한은 이 검사 Runner에 추가하지 않습니다.

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

기존 `gitlab-runner` 컨테이너나 등록이 있는 서버에서는 먼저 확인하고 중복 생성하지 않습니다. 등록 프롬프트에는 아래 값을 입력합니다.

| 항목 | 값 |
| --- | --- |
| GitLab instance URL | `https://lab.ssafy.com` |
| Authentication token | 생성 화면의 실제 `glrt-` 토큰 |
| Runner name | `a305-linux-ci` |
| Executor | `docker` |
| Default Docker image | `alpine:3.22` |

등록 후 `/srv/gitlab-runner/config/config.toml`을 서버 안에 백업하고 편집합니다. 토큰을 공유하지 않도록 파일 전체를 출력하지 않습니다.

```bash
sudo cp -a /srv/gitlab-runner/config/config.toml /srv/gitlab-runner/config/config.toml.before-limits
sudo nano /srv/gitlab-runner/config/config.toml
```

최상위 `concurrent`와 등록된 `[[runners]]`의 `limit`를 `2`로 맞추고 id·token·URL·이름·토큰 시각을 유지합니다. 템플릿의 `environment`와 `[runners.docker]` 설정을 그 항목에 적용합니다. 새 `[[runners]]`를 추가하거나 템플릿으로 파일 전체를 덮어쓰지 않습니다.

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
2. `a305-linux` 작업이 Pending에서 Running으로 바뀌는지 확인합니다.
3. 백엔드 DB 테스트·품질 검사·빌드가 성공하고 JUnit 보고서·JAR·커밋 식별자가 보관되는지 확인합니다.
4. Unity 포맷·AI 검사와 적용 가능한 인프라 검사를 확인합니다. 인프라 설정 MR과 통합한 버전에서도 검사합니다.
5. 일반 문서·백엔드·프론트·AI·API 명세 변경에 따라 작업 선택이 달라지고 열린 MR의 Push가 중복 파이프라인을 만들지 않는지 확인합니다.
6. 실제 필수 검사가 동작하는 것을 확인하고 아래 병합 조건을 적용합니다. 작성만 끝난 설정이나 Pending 상태를 성공으로 기록하지 않습니다.

### CI 성공을 요구하는 병합 조건

검사 성공을 요구하는 프로젝트 설정은 [GitLab 운영 정책의 병합 검사 설정](gitlab-setup.md#병합-검사-설정)을 따릅니다.

검사 작업에는 `allow_failure`를 지정하지 않고, CI에서 자동 포맷이나 수정 커밋을 만들지 않습니다. 리뷰·논의 해결과 최종 병합 판단은 [협업 규칙](development.md#병합-조건과-방식)을 따릅니다.

오류 검증은 기존 검증 MR을 재사용할 수 있습니다. 의도한 포맷·규칙·테스트·빌드 오류는 검증 브랜치에만 넣고 해당 작업의 실패 로그와 병합 차단을 확인합니다. 결과를 기록한 뒤 오류를 제거하고 병합 없이 종료합니다.

GitLab CI Lint의 성공은 설정 검증이며 서비스 테스트 성공과 구분합니다. Unity Editor 검사·실제 AI 모델 평가·배포·운영 통합 검증은 준비 조건과 실행 결과를 별도로 추적합니다.
