# main 운영 배포와 복구

보호된 `main`에 병합하면 전체 검사와 필요한 빌드를 거쳐 운영 앱을 자동 배포합니다. 운영 앱을 처음 기동하는 작업과 이후 이미지 교체를 구분합니다. 최초 앱·DB·운영 수집과 수신기 초기화를 마치기 전에는 자동 배포를 시작할 수 없으며 운영은 확인용 서비스를 유지합니다.

## 배포 조건과 흐름

일반 Linux 검사는 EC2와 노트북이 나눠 받지만 `prod:plan`·`prod:deliver`는 `a305-ec2` 태그를 추가로 요구해 EC2에서만 실행합니다. Runner 태그와 노트북 제한은 [검사 파이프라인](ci-runner.md#노트북-linux-runner)을 따릅니다.

1. `dev → main` 릴리스 MR을 병합하기 전에 [운영 통합 검증](ci-cd.md#4-운영-배포-전-통합-테스트-기준)을 완료합니다. 별도 통합 환경에서 로그인·권한·대화·음성·결과 저장과 실패 처리를 확인하고 검증한 코드·이미지·설정을 기록합니다.
2. 운영 DB의 스키마·이전 앱 호환성, 백업과 격리 DB 복원 결과를 릴리스 MR에 기록합니다. 앱 시작 시 Flyway가 검토된 `db/migration` SQL을 버전 순서대로 적용하고 JPA가 `JPA_DDL_AUTO=validate`로 검사합니다. 필요한 설정과 마이그레이션의 적용 순서를 병합 전에 확인합니다. 앱 복구로 DB는 되돌아가지 않으므로 이전 앱이 새 스키마에서도 동작해야 합니다. 호환되지 않는 변경은 별도 적용 순서와 중지 계획을 확정한 뒤 릴리스합니다.
3. [릴리스 절차](development.md#릴리스와-긴급-수정)에 따라 리뷰 후 보호된 `main`에 병합합니다. 병합으로 발생한 Push 파이프라인이 `prod:plan`을 자동 실행합니다. 별도 변수 입력이나 배포 버튼은 필요하지 않습니다. 직접 Push를 막고 리뷰·병합 권한을 제한하는 브랜치 보호 정책을 유지합니다.
4. 마지막 운영 성공 커밋과의 누적 차이로 이미지 빌드 범위를 정합니다. `prod:child`는 운영 배포를 직렬화하며 변경 영역과 관계없이 백엔드·AI·Unity Editor·인프라·앱 실행·관측 검사를 모두 수행합니다. 부모의 중복 검사는 생략하고 실제 `main` 커밋을 자식에서 검사합니다. WebGL은 이미지 입력이 바뀐 경우에만 새로 빌드합니다. 재사용 이미지의 ID와 실제 빌드 소스도 성공 기록에 남습니다.
5. 모든 필수 검사가 성공하면 `prod:deliver`가 검증한 이미지만 자동 전달합니다. 전달 전 후보가 여전히 최신 `main`인지 확인하고, 서버는 상태 토큰·환경 이름·파이프라인 순서를 확인합니다. 개발 키나 개발 번들은 운영 수신기에서 거절합니다.
6. 운영 Compose·환경 파일·관측 및 운영 워커 네트워크 추가 Compose로 필요한 앱만 교체합니다. DB·호스트 Nginx·인증서·모니터링 스택은 교체하지 않습니다.
7. 컨테이너 상태·이미지 ID·DB health·HTTPS·공개 WebGL 버전·인증된 관리 포트·운영 Prometheus 수집을 확인합니다. 모든 점검과 이미지 보관에 성공해야 성공 기록을 갱신합니다. 실패 시 이전 앱 복구를 시도하며 성공 기록을 유지합니다.

자동 CI의 정적 WebGL fixture는 실제 브라우저 학습 흐름 검증을 대신하지 않습니다. MR에 기록한 통합 검증·DB 복원 시험과 동료 리뷰는 병합 전에 완료해야 합니다. 검증 후 코드·이미지·설정이 달라졌다면 필요한 검증을 다시 수행합니다.

### 수동 재실행

자동 배포가 실패한 뒤 원인을 해결하고 현재 `main`을 다시 배포할 때는 GitLab의 **Build → Pipelines → New pipeline**에서 보호된 `main`과 아래 변수를 지정합니다. 이 경로도 전체 검사 후 자동 전달하며 별도 `prod:plan` 수동 버튼은 없습니다. 비밀값은 넣지 않습니다.

   | 변수 | 값 |
   | --- | --- |
   | `RUN_PROD_DEPLOY` | `true` |
   | `PROD_VERIFIED_COMMIT` | 통합 검증을 완료한 실제 `main` 전체 SHA |
   | `PROD_RELEASE_MR` | 해당 릴리스 MR의 전체 URL |
   | `PROD_DB_COMPATIBLE` | 백업·복원 및 이전 앱과의 DB 호환성을 확인한 경우에만 `true` |

위 변수는 수동 재실행에만 필요하며 검증된 실제 `main` SHA와 일치해야 합니다. 운영 환경 보호 기능을 사용할 수 있다면 `production`의 배포 허용자를 Maintainer로 제한합니다. 운영 변경·병합 권한과 자동화 실행 권한을 함께 관리합니다.

## 운영 환경 분리

| 항목 | 운영 값 |
| --- | --- |
| 브랜치·Compose 프로젝트 | `main`·`a305-prod` |
| 웹 포트 | `127.0.0.1:8080` |
| 앱 환경 파일 | `/home/ubuntu/apps/a305/prod/deploy.env` |
| DB·음성·로그 볼륨 | `a305-prod_database-data`·`a305-prod_npc-audio-data`·`a305-prod_backend-logs` |
| 수신기·설정 | `/usr/local/lib/a305-prod-deploy/deployment_server.py`·`/etc/a305-prod-deploy.json` |
| 상태·버전 보관 | `/home/ubuntu/apps/a305/prod/deployments/` |
| 대표 주소 | `https://elsehere.kr/` |

`infra/compose/deploy.prod.env.example`의 필수 값을 준비합니다. DB 계정·비밀번호, AI 내부 토큰과 모니터링 비밀번호는 개발과 분리합니다. Google OAuth 클라이언트는 개발·운영이 공용으로 사용하되 각각의 환경 파일에 연결하고 두 환경의 콜백을 등록합니다. 클라이언트 Secret을 교체할 때는 양쪽 환경에 함께 반영합니다. 환경 파일은 `ubuntu` 소유·권한 `600`으로 유지하고, 모니터링의 `MONITORING_PASSWORD`·`OBSERVABILITY_WEB_LOG_CONFIG`도 서버 파일에 설정합니다. 실제 값은 소스·MR·Jira·CI 산출물에 넣지 않습니다.

GPU 연결 주소는 운영 게이트웨이 `172.30.0.1`을 사용합니다. 같은 GPU 서비스를 호출하는 `STT_GPU_TOKEN`·`GUARD_GPU_TOKEN`·`TTS_GPU_TOKEN`은 개발과 같은 값을 서버에서 설정하며, 환경별 `AI_WORKER_TOKEN`과 구분합니다. 예제에는 토큰을 넣지 않습니다. GPU를 사용하지 않을 때는 `STT_CHAIN`·`STT_GPU_BASE_URL`·`GUARD_GPU_MODEL`·`GUARD_GPU_BASE_URL`·`TTS_GPU_BASE_URL`을 비워 기존 API·CPU 경로를 사용합니다. 고정 IP·터널·방화벽과 실제 GPU 경로 검증은 [GPU 워커 네트워크 운영](gpu-worker-network.md)을 따릅니다.

Google에는 `https://elsehere.kr/api/v1/auth/callback/google`을 승인된 리디렉션 URI로 등록합니다. `www.elsehere.kr`은 대표 도메인으로 리다이렉트하거나 별도 사용 시 해당 콜백도 등록합니다. [Nginx·HTTPS 운영 기준](server-operations.md#nginx-변경과-외부-접속-확인)의 기존 TLS 설정과 개발 경로를 보존합니다.

## 최초 앱 전환

운영 수신기의 초기화에는 실제 운영 앱 4개가 필요합니다. 확인용 컨테이너를 초기 성공 앱으로 등록할 수 없습니다. 최초 전환은 아래 순서로 담당자가 수행한 후 반복 배포를 연결합니다.

1. 검증된 전체 `main` 커밋의 소스와 CI의 JAR·AI 소스·실제 WebGL 산출물을 빌드용 PC 또는 Runner에 준비합니다. 최초 설치에서는 수신기가 아직 초기화되지 않아 자동 배포 계획이 중단됩니다. 이때 보호된 `main`의 웹 파이프라인에서 `RUN_UNITY_BUILD=true`만 지정하고 `RUN_PROD_DEPLOY`는 생략하여 배포 없이 전체 검사·산출물 생성을 수행합니다. JAR·AI의 `commit.txt`와 WebGL `version.json`이 해당 커밋을 가리키는지 확인합니다. [개발 배포의 이미지 빌드 기준](dev-auto-deployment.md)을 동일하게 적용하여 `infra/docker/`의 Dockerfile로 `a305/backend:<SHA>`, `a305/ai:<SHA>`, `a305/web:<SHA>`를 만들고 이미지 ID를 기록합니다. `docker save`로 운영 서버에 전달합니다. 검증되지 않은 최신 개발 이미지 태그로 대체하지 않습니다.
2. 운영 전용 `deploy.env`, 앱 Compose, 설치된 관측 및 운영 워커 네트워크 추가 Compose의 해석을 점검합니다. [GPU 워커 네트워크 운영](gpu-worker-network.md)에 따라 외부 네트워크를 먼저 준비합니다. DB와 음성 볼륨의 이름, DB·AI 비공개 포트, 루프백 `8080`, OAuth·내부 토큰과 관리 포트를 확인합니다. 이미지·설정·필수 변수와 브라우저 경로를 격리된 검증 환경에서 먼저 확인합니다.
3. 운영 DB만 먼저 준비하고, 새 빈 DB인지 확인합니다. 검증된 릴리스의 Flyway 마이그레이션과 기본 데이터 초기화를 격리된 PostgreSQL 16에서 먼저 확인합니다. 새 DB에는 V1부터 적용되며 기존 비어 있지 않은 DB는 `baseline-on-migrate`로 V1 상태로 인정될 수 있으므로, 이력 없이 운영 DB를 인계할 때는 V1과의 실제 일치 여부를 먼저 검증합니다. 이전에 엔티티에서 생성한 초기 SQL을 별도로 덧씌우거나 개발 DB 복사·운영 `update` 실행으로 이 절차를 대신하지 않습니다.
4. 기존 운영 확인용 Compose와 환경 파일·Nginx 설정을 보관합니다. 전환 작업에서 `environment-check` 서비스만 중지한 뒤, 검증된 운영 앱 Compose와 관측 및 운영 워커 네트워크 추가 Compose로 DB·AI·백엔드·웹을 기동합니다. 전체 프로젝트 `down`, `--remove-orphans`, 볼륨 삭제는 사용하지 않습니다. 최초 전환 실패 시 운영 앱만 중지하고 기존 확인용 서비스를 다시 기동합니다. 새 운영 DB·음성 볼륨은 보존합니다.
5. 호스트의 운영 도메인 루트·`/api/`·`/version.json`을 운영 웹으로 연결합니다. 필요한 location만 검토·수정하고 `nginx -t` 후 reload합니다. 운영·개발 HTTPS, gzip·Wasm MIME·SSE·로그인과 대표 브라우저 흐름을 함께 확인합니다.
6. [관측 운영 문서](observability.md)에 따라 운영 모니터링 secret과 `a305-prod-backend-1:9090`, `environment: prod` 수집 대상을 등록합니다. 기본 `api-prod.json`은 비어 있으므로 설치된 서버 설정을 별도로 갱신합니다. 수집 작업은 `elsehere-api-prod`입니다. 공유 스택은 필요한 변경만 반영하고 개발 수집도 재확인합니다.
7. 아래 수신기를 초기화합니다. 초기화는 현재 이미지·설정을 보관하고 `bootstrap.json`만 만들며 CI 성공을 주장하지 않습니다. 이후 운영 수동 파이프라인을 실행해 첫 CI 성공 기록을 만듭니다.

수동 앱 기동 명령에는 항상 다음 조합을 사용합니다. 이미지 참조·필수 값과 마이그레이션 검증이 이미 준비된 경우의 예제입니다. 백엔드 시작 시 Flyway가 미적용 마이그레이션을 실행하고 JPA 검사와 기본 데이터 초기화가 이어집니다.

```bash
sudo docker compose -p a305-prod \
  --env-file "$HOME/apps/a305/prod/deploy.env" \
  -f "$HOME/apps/a305/prod/compose.app.yaml" \
  -f "$HOME/apps/a305/monitoring/config/compose.observability.yaml" \
  -f "$HOME/apps/a305/prod/config/compose.prod-worker-network.yaml" config --quiet
```

## 전용 수신기와 키 설치

아래는 서버의 `ubuntu` 계정에서 검토된 소스 폴더를 기준으로 실행합니다. 기존 개발 수신기·개인 PEM·공개키 항목은 유지합니다.

```bash
sudo install -d -m 755 /usr/local/lib/a305-prod-deploy
sudo install -m 644 infra/scripts/deployment_server.py \
  /usr/local/lib/a305-prod-deploy/deployment_server.py
sudo install -m 644 infra/scripts/dev_worker_network.py \
  /usr/local/lib/a305-prod-deploy/dev_worker_network.py
install -d -m 700 "$HOME/apps/a305/prod/config"
install -m 600 infra/compose/compose.prod-worker-network.yaml \
  "$HOME/apps/a305/prod/config/compose.prod-worker-network.yaml"
cp infra/deploy/prod-server.example.json "$HOME/apps/a305/prod/server-config.json"
nano "$HOME/apps/a305/prod/server-config.json"
```

설정의 `minimum_backend_commit`에는 실제 배포 앱에 필요한 관측 코드의 전체 SHA를, `bootstrap_sources`에는 실행 중인 각 이미지의 검증된 소스 SHA를 적습니다. Compose·관측 경로는 현재 서버와 맞춥니다. 설정의 `environment`는 `prod`를 유지하며 운영 배포는 보호된 `main` 브랜치만 허용합니다.

`overlays`에는 관측 설정과 운영 워커 네트워크 파일을 모두 유지합니다. 기존 운영을 전환할 때도 같은 중단 창에서 `/etc/a305-prod-deploy.json`을 갱신해야 다음 자동 배포에 고정 IP가 유지됩니다. `fixed_worker_network`는 개발 전용 검사이므로 운영에는 추가하지 않습니다. 현재 수신기는 운영 고정 IP를 자동 검사하지 않으므로 배포 후 `docker inspect`로 실제 IP와 `IPAMConfig.IPv4Address`를 확인합니다.

```bash
sudo install -m 644 "$HOME/apps/a305/prod/server-config.json" /etc/a305-prod-deploy.json
python3 /usr/local/lib/a305-prod-deploy/deployment_server.py \
  --config /etc/a305-prod-deploy.json --initialize
```

실제 앱·HTTPS·운영 수집이 정상이어야 초기화가 성공합니다. 현재 앱 이미지 전체를 압축해 보관할 디스크 공간도 필요합니다. 기존 초기화 상태를 덮어쓰지 않으며, 이미 상태가 있으면 성공·실패 기록부터 확인합니다.

```bash
ssh-keygen -t ed25519 -f "$HOME/.ssh/a305-prod-deploy" -C a305-prod-ci -N ''
if ! grep -qF "$(cat "$HOME/.ssh/a305-prod-deploy.pub")" "$HOME/.ssh/authorized_keys"; then
  printf 'restrict,command="/usr/bin/python3 /usr/local/lib/a305-prod-deploy/deployment_server.py --config /etc/a305-prod-deploy.json" %s\n' \
    "$(cat "$HOME/.ssh/a305-prod-deploy.pub")" >> "$HOME/.ssh/authorized_keys"
fi
chmod 600 "$HOME/.ssh/authorized_keys"
awk '{print "k15a305.p.ssafy.io " $1 " " $2}' /etc/ssh/ssh_host_ed25519_key.pub \
  > "$HOME/apps/a305/prod/deploy-known-hosts"
ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub
```

같은 이름의 키가 이미 있으면 덮어쓰지 않고 기존 등록을 확인합니다. 강제 명령 키에는 `state`·`apply`만 허용하며 일반 셸·SCP·터널·수동 롤백은 허용하지 않습니다. 수동 복구는 담당자 개인 SSH 접속으로 수행합니다.

GitLab 프로젝트 변수에 아래 두 개를 **File·Protected·환경 범위 `production`·변수 확장 해제**로 등록합니다. 여러 줄 개인 키의 저장 형식과 고정 호스트 키 검증은 [개발 키 설치 기준](dev-auto-deployment.md#전용-ssh-키와-gitlab-변수)을 따르되 개발 키를 재사용하지 않습니다. `prod:plan`의 환경 동작은 `prepare`이므로 키를 읽을 수 있지만 배포 성공 기록은 만들지 않습니다. [GitLab 환경별 변수](https://docs.gitlab.com/ci/environments/#limit-the-environment-scope-of-a-cicd-variable)

| 변수 | 값 |
| --- | --- |
| `PROD_DEPLOY_SSH_KEY` | 운영 전용 개인 키 전체, 마지막 LF 포함 |
| `PROD_DEPLOY_KNOWN_HOSTS` | 서버에서 확인한 운영 배포용 호스트 키 파일 전체 |

설치 후 다음 읽기 전용 요청이 `environment: prod`를 반환하는지 확인합니다.

```bash
ssh -T -i "$HOME/.ssh/a305-prod-deploy" \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile="$HOME/apps/a305/prod/deploy-known-hosts" \
  ubuntu@k15a305.p.ssafy.io state
```

## 보관과 실패 복구

성공 릴리스의 폴더에는 `compose.app.yaml`·`images.env`·`record.json`·앱 이미지 전체 `images.tar.gz`를 보관합니다. 변경 없는 이미지도 함께 보관하므로 이전 릴리스가 Docker 캐시에서 제거되어도 복구할 수 있습니다. `last-success.json`은 현재 버전, `previous-success.json`은 직전 버전입니다. 초기화 시점의 실제 앱도 별도 `bootstrap/`에 보관합니다. 실제 비밀값은 이 폴더에 복제하지 않으며 환경 파일은 서버에서 따로 관리합니다.

CI에는 후보 manifest와 결과를 30일간 보관합니다. 서버의 이미지 아카이브는 자동 삭제하지 않습니다. 디스크를 감시하고 최소 현재·직전 성공·초기 복구본과 진행 중인 후보는 보존합니다. 더 오래된 버전을 정리하기 전에는 기록·Compose·아카이브를 서버 외부에 백업하고 격리 환경에서 로드·기동을 확인합니다. 일반 `docker prune`으로 보관 정책을 대신하지 않습니다.

배포 중 앱·공개 버전·관측 검사 또는 산출물 보관이 실패하면 같은 환경의 이전 이미지·Compose와 배포 전 환경 파일로 복구를 시도합니다. `last-success.json`은 갱신하지 않으며 `last-failure.json`의 `restored`가 복구 점검 결과입니다. `false`이면 후속 배포를 중지하고 담당자가 복구합니다. 최초 확인용 서비스에서 앱으로 바꾸는 작업은 이 자동 복구의 대상이 아니며 최초 전환 절차를 따릅니다.

배포 후 발견한 문제로 직전 앱 버전으로 돌아가려면 먼저 현재 SHA, 백업·DB 호환성과 관측 설정을 확인합니다. 운영 서버의 개인 SSH 세션에서 실행합니다.

```bash
python3 /usr/local/lib/a305-prod-deploy/deployment_server.py \
  --config /etc/a305-prod-deploy.json \
  --rollback-from '<현재 운영 전체 SHA>' \
  --confirm-database-compatible
```

현재 SHA가 달라졌거나 직전 이미지 아카이브가 없거나 관측 추가 설정이 달라졌다면 자동 진행하지 않습니다. 직전 앱을 로드하고 운영 앱 3개만 교체해 동일한 상태·공개 버전·수집 검사를 수행합니다. 복구 자체가 실패하면 복구 전 앱으로 되돌리며 실패 기록을 남깁니다. 성공해도 파이프라인 순서의 상한은 유지하여 대기 중인 오래된 배포가 복구를 덮어쓰지 않게 합니다.

이 작업은 DB 스키마·사용자 데이터·음성 파일을 과거로 되돌리지 않습니다. 이전 앱과 호환되지 않는 DB 변경이면 별도 승인한 SQL 복구·백업 복원 또는 전진 수정이 필요합니다. 백업에는 암호화·접근 제한과 운영 외 보관을 적용하고, 복원은 격리된 PostgreSQL 16에서 검증합니다. 운영 DB를 실제 복원하는 것은 별도 작업으로 진행합니다.

복구 뒤에는 마지막 성공 기록의 앱 SHA·이미지 ID와 다음 응답을 대조하고 브라우저의 대표 읽기 흐름을 점검합니다. 다시 배포할 때는 최신 `main`의 검증 결과를 확인하고 새 파이프라인을 실행합니다. 이전 전달 작업만 재시도하면 상태 토큰이 달라 거절됩니다.

```bash
cat "$HOME/apps/a305/prod/deployments/last-success.json"
cat "$HOME/apps/a305/prod/deployments/last-failure.json"
curl --fail https://elsehere.kr/api/health
curl --fail https://elsehere.kr/version.json
sudo docker ps --filter label=com.docker.compose.project=a305-prod \
  --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}'
```
