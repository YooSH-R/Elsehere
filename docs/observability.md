# 요청 추적과 API 모니터링

Nginx와 Spring Boot에서 같은 HTTP 요청 ID로 요청을 찾고, Prometheus와 Grafana에서 API 요청량·오류율·지연, 공개 HTTPS 응답과 인증서 만료일을 확인합니다. Alertmanager는 장애 발동·복구를 관리합니다. 기본 설정은 알림 발송을 비활성화하며, Mattermost 수신 채널과 메시지를 운영 담당자에게 보고하고 명시적으로 승인받은 후 연결합니다.

이 문서의 설정은 검토 후 서버에 적용합니다. 실제 앱이 배포되기 전에는 환경 확인용 서비스의 정상 응답을 앱 지표 수집 성공으로 보지 않습니다.

## 요청 ID와 기록 범위

- 공개 Nginx는 모든 요청에 32자리 소문자 16진수 `X-Request-ID`를 새로 생성합니다. 클라이언트가 보낸 같은 이름의 헤더는 덮어씁니다.
- 내부 프록시는 이 헤더를 유지합니다. Spring은 해당 형식의 값만 수용하고, 값이 없거나 형식이 잘못되면 새 ID를 생성합니다. 백엔드 포트는 공개하지 않습니다.
- Spring은 보안 필터보다 먼저 요청 문맥에 ID를 넣고 응답에도 반환합니다. 요청이 끝나거나 예외가 발생하면 이전 문맥을 복원합니다.
- Spring 요청 로그에는 HTTP 메서드, 컨트롤러의 경로 패턴, 상태 코드와 처리 시간만 남깁니다. 컨트롤러에 도달하지 않은 요청은 `/unmatched`로 기록합니다.
- Nginx는 원본 URL 대신 `/api/**`, `/api/v1/auth/**`, `/webgl/**` 등 고정된 경로 그룹을 기록합니다. 쿼리, 토큰, 쿠키, IP·사용자 식별자와 음성·대화 본문은 요청 요약에 넣지 않습니다.
- 처리하지 못한 Spring 예외는 종류와 코드 위치만 기록합니다. 외부 응답이나 사용자 입력을 포함할 수 있는 원본 예외 메시지는 기록하지 않습니다.

이 헤더는 전송 요청의 추적용입니다. 중복 처리를 방지하는 API 본문의 UUID `requestId`와는 용도가 다르며, 재시도 요청은 같은 본문 ID를 사용해도 새로운 HTTP 추적 ID를 받습니다.

Spring의 AI 호출 클라이언트는 아직 구현돼 있지 않습니다. AI 호출을 연결할 때 같은 HTTP 추적 ID를 워커의 `X-Request-Id`로 전달하고, 워커가 이미 지원하는 헤더를 재사용합니다.

## Nginx 설정 적용

HTTPS 인증서와 기존 location을 보존합니다. 전체 사이트 설정을 예제로 덮어쓰지 않습니다.

1. `infra/nginx/observability-http.conf`를 `/etc/nginx/conf.d/a305-observability.conf`에 설치합니다. 이 파일은 `http` 범위의 map과 로그 형식을 정의합니다.
2. `infra/nginx/observability-server.conf`를 `/etc/nginx/snippets/a305-observability.conf`에 설치합니다.
3. `/etc/nginx/sites-available/a305`의 A305 HTTP·HTTPS `server` 블록마다 아래 한 줄을 추가합니다.

```nginx
include /etc/nginx/snippets/a305-observability.conf;
```

4. 각 `location`에 별도 `proxy_set_header` 또는 `add_header`가 있으면 상위 설정의 상속 여부를 확인하고 요청 ID 전달·응답을 해당 위치에도 반영합니다. 내부 웹 프록시는 요청 ID를 재생성하지 않습니다.
5. `sudo nginx -t` 성공 후 reload하고 개발·운영의 정상·오류 응답에서 헤더와 로그가 일치하는지 확인합니다.

새 요청 형식은 기존 `/var/log/nginx/access.log`를 사용합니다. 프로젝트의 HTTP 요청 오류는 안전한 access 요약과 백엔드 오류 로그로 확인합니다. Nginx 자체 error 로그는 URL을 가릴 수 없어 사이트의 기록 수준을 `crit`으로 제한합니다. Nginx 기동·설정 장애는 `nginx -t`와 서비스 journal도 함께 확인합니다. 기본 원본 error 메시지는 구조화된 요청 로그와 같은 개인정보 제거 기능을 제공하지 않으므로 접근 권한을 유지합니다.

앱의 내부 웹 Nginx는 `infra/nginx/observability-web.conf`로 원본 access 로그를 끄고 error 수준을 `crit`으로 제한합니다. 앱 Compose에 `infra/compose/compose.observability.yaml`을 함께 적용하면 이 설정이 추가 마운트됩니다. 기존 웹 이미지와 API·정적 파일 설정은 유지하며, 요청 ID는 내부 프록시에서 그대로 전달합니다.

### 조회·보관·복구

