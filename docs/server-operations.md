# 개발·운영 서버 운영 방법

구성과 분리 기준은 [단일 서버의 개발·운영 구성](deployment.md)에서 관리합니다. 이 문서의 명령은 SSH로 접속한 Ubuntu 서버에서 실행합니다. 확인용 서비스와 실제 앱의 배포 명령을 구분합니다.

개발·운영 앱은 각각 PostgreSQL·AI·백엔드·WebGL 컨테이너를 사용합니다. 자동 배포 수신기를 설치한 환경은 해당 환경의 성공 기록과 관측 및 워커 네트워크 추가 Compose를 함께 사용합니다. 실행 파일과 고정 IP는 [GPU 워커 네트워크 운영](gpu-worker-network.md)을 따르며, 아래 확인용 명령은 실제 앱 전환 전의 초기 설치에만 사용합니다.

운영 앱의 최초 전환, 이후 main 자동 배포·버전 보관·이전 앱 복구와 DB 복구 범위는 [main 운영 배포와 복구](prod-deployment.md)에서 관리합니다. 운영 수신기는 개발과 다른 경로·키·환경 파일을 사용합니다.

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

## 개발 앱 상태 확인

실행 중인 컨테이너와 개발 HTTPS 응답을 확인합니다. 컨테이너 이름은 실제 `docker ps` 결과와 대조합니다.

```bash
sudo docker ps --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}'
curl --fail --silent --show-error -w '\n' https://dev.elsehere.kr/api/health
curl --fail --silent --show-error -w '\n' https://dev.elsehere.kr/version.json
sudo docker inspect --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}' a305-dev-backend-1
```

개발 health의 정상 응답은 `status: UP`입니다. 버전 파일은 웹 소스 커밋을 표시하며, 변경 없는 이미지를 재사용하면 전체 배포 후보와 다를 수 있습니다. 이미지·서버 성공 기록과 함께 확인합니다. 앱 재생성은 현재 성공 기록의 Compose·서버 환경 파일·모니터링 추가 파일 조합을 유지하고 [dev 자동 배포 운영](dev-auto-deployment.md)을 따릅니다. 확인용 Compose로 실제 개발 앱을 다시 기동하지 않습니다.

## 운영 확인용 서비스

운영 앱 전환 전의 확인용 서비스만 아래 명령을 사용합니다. 개발 포트는 이미 실제 앱이 사용합니다.

```bash
cd ~/apps/a305/bootstrap
sudo docker compose -p a305-prod --env-file "$HOME/apps/a305/prod/bootstrap.env" -f compose/compose.bootstrap.yaml config --quiet
sudo docker compose -p a305-prod --env-file "$HOME/apps/a305/prod/bootstrap.env" -f compose/compose.bootstrap.yaml ps
curl --fail http://127.0.0.1:8080/health
```

정상 응답은 `service: environment-check`, `status: ok`, `environment: prod`입니다. 포트 `8001`·`800`은 이 구성에서 사용하지 않습니다.

확인용 서비스 설정을 반영할 때는 검사가 성공한 경우에만 해당 프로젝트를 기동합니다.

```bash
sudo docker compose -p a305-prod --env-file "$HOME/apps/a305/prod/bootstrap.env" -f compose/compose.bootstrap.yaml up -d --wait
```

기동 후 해당 포트와 공개 HTTPS 응답을 확인합니다. 확인용 서비스의 로그 조회는 다음과 같습니다.

```bash
sudo docker compose -p a305-prod --env-file "$HOME/apps/a305/prod/bootstrap.env" -f compose/compose.bootstrap.yaml logs --tail=100 environment-check
```

일상적인 배포·정리에서 전체 프로젝트를 대상으로 한 `prune`이나 볼륨 삭제를 사용하지 않습니다. 앱 도입 후 데이터 삭제와 복구는 별도 절차로 관리합니다.

## Nginx 변경과 외부 접속 확인

도메인별 HTTPS 설정은 `/etc/nginx/sites-available/a305-elsehere`, 기존 SSAFY 경로는 `/etc/nginx/sites-available/a305`에서 관리합니다. 변경 전 해당 파일을 백업하고 인증서 경로·443 리스너·HTTP 리다이렉트·관측 include를 보존합니다. 문법 검사가 성공한 경우에만 reload합니다.

```bash
sudo nginx -t
sudo systemctl reload nginx
sudo systemctl status nginx --no-pager
```

PC 브라우저에서 다음 주소를 열어 환경에 맞는 응답을 확인합니다.

- [개발 앱](https://dev.elsehere.kr/): `/dev/` 없이 WebGL 접속
- [개발 상태](https://dev.elsehere.kr/api/health): HTTP 200·`status: UP`
- [운영 상태](https://elsehere.kr/api/health)·[www 상태](https://www.elsehere.kr/api/health): 운영 앱 전환 전의 `environment: prod` 확인용 응답

기존 SSAFY `/dev/`·`/dev/api/`·`/api/`는 호환을 위해 유지합니다. 403이면 해당 location의 접근 제한과 웹 파일 권한을 확인합니다. 공유 Nginx를 수정한 뒤 새 도메인과 호환 경로를 모두 확인합니다.

Certbot이 발급한 HTTPS 인증서는 예약 작업으로 자동 갱신합니다. 실제 인증서를 교체하지 않는 갱신 시험과 예약 확인은 다음과 같습니다.

```bash
sudo /snap/bin/certbot renew --cert-name elsehere.kr --dry-run
systemctl list-timers --all '*certbot*' --no-pager
```

`all simulated renewals succeeded`가 나오면 모의 갱신 시험이 성공한 것입니다. 예약 목록에서는 타이머의 다음 실행 시각을 확인합니다. 기존 SSAFY 인증서까지 검사할 때는 `--cert-name elsehere.kr`을 제외합니다. 상세 절차와 실제 브라우저 검증은 [HTTPS 운영](https.md)을 따릅니다.

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

1. `ps`·로그와 루프백 주소의 `/api/health`로 앱 상태와 포트를 확인합니다. 확인용 서비스의 `/health`와 구분합니다.
2. 내부는 정상인데 외부가 실패하면 Nginx 문법·실행 상태·error 로그와 URI 전달 경로를 확인합니다.
3. 도메인을 찾지 못하면 `dev.elsehere.kr`·`elsehere.kr` 철자와 DNS를 확인합니다. SSH 주소는 `k15a305.p.ssafy.io`입니다. 인증서 오류는 도메인·유효기간·체인과 Nginx 적용 경로를 확인합니다.
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
