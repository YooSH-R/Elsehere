# HTTPS와 인증서 운영

Unity WebGL과 API의 외부 HTTPS 연결은 Ubuntu 호스트 Nginx에서 종료합니다. Certbot의 Nginx 플러그인으로 Let's Encrypt 인증서를 발급·적용하고 HTTP 요청은 같은 경로·쿼리의 HTTPS 주소로 이동시킵니다. 앱 컨테이너는 루프백·내부 네트워크로 연결합니다.

## 접속 주소와 설정 위치

| 용도 | 주소 | 내부 대상과 현재 역할 |
| --- | --- | --- |
| 개발 앱 | `https://dev.elsehere.kr/` | `127.0.0.1:8081`, WebGL과 같은 출처의 `/api/` |
| 운영 | `https://elsehere.kr/`, `https://www.elsehere.kr/` | `127.0.0.1:8080`, 운영 앱 전환 전의 확인용 서비스 |
| 기존 주소 | `https://k15a305.p.ssafy.io/` | 개발 `/dev/`·`/dev/api/`, 운영 `/api/` 경로 호환 |
| 서버 관리 | `ubuntu@k15a305.p.ssafy.io` | PEM 인증키를 사용하는 SSH |

새 개발 주소에는 `/dev/`를 붙이지 않습니다. 운영 HTTPS 응답만으로 실제 운영 앱이 배포되었다고 판단하지 않습니다. 환경 분리는 [배포 구성](deployment.md), 이미지 전달과 성공 기록은 [dev 자동 배포](dev-auto-deployment.md)에서 관리합니다.

| 항목 | 구성 |
| --- | --- |
| 새 도메인 DNS | `elsehere.kr`·`dev.elsehere.kr`의 A 레코드는 서버 IPv4, `www`는 같은 서버로 해석 |
| 인증서 대상 | `elsehere.kr`·`www.elsehere.kr`·`dev.elsehere.kr` |
| 외부 포트 | HTTP 80·HTTPS 443 |
| 도메인별 Nginx 사이트 | `/etc/nginx/sites-available/a305-elsehere`, `sites-enabled`에서 활성화 |
| 기존 SSAFY 사이트 | `/etc/nginx/sites-available/a305`와 기존 인증서 유지 |
| Certbot 실행 파일 | snap 설치의 `/snap/bin/certbot` |
| 새 도메인 인증서 | 이름 `elsehere.kr`, `/etc/letsencrypt/live/elsehere.kr/fullchain.pem`·`privkey.pem` |
| 갱신 설정 | `/etc/letsencrypt/renewal/`의 해당 인증서 설정 |
| 진단 로그 | `/var/log/letsencrypt/letsencrypt.log`, Nginx 서비스 journal·[요청 요약](observability.md) |

실제 인증서 이름과 파일 경로는 `certbot certificates`와 활성 Nginx 설정에서 확인합니다. 인증서 이름에 번호가 붙을 수 있으므로 경로를 추측해서 바꾸지 않습니다. 개인 키·계정 정보·갱신 설정 원본은 저장소나 MR에 붙이지 않습니다.

