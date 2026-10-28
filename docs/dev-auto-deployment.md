# dev 자동 배포 운영

검증한 앱 이미지를 개발 환경에 전달하고, 배포 후 점검에 성공했을 때만 성공 기록을 갱신합니다. 운영 환경과 호스트 Nginx, 별도 모니터링 스택은 이 파이프라인에서 변경하지 않습니다. 서버 수신기·전용 SSH 키·CI 변수를 설치하고 연결 시험을 마친 뒤 사용합니다.

같은 배포 도구의 운영 모드는 별도 수신기·키·`production` 범위 변수를 사용합니다. main 병합 후 운영 자동 배포와 복구는 [main 운영 배포 문서](prod-deployment.md)를 따릅니다. 개발 수신기의 환경 설정 기본값은 `dev`이며 기존 서버 설정은 유지할 수 있습니다.

## 실행 흐름

1. 보호된 `dev` Push 또는 명시적인 보호 후보 시험에서 `dev:plan`이 서버의 마지막 성공 기록을 읽습니다.
2. `deployment_plan.py`가 마지막 성공 커밋부터 후보까지 누적 변경을 비교합니다. 첫 자동 배포는 전체 검사·빌드를 수행합니다. 이전 수동 배포의 혼합 이미지 상태를 하나의 검증 커밋으로 간주하지 않습니다.
3. 생성한 자식 파이프라인이 필요한 백엔드·AI·Unity·인프라 검사만 실행합니다. 백엔드 또는 인프라 검사가 필요한 배포는 모니터링 구성·알림 검사도 통과해야 이미지 전달로 진행합니다. 일반 MR은 기존 변경 영역 검사를 유지하며 서버 배포 수신기를 호출하지 않습니다.
4. Unity 검사가 필요하면 노트북 Runner에서 EditMode·PlayMode를 실행합니다. `web` 이미지 빌드가 필요한 경우에만 WebGL도 빌드합니다. API 명세 변경처럼 검사만 필요한 경우에는 WebGL 빌드를 생략합니다.
5. `dev:deliver`가 통과한 JAR·WebGL 산출물의 커밋을 확인하고 선택된 이미지만 만듭니다. 변경 없는 이미지 참조·ID·소스 커밋은 성공 기록에서 재사용합니다.
6. 검증한 이미지와 Compose를 SSH로 전달합니다. 별도 컨테이너 레지스트리와 부모 산출물 다운로드용 유료 기능에 의존하지 않습니다. 부모→자식 설정에는 비밀값이 없는 계획·이미지 메타데이터만 넣습니다.
7. 서버 수신기가 상태 변경·오래된 파이프라인·허용되지 않은 파일을 거절하고, 앱 Compose와 설치된 모니터링 추가 Compose를 함께 사용해 해당 앱 서비스만 교체합니다.
8. 앱·DB health, 이미지 ID, WebGL 버전, 기존 HTTPS API, 인증이 필요한 관리 포트와 새로운 Prometheus 수집 결과를 확인합니다. 성공 후 이미지 변수와 성공 기록을 갱신합니다.

배포 직전에 후보가 여전히 해당 브랜치의 최신 커밋인지 확인합니다. `resource_group`은 부모의 자식 실행 작업에만 걸어 자식 작업과의 교착을 피하고, 서버에서도 파일 잠금과 상태 토큰으로 직렬화합니다. 이미 시작한 배포는 새 Push 때문에 중간 취소하지 않습니다.

## 서버에서 보존하는 구성

- 환경 파일: `/home/ubuntu/apps/a305/dev/deploy.env`.
- 모니터링 추가 Compose: `/home/ubuntu/apps/a305/monitoring/config/compose.observability.yaml`.
- `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `MONITORING_PASSWORD`, `OBSERVABILITY_WEB_LOG_CONFIG`, DB·AI·공급자 비밀값은 서버 파일에 유지합니다. CI에 전달하지 않습니다.
- 웹 로그 설정의 읽기 전용 마운트, 백엔드 내부 관리 주소와 비공개 포트를 유지합니다.
- NPC 음성은 백엔드의 쓰기 가능한 `/app/data/npc-audio`에 저장하고 환경별 `npc-audio-data` 볼륨으로 유지합니다. 백엔드의 AI 내부 주소는 `AI_WORKER_BASE_URL=http://ai-worker:8020`으로 연결합니다.
- 수신기는 NPC 음성 경로·마운트·개발 볼륨 이름과 실행 중인 백엔드의 실제 볼륨을 대조합니다. 누락·읽기 전용·다른 볼륨으로의 변경은 앱 교체 전에 거절하며, 저장 위치 이전은 [음성 저장 운영 기준](npc-audio-storage.md)에 따라 별도로 준비합니다.
- DB 컨테이너·볼륨은 자동 교체하지 않습니다. DB 실행 설정이 달라지면 별도 마이그레이션 절차를 요구합니다.
- 호스트 Nginx·TLS·운영 확인용 서비스·모니터링 스택은 배포 대상에 포함하지 않습니다. 새 WebGL HTTPS 경로의 브라우저 검증은 HTTPS 운영 작업에서 진행합니다.