```bash
sudo grep -F '"request_id":"<응답 헤더의 ID>"' /var/log/nginx/access.log
sudo docker compose -p a305-dev --env-file "$HOME/apps/a305/dev/deploy.env" -f infra/compose/compose.app.yaml logs --since=10m backend | grep -F 'request_id=<같은 ID>'
```

- Nginx 로그는 `www-data:adm`, `0640` 권한과 기존 `/etc/logrotate.d/nginx`를 유지합니다. 매일 확인하고 최대 14회분을 보관하며 압축은 한 회차 지연합니다. `notifempty`이므로 정확히 14일 보관과 같지는 않습니다.
- 백엔드·웹·DB·AI 및 모니터링 컨테이너 로그는 `json-file`, 파일당 10MiB·최대 3개를 기준으로 관리합니다. Docker 로그 파일을 호스트 logrotate로 다시 순환시키지 않습니다.
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

검토된 앱 Compose와 `infra/compose/compose.observability.yaml`을 함께 사용합니다. 이 추가 파일은 백엔드 관리 주소·비밀번호와 웹의 로그 설정만 주입하며 포트를 게시하지 않습니다. `OBSERVABILITY_WEB_LOG_CONFIG`는 서버의 `observability-web.conf` 절대 경로입니다. 배포 명령과 자동화에서도 같은 추가 파일을 사용해야 합니다. 환경별 `deploy.env`의 비밀번호는 아래 수집용 비밀번호 파일과 일치해야 합니다.

메트릭은 Spring의 경로 패턴·메서드·상태 코드 등을 사용합니다. 요청 ID, 사용자 ID, 원본 동적 URL을 라벨에 추가하지 않습니다. `http.server.requests` histogram으로 p95를 계산하며, 아직 없는 AI 클라이언트의 구간 지연이나 사용량을 수집한 것으로 보지 않습니다.

## Prometheus·Grafana 실행

선행 조건은 `a305-dev`·`a305-prod` 네트워크와 활성화할 백엔드의 인증된 관리 포트입니다. `targets/api-dev.json`은 `a305-dev-backend-1:9090`을 수집합니다. `targets/api-prod.json`은 기본적으로 비어 있습니다. 실제 운영 앱을 배포하고 관리 포트를 검증한 뒤 `a305-prod-backend-1:9090`과 `environment: prod`를 등록합니다. 환경 확인용 서비스는 앱 수집 대상으로 등록하지 않습니다.

- 설정: `infra/monitoring/`
- 서버 실행 폴더: `~/apps/a305/monitoring/config/`
- 서버 비밀 파일: `~/apps/a305/monitoring/secrets/`
- Compose 프로젝트: `a305-monitoring`
- 저장소: `prometheus-data`, `grafana-data` 전용 볼륨
- Prometheus 보관 상한: 14일·1GB 설정. 디스크와 실제 사용량은 별도로 확인합니다.
- 초기 자원 상한: Prometheus 0.5 CPU·512MiB, Grafana 0.5 CPU·256MiB, Alertmanager 0.25 CPU·128MiB, Blackbox와 관리 화면 프록시는 각각 0.25 CPU·64MiB. 실제 수집 부하에 따라 조정합니다.

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
ssh -i "<PEM 경로>" -L 3000:127.0.0.1:3000 -L 9091:127.0.0.1:9091 -L 9093:127.0.0.1:9093 ubuntu@k15a305.p.ssafy.io
```

- Grafana: `http://localhost:3000`, 사용자 `admin`, 준비한 관리자 비밀번호
- Prometheus: `http://localhost:9091`, Targets에서 환경별 수집 결과 확인
- Alertmanager: `http://localhost:9093`, 발동 알림·묶음·일시 중지 상태 확인
- 대시보드: A305 폴더의 `ELSEHERE API`, 환경·조회 기간 선택
- HTTPS 대시보드: A305 폴더의 `ELSEHERE HTTPS`, 공개 응답·인증서 잔여 일수·발동 규칙 수 확인

대시보드는 수집 상태, 선택 기간 요청 수, 초당 요청량, 4xx·5xx 비율과 p95를 표시합니다. 요청이 없거나 수집 시간이 짧으면 지연·비율이 비어 있을 수 있습니다. 요청 수·비율·지연은 같은 백엔드 요청 집합으로 계산하며 컨트롤러에 도달하기 전 거절된 요청도 포함합니다. 관리 포트의 수집 요청은 API 요청 통계에 포함되지 않는지 검증합니다.

관리 포트가 외부에서 접근되지 않는지 확인하고 정상 요청·인증 실패 요청의 지표 증가, 환경 필터·기간 변경과 재기동 후 대시보드 유지를 검증합니다. 수집 실패를 해소하기 전 알림 임계값을 확정하지 않습니다.

## 장애 감지와 발송 승인

`blackbox.yml`은 유효한 TLS와 HTTP 200을 요구하며 리다이렉트를 따라가지 않습니다. `targets/https.json`에 등록한 실제 앱 health 주소만 검사합니다. 기본 대상은 개발 앱의 공개 HTTPS 주소이며 운영 앱을 배포한 뒤 해당 주소를 추가합니다.

