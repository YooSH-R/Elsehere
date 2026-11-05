# AI 워커의 GPU 터널 네트워크

GPU 서버는 학원 방화벽 때문에 바깥에서 들어갈 수 없어서, GPU 서버가 배포 서버로 역방향 SSH 터널을 겁니다. 터널 입구는 각 환경 Docker 네트워크의 게이트웨이에 열리며 포트는 `18431`(STT), `18432`(판정), `18433`(TTS)입니다. 해당 환경의 AI 워커만 접근하도록 워커 주소와 방화벽이 사용하는 브리지 이름을 고정합니다.

| 항목 | 개발 | 운영 |
| --- | --- | --- |
| Docker 네트워크 | `a305-dev_default` | `a305-prod_default` |
| 대역·게이트웨이(터널 입구) | `172.19.0.0/16` · `172.19.0.1` | `172.20.0.0/16` · `172.20.0.1` |
| 워커 주소 | `172.19.0.2` | `172.20.0.2` |
| 나머지 컨테이너 자동 할당 범위 | `172.19.128.0/17` | `172.20.128.0/17` |
| 호스트 브리지 | `br-a305-dev` | `br-a305-prod` |
| 추가 Compose | `infra/compose/compose.dev-worker-network.yaml` | `infra/compose/compose.prod-worker-network.yaml` |

두 네트워크 모두 앱 배포와 별도로 관리하는 외부 네트워크입니다. 워커 주소는 자동 할당 범위 밖에 두므로 다른 컨테이너가 해당 주소를 가져가지 않습니다. 백엔드는 기존 서비스 이름 `ai-worker:8020`으로 호출합니다. 폐기용 CI 프로젝트에는 이 설정을 적용하지 않습니다.

GPU 서버의 모델 프로세스는 하나이며 개발·운영 터널이 같은 프로세스로 이어집니다. 토큰과 동시 처리 한도도 두 환경이 함께 사용하므로, 개발에서 부하 시험을 하면 운영 응답도 느려집니다. 터널이나 GPU 서버가 끊기면 워커는 STT를 API로, 판정을 프로필 모델로, 합성을 CPU Kokoro로 대체합니다.

## 네트워크 준비와 최초 전환

개발 네트워크는 검사 스크립트로 만들거나 검사합니다.

```bash
python3 infra/scripts/dev_worker_network.py --create
```

네트워크가 없으면 생성하고 이미 있으면 설정만 검사합니다. 설정이 다른 기존 네트워크를 자동 삭제하지 않습니다.

운영 네트워크는 서버에서 다른 네트워크가 `172.20.0.0/16`을 쓰지 않는지 확인한 뒤 직접 만듭니다. 대역을 바꿔야 하면 추가 Compose·방화벽·GPU 서버 터널 주소·운영 환경 파일을 같은 값으로 함께 바꿉니다.

```bash
sudo docker network create --driver bridge \
  --subnet 172.20.0.0/16 --gateway 172.20.0.1 --ip-range 172.20.128.0/17 \
  -o com.docker.network.bridge.name=br-a305-prod a305-prod_default
```

최초 전환은 해당 환경의 배포 잠금을 확보하고, 운영은 배포 파이프라인이 돌지 않는 사용자가 적은 시간에 진행합니다. 연결된 앱·Prometheus, 네트워크 주소·별칭, 방화벽과 배포 설정을 백업합니다. 앱 컨테이너를 중지하고 기존 연결을 해제한 뒤 위 설정으로 네트워크를 다시 만듭니다. 기존 데이터 볼륨과 이미지를 유지하고 워커는 고정 주소, 나머지는 자동 주소와 기존 서비스 별칭으로 연결합니다. DB·워커, 백엔드, 웹·Prometheus 순서로 시작하며 정상 상태를 확인합니다. 실패하면 백업한 네트워크 설정·원래 주소·별칭과 방화벽으로 복구합니다.

Prometheus도 연결되어 있으므로 앱 컨테이너만 확인하고 네트워크를 삭제하지 않습니다. 전체 Docker 정리나 데이터 볼륨 삭제는 사용하지 않습니다.

## Compose와 자동 배포

추가 Compose를 서버에 설치하고 각 환경 수신기의 `overlays`에 기존 관측성 설정과 함께 등록합니다. 수동 기동 명령에도 같은 파일을 `-f`로 더합니다. 등록하지 않으면 다음 자동 배포가 고정 주소 없이 워커를 다시 만듭니다.

```bash
install -d -m 700 "$HOME/apps/a305/dev/config"
install -m 600 infra/compose/compose.dev-worker-network.yaml \
  "$HOME/apps/a305/dev/config/compose.dev-worker-network.yaml"
```

