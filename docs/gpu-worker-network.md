# 개발 AI 워커의 GPU 터널 네트워크

GPU 서버의 역방향 SSH 터널은 개발 서버의 `172.19.0.1:18431`(STT), `18432`(판정), `18433`(TTS)에서 받습니다. 개발 AI 워커만 접근하도록 워커 주소와 방화벽이 사용하는 브리지 이름을 고정합니다.

| 항목 | 설정 |
| --- | --- |
| Docker 네트워크 | `a305-dev_default`, 앱 배포와 별도로 관리하는 외부 네트워크 |
| 대역·게이트웨이 | `172.19.0.0/16` · `172.19.0.1` |
| 워커 주소 | `172.19.0.2` |
| 나머지 컨테이너 자동 할당 범위 | `172.19.128.0/17` |
| 호스트 브리지 | `br-a305-dev` |

워커 주소는 자동 할당 범위 밖에 두므로 다른 컨테이너가 해당 주소를 가져가지 않습니다. 백엔드는 기존 서비스 이름 `ai-worker:8020`으로 호출하고 GPU 터널 주소는 유지합니다. 운영과 폐기용 CI 프로젝트에는 이 개발 전용 설정을 적용하지 않습니다.

## 네트워크 준비와 최초 전환

```bash
python3 infra/scripts/dev_worker_network.py --create
```

네트워크가 없으면 생성하고 이미 있으면 설정만 검사합니다. 설정이 다른 기존 네트워크를 자동 삭제하지 않습니다.

최초 전환은 개발 배포 잠금을 확보한 뒤 진행합니다. 연결된 앱·Prometheus, 네트워크 주소·별칭, 방화벽과 배포 설정을 백업합니다. 개발 컨테이너를 중지하고 기존 연결을 해제한 뒤 위 설정으로 네트워크를 다시 만듭니다. 기존 데이터 볼륨과 이미지를 유지하고 워커는 고정 주소, 나머지는 자동 주소와 기존 서비스 별칭으로 연결합니다. DB·워커, 백엔드, 웹·Prometheus 순서로 시작하며 정상 상태를 확인합니다. 실패하면 백업한 네트워크 설정·원래 주소·별칭과 방화벽으로 복구합니다.

Prometheus도 연결되어 있으므로 앱 컨테이너만 확인하고 네트워크를 삭제하지 않습니다. 전체 Docker 정리나 데이터 볼륨 삭제는 사용하지 않습니다.

## Compose와 자동 배포

`infra/compose/compose.dev-worker-network.yaml`을 서버의 `/home/ubuntu/apps/a305/dev/config/compose.dev-worker-network.yaml`에 설치합니다. 개발 수신기의 `overlays`에 기존 관측성 설정과 함께 등록하고 `fixed_worker_network: true`를 설정합니다. 예시는 `infra/deploy/dev-server.example.json`입니다.

```bash
install -d -m 700 "$HOME/apps/a305/dev/config"
install -m 600 infra/compose/compose.dev-worker-network.yaml \
  "$HOME/apps/a305/dev/config/compose.dev-worker-network.yaml"
```

수신기와 같은 폴더에 `dev_worker_network.py`도 설치합니다. 교체 전 외부 네트워크 참조, 워커 고정 IP, 실제 대역·자동 할당 범위·브리지 이름과 주소 충돌을 검사합니다. 교체 후에는 실제 워커 주소와 명시적인 IP 지정 여부를 확인합니다. 설정이 다르면 배포를 실패 처리합니다. 재기동과 배포 복구에서도 같은 서버 추가 Compose를 사용합니다.

## 방화벽

UFW INPUT 규칙은 워커만 허용하고 다른 주소는 차단합니다. 허용 규칙을 해당 포트의 DENY보다 앞에 둡니다.

```bash
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18431 proto tcp comment 'a305 dev GPU STT worker only'
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18432 proto tcp comment 'a305 dev GPU guard worker only'
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18433 proto tcp comment 'a305 dev GPU TTS worker only'
```

다른 주소에서 `172.19.0.1`의 세 포트로 들어오는 DENY 규칙을 유지합니다. 이전 브리지 허용은 전환 검증 후 정확히 일치하는 규칙만 제거합니다. SSH·HTTP·HTTPS와 다른 환경의 방화벽은 기존 정책을 유지합니다.

## 재배포 검증

1. 현재 배포 기록의 Compose·환경 파일·관측성 및 네트워크 추가 Compose를 함께 사용해 워커를 재생성합니다.
2. `docker inspect`에서 워커 주소와 `IPAMConfig.IPv4Address`가 모두 `172.19.0.2`인지 확인합니다.
3. 워커에서 세 GPU 포트의 `/health`가 모두 HTTP 200인지 확인합니다. 실제 음성 처리 검증은 별도로 수행합니다.
4. 웹의 동일 포트 접근 차단과 UFW 허용·차단 카운터를 확인합니다.
5. 개발 HTTPS API, 백엔드의 워커 서비스 이름 조회, Prometheus의 새로운 수집 결과와 최근 GPU 연결 오류를 확인합니다.

고정 IP 설정과 GPU 터널·모델의 실제 동작은 각각 확인합니다.