현재 배포한 모니터링 백엔드의 소스 커밋을 `minimum_backend_commit`으로 지정합니다. 해당 커밋이 없는 후보로 백엔드를 새로 만들면 계획 단계에서 실패합니다. 모니터링 코드를 포함한 선행 MR이 `dev`에 반영된 뒤 자동 배포를 진행합니다.

### Google 로그인 설정 유지

개발 Google 웹 클라이언트의 두 값은 `~/apps/a305/dev/deploy.env`에서 관리합니다. 앱 Compose는 두 값을 백엔드에 필수 환경변수로 전달합니다. 배포 수신기는 이 서버 환경 파일과 릴리스의 `images.env`를 함께 읽고, 성공 시 이미지 변수만 갱신해 Google 설정을 보존합니다. 실패 복구에서도 배포 전 환경 파일을 복원합니다.

수신기는 Google 값 누락·빈 값·`not-configured` 또는 기존 서버 설정과 다른 값이 해석되는 후보를 컨테이너 교체 전에 거절합니다. 클라이언트 값을 바꿀 때는 서버 `deploy.env`를 수정합니다. 후보와 이전 Compose 모두 같은 현재 환경 파일로 해석하므로 정상적인 값 교체는 가능합니다. 실제 값과 인증 JSON 파일은 소스·이미지·CI 변수·산출물에 넣지 않습니다.

Google 콘솔에는 `https://dev.elsehere.kr/api/v1/auth/callback/google`을 승인된 리디렉션 URI로 등록합니다. 환경변수만 바꿀 때는 현재 배포의 Compose·모니터링 추가 파일·환경 파일 조합을 유지하고 백엔드만 `up -d --no-deps --no-build --pull never --force-recreate backend`으로 다시 만듭니다. 단순 `restart`는 바뀐 환경변수를 반영하지 않습니다. 적용 뒤 로그인 시작 응답의 클라이언트 ID와 콜백 주소를 확인하며 Secret과 OAuth 상태 값을 로그에 남기지 않습니다.

## 서버 수신기 설치

아래 명령은 Ubuntu의 `ubuntu` 계정에서 실행합니다. 검증한 전체 커밋을 별도 `deploy-tools` 작업 폴더에 받습니다. 기존 앱 실행 폴더나 환경 파일을 새 소스로 덮어쓰지 않습니다.

```bash
git -C "$HOME/apps/a305/dev/source" fetch origin chore/S15P31A305-37-server-environments
git -C "$HOME/apps/a305/dev/source" worktree add --detach \
  "$HOME/apps/a305/dev/deploy-tools" '<검증한 전체 커밋 SHA>'
cd "$HOME/apps/a305/dev/deploy-tools"
sudo install -d -m 755 /usr/local/lib/a305-dev-deploy
sudo install -m 644 infra/scripts/deployment_server.py /usr/local/lib/a305-dev-deploy/deployment_server.py
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

## 병합 전 연결 시험

1. 선행 모니터링 코드가 반영됐는지 확인하고, 후보의 필수 MR 검사를 통과시킵니다.
2. Windows 노트북 Runner를 켜고 빌드가 끝날 때까지 PC를 유지합니다. 기본 `dev`·`main` 보호와 Runner Protected는 유지합니다.
3. 시험할 때만 정확한 `chore/S15P31A305-37-server-environments` 브랜치를 임시 보호합니다. 병합은 Maintainer, 직접 Push는 금지로 두고 force push를 허용하지 않습니다.
4. 해당 브랜치에서 New pipeline을 열어 `RUN_DEV_DEPLOY=true`로 실행합니다. 수신기 설정의 `pilot_branch`도 같은 정확한 브랜치여야 합니다.
5. 부모의 계획 작업, 자식의 선택된 검사·이미지 전달·실제 서버 점검과 성공 기록을 확인합니다. 첫 자동 시험은 모든 앱을 검증·빌드하므로 노트북에서 실제 WebGL을 새로 빌드합니다.
6. 시험 뒤 임시 보호를 제거합니다. 수정이 필요하면 임시 보호를 먼저 해제하고 새 후보를 검증합니다.

시험용 후보도 실제 개발 앱을 교체합니다. 운영을 배포하는 작업은 아닙니다. 후보 시험·서버 성공 확인·실패 복구 검증이 끝난 뒤 Draft를 해제하고 팀원 리뷰를 받습니다. 이 절차가 `dev` 병합이나 작성자 자신의 승인을 대신하지 않습니다.

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
