# 요청 추적과 API 모니터링

Nginx와 Spring Boot에서 같은 HTTP 요청 ID로 요청을 찾고, Prometheus와 Grafana에서 API 요청량·오류율·지연을 확인합니다. 알림 수신 경로·장애 임계값·외부 HTTPS 및 인증서 만료 감시는 별도 준비 범위입니다.

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

앱의 내부 웹 Nginx도 원본 요청 행을 남기는 기본 access 형식을 사용하지 않도록 배포 구성에서 확인합니다. 내부 프록시 로그를 끄거나 동일한 최소 필드 형식을 사용하고, 공개 Nginx와 백엔드에서 요청을 연결해 조회합니다.

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

검토된 앱 Compose와 `infra/compose/compose.backend-monitoring.yaml`을 함께 사용합니다. 이 추가 파일은 관리 주소와 비밀번호만 주입하며 포트를 게시하지 않습니다. 배포 명령과 자동화에서도 같은 추가 파일을 사용해야 합니다. 환경별 `deploy.env`의 비밀번호는 아래 수집용 비밀번호 파일과 일치해야 합니다.

메트릭은 Spring의 경로 패턴·메서드·상태 코드 등을 사용합니다. 요청 ID, 사용자 ID, 원본 동적 URL을 라벨에 추가하지 않습니다. `http.server.requests` histogram으로 p95를 계산하며, 아직 없는 AI 클라이언트의 구간 지연이나 사용량을 수집한 것으로 보지 않습니다.

## Prometheus·Grafana 실행

선행 조건은 `a305-dev`·`a305-prod` 앱 네트워크와 백엔드의 인증된 관리 포트입니다. 현재 수집 대상은 Compose 기본 이름 `a305-dev-backend-1:9090`, `a305-prod-backend-1:9090`입니다. 해당 앱이 없으면 수집 실패로 표시되므로 정상으로 해석하지 않습니다.

- 설정: `infra/monitoring/`
- 서버 실행 폴더: `~/apps/a305/monitoring/config/`
- 서버 비밀 파일: `~/apps/a305/monitoring/secrets/`
- Compose 프로젝트: `a305-monitoring`
- 저장소: `prometheus-data`, `grafana-data` 전용 볼륨
- Prometheus 보관 상한: 14일·1GB 설정. 디스크와 실제 사용량은 별도로 확인합니다.
- 초기 자원 상한: Prometheus 0.5 CPU·512MiB, Grafana 0.5 CPU·256MiB. 실제 수집 부하에 따라 조정합니다.

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

두 UI 포트는 서버의 루프백에만 게시합니다. PC에서 SSH 터널을 유지한 상태로 접속합니다.

```powershell
ssh -i "<PEM 경로>" -L 3000:127.0.0.1:3000 -L 9091:127.0.0.1:9091 ubuntu@k15a305.p.ssafy.io
```

- Grafana: `http://localhost:3000`, 사용자 `admin`, 준비한 관리자 비밀번호
- Prometheus: `http://localhost:9091`, Targets에서 환경별 수집 결과 확인
- 대시보드: A305 폴더의 `ELSEHERE API`, 환경·조회 기간 선택

대시보드는 수집 상태, 선택 기간 요청 수, 초당 요청량, 4xx·5xx 비율과 p95를 표시합니다. 요청이 없거나 수집 시간이 짧으면 지연·비율이 비어 있을 수 있습니다. 요청 수·비율·지연은 같은 백엔드 요청 집합으로 계산하며 컨트롤러에 도달하기 전 거절된 요청도 포함합니다. 관리 포트의 수집 요청은 API 요청 통계에 포함되지 않는지 검증합니다.

관리 포트가 외부에서 접근되지 않는지 확인하고 정상 요청·인증 실패 요청의 지표 증가, 환경 필터·기간 변경과 재기동 후 대시보드 유지를 검증합니다. 수집 실패를 해소하기 전 알림 임계값을 확정하지 않습니다.

## 저장소 검증

```bash
cd backend
./gradlew spotlessApply check bootJar
cd ..
python infra/scripts/check_observability.py
```

두 번째 검사는 고정된 공식 이미지의 임시 컨테이너와 외부 통신이 차단된 전용 네트워크를 사용합니다. 공개 포트 없이 Nginx 요청 ID·오류·민감 정보 제외, Prometheus 설정·대시보드 쿼리 문법과 Grafana 데이터 소스·대시보드 등록·인증을 확인하고 검사 리소스만 정리합니다. 실제 서버 TLS·앱 배포와 외부 HTTPS 감시·알림 발동 검증은 포함하지 않습니다.

설계 참고: [Spring Boot 지표](https://docs.spring.io/spring-boot/reference/actuator/metrics.html), [관리 HTTP 포트](https://docs.spring.io/spring-boot/reference/actuator/monitoring.html), [Nginx 로그 형식](https://nginx.org/en/docs/http/ngx_http_log_module.html), [Prometheus 설정](https://prometheus.io/docs/prometheus/latest/configuration/configuration/), [Grafana provisioning](https://grafana.com/docs/grafana/latest/administration/provisioning/).