```bash
install -d -m 700 "$HOME/apps/a305/prod/config"
install -m 600 infra/compose/compose.prod-worker-network.yaml \
  "$HOME/apps/a305/prod/config/compose.prod-worker-network.yaml"
```

개발 수신기는 `fixed_worker_network: true`를 설정하고(예시 `infra/deploy/dev-server.example.json`), 같은 폴더에 `dev_worker_network.py`도 설치합니다. 교체 전 외부 네트워크 참조, 워커 고정 IP, 실제 대역·자동 할당 범위·브리지 이름과 주소 충돌을 검사합니다. 교체 후에는 실제 워커 주소와 명시적인 IP 지정 여부를 확인합니다. 설정이 다르면 배포를 실패 처리합니다. 재기동과 배포 복구에서도 같은 서버 추가 Compose를 사용합니다.

운영 수신기는 추가 Compose만 `overlays`에 등록합니다(예시 `infra/deploy/prod-server.example.json`). 수신기의 고정 네트워크 자동 검사는 개발 값만 지원하므로 운영 설정에는 `fixed_worker_network`를 넣지 않고, 아래 재배포 검증을 직접 수행합니다.

## 방화벽과 터널 허용

UFW INPUT 규칙은 해당 환경의 워커만 허용하고 다른 주소는 차단합니다. 허용 규칙을 해당 포트의 DENY보다 앞에 둡니다.

```bash
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18431 proto tcp comment 'a305 dev GPU STT worker only'
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18432 proto tcp comment 'a305 dev GPU guard worker only'
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18433 proto tcp comment 'a305 dev GPU TTS worker only'
```

```bash
sudo ufw insert 1 allow in on br-a305-prod from 172.20.0.2 to 172.20.0.1 port 18431 proto tcp comment 'a305 prod GPU STT worker only'
sudo ufw insert 1 allow in on br-a305-prod from 172.20.0.2 to 172.20.0.1 port 18432 proto tcp comment 'a305 prod GPU guard worker only'
sudo ufw insert 1 allow in on br-a305-prod from 172.20.0.2 to 172.20.0.1 port 18433 proto tcp comment 'a305 prod GPU TTS worker only'
```

다른 주소에서 각 게이트웨이의 세 포트로 들어오는 DENY 규칙을 유지합니다. 이전 브리지 허용은 전환 검증 후 정확히 일치하는 규칙만 제거합니다. SSH·HTTP·HTTPS와 다른 환경의 방화벽은 기존 정책을 유지합니다.

터널 전용 계정 `a305-stt-tunnel`은 포트 전달만 허용합니다. 운영 터널을 받으려면 기존 개발 입구와 같은 방식으로 `172.20.0.1:18431`, `172.20.0.1:18432`, `172.20.0.1:18433`의 원격 전달을 추가로 허용하고 sshd 설정을 다시 읽습니다.

## GPU 서버 터널

GPU 서버의 감시 스크립트([`ai/gpu_stt`](../ai/gpu_stt/README.md), [`ai/gpu_tts`](../ai/gpu_tts/README.md))가 30초마다 터널을 확인하고 끊기면 다시 겁니다. 운영 터널은 배포 서버의 허용·방화벽·네트워크가 준비된 뒤 GPU 서버에 `prod_tunnel_enabled` 파일을 만들어 켭니다. 판정(vLLM) 터널도 같은 입구 주소를 사용합니다.

## 환경 파일

GPU 경로는 각 환경 `deploy.env`의 `STT_CHAIN`, `STT_GPU_*`, `GUARD_GPU_*`, `TTS_GPU_*`로 켭니다. 주소는 해당 환경의 게이트웨이를 사용하고(개발 `172.19.0.1`, 운영 `172.20.0.1`), 토큰은 GPU 서버가 하나이므로 두 환경에 같은 값을 넣습니다. 값이 비어 있으면 해당 GPU 경로를 쓰지 않습니다.

## 재배포 검증

1. 현재 배포 기록의 Compose·환경 파일·관측성 및 네트워크 추가 Compose를 함께 사용해 워커를 재생성합니다.
2. `docker inspect`에서 워커 주소와 `IPAMConfig.IPv4Address`가 모두 해당 환경의 워커 주소인지 확인합니다.
3. 워커에서 세 GPU 포트의 `/health`가 모두 HTTP 200인지 확인합니다. 실제 음성 처리 검증은 별도로 수행합니다.
4. 웹의 동일 포트 접근 차단과 UFW 허용·차단 카운터를 확인합니다.
5. 해당 환경의 HTTPS API, 백엔드의 워커 서비스 이름 조회, Prometheus의 새로운 수집 결과와 최근 GPU 연결 오류를 확인합니다.

고정 IP 설정과 GPU 터널·모델의 실제 동작은 각각 확인합니다.
