# 개발·운영 서버 운영 방법

구성과 분리 기준은 [단일 서버의 개발·운영 구성](deployment.md)에서 관리합니다. 이 문서의 명령은 SSH로 접속한 Ubuntu 서버에서 실행합니다. 확인용 서비스와 실제 앱의 배포 명령을 구분합니다.

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
