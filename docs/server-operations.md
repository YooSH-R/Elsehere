# 개발·운영 서버 운영 방법

구성과 분리 기준은 [단일 서버의 개발·운영 구성](deployment.md)에서 관리합니다. 이 문서의 명령은 SSH로 접속한 Ubuntu 서버에서 실행합니다. 확인용 서비스와 실제 앱의 배포 명령을 구분합니다.

개발 앱은 실제 PostgreSQL·AI·백엔드·WebGL 컨테이너를 사용하고 운영은 확인용 서비스를 유지합니다. 자동 배포 수신기를 설치한 환경은 [dev 자동 배포 운영](dev-auto-deployment.md)의 성공 기록과 모니터링 추가 Compose를 함께 사용합니다. 아래 확인용 명령을 실제 앱 프로젝트 전체에 적용하지 않습니다.

## 서버 접속과 실행 파일

PC에서 각자의 인증키 경로로 접속합니다.

```sh
ssh -i "<PEM 파일 경로>" ubuntu@k15a305.p.ssafy.io
```

서버의 `~/apps/a305/bootstrap/compose`에는 Compose와 환경 예제를, `~/apps/a305/bootstrap/nginx`에는 응답 템플릿과 HTTP 프록시 예제를 둡니다. Compose의 상대 마운트 경로 때문에 두 폴더의 위치 관계를 유지합니다. 검토된 설정 버전만 이 경로에 반영합니다.

확인용 환경 변수는 다음 두 개입니다. 실제 값은 환경별 `bootstrap.env`에서 관리합니다.

| 변수 | 개발 | 운영 |
| --- | --- | --- |
| `DEPLOYMENT_ENVIRONMENT` | `dev` | `prod` |
| `BACKEND_HOST_PORT` | `8081` | `8080` |

환경 파일은 소스 폴더 밖에 두고 소유자만 읽고 쓸 수 있게 관리합니다. 예제에는 비밀값을 넣지 않습니다.

## 상태 확인과 재기동

먼저 개발 설정과 상태를 확인합니다.

```bash
cd ~/apps/a305/bootstrap
sudo docker compose -p a305-dev --env-file "$HOME/apps/a305/dev/bootstrap.env" -f compose/compose.bootstrap.yaml config --quiet
sudo docker compose -p a305-dev --env-file "$HOME/apps/a305/dev/bootstrap.env" -f compose/compose.bootstrap.yaml ps
curl --fail http://127.0.0.1:8081/health
```

운영은 프로젝트·환경 파일·포트를 함께 바꿔 확인합니다.

```bash
sudo docker compose -p a305-prod --env-file "$HOME/apps/a305/prod/bootstrap.env" -f compose/compose.bootstrap.yaml config --quiet
sudo docker compose -p a305-prod --env-file "$HOME/apps/a305/prod/bootstrap.env" -f compose/compose.bootstrap.yaml ps
curl --fail http://127.0.0.1:8080/health
```

정상 응답은 `service`가 `environment-check`, `status`가 `ok`이고, `environment`가 각각 `dev`·`prod`입니다. 포트 `8001`·`800`은 이 구성에서 사용하지 않습니다.

기동 또는 설정 반영은 필요한 환경에만 실행합니다. 설정 검사가 실패하면 실행하지 않습니다.

```bash
sudo docker compose -p a305-dev --env-file "$HOME/apps/a305/dev/bootstrap.env" -f compose/compose.bootstrap.yaml up -d --wait
```

운영을 반영할 때는 위 명령의 프로젝트를 `a305-prod`, 환경 파일을 `$HOME/apps/a305/prod/bootstrap.env`로 지정합니다. 기동 후 해당 포트의 응답을 다시 확인합니다.

로그와 중지는 개발 환경을 예로 들면 다음과 같습니다. `stop`은 해당 프로젝트의 컨테이너만 중지합니다.

```bash
sudo docker compose -p a305-dev --env-file "$HOME/apps/a305/dev/bootstrap.env" -f compose/compose.bootstrap.yaml logs --tail=100 environment-check
sudo docker compose -p a305-dev --env-file "$HOME/apps/a305/dev/bootstrap.env" -f compose/compose.bootstrap.yaml stop
```

일상적인 배포·정리에서 전체 프로젝트를 대상으로 한 `prune`이나 볼륨 삭제를 사용하지 않습니다. 앱 도입 후 데이터 삭제와 복구는 별도 절차로 관리합니다.

