# dev 자동 배포 운영

검증한 앱 이미지를 개발 환경에 전달하고, 배포 후 점검에 성공했을 때만 성공 기록을 갱신합니다. 운영 환경과 호스트 Nginx, 별도 모니터링 스택은 이 파이프라인에서 변경하지 않습니다. 서버 수신기·전용 SSH 키·CI 변수를 설치하고 연결 시험을 마친 뒤 사용합니다.

같은 배포 도구의 운영 모드는 별도 수신기·키·`production` 범위 변수를 사용합니다. main 병합 후 운영 자동 배포와 복구는 [main 운영 배포 문서](prod-deployment.md)를 따릅니다. 개발 수신기의 환경 설정 기본값은 `dev`이며 개발 배포는 보호된 `dev` 브랜치만 허용합니다.

## 실행 흐름

일반 Linux 검사는 EC2와 노트북이 나눠 받지만 `dev:plan`·`dev:deliver`는 `a305-ec2` 태그를 추가로 요구해 EC2에서만 실행합니다. Runner 태그와 노트북 제한은 [검사 파이프라인](ci-runner.md#노트북-linux-runner)을 따릅니다.

1. 보호된 `dev`의 각 병합 커밋 또는 `dev`의 수동 재실행에서 `dev:plan`이 서버의 마지막 성공 기록을 읽습니다. 새 `dev` 커밋이 생기면 Unity 빌드가 시작되지 않은 이전 후보를 자동 취소합니다. 빌드가 시작된 후보와 이미 시작한 배포는 끝까지 진행합니다.
2. `deployment_plan.py`가 마지막 성공 커밋부터 후보까지 누적 변경을 비교합니다. 첫 자동 배포는 모든 영역의 개발 검증·필요한 이미지 빌드를 수행하며 Unity 전체 테스트는 생략합니다. 이전 수동 배포의 혼합 이미지 상태를 하나의 검증 커밋으로 간주하지 않습니다.
3. 필요한 검사 중 입력이 같은 성공 MR 결과는 재사용하고, 달라졌거나 성공 기록을 확인할 수 없는 검사는 자식 파이프라인에서 실행합니다. 백엔드·AI의 빌드 산출물도 검증 후 복원합니다. 백엔드 또는 인프라 변경은 앱 실행·모니터링 검사까지 같은 기준으로 확인합니다. 일반 MR의 검사 종류와 병합 조건은 유지합니다.
4. `web` 이미지가 필요하면 노트북의 `unity:webgl-build`가 WebGL 빌드·업로드만 수행합니다. 개발 배포에서는 EditMode·PlayMode 전체를 실행하지 않습니다. Unity 포맷·브라우저 연동 검사는 재사용하거나 실행합니다. 운영 배포 전 전체 검사와 보호된 후보의 수동 전체 검사는 유지합니다.
5. 검사를 통과한 후보의 `dev:deliver`만 배포 대기열에 들어갑니다. 배포 차례가 오면 서버의 현재 성공 기록을 다시 읽고 누적 변경 계획을 갱신합니다. 필요한 검사·빌드가 이번 파이프라인에서 검증한 범위에 포함되는지 확인한 뒤, 통과한 JAR·WebGL 산출물의 커밋을 확인하고 필요한 이미지만 만듭니다. 변경 없는 이미지 참조·ID·소스 커밋은 새 성공 기록에서 재사용합니다.
6. 검증한 이미지와 Compose를 SSH로 전달합니다. MR 결과를 재사용하면 `dev:reuse-checks`가 같은 프로젝트의 산출물 다운로드 API로 원래 성공한 MR 작업의 ZIP을 직접 받아 복원합니다. 부모 산출물 전달용 유료 YAML 기능에는 의존하지 않습니다. 부모→자식 설정에는 비밀값이 없는 계획·이미지·검사 출처 메타데이터만 넣고 빌드 ZIP은 포함하지 않습니다. 동적 자식 설정을 읽을 때 적용되는 GitLab 산출물 용량 제한을 지키기 위함입니다.
7. 서버 수신기가 상태 토큰·검증한 기준 커밋·허용 파일을 확인하고, 앱 Compose와 설치된 모니터링 추가 Compose를 함께 사용해 해당 앱 서비스만 교체합니다. 개발 후보의 순서는 CI가 Git 조상 관계로 검증하며 자식 파이프라인 생성 번호를 커밋 순서로 간주하지 않습니다. 운영 모드와 기존 형식의 전달은 파이프라인 번호 검사도 유지합니다.
8. 앱·DB health, 이미지 ID, WebGL 버전, 기존 HTTPS API, 인증이 필요한 관리 포트와 새로운 Prometheus 수집 결과를 확인합니다. 성공 후 이미지 변수와 성공 기록을 갱신합니다.

배포 계획은 경량 Runner에서, 앱 실행 검사와 이미지 전달은 일반 Runner에서 수행합니다. 이미지 빌드는 [Docker 계층 캐시](ci-runner.md#docker-이미지와-ai-모델-계층-캐시)로 변경되지 않은 AI 모델·의존성을 재사용하며, 배포 후보의 JAR·WebGL 커밋과 이미지 식별자는 계속 검증합니다.

### MR 결과 재사용

MR 검사의 종류는 유지하고, 성공한 작업은 `dist/ci/success.json`에 소스 커밋·프로젝트·파이프라인·작업 식별자를 남깁니다. 개발 배포 계획은 같은 프로젝트에서 `dev`에 병합됐고 후보 이력에 포함된 최근 MR만 조회합니다. GitLab의 성공한 MR 파이프라인 산출물 경로에서 기록을 읽으며, 기록의 작업 이름·소스 커밋이 조회 대상과 일치해야 합니다.

`infra/scripts/mr_results.py`의 `JOB_INPUTS`와 `COMMON_INPUTS`로 코드·의존성·API 계약·CI 설정을 비교합니다. 해당 입력이 같으면 검사를 다시 실행하지 않습니다. 비교는 검사 영역과 연관 입력 전체를 대상으로 하며, 새 입력을 읽도록 검사를 바꾸면 이 목록도 갱신합니다. CI 회귀 검사는 MR 작업 선택 경로가 재사용 비교 범위에서 빠지지 않는지 확인합니다.

JAR와 AI 산출물은 원래 소스 커밋 표시를 유지합니다. 부모에서 확인한 ZIP의 SHA256·원본 작업 ID·내부 성공 기록을 자식에서 대조하고 필요한 파일만 복원합니다. 부모의 `dev:plan` 산출물에는 상태·계획·자식 YAML만 올립니다. 전달 시에도 원래 검사 입력과 현재 후보가 같은지 확인합니다. `plan.json`의 `reused_jobs`와 `reused-results.json`에서 MR·파이프라인·작업 출처를 확인할 수 있습니다.

최근 MR 20개 안에서 검사별 최대 두 개의 같은 입력 후보를 조회합니다. 기록이 없거나 만료됐거나 권한·통신·입력 비교를 확인할 수 없으면 해당 검사를 새로 실행합니다. 재사용을 결정한 뒤 산출물 무결성 확인에 실패하면 배포를 중단합니다. 운영 배포는 MR 결과를 재사용하지 않고 기존 전체 검사를 수행합니다. 개발 배포의 전체 Unity 검사를 생략하므로 여러 변경의 통합 문제는 운영 전 전체 검사나 별도 플레이 검수에서 발견될 수 있습니다.

### WebGL 빌드 전달과 정리

WebGL 정적 파일은 같은 프로젝트의 Generic Package Registry를 통해 Windows 빌드 작업에서 Linux 전달 작업으로 옮깁니다. 개발·운영·수동 검사의 `unity:webgl-build`는 빌드 커밋을 확인한 뒤 `a305-webgl/<파이프라인 ID>-<작업 ID>/webgl.tar`를 게시하고, 작은 식별 파일 `dist/webgl-package.json`만 일반 산출물로 전달합니다. 패키지 게시·다운로드에는 각 작업의 `CI_JOB_TOKEN`을 사용합니다. 패키지 저장소를 활성화한 상태로 유지하며 외부 공개나 새 비밀값은 필요하지 않습니다.

`dev:deliver`와 `prod:deliver`는 웹 이미지가 필요한 경우에만 패키지를 내려받습니다. 커밋·파이프라인 ID·파일 크기·SHA256을 확인하고 경로 이탈을 차단해 `dist/webgl/`로 복원한 뒤, 기존 `version.json` 검증과 이미지 빌드를 수행합니다. 패키지 업로드·다운로드·무결성 검증 실패는 배포 실패로 처리합니다. 이미지와 서버 배포 번들의 전달은 기존 SSH 경로를 유지합니다.

`unity:package-cleanup`은 전달 작업이 끝난 뒤 성공·실패에 관계없이 해당 빌드 작업의 임시 패키지만 삭제합니다. 웹 빌드를 생략했다면 할 일이 없습니다. 보호된 ref의 `RUN_UNITY_BUILD=true` 연결 시험에서도 업로드를 확인한 뒤 같은 방식으로 정리합니다. 패키지가 정리된 전달 작업만 재시도하지 않고, 실패를 수정한 뒤 새 파이프라인 전체를 실행합니다. 패키지 삭제에는 Maintainer 권한이 필요하므로 Developer가 시작한 파이프라인에서 HTTP 403을 받으면 종료 코드 `77`의 정리 경고로 남깁니다. 이 경우 Maintainer가 정리 작업만 재시도합니다. 다른 정리 오류는 실패로 처리하며 원인을 수정한 뒤 재시도합니다. 파이프라인 전체를 취소하면 정리 작업도 실행되지 않을 수 있으므로, 프로젝트의 **Deploy → Package registry**에서 취소한 파이프라인 ID에 해당하는 `a305-webgl` 버전만 확인해 정리합니다. 운영 이미지 보관과 서버 롤백 기록은 이 임시 패키지 정리와 독립적입니다.

개발 후보는 보호된 `dev` 이력에 포함되고 현재 배포 커밋보다 뒤에 있어야 합니다. 아직 검증되지 않은 새 merge가 생겼다는 이유로 검증된 후보를 제외하지 않습니다. 이미 같은 후보나 더 새 커밋이 배포됐다면 서버에 전달하지 않고 `result.json`에 `superseded`를 기록하며 종료 코드 `78`로 끝냅니다. `dev:deliver`만 이 코드를 허용하므로 완료된 검사 결과는 유지되고 파이프라인에는 배포 제외 경고가 표시됩니다. Git 조회 실패·분기된 이력·미검증 검사나 빌드가 필요한 기준 변경·서버 오류는 계속 실패합니다. 운영 후보는 기존의 최신 `main` 일치 확인을 유지합니다.

개발 `resource_group: a305-dev-release`는 `dev:deliver`에만 적용합니다. `dev:child`는 잠금을 잡지 않습니다. dev 자식 파이프라인은 `on_new_commit: conservative`, `unity:webgl-build`는 `interruptible: false`를 사용합니다. Unity 빌드가 대기 중인 후보는 새 커밋이 생기면 취소되지만, 빌드가 시작되면 해당 후보의 나머지 검사와 배포도 함께 보존됩니다. dev 부모는 `on_new_commit: none`으로 자식과의 연결을 유지해 자식의 최종 결과를 보고합니다. 웹 빌드가 필요 없는 후보는 `dev:deliver`가 시작될 때부터 보호됩니다.

이미 시작한 `dev:deliver`도 `interruptible: false`로 보호합니다. Unity 빌드와 수동·운영 전체 검사는 별도 작업 폴더에서 최대 두 개를 실행하며, 제한과 기존 대기 작업의 전환 조건은 [Windows Unity Runner](unity/windows-runner.md)를 따릅니다. 서버에서도 파일 잠금과 상태 토큰으로 동시 교체를 막습니다.

### 검증된 최신 후보 우선 실행

GitLab의 `a305-dev-release` 자원 그룹은 `process_mode=newest_ready_first`로 설정합니다. 준비된 배포 작업 중 새 파이프라인을 우선 선택하며 검사가 끝나지 않은 후보를 기다리느라 준비된 후보를 멈추지 않습니다. 새 후보가 먼저 배포되면 이후 실행되는 이전 후보는 Git 조상 관계 확인에서 제외됩니다. 생성 번호와 커밋 순서가 다른 경우에도 이미 반영된 버전보다 과거로 돌아가지 않습니다.

`process_mode`는 YAML 설정이 아니라 GitLab Resource groups API의 프로젝트 설정입니다. `PUT /projects/:id/resource_groups/a305-dev-release`에 `process_mode=newest_ready_first`를 지정하고 같은 경로의 `GET`으로 저장값을 확인합니다. API 토큰을 저장소나 CI 로그에 기록하지 않습니다. [자원 그룹 실행 순서](https://docs.gitlab.com/ci/resource_groups/#process-modes)

`verified_base_commit` 형식으로 전환할 때는 이를 지원하는 서버 수신기를 먼저 설치하고 성공 기록·환경 파일을 보존합니다. 자원 그룹 실행 순서를 설정한 뒤 CI를 반영합니다. 기존 파이프라인이 부모에서 같은 자원 그룹을 잡고 있으면 완료될 때까지 새 전달도 기다릴 수 있습니다. 자동 취소 규칙은 변경 후 생성된 파이프라인부터 적용되므로 이미 생성된 검사에는 소급 적용되지 않습니다.

## 서버에서 보존하는 구성

- 환경 파일: `/home/ubuntu/apps/a305/dev/deploy.env`.
- 모니터링 추가 Compose: `/home/ubuntu/apps/a305/monitoring/config/compose.observability.yaml`.
- 개발 워커 네트워크 추가 Compose: `/home/ubuntu/apps/a305/dev/config/compose.dev-worker-network.yaml`. 고정 IP·자동 할당 범위·브리지와 최초 전환은 [GPU 워커 네트워크 운영](gpu-worker-network.md)을 따릅니다. `fixed_worker_network` 검사를 활성화하고 재기동·복구에서도 같은 추가 설정을 사용합니다.
- `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `ADMIN_LOGIN_ID`, `ADMIN_PASSWORD`, `MONITORING_PASSWORD`, `OBSERVABILITY_WEB_LOG_CONFIG`, DB·AI·공급자 비밀값은 서버 파일에 유지합니다. CI에 전달하지 않습니다. 개발 어드민 비밀번호는 운영과 다르게 정하고 12자 이상·72바이트 이하로 설정합니다. 두 어드민 값이 비어 있으면 로그인만 비활성화됩니다.
- 웹 로그 설정의 읽기 전용 마운트, 백엔드 내부 관리 주소와 비공개 포트를 유지합니다.
- NPC 음성은 백엔드의 쓰기 가능한 `/app/data/npc-audio`에 저장하고 환경별 `npc-audio-data` 볼륨으로 유지합니다. 백엔드의 AI 내부 주소는 `AI_WORKER_BASE_URL=http://ai-worker:8020`으로 연결합니다.
- 수신기는 NPC 음성 경로·마운트·개발 볼륨 이름과 실행 중인 백엔드의 실제 볼륨을 대조합니다. 누락·읽기 전용·다른 볼륨으로의 변경은 앱 교체 전에 거절하며, 저장 위치 이전은 [음성 저장 운영 기준](npc-audio-storage.md)에 따라 별도로 준비합니다.
- 백엔드 로그 파일은 `/app/logs`의 환경별 `backend-logs` 볼륨으로 유지합니다. 처음 배포할 때 Compose가 빈 볼륨을 만들며, 수신기는 이 볼륨을 검사하지 않습니다.
- DB 컨테이너·볼륨은 자동 교체하지 않습니다. DB 실행 설정이 달라지면 별도 마이그레이션 절차를 요구합니다.
- 호스트 Nginx·TLS·운영 확인용 서비스·모니터링 스택은 배포 대상에 포함하지 않습니다. 새 WebGL HTTPS 경로의 브라우저 검증은 HTTPS 운영 작업에서 진행합니다.

현재 배포한 모니터링 백엔드의 소스 커밋을 `minimum_backend_commit`으로 지정합니다. 해당 커밋이 없는 후보로 백엔드를 새로 만들면 계획 단계에서 실패합니다. 모니터링 코드를 포함한 선행 MR이 `dev`에 반영된 뒤 자동 배포를 진행합니다.

### Google 로그인 설정 유지

개발 Google 웹 클라이언트의 두 값은 `~/apps/a305/dev/deploy.env`에서 관리합니다. 앱 Compose는 두 값을 백엔드에 필수 환경변수로 전달합니다. 배포 수신기는 이 서버 환경 파일과 릴리스의 `images.env`를 함께 읽고, 성공 시 이미지 변수만 갱신해 Google 설정을 보존합니다. 실패 복구에서도 배포 전 환경 파일을 복원합니다.

어드민 로그인 계정은 같은 서버 환경 파일의 `ADMIN_LOGIN_ID`·`ADMIN_PASSWORD`로 관리합니다. 비밀번호에 `$`가 있으면 Compose 치환을 피하도록 `$$`로 적어야 할 수 있으므로 적용 전에 `docker compose config`로 확인합니다. 값을 바꿀 때는 백엔드 컨테이너를 다시 만들어야 하며 모든 로그인 세션이 종료됩니다. 비밀번호나 해석된 환경변수 값은 출력·로그에 남기지 않습니다.

수신기는 Google 값 누락·빈 값·`not-configured` 또는 기존 서버 설정과 다른 값이 해석되는 후보를 컨테이너 교체 전에 거절합니다. 클라이언트 값을 바꿀 때는 서버 `deploy.env`를 수정합니다. 후보와 이전 Compose 모두 같은 현재 환경 파일로 해석하므로 정상적인 값 교체는 가능합니다. 실제 값과 인증 JSON 파일은 소스·이미지·CI 변수·산출물에 넣지 않습니다.

Google 콘솔에는 `https://dev.elsehere.kr/api/v1/auth/callback/google`을 승인된 리디렉션 URI로 등록합니다. 환경변수만 바꿀 때는 현재 배포의 Compose·모니터링 추가 파일·환경 파일 조합을 유지하고 백엔드만 `up -d --no-deps --no-build --pull never --force-recreate backend`으로 다시 만듭니다. 단순 `restart`는 바뀐 환경변수를 반영하지 않습니다. 적용 뒤 로그인 시작 응답의 클라이언트 ID와 콜백 주소를 확인하며 Secret과 OAuth 상태 값을 로그에 남기지 않습니다.

## 서버 수신기 설치

아래 명령은 Ubuntu의 `ubuntu` 계정에서 실행합니다. 검증한 전체 커밋을 별도 `deploy-tools` 작업 폴더에 받습니다. 기존 앱 실행 폴더나 환경 파일을 새 소스로 덮어쓰지 않습니다.

```bash
git -C "$HOME/apps/a305/dev/source" fetch origin dev
git -C "$HOME/apps/a305/dev/source" worktree add --detach \
  "$HOME/apps/a305/dev/deploy-tools" '<검증한 전체 커밋 SHA>'
cd "$HOME/apps/a305/dev/deploy-tools"
sudo install -d -m 755 /usr/local/lib/a305-dev-deploy
sudo install -m 644 infra/scripts/deployment_server.py /usr/local/lib/a305-dev-deploy/deployment_server.py
sudo install -m 644 infra/scripts/dev_worker_network.py /usr/local/lib/a305-dev-deploy/dev_worker_network.py
cp infra/deploy/dev-server.example.json "$HOME/apps/a305/dev/server-config.json"
nano "$HOME/apps/a305/dev/server-config.json"
```

설정의 `minimum_backend_commit`과 `bootstrap_sources`를 실제 실행 중인 백엔드·AI·WebGL의 전체 소스 SHA로 바꿉니다. 웹 소스는 실제 `/version.json`의 값입니다. 초기 Compose와 모니터링 추가 Compose 경로도 서버의 현재 실행 경로와 맞춥니다. `public_health_url`은 개발 대표 주소의 `https://dev.elsehere.kr/api/health`를 사용합니다. SSH 접속 주소는 기존 `k15a305.p.ssafy.io`를 유지합니다. 설정에는 비밀번호·API 키를 넣지 않습니다. 환경 파일은 배포를 실행하는 `ubuntu` 계정만 읽고 쓸 수 있어야 합니다. 내용 대신 소유자와 권한만 확인합니다.

```bash
stat -c '%U %a %n' "$HOME/apps/a305/dev/deploy.env"
```

소유자가 `ubuntu`가 아니거나 권한이 `600`이 아니면 이 파일 하나만 수정합니다.

```bash
sudo chown ubuntu:ubuntu "$HOME/apps/a305/dev/deploy.env"
chmod 600 "$HOME/apps/a305/dev/deploy.env"
```

```bash
sudo install -m 644 "$HOME/apps/a305/dev/server-config.json" /etc/a305-dev-deploy.json
python3 /usr/local/lib/a305-dev-deploy/deployment_server.py \
  --config /etc/a305-dev-deploy.json --initialize
```

초기화는 현재 앱·모니터링을 점검하고 `bootstrap.json`만 만듭니다. 아직 CI가 검증·배포한 버전이 아니므로 `last-success.json`을 만들지 않습니다. 이미 초기화된 디렉터리를 다시 초기화하면 실패합니다.

수동으로 앱을 교체한 뒤 자동 배포를 재개할 때는 기존 설정과 상태 디렉터리를 보존합니다. 설치한 설정을 백업하고 `directory`에 새 상태 디렉터리, `initial_compose_file`에 현재 컨테이너의 Compose 실행 경로, `bootstrap_sources`에 실제 이미지의 검증한 소스 SHA를 지정합니다. 모니터링 추가 Compose는 그대로 유지하고 `minimum_backend_commit`도 현재 백엔드의 기준과 맞춥니다. 새 설정으로 `--initialize`와 제한된 SSH의 `state` 조회를 통과한 뒤 새 파이프라인을 실행합니다. 이 재등록은 앱을 교체하지 않으며 수동 배포를 CI 성공으로 기록하지 않습니다.

## 전용 SSH 키와 GitLab 변수

개인 PEM과 Runner 등록 토큰을 배포 키로 재사용하지 않습니다. 서버에서 새 키를 만들고, 공개 키에 두 작업만 허용하는 강제 명령을 연결합니다.

```bash
ssh-keygen -t ed25519 -f "$HOME/.ssh/a305-dev-deploy" -C a305-dev-ci -N ''
if ! grep -qF "$(cat "$HOME/.ssh/a305-dev-deploy.pub")" "$HOME/.ssh/authorized_keys"; then
  printf 'restrict,command="/usr/bin/python3 /usr/local/lib/a305-dev-deploy/deployment_server.py --config /etc/a305-dev-deploy.json" %s\n' \
    "$(cat "$HOME/.ssh/a305-dev-deploy.pub")" >> "$HOME/.ssh/authorized_keys"
fi
chmod 600 "$HOME/.ssh/authorized_keys"
awk '{print "k15a305.p.ssafy.io " $1 " " $2}' /etc/ssh/ssh_host_ed25519_key.pub \
  > "$HOME/apps/a305/dev/deploy-known-hosts"
ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub
```

기존 공개 키 항목은 유지합니다. 수신기는 `state`와 `apply`만 허용하므로 이 키로 일반 셸·SCP·포트 포워딩을 사용하지 않습니다. 사용자 SSH 접속과 Grafana 터널에는 기존 개인 키를 계속 사용합니다.

```bash
ssh -T -i "$HOME/.ssh/a305-dev-deploy" \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile="$HOME/apps/a305/dev/deploy-known-hosts" \
  ubuntu@k15a305.p.ssafy.io state
```

GitLab Settings → CI/CD → Variables에 다음을 등록합니다. 두 항목 모두 **File 유형**, **Protected**, 환경 범위 `*`, 변수 확장 해제를 사용합니다. 개인 키의 여러 줄·공백 때문에 마스킹 저장이 불가능하면 Visible을 선택하고 파일 내용을 작업 로그에 출력하지 않습니다.

| 변수 | 사용자가 직접 입력할 내용 |
| --- | --- |
| `DEV_DEPLOY_SSH_KEY` | 새 개인 키 파일 전체. 마지막 줄 뒤 LF 포함 |
| `DEV_DEPLOY_KNOWN_HOSTS` | 위에서 만든 호스트 키 파일 전체 |

개인 키는 채팅·MR·Jira·저장소에 붙이지 않습니다. CI는 호스트 키를 고정해 검증하며 실행 중 `ssh-keyscan`으로 새 키를 신뢰하지 않습니다. [GitLab SSH 키 안내](https://docs.gitlab.com/ci/jobs/ssh_keys/)

## 수동 연결 확인과 재실행

1. 변경이 필수 MR 검사를 통과하면 병합 조건을 확인해 `dev`에 병합합니다. 선행 모니터링 코드와 서버 수신기·전용 SSH 키·CI 변수의 설치 상태를 확인합니다.
2. Windows 노트북 Runner를 켜고 빌드가 끝날 때까지 PC를 유지합니다. `dev`·`main` 보호와 Runner의 프로젝트 잠금·태그 제한을 유지합니다. MR 검사와 Runner 실행 범위는 [Windows Unity Runner](unity/windows-runner.md)를 따릅니다.
3. GitLab의 New pipeline에서 보호된 `dev`와 `RUN_DEV_DEPLOY=true`를 지정합니다.
4. 부모의 계획 작업, 자식의 선택된 검사·이미지 전달·실제 서버 점검과 성공 기록을 확인합니다. 첫 자동 배포는 모든 앱을 검증·빌드하므로 노트북에서 실제 WebGL을 새로 빌드합니다.

수동 실행도 실제 개발 앱을 교체하므로 이미 실행 중인 배포와 서버 상태를 먼저 확인합니다. 필수 MR 검사는 병합 전에 수행하고, 병합 후 배포 성공과 실패 복구 검증은 별도로 기록합니다.

## 상태와 실패 후 재실행

SSH 상태 조회가 실패하면 CI는 개인 키 형식·호스트 키 검증·공개 키 인증·이름 해석·연결 거절·연결 시간 초과를 구분해 안내합니다. 원본 stderr와 키·변수 내용은 로그에 출력하지 않습니다. 개인 키 형식 오류는 GitLab File 변수의 전체 키와 마지막 LF 줄바꿈을, 호스트 키 오류는 등록한 고정 호스트 키를 확인합니다. 이 단계에서 실패하면 앱 교체는 시작되지 않습니다.

서버의 `deployments/last-success.json`에는 성공 커밋, 서비스별 이미지 참조·ID·소스 커밋, 실제 Compose 경로와 모니터링 추가 설정의 식별값을 보관합니다. 다음 배포는 이 기록과 서버에서 실행 중인 이미지가 같은지 대조합니다.

실패하면 성공 기록을 유지하고 이전 이미지·Compose로 변경했던 앱만 되돌립니다. `last-failure.json`의 `restored`로 복구 점검 결과를 구분합니다. `false`이면 수동 복구가 필요합니다. DB 스키마·데이터를 과거로 되돌리는 기능은 포함하지 않습니다.

문제를 고친 뒤 최신 브랜치에서 `RUN_DEV_DEPLOY=true`로 **새 파이프라인 전체를 실행**합니다. 이전 계획의 전달 작업만 재시도하면 상태 토큰이 달라 거절될 수 있습니다. 최신 계획은 이전에 실패한 서비스 변경도 다시 포함합니다. 서버에서 이미지를 별도로 교체해 성공 기록과 달라진 경우에는 자동 배포를 멈추고 담당자가 실행 상태와 기록을 먼저 맞춥니다.

수동 점검·기동에도 성공 기록의 `compose_file`과 설치된 모니터링 추가 Compose를 함께 사용합니다. 일반 `up`으로 의존성을 다시 생성하거나 `--remove-orphans`·볼륨 삭제·전체 Docker 정리를 사용하지 않습니다.

```bash
cat "$HOME/apps/a305/dev/deployments/last-success.json"
cat "$HOME/apps/a305/dev/deployments/last-failure.json"
sudo docker ps --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}'
curl --fail http://127.0.0.1:8081/version.json
curl --fail https://dev.elsehere.kr/api/health
```

기본 CI와 Runner 운영은 [검사 파이프라인](ci-runner.md), 구성 정책은 [CI/CD 기준](ci-cd.md), 환경 구분은 [배포 구성](deployment.md)을 따릅니다.
