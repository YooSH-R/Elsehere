# 요청 추적과 API 모니터링

Nginx와 Spring Boot에서 같은 HTTP 요청 ID로 요청을 찾고, Prometheus와 Grafana에서 API 요청량·오류율·지연, 공개 HTTPS 응답과 인증서 만료일을 확인합니다. Alertmanager는 서버 내부 경보의 발동·복구를 관리하고, Grafana Cloud는 독립된 위치에서 공개 HTTPS를 감시합니다.

개발 앱에는 요청 로그·지표·대시보드와 승인된 Mattermost 경로를 적용합니다. 운영은 환경 확인용 서비스이므로 앱 지표 수집 대상에서 제외합니다. 외부 검사의 위치·대상·경보는 [독립 외부 감시 문서](external-monitoring.md)에서 관리합니다.

저장소의 기본 알림 템플릿은 발송을 비활성화한 상태입니다. 새 수신 경로를 연결하거나 기존 채널을 바꿀 때는 [Mattermost 발송 승인](#mattermost-발송-승인)을 따릅니다.

## 요청 ID와 기록 범위

- 공개 Nginx는 모든 요청에 32자리 소문자 16진수 `X-Request-ID`를 새로 생성합니다. 클라이언트가 보낸 같은 이름의 헤더는 덮어씁니다.
- 내부 프록시는 이 헤더를 유지합니다. Spring은 해당 형식의 값만 수용하고, 값이 없거나 형식이 잘못되면 새 ID를 생성합니다. 백엔드 포트는 공개하지 않습니다.
- Spring은 보안 필터보다 먼저 요청 문맥에 ID를 넣고 응답에도 반환합니다. 요청이 끝나거나 예외가 발생하면 이전 문맥을 복원합니다.
- Spring 요청 로그에는 HTTP 메서드, 컨트롤러의 경로 패턴, 상태 코드와 처리 시간만 남깁니다. 컨트롤러에 도달하지 않은 요청은 `/unmatched`로 기록합니다.
- Spring의 모든 로그 줄에는 `request_id`와 함께 로그인 사용자의 내부 ID(`users.id`)가 `user_id`로 붙습니다. 운영 중 한 사용자의 요청·오류를 이어서 찾기 위한 값이며, 로그인 전 요청과 요청 밖 작업(예약 작업 등)은 `user_id=none`입니다. 보안 필터가 인증을 마친 뒤 넣고 요청이 끝나면 지웁니다. 이메일·닉네임·소셜 계정 ID 같은 개인정보는 넣지 않습니다. 비동기 작업(@Async)에는 아직 전달하지 않습니다.
- Nginx는 원본 URL 대신 `/api/**`, `/api/v1/auth/**`, `/webgl/**` 등 고정된 경로 그룹을 기록합니다. 쿼리, 토큰, 쿠키, IP·사용자 식별자와 음성·대화 본문은 요청 요약에 넣지 않습니다.
- 요청 보호용 `rate_limit`·`connection_limit` 필드는 판정 상태만 기록합니다. 관찰과 실제 차단의 구분·설치 조건은 [Nginx 요청 보호 정책](traffic-protection.md)을 따릅니다.
- 처리하지 못한 Spring 예외는 종류와 코드 위치만 기록합니다. 외부 응답이나 사용자 입력을 포함할 수 있는 원본 예외 메시지는 기록하지 않습니다.
- 백엔드 애플리케이션 로그(`com.elsehere`)는 운영 INFO, 개발 서버 DEBUG입니다. 서버 `deploy.env`의 `BACKEND_LOG_LEVEL`로 정하며 없으면 INFO입니다. 라이브러리 로그는 INFO로 둡니다. 설정 방법은 [백엔드 README](../backend/README.md#요청-추적과-관리-지표)를 따릅니다.

이 헤더는 전송 요청의 추적용입니다. 중복 처리를 방지하는 API 본문의 UUID `requestId`와는 용도가 다르며, 재시도 요청은 같은 본문 ID를 사용해도 새로운 HTTP 추적 ID를 받습니다.

저장소의 [AiWorkerClient](../backend/src/main/java/com/elsehere/ai/client/AiWorkerClient.java)는 HTTP 요청 문맥의 추적 ID를 워커의 `X-Request-Id`로 전달합니다. HTTP 요청 문맥이 없으면 `WorkerTrace`의 UUID를 사용합니다. 실제 배포본에서도 Spring과 워커의 로그를 같은 ID로 찾을 수 있는지 확인합니다.

### 백엔드 업무 흐름 로그

운영 중 한 사용자에게 일어난 일을 따라갈 수 있도록 양이 적고 중요한 업무 흐름을 남깁니다. 요청 요약(`http_request …`)과 같이 `이벤트명 키=값` 형식이고 `request_id`가 함께 붙습니다. ID와 코드값만 남기며 대화 본문·음성·인증값·이메일·닉네임은 남기지 않습니다. 응답 시간·호출 수 같은 수치는 로그가 아니라 Prometheus 지표와 `ai_requests` 기록으로 봅니다. `ai_requests`에 쌓인 AI 사용량은 [어드민 AI 사용량 화면](admin.md#ai-사용량)에서 봅니다.

| 레벨 | 이벤트 | 키 | 남기는 때 |
| --- | --- | --- | --- |
| INFO | `conversation_started` | `conversation_id`, `npc`, `initiated_by` | 대화 시작(CONV-01) 저장 뒤 |
| INFO | `conversation_ended` | `conversation_id`, `status`, `end_reason`, (사용자 종료면) `user_turns` | 사용자가 끝냄(CONV-08), 무례 누적으로 NPC가 끝냄(CONV-03). 이미 끝난 대화를 다시 끝내면 남기지 않음 |
| WARN | `feedback_failed` | `conversation_id`, `reason` | 짧은 피드백을 만들지 못함(AI 시간 초과 등) |
| INFO | `feedback_skipped` | `conversation_id`, `reason` | 피드백을 건너뜀. 사용자가 건너뛰면 `USER_SKIPPED` |
| INFO | `guest_conversation_limit_reached` | `user_id`, `limit` | 게스트 대화 한도로 대화 시작을 거절 |
| INFO | `user_signed_up` | `user_id`, `provider` | 가입 완료(AUTH-04) |
| INFO | `guest_started` | `user_id` | 게스트 계정 생성(AUTH-07) |
| INFO | `user_withdrew` | `user_id` | 회원 탈퇴(USER-05) |
| DEBUG | `ai_worker_call` | `path`, `status`, `duration_ms` | AI 워커 호출마다(개발 서버에서만 보임) |
| DEBUG | `business_error` | `code`, `status` | 업무 오류 응답마다(개발 서버에서만 보임) |

이 밖의 실패 상황(AI 워커 시간 초과·연결 실패, NPC 음성 저장 실패, 기억 카드·관계 갱신 실패 등)은 기존처럼 WARN으로 남깁니다. 운영 백엔드의 로그 파일은 JSON(ECS)으로 남기며([조회·보관·복구](#조회보관복구)), 로그를 모으는 도구는 아직 정하지 않았습니다.

## Nginx 설정 적용

HTTPS 인증서와 기존 location을 보존합니다. 전체 사이트 설정을 예제로 덮어쓰지 않습니다.

1. `infra/nginx/observability-http.conf`를 `/etc/nginx/conf.d/a305-observability.conf`에 설치합니다. 이 파일은 `http` 범위의 map과 로그 형식을 정의합니다.
2. `infra/nginx/observability-server.conf`를 `/etc/nginx/snippets/a305-observability.conf`에 설치합니다.
3. 활성 사이트의 A305 HTTP·HTTPS `server` 블록마다 아래 한 줄을 적용합니다. 현재 도메인별 사이트는 `/etc/nginx/sites-available/a305-elsehere`, 기존 SSAFY 주소 사이트는 `/etc/nginx/sites-available/a305`입니다. `/etc/nginx/sites-enabled/`의 연결을 확인하고 다른 사이트는 변경하지 않습니다.

```nginx
include /etc/nginx/snippets/a305-observability.conf;
```

4. 각 `location`에 별도 `proxy_set_header` 또는 `add_header`가 있으면 상위 설정의 상속 여부를 확인하고 요청 ID 전달·응답을 해당 위치에도 반영합니다. 내부 웹 프록시는 요청 ID를 재생성하지 않습니다.
5. Certbot이 만든 HTTP 블록에서도 `return`보다 먼저 환경 구분과 include가 적용되게 합니다. `sudo nginx -t` 성공 후 reload하고 개발·운영의 정상·오류 응답에서 헤더와 로그가 일치하는지 확인합니다.

새 요청 형식은 기존 `/var/log/nginx/access.log`를 사용합니다. 프로젝트의 HTTP 요청 오류는 안전한 access 요약과 백엔드 오류 로그로 확인합니다. Nginx 자체 error 로그는 URL을 가릴 수 없어 사이트의 기록 수준을 `crit`으로 제한합니다. Nginx 기동·설정 장애는 `nginx -t`와 서비스 journal도 함께 확인합니다. 기본 원본 error 메시지는 구조화된 요청 로그와 같은 개인정보 제거 기능을 제공하지 않으므로 접근 권한을 유지합니다.

앱의 내부 웹 Nginx는 `infra/nginx/observability-web.conf`로 원본 access 로그를 끄고 error 수준을 `crit`으로 제한합니다. 앱 Compose에 `infra/compose/compose.observability.yaml`을 함께 적용하면 이 설정이 추가 마운트됩니다. 기존 웹 이미지와 API·정적 파일 설정은 유지하며, 요청 ID는 내부 프록시에서 그대로 전달합니다.

### 조회·보관·복구

```bash
sudo grep -F '"request_id":"<응답 헤더의 ID>"' /var/log/nginx/access.log
sudo docker logs --since=10m a305-dev-backend-1 2>&1 | grep -F 'request_id=<같은 ID>'
```

서버에서 실행하는 예시입니다. 컨테이너 이름은 `sudo docker ps --format '{{.Names}}'`로 확인합니다. 로그 조회는 앱 Compose 파일의 설치 위치와 관계없이 실행할 수 있습니다.

`docker logs`는 컨테이너를 새로 만들면 비워지므로, 배포 전 기록은 백엔드 로그 파일에서 찾습니다. 파일은 환경별 `backend-logs` 볼륨(`a305-dev_backend-logs`, `a305-prod_backend-logs`)에 있습니다. 개발 서버 파일은 콘솔과 같은 텍스트, 운영 파일은 요청 ID가 `"requestId"`(로그인 사용자는 `"userId"`)로 들어간 JSON입니다.

```bash
logs=$(sudo docker volume inspect --format '{{.Mountpoint}}' a305-dev_backend-logs)
sudo grep -F 'request_id=<같은 ID>' "$logs/backend.log"
sudo zgrep -F 'request_id=<같은 ID>' "$logs"/backend.log.*.gz
logs=$(sudo docker volume inspect --format '{{.Mountpoint}}' a305-prod_backend-logs)
sudo grep -F '"requestId":"<같은 ID>"' "$logs/backend.log"
```

- Nginx 로그는 `www-data:adm`, `0640` 권한과 기존 `/etc/logrotate.d/nginx`를 유지합니다. 매일 확인하고 최대 14회분을 보관하며 압축은 한 회차 지연합니다. `notifempty`이므로 정확히 14일 보관과 같지는 않습니다.
- 백엔드·웹·DB·AI 및 모니터링 컨테이너 로그는 `json-file`, 파일당 10MiB·최대 3개를 기준으로 관리합니다. Docker 로그 파일을 호스트 logrotate로 다시 순환시키지 않습니다.
- 백엔드 로그 파일은 Spring Boot(Logback)가 직접 나눕니다. 하루마다·10MB마다 압축하고 14일, 압축본 합계 1GB를 넘으면 오래된 것부터 지웁니다. 호스트 logrotate를 쓰지 않습니다. 설정은 [백엔드 README](../backend/README.md#요청-추적과-관리-지표)를 따릅니다.
- `backend-logs` 볼륨은 배포·롤백에서 유지합니다. `docker compose down --volumes`나 볼륨 삭제로 지우지 않습니다. 로그는 복구용 데이터가 아니므로 DB 백업 대상에 넣지 않습니다. 개발 자동 배포 수신기는 NPC 음성 볼륨만 검사하고 이 볼륨은 검사하지 않습니다.
- 로그 접근은 서버 관리 담당자로 제한하고, 공유 자료에는 필요한 ID와 요약만 남깁니다. 로그·대시보드에 원문을 붙이지 않습니다.
- Nginx 반영 전 원본 사이트 설정을 백업합니다. 문제 시 사이트 설정을 복원하고 `nginx -t` 성공 후 reload합니다. 기존 TLS와 인증서 갱신 설정은 보존합니다.

## 관리 포트와 인증

| 설정 | 기본값 | 운영 기준 |
| --- | --- | --- |
| `MANAGEMENT_ADDRESS` | `127.0.0.1` | 컨테이너 내부 수집을 사용할 때만 `0.0.0.0`으로 지정 |
| `MANAGEMENT_PORT` | `9090` | 호스트에 게시하거나 Nginx 공개 경로로 전달하지 않음 |
| `MONITORING_PASSWORD` | 없음 | 환경별로 별도 관리. 비어 있으면 관리 사용자를 만들지 않아 접근 거절 |
| 관리 사용자 | `monitoring` | 앱 사용자 세션과 분리된 HTTP Basic 인증 |

관리 포트에서는 인증된 `GET /actuator/health`, `GET /actuator/prometheus`만 허용합니다. health의 내부 연결 상세는 노출하지 않습니다. 앱의 기존 인증 규칙은 유지합니다.

기존 컨테이너·공개 프록시의 `GET /api/health`는 앱 포트에서 계속 사용할 수 있습니다. 같은 Actuator health 결과의 `status`만 반환하며 DB 등 필수 연결이 비정상이면 HTTP 503을 반환합니다. 하위 경로나 다른 관리 지표를 공개하지 않습니다. 관리 포트를 분리한 뒤에도 이 경로를 제거하거나 로그인 필수로 바꾸지 않습니다.

검토된 앱 Compose와 [관측 추가 파일](../infra/compose/compose.observability.yaml)을 함께 사용합니다. 현재 `dev`에는 관측 추가 파일만 있으며, 기반 앱 Compose는 앱 배포 구성에서 별도로 제공합니다. 저장소 최상위의 `infra/compose/compose.app.yaml`이 존재한다고 가정해 명령을 실행하지 않습니다. 서버에서 실행 중인 앱의 기반 Compose 위치는 다음 명령으로 확인합니다. 출력에 관측 추가 파일도 포함되어야 합니다.

```bash
sudo docker inspect --format '{{ index .Config.Labels "com.docker.compose.project.config_files" }}' a305-dev-backend-1
```

추가 파일의 서버 위치는 `~/apps/a305/monitoring/config/compose.observability.yaml`입니다. 이 파일은 백엔드 관리 주소·비밀번호와 웹의 로그 설정만 주입하며 포트를 게시하지 않습니다. `OBSERVABILITY_WEB_LOG_CONFIG`는 서버의 `observability-web.conf` 절대 경로입니다. 배포 명령과 자동화에서도 기반 파일 뒤에 이 추가 파일을 적용해 기존 수집·로그 설정을 보존합니다. 환경별 `deploy.env`의 비밀번호는 아래 수집용 비밀번호 파일과 일치해야 합니다.

메트릭은 Spring의 경로 패턴·메서드·상태 코드 등을 사용합니다. 요청 ID, 사용자 ID, 원본 동적 URL을 라벨에 추가하지 않습니다. `http.server.requests` histogram으로 p95를 계산합니다. AI 호출의 단계별 지연·사용량에 대한 Prometheus 계측과 대시보드는 별도 구현 범위입니다.

## Prometheus·Grafana 실행

선행 조건은 `a305-dev`·`a305-prod` 네트워크와 활성화할 백엔드의 인증된 관리 포트입니다. `targets/api-dev.json`은 `a305-dev-backend-1:9090`을 수집합니다. `targets/api-prod.json`은 기본적으로 비어 있습니다. 실제 운영 앱을 배포하고 관리 포트를 검증한 뒤 `a305-prod-backend-1:9090`과 `environment: prod`를 등록합니다. 환경 확인용 서비스는 앱 수집 대상으로 등록하지 않습니다.

- 설정: `infra/monitoring/`
- 서버 실행 폴더: `~/apps/a305/monitoring/config/`
- 서버 비밀 파일: `~/apps/a305/monitoring/secrets/`
- Compose 프로젝트: `a305-monitoring`
- 저장소: `prometheus-data`, `grafana-data` 전용 볼륨
- Prometheus 보관 상한: 14일·1GB 설정. 디스크와 실제 사용량은 별도로 확인합니다.
- 초기 자원 상한: Prometheus 0.5 CPU·512MiB, Grafana 0.5 CPU·512MiB, Alertmanager 0.25 CPU·128MiB, Blackbox와 관리 화면 프록시는 각각 0.25 CPU·64MiB. 로그인·대시보드 로딩을 포함한 실제 사용 중 메모리와 재시작 여부를 확인하고 수집 부하에 따라 조정합니다.

설정 폴더에는 저장소의 `infra/monitoring/` 파일을 같은 상대 위치로 복사합니다. `monitoring.env.example`을 실행 폴더의 `monitoring.env`로 복사하고 그룹 ID는 서버에서 `id -g`로 확인합니다.

서버 관리 담당자가 비밀 폴더를 `0700`으로 만들고 다음 세 파일을 준비합니다. 실제 비밀번호를 저장소나 채팅에 넣지 않습니다.

| 파일 | 내용 |
| --- | --- |
| `dev-monitoring-password` | 개발 백엔드의 `MONITORING_PASSWORD`와 같은 값 |
| `prod-monitoring-password` | 운영 백엔드의 별도 `MONITORING_PASSWORD` |
| `grafana-admin-password` | Grafana 관리자 전용 비밀번호 |

비밀 파일은 실행 계정 소유, `MONITORING_SECRET_GID` 그룹, `0640`으로 관리합니다. 컨테이너에는 해당 그룹을 보조 그룹으로 추가하고 필요한 파일만 읽기 전용 secret으로 제공합니다. 개발·운영·Grafana 비밀번호는 서로 다르게 정하며 BCrypt 제한을 고려해 32~64자의 임의 ASCII 문자열을 사용합니다. 파일 내용은 끝 줄바꿈을 제외한 동일한 값으로 관리합니다.

```bash
cd ~/apps/a305/monitoring/config
sudo docker compose -p a305-monitoring --env-file monitoring.env -f compose.yaml config --quiet
sudo docker compose -p a305-monitoring --env-file monitoring.env -f compose.yaml up -d
sudo docker compose -p a305-monitoring --env-file monitoring.env -f compose.yaml ps
```

UI 포트는 서버의 루프백에만 게시합니다. PC에서 SSH 터널을 유지한 상태로 접속합니다.

Grafana와 Alertmanager는 내부 모니터링 네트워크만 사용합니다. 루프백의 관리 화면 프록시가 두 UI를 전달하므로 내부 서비스에 외부 연결을 추가하지 않아도 SSH 터널로 접근할 수 있습니다. Alertmanager의 실제 알림 전송용 연결은 수신 채널을 승인한 뒤 별도 추가 파일로 활성화합니다.

```powershell
ssh -i "<PEM 경로>" -N -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 -L 127.0.0.1:13000:127.0.0.1:3000 -L 127.0.0.1:19091:127.0.0.1:9091 -L 127.0.0.1:19093:127.0.0.1:9093 ubuntu@k15a305.p.ssafy.io
```

- Grafana: `http://127.0.0.1:13000`, 사용자 `admin`, 준비한 관리자 비밀번호
- Prometheus: `http://127.0.0.1:19091`, Targets에서 환경별 수집 결과 확인
- Alertmanager: `http://127.0.0.1:19093`, 발동 알림·묶음·일시 중지 상태 확인
- 대시보드: A305 폴더의 `ELSEHERE API`, 환경·조회 기간 선택
- HTTPS 대시보드: A305 폴더의 `ELSEHERE HTTPS`, 공개 응답·인증서 잔여 일수·발동 규칙 수 확인

터널 명령이 실행 중인 창을 유지합니다. 로컬 포트를 이미 사용 중이면 `-L`의 첫 번째 포트와 브라우저 주소를 함께 바꿉니다. 서버 포트를 인터넷에 공개하지 않습니다.

대시보드는 수집 상태, 선택 기간 요청 수, 초당 요청량, 4xx·5xx 비율과 p95를 표시합니다. 요청이 없거나 수집 시간이 짧으면 지연·비율이 비어 있을 수 있습니다. 요청 수·비율·지연은 같은 백엔드 요청 집합으로 계산하며 컨트롤러에 도달하기 전 거절된 요청도 포함합니다. 관리 포트의 수집 요청은 API 요청 통계에 포함되지 않는지 검증합니다.

관리 포트가 외부에서 접근되지 않는지 확인하고 정상 요청·인증 실패 요청의 지표 증가, 환경 필터·기간 변경과 재기동 후 대시보드 유지를 검증합니다. 수집 실패를 해소하기 전 알림 임계값을 확정하지 않습니다.

## 장애 감지와 발송 승인

`blackbox.yml`은 유효한 TLS와 HTTP 200을 요구하며 리다이렉트를 따라가지 않습니다. [내부 HTTPS 대상 파일](../infra/monitoring/targets/https.json)에 등록한 실제 앱 health 주소를 검사합니다. 개발 대상은 외부 감시와 같은 `https://dev.elsehere.kr/api/health`입니다. 운영 앱을 배포한 뒤에는 실제 운영 주소도 검사 대상으로 추가합니다.

내부 검사기는 앱과 같은 서버에서 공개 주소로 접속합니다. Nginx·앱·TLS 오류는 감지하지만 서버 전체가 멈추거나 서버의 외부 연결이 끊어지면 이 구성 자체도 알림을 보낼 수 없습니다. 전체 서버 장애는 [독립 외부 감시](external-monitoring.md)로 확인합니다.

| 규칙 | 조건 | 지속 시간 |
| --- | --- | --- |
| API 관리 지표 수집 실패 | 활성화한 API 대상의 수집 실패 | 2분 |
| 공개 HTTPS 응답 실패 | TLS 검증 또는 HTTP 200 응답 실패 | 2분 |
| HTTPS 검사 지표 수집 실패 | Blackbox 지표 수집 실패 | 2분 |
| 인증서 만료 임박 | 잔여 기간 14일 미만 | 1시간 |
| API 오류율 증가 | 최근 5분 요청 20건 이상, 5xx 비율 5% 초과 | 5분 |
| API 응답 지연 | 최근 5분 요청 20건 이상, p95 3초 초과 | 10분 |

오류율·지연 규칙은 `/api/health`를 제외합니다. 데이터가 적을 때의 비율·지연만으로 알림을 발동하지 않습니다. 운영 앱이 없는 환경은 수집 대상에서 제외해 배포 전 장애 알림을 만들지 않습니다.

### 정상 이용 기준과 재조정

제품의 목표 동시 사용자 수·초당 요청 수·성능 합격 수치는 아직 확정되지 않았습니다. 아래 기준은 현재 구현된 일반 API의 심각한 이상을 찾는 운영 경보이며 서비스 수용 인원이나 AI 대화 전체의 성능 보장이 아닙니다.

- 배포된 백엔드·내부 웹 이미지와 같은 자원 상한으로 별도 내부 네트워크·임시 PostgreSQL을 구성해 측정합니다. 실제 앱의 인증 설정·사용자 데이터는 변경하지 않고 외부 AI 호출은 차단합니다.
- 시험 사용자 1명이 로그인·학습 언어·레벨 설정을 마친 뒤 초기 상태·언어 목록·내 언어 설정을 조회합니다. 세 번의 준비 조회 후, 초당 한 번의 3개 조회 묶음을 60초씩 세 차례 실행합니다.
- 이 기준 측정의 540개 조회는 모두 HTTP 200이었습니다. 회차별 전체 p95는 9.374~16.855ms, 전체 p99의 최댓값은 22.939ms, 단일 요청 최댓값은 82.410ms였습니다. 초기 로그인·설정 요청, 실제 소셜 인증, 공개 TLS 구간과 AI 호출은 이 지연 집계에 포함하지 않습니다.
- 5xx 정상 관측값은 0%입니다. 오류 경보는 소수 실패를 확대하지 않도록 5분 20건 이상의 표본에서 5% 초과가 5분 지속될 때 발동하도록 유지합니다.
- 지연 경보의 3초는 관측한 단일 최장 요청의 약 36배로, 현재 소규모 개발 환경에서 일시적 실행 편차와 심각한 지속 지연을 구분하는 보수적인 기준입니다. p95가 이를 10분 넘을 때 알리며 정상 지연을 3초까지 허용한다는 제품 계약으로 해석하지 않습니다.
- 공개 HTTPS는 서버 밖에서도 유효한 인증서·HTTP 200·상태 본문을 확인합니다. 인증서 14일 경보는 자동 갱신 실패 후 대응 여유를 확보하기 위한 기준이며 짧은 요청 지연 측정으로 결정하지 않습니다.

실제 이용량이 쌓이거나 목표 동시 사용자 수, API·DB 데이터 규모, 자원 상한 또는 AI 호출 경계가 바뀌면 다시 측정합니다. AI가 포함된 요청의 STT·LLM·TTS·전체 지연을 일반 조회 기준과 혼동하지 않습니다. 측정 환경·표본·분위수·오류율과 결정 근거는 Jira에 기록하고 경보값을 바꾸면 규칙의 대기·발동·복구와 정상 구간의 무알림을 검증합니다.

## Mattermost 발송 승인

서버 Alertmanager와 Grafana Cloud는 승인된 `ELSEHERE 운영 알림`(`elsehere-alerts`) 채널을 사용합니다. 수신 채널·문구·경보 조건을 유지하며 운영하고, 변경하거나 새 발송 경로를 연결할 때는 실제 채널명과 문구·규칙·반복 주기를 운영 담당자에게 보고하고 명시적으로 승인받습니다. 다른 채널이나 멘션을 임의로 추가하지 않습니다. 이 승인 기준은 내부·외부 감시에 공통으로 적용합니다.

웹훅 URL은 비밀번호처럼 관리하며 저장소·Jira·MR·로그·채팅에 기록하지 않습니다. Mattermost 확인에 computer-use를 사용하지 않습니다. 기본 묶음 대기는 30초, 변경 알림 주기는 5분, 동일 장애 반복 주기는 4시간이며 복구도 통지합니다.

기본 `alertmanager.yml`에는 발송 수신기가 없습니다. 네트워크도 내부 모니터링 연결로 제한합니다. 채널 승인 전에는 Mattermost 연결이나 시험 발송을 하지 않습니다.

승인 후 적용 절차는 다음과 같습니다.

1. 위 승인 기준에 따라 연결할 채널·문구·조건을 확정합니다.
2. 승인한 채널에 고정된 incoming webhook을 준비하고 URL을 `secrets/mattermost-webhook-url`에 저장합니다.
3. `alertmanager.mattermost.yml.example`을 실행 폴더의 `alertmanager.mattermost.yml`로 복사하고 `REQUIRES_USER_APPROVAL`을 승인된 채널의 내부 이름으로 바꿉니다. 기본 채널로 임의 발송하거나 채널을 우회해 덮어쓰지 않습니다.
4. `compose.yaml`과 `compose.mattermost.yaml`을 함께 해석·검사한 후 Alertmanager만 교체합니다. 이 추가 파일을 사용해야 비밀 파일과 알림 전송용 외부 연결이 제공됩니다.
5. 승인받은 시험 문구로 발동·복구를 확인하고 정상 알림 규칙으로 돌아갑니다. 확인 전에는 실제 수신까지 완료한 것으로 기록하지 않습니다.

알림은 환경·규칙명·정해진 요약·시작 및 복구 시각·확인 위치만 포함합니다. 사용자가 요청한 원본 URL·사용자 식별자·본문과 비밀값은 포함하지 않습니다.

## 저장소 검증

```bash
python infra/scripts/check_observability.py
python infra/scripts/check_alerting.py
```

저장소 최상위에서 Python과 Docker가 실행 가능한 환경을 사용합니다. 백엔드 코드를 함께 변경하면 [백엔드 실행 문서](../backend/README.md#명령)의 검사와 빌드도 수행합니다.

두 인프라 검사는 고정된 공식 이미지의 임시 컨테이너와 외부 통신이 차단된 전용 네트워크를 사용합니다. 공개 포트 없이 Nginx 요청 ID·오류·민감 정보 제외, Prometheus 설정·대시보드 쿼리 문법과 Grafana 데이터 소스·대시보드 등록·인증을 확인합니다. 알림 검사는 규칙의 대기·발동·복구와 저트래픽 제외, Blackbox 성공·실패·HTTPS 강제, Alertmanager의 Mattermost 발동·복구 형식 전송을 격리된 가짜 수신기로 확인합니다. 실제 Mattermost에는 접속하지 않고 검사 리소스만 정리합니다. 실제 서버 TLS·앱 배포, 독립된 위치의 가용성 검사와 승인한 채널의 수신 확인은 별도 검증합니다.

설계 참고: [Spring Boot 지표](https://docs.spring.io/spring-boot/reference/actuator/metrics.html), [관리 HTTP 포트](https://docs.spring.io/spring-boot/reference/actuator/monitoring.html), [Nginx 로그 형식](https://nginx.org/en/docs/http/ngx_http_log_module.html), [Prometheus 설정](https://prometheus.io/docs/prometheus/latest/configuration/configuration/), [Grafana provisioning](https://grafana.com/docs/grafana/latest/administration/provisioning/).
