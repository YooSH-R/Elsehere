# HTTPS와 인증서 운영

`k15a305.p.ssafy.io`의 HTTPS는 Ubuntu 호스트 Nginx에서 종료합니다. Certbot의 Nginx 플러그인으로 Let's Encrypt 인증서를 발급·적용하고 HTTP 요청은 HTTPS로 이동하도록 구성합니다. 인증서 만료 전 교체를 위한 자동 갱신 설정도 Certbot에서 관리합니다.

## 연결과 설정 위치

| 항목 | 구성 |
| --- | --- |
| 도메인·인증서 대상 | `k15a305.p.ssafy.io` |
| 외부 포트 | HTTP 80·HTTPS 443 |
| Nginx 사이트 | `/etc/nginx/sites-available/a305`, `sites-enabled`에서 활성화 |
| Certbot 실행 파일 | snap 설치의 `/snap/bin/certbot` |
| 인증서 관리 위치 | `/etc/letsencrypt/` |
| 갱신 설정 | `/etc/letsencrypt/renewal/`의 해당 인증서 설정 |
| 진단 로그 | `/var/log/letsencrypt/letsencrypt.log`, `/var/log/nginx/error.log` |

실제 인증서 이름과 파일 경로는 `certbot certificates`와 활성 Nginx 설정에서 확인합니다. 인증서 이름에 번호가 붙을 수 있으므로 경로를 추측해서 바꾸지 않습니다. 개인 키·계정 정보·갱신 설정 원본은 저장소나 MR에 붙이지 않습니다.

최초 적용에는 다음 명령을 사용합니다. 이미 적용된 서버에서는 반복 설치 대신 아래 상태 점검과 변경 절차를 사용합니다.

```bash
sudo /snap/bin/certbot --nginx --redirect -d k15a305.p.ssafy.io
```

## HTTPS 상태 확인

인증서 이름·도메인·만료일과 Nginx 설정을 확인합니다.

```bash
sudo /snap/bin/certbot certificates
sudo nginx -t
curl -I 'http://k15a305.p.ssafy.io/api/health?check=redirect'
curl --fail https://k15a305.p.ssafy.io/api/health
curl --fail https://k15a305.p.ssafy.io/dev/api/health
```

HTTP 응답의 `Location`은 같은 경로·쿼리의 HTTPS 주소여야 합니다. 운영 응답의 `environment`는 `prod`, 개발은 `dev`여야 합니다. TLS 검증을 건너뛰는 `curl -k` 결과로 인증서 정상 여부를 판단하지 않습니다.

PC 브라우저에서도 [운영 상태](https://k15a305.p.ssafy.io/api/health)와 [개발 상태](https://k15a305.p.ssafy.io/dev/api/health)를 열어 인증서 오류가 없고 환경이 맞는지 확인합니다. 개발 주소는 별도 도메인을 사용하지 않으므로 같은 도메인 인증서를 적용받습니다.

## 자동 갱신의 의미와 점검

자동 갱신은 HTTPS 연결에 사용하는 **TLS 인증서**를 만료 전에 새로 발급받고 Nginx에서 사용하도록 반영하는 작업입니다. 앱 코드·Git 브랜치·Docker 이미지를 자동 배포하는 기능과는 별개입니다.

Certbot의 예약 작업은 주기적으로 실행되며 갱신이 필요한 인증서를 처리합니다. Nginx 플러그인은 저장된 발급·설치 설정을 사용합니다. 발급 성공과 서비스 반영 여부를 함께 점검하며, 예약이 있다는 사실만으로 향후 갱신 성공을 보장하지 않습니다. [Certbot 갱신 안내](https://eff-certbot.readthedocs.io/en/stable/using.html#renewing-certificates)

```bash
systemctl list-timers --all '*certbot*'
sudo /snap/bin/certbot renew --dry-run
```

- 타이머 목록에서 해당 예약의 다음 실행 시각과 활성 상태를 확인합니다. 단위 이름은 설치 방식에 따라 달라질 수 있습니다.
- `all simulated renewals succeeded`는 모의 갱신 성공을 뜻합니다. 운영 인증서를 새것으로 교체했다는 뜻은 아닙니다.
- `--dry-run`은 통상 배포 후크를 실행하지 않습니다. 사용자 정의 후크를 도입했다면 모의 발급 성공과 후크 검증을 구분하여 기록합니다.
- 실제 갱신 후에는 인증서 만료일, `nginx -t`, 외부 HTTPS 응답과 브라우저에 제공되는 인증서를 확인합니다.

예약이 없거나 비활성 상태이면 snap 설치 상태와 해당 systemd 단위의 상태·journal을 확인하고 복구합니다. 중복 cron을 먼저 추가하지 않습니다. 발급에 사용하는 HTTP 인증 경로와 도메인 DNS가 유효해야 하므로 80번 포트나 Nginx 라우팅을 바꿀 때 모의 갱신도 다시 확인합니다.

## 기존 Nginx 설정 수정

Certbot은 사이트 설정에 인증서 경로·443 리스너·HTTP 리다이렉트를 추가합니다. 저장소의 `a305.bootstrap.conf`는 최초 HTTP 연결 예제이므로 HTTPS가 적용된 서버 파일에 그대로 덮어쓰지 않습니다.

1. 현재 사이트 파일을 백업하고 필요한 location·upstream만 수정합니다.
2. 인증서 경로, HTTPS 리스너와 HTTP 리다이렉트를 보존합니다.
3. 아래 문법 검사가 성공하면 reload합니다. 실패하면 reload하지 않고 변경 부분을 수정하거나 백업을 복구합니다.
4. 개발·운영 HTTPS 응답과 HTTP 리다이렉트를 모두 확인합니다.

```bash
sudo nginx -t
sudo systemctl reload nginx
```

개발 주소가 403이면 해당 location의 접근 제한을, 502이면 내부 포트와 컨테이너를 확인합니다. 인증서 오류가 나면 도메인·유효기간·체인·활성 설정의 인증서 경로를 확인합니다. 갱신 실패는 Certbot 로그와 예약 작업의 journal에서 원인을 확인하며 개인 키 내용은 출력하지 않습니다.

## 실제 WebGL 앱의 추가 검증

확인용 JSON 응답의 HTTPS 성공은 WebGL 서비스 검증의 일부입니다. 실제 WebGL 배포에는 정적 파일의 Content-Type·Content-Encoding, 혼합 콘텐츠, API 주소·CORS, 브라우저 보안 컨텍스트와 마이크 사용 조건을 추가로 검증합니다. 스트리밍이나 WebSocket을 도입하면 해당 연결도 끝까지 확인합니다.

설정 반영 이력과 실행 결과는 Jira·MR에 남깁니다. 인증서 적용은 `S15P31A305-149`, 브라우저 통합 검증은 `S15P31A305-150`, 자동 갱신 검증은 `S15P31A305-151`에서 관리합니다.