최초 적용 시 DNS와 Nginx의 `server_name`·환경별 프록시를 먼저 맞추고 HTTP 80·HTTPS 443을 허용합니다. AAAA 레코드를 사용한다면 해당 IPv6 서버도 같은 인증 경로를 제공해야 합니다. SSH 22와 공개키를 유지합니다. 다음은 세 도메인 인증서의 최초 발급·설치 명령이며, 이미 적용된 서버에서는 반복 발급 대신 아래 점검 절차를 사용합니다. [Certbot Nginx 안내](https://eff-certbot.readthedocs.io/en/stable/using.html#nginx)

```bash
sudo /snap/bin/certbot --nginx \
  --cert-name elsehere.kr \
  -d elsehere.kr \
  -d www.elsehere.kr \
  -d dev.elsehere.kr \
  --redirect
```

## HTTPS 상태 확인

인증서 이름·도메인·만료일과 Nginx 설정을 확인합니다.

```bash
sudo /snap/bin/certbot certificates
sudo nginx -t
curl -I 'http://dev.elsehere.kr/api/health?check=redirect'
curl --fail --silent --show-error -w '\n' https://dev.elsehere.kr/api/health
curl --fail --silent --show-error -w '\n' https://dev.elsehere.kr/version.json
curl --fail --silent --show-error -w '\n' https://elsehere.kr/api/health
curl --fail --silent --show-error -w '\n' https://www.elsehere.kr/api/health
```

HTTP 응답은 301이며 `Location`은 같은 경로·쿼리의 HTTPS 주소여야 합니다. 루트와 운영·www의 리다이렉트도 확인합니다. 개발 health는 HTTP 200과 `status: UP`, 운영·www는 실제 운영 앱 전환 전까지 `service: environment-check`·`environment: prod`·`status: ok`가 기준입니다. TLS 검증을 건너뛰는 `curl -k` 결과로 인증서 정상 여부를 판단하지 않습니다.

PC 브라우저에서도 [개발 앱](https://dev.elsehere.kr/)·[개발 상태](https://dev.elsehere.kr/api/health)와 [운영 상태](https://elsehere.kr/api/health)를 열어 인증서 오류와 환경을 확인합니다. `/version.json`은 웹 이미지의 소스 커밋입니다. 변경 없는 웹 이미지를 재사용하면 백엔드·전체 배포 후보의 커밋과 다를 수 있습니다.

## 자동 갱신의 의미와 점검

자동 갱신은 HTTPS 연결에 사용하는 **TLS 인증서**를 만료 전에 새로 발급받고 Nginx에서 사용하도록 반영하는 작업입니다. 앱 코드·Git 브랜치·Docker 이미지를 자동 배포하는 기능과는 별개입니다.

Certbot의 예약 작업은 주기적으로 실행되며 갱신이 필요한 인증서를 처리합니다. Nginx 플러그인은 저장된 발급·설치 설정을 사용합니다. 발급 성공과 서비스 반영 여부를 함께 점검하며, 예약이 있다는 사실만으로 향후 갱신 성공을 보장하지 않습니다. [Certbot 갱신 안내](https://eff-certbot.readthedocs.io/en/stable/using.html#renewing-certificates)

```bash
systemctl list-timers --all '*certbot*' --no-pager
sudo /snap/bin/certbot renew --cert-name elsehere.kr --dry-run
```

- 타이머 목록에서 해당 예약의 다음 실행 시각과 활성 상태를 확인합니다. 단위 이름은 설치 방식에 따라 달라질 수 있습니다.
- `all simulated renewals succeeded`는 모의 갱신 성공을 뜻합니다. 운영 인증서를 새것으로 교체했다는 뜻은 아닙니다.
- 기존 SSAFY 인증서까지 전체 점검할 때는 `--cert-name elsehere.kr`을 제외한 `renew --dry-run`을 사용합니다.
- `--dry-run`은 통상 배포 후크를 실행하지 않습니다. 사용자 정의 후크를 도입했다면 모의 발급 성공과 후크 검증을 구분하여 기록합니다.
- 실제 갱신 후에는 인증서 만료일, `nginx -t`, 외부 HTTPS 응답과 브라우저에 제공되는 인증서를 확인합니다.

예약이 없거나 비활성 상태이면 snap 설치 상태와 해당 systemd 단위의 상태·journal을 확인하고 복구합니다. 중복 cron을 먼저 추가하지 않습니다. 발급에 사용하는 HTTP 인증 경로와 도메인 DNS가 유효해야 하므로 80번 포트나 Nginx 라우팅을 바꿀 때 모의 갱신도 다시 확인합니다.

## 기존 Nginx 설정 수정

Certbot은 사이트 설정에 인증서 경로·443 리스너·HTTP 리다이렉트를 추가합니다. 저장소의 `a305.bootstrap.conf`는 최초 HTTP 연결 예제이므로 HTTPS가 적용된 서버 파일에 그대로 덮어쓰지 않습니다.

1. 현재 사이트 파일을 백업하고 필요한 location·upstream만 수정합니다.
2. 인증서 경로, HTTPS 리스너와 HTTP 리다이렉트, `Host`·`X-Forwarded-Proto`·요청 ID 전달과 스트리밍 응답 설정을 보존합니다. [Google 로그인 설정](dev-auto-deployment.md#google-로그인-설정-유지)의 콜백도 개발 HTTPS 출처와 일치해야 합니다.
3. 아래 문법 검사가 성공하면 reload합니다. 실패하면 reload하지 않고 변경 부분을 수정하거나 백업을 복구합니다.
4. 개발·운영·www·기존 SSAFY 주소의 HTTPS 응답과 HTTP 리다이렉트를 확인합니다. Certbot이 만든 HTTP 블록의 환경 구분·관측 include는 `return`보다 먼저 적용합니다. 상세 기준은 [Nginx 요청 로그](observability.md)를 따릅니다.

```bash
sudo nginx -t
sudo systemctl reload nginx
```

개발 주소가 403이면 해당 location의 접근 제한을, 502이면 내부 포트와 컨테이너를 확인합니다. 인증서 오류가 나면 도메인·유효기간·체인·활성 설정의 인증서 경로를 확인합니다. 갱신 실패는 Certbot 로그와 예약 작업의 journal에서 원인을 확인하며 개인 키 내용은 출력하지 않습니다.

## 실제 WebGL과 브라우저 통신 검증

Chrome에서 [개발 앱](https://dev.elsehere.kr/)을 열고 F12의 Network·Console을 사용합니다. [Network 문서](https://developer.chrome.com/docs/devtools/network/)에 따라 상태·헤더·소요 시간을 확인합니다. 별도 프록시가 필요한 문제가 없다면 Fiddler용 인증서를 추가할 필요는 없습니다.

1. Network를 연 상태로 로그인·가입·언어 선택·튜토리얼 등 대표 경로를 사용합니다. Fetch/XHR에서 API 경로·상태 코드·소요 시간과 차단 여부를 확인합니다.
2. API가 같은 HTTPS 출처를 사용하는지, Console에 혼합 콘텐츠·CORS·인증서 오류가 있는지 확인합니다. 다른 출처를 도입할 때는 해당 CORS 정책도 검증합니다.
3. 실제 페이지가 참조하는 해시 WebGL 파일의 응답 헤더를 확인합니다. 이전 배포의 파일 이름을 고정해서 검사하지 않습니다.

| 리소스 | Content-Type | Content-Encoding |
| --- | --- | --- |
| `.data.gz` | `application/octet-stream` | `gzip` |
| `.framework.js.gz` | `application/javascript` | `gzip` |
| `.wasm.gz` | `application/wasm` | `gzip` |

웹 Nginx의 기준은 [web.conf](../infra/nginx/web.conf)입니다. 표는 gzip 빌드 기준이며 압축 방식을 바꾸면 헤더와 실행 검증도 함께 바꿉니다. 진입 HTML은 `no-cache`, 버전 파일은 `no-store`로 제공하고 서로 다른 버전의 빌드 파일이 섞이지 않는지 확인합니다.

4. Console에서 다음 결과가 모두 `true`인지 확인합니다. 이는 마이크 API 사용의 기반 조건이며, 실제 권한 허용·거부 UI와 녹음·음성 전체 기능 성공은 별도 검증합니다.

```javascript
({
  secure: window.isSecureContext,
  microphoneApi: typeof navigator.mediaDevices?.getUserMedia === 'function'
})
```

5. 정상·오류 응답의 `X-Request-ID`를 확인해 [요청 추적 절차](observability.md)와 연결합니다. 비인증 읽기 요청의 401은 서버의 인증 거절이며 TLS·네트워크 장애와 구분합니다.
6. 검증한 경로·기대/실제 상태·소요 시간·요청 ID와 관찰 범위를 Jira·MR에 남깁니다. 쿼리의 사용자 값, 토큰·쿠키·인증 코드, 음성·대화 원문을 제외하고 원본 HAR·전체 헤더를 그대로 공유하지 않습니다.

브라우저가 관찰할 수 없는 백엔드→AI 구간은 서버 로그·지표로 확인합니다. Grafana·Prometheus는 서버 상태와 지연 추세를 보완하며 브라우저의 보안 컨텍스트·혼합 콘텐츠 검사를 대신하지 않습니다. TCP·TLS 등 실제 전송 장애가 남으면 필요한 구간만 Wireshark로 분석합니다. WebSocket을 도입할 때는 WSS 연결과 종료·재연결까지 추가로 검증합니다.

설정 반영 이력과 실제 검증 범위는 Jira·MR에서 관리합니다. 인증서 적용·브라우저 연결·갱신·통신 관찰을 구분하고, health 성공을 대화·음성 등 전체 기능 성공으로 확대하지 않습니다.