검사기는 앱과 같은 서버에서 공개 주소로 접속합니다. Nginx·앱·TLS 오류는 감지하지만 서버 전체가 멈추거나 서버의 외부 연결이 끊어지면 이 구성 자체도 알림을 보낼 수 없습니다. 전체 서버 장애까지 감시하려면 독립된 서버 또는 외부 가용성 서비스에 검사를 추가해야 합니다.

| 규칙 | 초기 조건 | 지속 시간 |
| --- | --- | --- |
| API 관리 지표 수집 실패 | 활성화한 API 대상의 수집 실패 | 2분 |
| 공개 HTTPS 응답 실패 | TLS 검증 또는 HTTP 200 응답 실패 | 2분 |
| HTTPS 검사 지표 수집 실패 | Blackbox 지표 수집 실패 | 2분 |
| 인증서 만료 임박 | 잔여 기간 14일 미만 | 1시간 |
| API 오류율 증가 | 최근 5분 요청 20건 이상, 5xx 비율 5% 초과 | 5분 |
| API 응답 지연 | 최근 5분 요청 20건 이상, p95 3초 초과 | 10분 |

오류율·지연 규칙은 `/api/health`를 제외합니다. 성능 수치는 초기 검토값이며 실제 이용 흐름의 정상 부하·응답시간을 측정한 뒤 조정합니다. 데이터가 적을 때의 비율·지연만으로 알림을 발동하지 않습니다. 운영 앱이 없는 환경은 수집 대상에서 제외해 배포 전 장애 알림을 만들지 않습니다.

기본 `alertmanager.yml`에는 발송 수신기가 없습니다. 네트워크도 내부 모니터링 연결로 제한합니다. 채널 승인 전에는 Mattermost 연결이나 시험 발송을 하지 않습니다.

승인 후 적용 절차는 다음과 같습니다.

1. 실제 채널명과 발송 문구·규칙·반복 주기를 운영 담당자에게 보고하고 명시적으로 승인받습니다. 다른 채널로 변경할 때도 다시 승인받습니다.
2. 승인한 채널에 고정된 incoming webhook을 준비합니다. URL은 비밀번호와 같은 접근 기준으로 `secrets/mattermost-webhook-url`에 저장합니다. 채팅·MR·로그에 URL을 넣지 않습니다.
3. `alertmanager.mattermost.yml.example`을 실행 폴더의 `alertmanager.mattermost.yml`로 복사하고 `REQUIRES_USER_APPROVAL`을 승인된 채널의 내부 이름으로 바꿉니다. 기본 채널로 임의 발송하거나 채널을 우회해 덮어쓰지 않습니다.
4. `compose.yaml`과 `compose.mattermost.yaml`을 함께 해석·검사한 후 Alertmanager만 교체합니다. 이 추가 파일을 사용해야 비밀 파일과 알림 전송용 외부 연결이 제공됩니다.
5. 승인받은 시험 문구로 발동·복구를 확인하고 정상 알림 규칙으로 돌아갑니다. 확인 전에는 실제 수신까지 완료한 것으로 기록하지 않습니다.

알림은 환경·규칙명·정해진 요약·시작 및 복구 시각·확인 위치만 포함합니다. 요청 URL·사용자 식별자·본문과 비밀값은 포함하지 않습니다. 기본 묶음 대기는 30초, 변경 알림 주기는 5분, 동일 장애 반복 주기는 4시간이며 복구도 통지합니다.

## 저장소 검증

```bash
cd backend
./gradlew spotlessApply check bootJar
cd ..
python infra/scripts/check_observability.py
python infra/scripts/check_alerting.py
```

두 인프라 검사는 고정된 공식 이미지의 임시 컨테이너와 외부 통신이 차단된 전용 네트워크를 사용합니다. 공개 포트 없이 Nginx 요청 ID·오류·민감 정보 제외, Prometheus 설정·대시보드 쿼리 문법과 Grafana 데이터 소스·대시보드 등록·인증을 확인합니다. 알림 검사는 규칙의 대기·발동·복구와 저트래픽 제외, Blackbox 성공·실패·HTTPS 강제, Alertmanager의 Mattermost 발동·복구 형식 전송을 격리된 가짜 수신기로 확인합니다. 실제 Mattermost에는 접속하지 않고 검사 리소스만 정리합니다. 실제 서버 TLS·앱 배포, 독립된 위치의 가용성 검사와 승인한 채널의 수신 확인은 별도 검증합니다.

설계 참고: [Spring Boot 지표](https://docs.spring.io/spring-boot/reference/actuator/metrics.html), [관리 HTTP 포트](https://docs.spring.io/spring-boot/reference/actuator/monitoring.html), [Nginx 로그 형식](https://nginx.org/en/docs/http/ngx_http_log_module.html), [Prometheus 설정](https://prometheus.io/docs/prometheus/latest/configuration/configuration/), [Grafana provisioning](https://grafana.com/docs/grafana/latest/administration/provisioning/).
