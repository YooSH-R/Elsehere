# 개발·운영 AI 워커의 GPU 터널 네트워크

GPU 서버는 앱 서버로 역방향 SSH 터널을 연결합니다. 개발과 운영의 수신 주소를 나누고, 각 환경의 AI 워커만 접근하도록 워커 주소와 방화벽이 사용하는 브리지 이름을 고정합니다. 포트는 두 환경 모두 `18431`(STT), `18432`(판정), `18433`(TTS)이고, 주 GPU가 꺼졌을 때 부르는 [예비 GPU 입구](#예비-gpu-입구)는 `18441`(STT), `18443`(TTS)입니다.

| 항목 | 개발 | 운영 |
| --- | --- | --- |
| Docker 외부 네트워크 | `a305-dev_default` | `a305-prod_default` |
| 대역·게이트웨이 | `172.19.0.0/16` · `172.19.0.1` | `172.30.0.0/16` · `172.30.0.1` |
| 워커 주소 | `172.19.0.2` | `172.30.0.2` |
| 나머지 컨테이너 자동 할당 범위 | `172.19.128.0/17` | `172.30.128.0/17` |
| 호스트 브리지 | `br-a305-dev` | `br-a305-prod` |

네트워크는 앱 배포와 별도로 관리합니다. 워커 주소는 자동 할당 범위 밖에 두므로 다른 컨테이너가 해당 주소를 가져가지 않습니다. 백엔드는 같은 환경의 `ai-worker:8020`으로 호출합니다. 환경별 추가 Compose를 서로 바꿔 쓰거나 폐기용 CI 프로젝트에 적용하지 않습니다.

## 네트워크 준비와 최초 전환

대역을 정하기 전에 모든 Docker 네트워크와 호스트 라우팅을 확인합니다. CI 작업도 네트워크를 만들므로 현재 비어 있다는 가정만으로 주소를 정하지 않습니다. 운영 기준 대역은 `172.30.0.0/16`이며 `172.20.0.0/16`을 운영 값으로 사용하지 않습니다. 대역을 바꿀 때는 Compose·환경 파일·SSH 허용 주소·방화벽과 GPU 서버의 터널 수신 주소를 함께 변경합니다.

```bash
for network in $(sudo docker network ls -q); do
  sudo docker network inspect "$network" \
    --format '{{.Name}} {{range .IPAM.Config}}{{.Subnet}} {{end}}'
done
ip -4 route
```

개발 네트워크는 다음 도구로 생성하거나 검사합니다.

```bash
python3 infra/scripts/dev_worker_network.py --create
```

이 도구는 개발 전용입니다. 네트워크가 없으면 생성하고 이미 있으면 설정만 검사하며, 설정이 다른 기존 네트워크를 자동 삭제하지 않습니다. 운영 네트워크는 기존 연결을 안전하게 해제한 뒤 다음 기준으로 직접 생성합니다.

```bash
sudo docker network create --driver bridge \
  --subnet 172.30.0.0/16 --gateway 172.30.0.1 --ip-range 172.30.128.0/17 \
  --opt com.docker.network.bridge.name=br-a305-prod a305-prod_default
```

기존 네트워크의 최초 전환은 해당 환경의 배포 잠금을 확보한 뒤 진행합니다. 운영은 사용자가 없는 중단 창을 정하고 `prod:plan`·`prod:deliver`와 진행 중인 main 파이프라인이 없으며 main 병합 직후가 아닌지 확인합니다. 운영 잠금은 `/home/ubuntu/apps/a305/prod/deployments/deploy.lock`입니다.

1. 연결된 앱·Prometheus, 네트워크 주소·별칭, 이미지·볼륨, 환경 파일·추가 Compose·수신기 설정과 SSH·방화벽 설정을 백업합니다. 비밀값을 포함한 백업은 접근을 제한합니다.
2. SSH 허용 주소와 워커 전용 방화벽 규칙을 먼저 준비합니다. 이 준비에는 앱 중단이 필요하지 않습니다.
3. 해당 환경의 웹·백엔드·워커·DB를 중지하고 Prometheus를 포함한 기존 네트워크 연결을 해제합니다. 연결이 남지 않았는지 확인한 뒤 해당 네트워크만 교체합니다.
4. 기존 데이터 볼륨과 이미지를 유지하고 워커는 고정 주소, 나머지는 자동 주소와 기존 서비스 별칭으로 연결합니다. Prometheus의 다른 네트워크 연결도 유지합니다.
5. 아래 추가 Compose와 GPU 환경변수를 적용하고, 같은 중단 창에서 수신기의 `overlays`도 갱신합니다. DB·워커, 백엔드, 웹 순서로 시작하고 상태를 확인합니다.
6. 공개 HTTPS·Prometheus·IP 설정을 검증합니다. 실패하면 백업한 환경 파일·수신기·네트워크 설정·원래 주소·별칭과 방화벽으로 복구합니다.

이미 같은 기준으로 준비된 네트워크는 배포 때 다시 만들지 않습니다. Prometheus도 연결되어 있으므로 앱 컨테이너만 확인하고 네트워크를 삭제하지 않습니다. 전체 프로젝트 `down`, Docker 전체 정리와 데이터 볼륨 삭제는 사용하지 않습니다.

## Compose와 자동 배포

환경별 파일을 서버에 권한 `600`으로 설치하고 수신기의 `overlays`에 관측성 설정과 함께 등록합니다. 수동 재기동과 자동 배포의 실패 복구에도 같은 추가 Compose를 사용합니다.

| 환경 | 저장소 파일 | 서버 설치 경로 | 수신기 예제 |
| --- | --- | --- | --- |
| 개발 | `infra/compose/compose.dev-worker-network.yaml` | `/home/ubuntu/apps/a305/dev/config/compose.dev-worker-network.yaml` | `infra/deploy/dev-server.example.json` |
| 운영 | `infra/compose/compose.prod-worker-network.yaml` | `/home/ubuntu/apps/a305/prod/config/compose.prod-worker-network.yaml` | `infra/deploy/prod-server.example.json` |

```bash
install -d -m 700 "$HOME/apps/a305/dev/config"
install -m 600 infra/compose/compose.dev-worker-network.yaml \
  "$HOME/apps/a305/dev/config/compose.dev-worker-network.yaml"
install -d -m 700 "$HOME/apps/a305/prod/config"
install -m 600 infra/compose/compose.prod-worker-network.yaml \
  "$HOME/apps/a305/prod/config/compose.prod-worker-network.yaml"
```

수신기와 같은 폴더에 `dev_worker_network.py`도 설치합니다. 개발 수신기는 `fixed_worker_network: true`로 외부 네트워크 참조, 워커 고정 IP, 실제 대역·자동 할당 범위·브리지 이름과 주소 충돌을 검사합니다. 교체 후에는 실제 워커 주소와 명시적인 IP 지정 여부를 확인하며 설정이 다르면 배포를 실패 처리합니다.

운영 `/etc/a305-prod-deploy.json`의 `overlays`에는 관측 파일과 운영 네트워크 파일을 함께 넣습니다. 빠뜨리면 다음 main 배포에서 고정 IP가 사라지거나 외부 네트워크 충돌로 배포가 실패할 수 있습니다. **`fixed_worker_network`는 운영에 추가하지 않습니다.** 현재 구현은 개발 전용이라 운영에서 켜면 배포를 거절하며, 운영 고정 IP 자동 검사는 아직 구현되어 있지 않습니다. 아래 수동 IP 검증을 수행합니다.

## GPU 환경변수와 터널

운영 `deploy.env`에는 `infra/compose/deploy.prod.env.example`의 GPU 항목을 설정합니다. 실제 토큰은 서버에서만 관리하고, 기존에 같은 키가 있는지 확인한 뒤 추가하거나 수정합니다. 단순히 줄을 덧붙여 중복 키를 만들지 않습니다.

| 항목 | 운영 값 |
| --- | --- |
| `STT_CHAIN` | `gpu:turbo-lr1e4,gms:whisper-1,groq:whisper-large-v3-turbo` |
| `STT_GPU_BASE_URL` | `http://172.30.0.1:18431/v1` |
| `STT_FIRST_TIMEOUT_S` | `3` |
| `GUARD_GPU_MODEL` | `guard-qwen3.8-27b-r16` |
| `GUARD_GPU_BASE_URL` | `http://172.30.0.1:18432/v1` |
| `JUDGE_GPU_MODEL` | 비움. GPU 서버 의도 판정은 개발 서버에서 지연·대체 비율·사건 분포를 확인한 뒤 정함 |
| `NPC_GPU_MODEL` | `npc-qwen3.8-27b-r16`. 무례 판정과 같은 vLLM의 NPC 대답 LoRA. 워커마다 동시 4개(`NPC_GPU_MAX_CONCURRENT`)까지 GPU, 넘치거나 첫 글자가 1.5초를 넘으면 SOL |
| `TTS_GPU_BASE_URL` | `http://172.30.0.1:18433` |
| `STT_GPU_TOKEN`·`GUARD_GPU_TOKEN`·`TTS_GPU_TOKEN` | 같은 GPU 서비스의 개발 설정과 일치하는 값. 화면·저장소에 출력하지 않음 |

Spring과 AI 워커 사이의 `AI_WORKER_TOKEN`은 환경별로 분리합니다. 위 세 GPU 토큰을 공유하는 것과 구분합니다. GPU 설정을 켰다면 토큰도 채워야 워커가 시작할 수 있습니다. GPU를 쓰지 않는 구성은 `STT_CHAIN`·`STT_GPU_BASE_URL`·`STT_GPU_BACKUP_BASE_URL`·`GUARD_GPU_MODEL`·`GUARD_GPU_BASE_URL`·`TTS_GPU_BASE_URL`·`TTS_GPU_BACKUP_BASE_URL`을 비워 API·CPU 경로를 사용합니다.

앱 서버의 터널 계정 `a305-stt-tunnel`은 기존 공개키 인증·원격 포워딩 제한을 유지하고 `PermitListen`에 운영 수신 주소를 함께 허용합니다. 기존 개발 주소도 유지합니다.

```text
PermitListen 172.19.0.1:18431 172.19.0.1:18432 172.19.0.1:18433 172.30.0.1:18431 172.30.0.1:18432 172.30.0.1:18433
```

`/etc/ssh/sshd_config.d/60-a305-stt-tunnel.conf`를 변경한 뒤 `sudo sshd -t`가 성공할 때만 `sudo systemctl reload ssh`로 반영합니다. GPU 서버 담당자는 개발 터널을 유지하면서 운영 `172.30.0.1:18431`·`18432`·`18433`으로 두 번째 역방향 터널을 연결합니다. 네트워크 설정만으로 GPU 서버 쪽 터널이 켜지지는 않습니다.

`PermitListen`과 `/home/a305-stt-tunnel/.ssh/authorized_keys`의 키별 `permitlisten` 제한을 모두 통과해야 터널이 연결됩니다. 기존 키 줄의 공개키·`restrict`·포워딩 제한·개발 주소를 유지하면서 다음 옵션을 같은 키 줄에 추가합니다. 키 파일의 기존 소유자와 권한 `600`도 유지합니다.

```text
permitlisten="172.30.0.1:18431",permitlisten="172.30.0.1:18432",permitlisten="172.30.0.1:18433"
```

sshd 설정에만 운영 주소를 허용하면 키별 제한에 걸려 `remote port forwarding failed`가 발생할 수 있습니다. 키 옵션 변경은 다음 SSH 인증부터 적용되므로 기존 개발 연결을 끊을 필요는 없습니다.

GPU 서버의 STT·TTS·판정 감시 스크립트는 각 서비스 폴더의 `prod_tunnel_enabled` 파일이 있을 때 운영 터널을 함께 유지합니다. 서버 준비 뒤 `~/stt/gpu_stt/prod_tunnel_enabled`·`~/tts/gpu_tts/prod_tunnel_enabled`·`~/guard/prod_tunnel_enabled`를 만듭니다. 감시 스크립트가 실행 중이면 끊어진 터널을 30초마다 다시 연결하고, Jupyter 서버가 다시 시작되면 [GPU 서버 자동 시작](../ai/gpu_autostart/README.md) 훅이 감시 스크립트를 다시 띄웁니다. 실행·토큰 기준은 [GPU STT](../ai/gpu_stt/README.md), [GPU TTS](../ai/gpu_tts/README.md), [무례 판정](../ai/guard/README.md)을 따릅니다. SSAFY GPU 서버를 쓸 수 없을 때 STT·TTS·판정 vLLM을 옮기는 임대 서버도 같은 터널 계정·입구·토큰을 쓰며, 절차는 [임대 GPU 서버](../ai/gpu_rental/README.md)를 따릅니다.

## 예비 GPU 입구

같은 입구는 한 GPU만 잡을 수 있으므로, 주 GPU가 꺼졌을 때 이어받을 GPU는 별도 입구로 터널을 겁니다. 워커는 주 GPU, 예비 GPU 순서로 부르고 둘 다 실패할 때만 STT는 API 공급자, TTS는 CPU Kokoro로 넘깁니다. 연속 실패에 따른 건너뛰기는 GPU마다 따로 셉니다. 무례·의도 판정은 SSAFY GPU 서버만 쓰므로 예비 입구를 두지 않습니다.

| 서비스 | 주 입구 | 예비 입구 | 워커 설정 |
| --- | --- | --- | --- |
| STT | `18431` | `18441` | `STT_CHAIN`의 `gpu` 다음에 `gpu_backup`을 넣고 `STT_GPU_BACKUP_BASE_URL`을 지정 |
| TTS | `18433` | `18443` | `TTS_GPU_BACKUP_BASE_URL`을 지정 |

예비 GPU도 같은 STT·TTS 서버 프로그램과 토큰 파일을 쓰므로 토큰은 `STT_GPU_TOKEN`·`TTS_GPU_TOKEN`을 그대로 씁니다. 개발은 팀 노트북 GPU를 주 입구에, SSAFY GPU 서버를 예비 입구에 둡니다.

```text
STT_CHAIN=gpu:turbo-lr1e4,gpu_backup:turbo-lr1e4,gms:whisper-1,groq:whisper-large-v3-turbo
STT_GPU_BACKUP_BASE_URL=http://172.19.0.1:18441/v1
TTS_GPU_BACKUP_BASE_URL=http://172.19.0.1:18443
```

운영은 SSAFY GPU 서버를 주 입구에 두고 예비 항목을 비웁니다. 사용자 녹음과 대화 문장이 예비 GPU로도 전달되므로, 운영 예비 GPU는 [개인정보 처리방침](../backend/src/main/resources/templates/legal/privacy.html)의 처리 위탁 표에 해당 GPU가 들어간 뒤에만 지정합니다.

앱 서버는 기존 주소에 예비 입구를 더해 `PermitListen`과 키별 `permitlisten`을 허용합니다.

```text
PermitListen 172.19.0.1:18431 172.19.0.1:18432 172.19.0.1:18433 172.19.0.1:18441 172.19.0.1:18443 172.30.0.1:18431 172.30.0.1:18432 172.30.0.1:18433 172.30.0.1:18441 172.30.0.1:18443
permitlisten="172.19.0.1:18441",permitlisten="172.19.0.1:18443",permitlisten="172.30.0.1:18441",permitlisten="172.30.0.1:18443"
```

같은 `Match User a305-stt-tunnel` 블록에서 끊긴 터널 감지를 켭니다. 노트북 절전이나 전원 차단처럼 GPU 쪽이 연결 종료를 알리지 못하고 꺼지면 sshd가 입구를 계속 열어 둡니다. 그동안 워커의 연결은 성공하지만 응답이 오지 않아 시간 상한(TTS 10초, STT 첫 공급자 `STT_FIRST_TIMEOUT_S`)까지 기다립니다. 감지를 켜면 약 45초 안에 입구가 닫히고, 이후 연결은 바로 거절되어 워커가 예비 GPU로 넘어갑니다.

```text
ClientAliveInterval 15
ClientAliveCountMax 3
```

GPU 쪽 감시 스크립트는 파일이 있을 때만 예비 터널을 겁니다. 개발 예비는 `~/stt/gpu_stt/dev_backup_tunnel_enabled`·`~/tts/gpu_tts/dev_backup_tunnel_enabled`, 운영 예비는 같은 폴더의 `prod_backup_tunnel_enabled`입니다. 예비를 맡는 GPU는 같은 환경의 주 터널을 끕니다(STT는 `dev_tunnel_disabled`를 만들고, TTS는 `tunnel_enabled`를 두지 않음).

## 방화벽

UFW INPUT 규칙은 워커만 허용하고 다른 주소는 차단합니다. 허용 규칙을 해당 포트의 DENY보다 앞에 둡니다.

```bash
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18431 proto tcp comment 'a305 dev GPU STT worker only'
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18432 proto tcp comment 'a305 dev GPU guard worker only'
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18433 proto tcp comment 'a305 dev GPU TTS worker only'
```

예비 입구도 워커만 허용하고 다른 출발지를 차단합니다.

```bash
sudo ufw deny in from any to 172.19.0.1 port 18441 proto tcp comment 'a305 dev backup GPU STT deny other sources'
sudo ufw deny in from any to 172.19.0.1 port 18443 proto tcp comment 'a305 dev backup GPU TTS deny other sources'
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18441 proto tcp comment 'a305 dev backup GPU STT worker only'
sudo ufw insert 1 allow in on br-a305-dev from 172.19.0.2 to 172.19.0.1 port 18443 proto tcp comment 'a305 dev backup GPU TTS worker only'
```

다른 주소에서 `172.19.0.1`의 GPU 포트로 들어오는 DENY 규칙을 유지합니다. 이전 브리지 허용은 전환 검증 후 정확히 일치하는 규칙만 제거합니다. SSH·HTTP·HTTPS와 다른 환경의 방화벽은 기존 정책을 유지합니다.

운영도 워커 전용 허용과 그 외 출발지 차단을 함께 설정합니다. 규칙은 브리지가 만들어지기 전에도 준비할 수 있습니다.

```bash
sudo ufw deny in from any to 172.30.0.1 port 18431 proto tcp comment 'a305 prod GPU STT deny other sources'
sudo ufw deny in from any to 172.30.0.1 port 18432 proto tcp comment 'a305 prod GPU guard deny other sources'
sudo ufw deny in from any to 172.30.0.1 port 18433 proto tcp comment 'a305 prod GPU TTS deny other sources'
sudo ufw insert 1 allow in on br-a305-prod from 172.30.0.2 to 172.30.0.1 port 18431 proto tcp comment 'a305 prod GPU STT worker only'
sudo ufw insert 1 allow in on br-a305-prod from 172.30.0.2 to 172.30.0.1 port 18432 proto tcp comment 'a305 prod GPU guard worker only'
sudo ufw insert 1 allow in on br-a305-prod from 172.30.0.2 to 172.30.0.1 port 18433 proto tcp comment 'a305 prod GPU TTS worker only'
```

운영 예비 입구 `18441`·`18443`도 같은 형식으로 준비합니다. 운영 허용 규칙은 해당 DENY보다 앞에 있어야 합니다. GPU 포트는 공개 IP나 `0.0.0.0`에 열지 않습니다.

## 재배포 검증

1. 현재 배포 기록의 Compose·환경 파일·관측성 및 네트워크 추가 Compose를 함께 사용해 워커를 재생성합니다.
2. `docker inspect`에서 워커 주소와 `IPAMConfig.IPv4Address`가 모두 개발 `172.19.0.2`, 운영 `172.30.0.2`인지 확인합니다. 실제 IP만 맞고 `IPAMConfig`가 비어 있으면 고정 IP 적용으로 판단하지 않습니다.
3. 워커에서 세 GPU 포트의 `/health`가 모두 HTTP 200인지 확인합니다. 예비 입구를 지정한 환경은 `18441`·`18443`도 확인합니다. 실제 음성 처리 검증은 별도로 수행합니다.
4. 웹의 동일 포트 접근 차단과 UFW 허용·차단 카운터를 확인합니다.
5. 개발·운영 HTTPS API, 백엔드의 워커 서비스 이름 조회, Prometheus의 새로운 수집 결과와 최근 GPU 연결 오류를 확인합니다.

```bash
sudo docker inspect a305-prod-ai-worker-1 --format '{{json .NetworkSettings.Networks}}'
sudo docker exec -i a305-prod-ai-worker-1 python - <<'PY'
import urllib.request

for port in [18431, 18432, 18433]:
    with urllib.request.urlopen(f"http://172.30.0.1:{port}/health", timeout=3) as response:
        assert response.status == 200
        print(port, response.status)
PY
```

고정 IP 설정과 GPU 터널·모델의 실제 동작은 각각 확인합니다. 터널이 꺼져 있으면 워커의 연결이 거절될 수 있습니다. 이때 서비스 health가 정상이더라도 GPU 검증이 끝난 것은 아닙니다. 실제 대화에서 STT·판정·TTS의 사용 모델과 대체 여부를 확인합니다. GPU 오류 시 STT·TTS는 예비 GPU를 지정했으면 그쪽을 먼저 부르고, 그다음 STT는 API 공급자, TTS는 CPU Kokoro로 대체합니다. 판정은 구성의 판정 모델로 대체합니다. 자세한 응답과 대체 조건은 [AI 워커 API](api/ai-worker.md)를 따릅니다.

## GPU 자원 공유와 자동 복구

개발과 운영은 같은 GPU 5번과 동시 처리 한도를 공유하므로 개발 부하 시험도 운영 지연에 영향을 줍니다. 운영 사용 중에는 GPU 부하 시험을 조율합니다.

GPU 감시 스크립트가 실행 중이면 서비스와 터널을 다시 띄웁니다. Jupyter 서버가 종료되면 감시 스크립트도 함께 종료되므로, 담당자가 Jupyter 서버를 시작한 뒤 감시 스크립트들을 다시 실행합니다. Jupyter 시작 시 자동 실행과 내부망 PC를 통한 JupyterHub 재기동 감시는 구성하지 않습니다. 학원 GPU 자원의 서비스용 24시간 사용 가능 여부와 점검 일정은 운영 규칙에 따라 확인합니다.