## Nginx 변경과 외부 접속 확인

기존 HTTPS 설정은 `/etc/nginx/sites-available/a305`에서 필요한 부분만 수정합니다. 변경 전 파일을 백업하고 인증서 경로·443 리스너·HTTP 리다이렉트를 보존합니다. 문법 검사가 성공한 경우에만 reload합니다.

```bash
sudo nginx -t
sudo systemctl reload nginx
sudo systemctl status nginx --no-pager
```

PC 브라우저에서 다음 주소를 열어 각각의 환경 이름을 확인합니다.

- [운영 상태](https://k15a305.p.ssafy.io/api/health): `prod`
- [개발 상태](https://k15a305.p.ssafy.io/dev/api/health): `dev`

프로젝트 예제에서 개발 경로는 외부 접근을 허용합니다. 기존 서버에서 개발 주소가 403이면 해당 location의 `allow`·`deny` 제한 여부를 확인합니다. TLS 항목을 보존하며 개발 경로만 반영한 뒤 양쪽 환경을 다시 확인합니다.

Certbot이 발급한 HTTPS 인증서는 예약 작업으로 자동 갱신합니다. 실제 인증서를 교체하지 않는 갱신 시험과 예약 확인은 다음과 같습니다.

```bash
sudo /snap/bin/certbot renew --dry-run
systemctl list-timers --all '*certbot*' --no-pager
```

`all simulated renewals succeeded`가 나오면 갱신 시험이 성공한 것입니다. 예약 목록에서는 Certbot 갱신 타이머의 다음 실행 시각을 확인합니다.

## 웹 파일 읽기 권한 오류

웹 컨테이너가 `unhealthy`이고 `/version.json`이 403을 반환하면 웹 로그에서 `Permission denied`를 확인합니다. 압축 파일로 옮긴 WebGL 산출물에 소유자만 읽을 수 있는 권한이 남으면 Nginx 작업 프로세스가 파일을 읽지 못할 수 있습니다.

```bash
sudo docker logs --tail 60 a305-dev-web-1
sudo docker inspect --format '{{json .State.Health}}' a305-dev-web-1
sudo docker exec a305-dev-web-1 ls -ld /usr/share/nginx/html /usr/share/nginx/html/version.json
```

웹 Dockerfile은 산출물을 복사한 뒤 이미지 내부의 정적 파일 디렉터리를 755, 파일을 644로 설정합니다. 수정한 Dockerfile로 새 태그의 웹 이미지를 만들고 개발 `deploy.env`의 `WEB_IMAGE`를 갱신한 다음, 앱 Compose에서 `up -d --no-deps --force-recreate --wait --pull never web`으로 웹 컨테이너만 교체합니다. 기존 DB·AI·백엔드는 유지합니다.

호스트의 `/home`이나 시스템 디렉터리 권한을 일괄 변경하지 않습니다. 실행 중인 컨테이너에서만 권한을 고치면 다음 컨테이너 생성 때 사라지므로 이미지에 반영합니다. 기동 후 루프백 `/version.json`과 `/api/health`, 개발 HTTPS 응답을 확인합니다. CI의 앱 실행 검사도 제한된 입력 권한을 사용해 같은 오류를 검증합니다.

## 장애 점검 순서

1. `ps`·로그와 루프백 주소의 `/health`로 컨테이너 상태와 포트를 확인합니다.
2. 내부는 정상인데 외부가 실패하면 Nginx 문법·실행 상태·error 로그와 URI 전달 경로를 확인합니다.
3. 도메인을 찾지 못하면 `k15a305.p.ssafy.io` 철자와 DNS를 확인합니다. 인증서 오류는 인증서 도메인·유효기간·체인과 Nginx 적용 경로를 확인합니다.
4. 접속이 끊기면 UFW 규칙, 서비스 리스너와 서버 자원을 점검합니다. SSH 접근 경로를 유지한 채 변경합니다.

```bash
sudo tail -n 100 /var/log/nginx/error.log
sudo ufw status verbose
sudo ss -lntp
sudo docker stats --no-stream
free -h
df -h /
```

공유 Nginx 변경 후에는 개발·운영을 모두 확인합니다. 앱 도입 후에는 상태 확인뿐 아니라 해당 앱의 대표 요청과 데이터 호환성을 포함한 복구 절차를 추가합니다.
