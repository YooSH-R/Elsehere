# 임대 GPU 서버 (예비)

SSAFY GPU 서버를 쓸 수 없을 때 GPU 서비스를 옮겨 돌릴 임대 GPU 서버의 설치·전환 절차입니다. SSAFY 서버는 여러 팀이 같이 쓰는 공용 서버라 사용이 제한되거나, 다른 팀이 5번 GPU 메모리를 차지해 우리 서버가 뜨지 못할 수 있습니다.

| 서비스 | 설치 | 설명 |
| --- | --- | --- |
| STT | `setup.sh` | [`ai/gpu_stt`](../gpu_stt/README.md) |
| 음성 합성 | `setup.sh` | [`ai/gpu_tts`](../gpu_tts/README.md) |
| 무례 판정·NPC 대답 vLLM | `setup_vllm.sh` | Qwen3.8-27B에 LoRA 2개를 얹은 vLLM 하나([무례 판정](../guard/README.md), [NPC 대답](../npc_reply/README.md)) |

- 필요한 서비스만 설치합니다. STT·합성을 다른 GPU(팀 노트북 등)가 맡으면 vLLM만 설치합니다. 옮기지 않은 GPU 서비스는 워커가 구성의 대체 경로(STT는 GMS·Groq, 합성은 CPU, 판정은 프로필 모델, NPC 대답은 SOL)로 처리합니다.
- 임대 서버도 SSAFY 서버와 같은 터널 계정(`a305-stt-tunnel`), 터널 입구, 토큰·키, 모델 이름을 씁니다. 그래서 워커 환경변수와 배포 서버 설정은 바꾸지 않습니다([GPU 터널 네트워크](../../docs/gpu-worker-network.md)).
- 서버는 127.0.0.1에서만 요청을 받고 임대 서버가 배포 서버로 터널을 겁니다. 임대 서버에서 공개 포트를 열지 않습니다.
- 운영 사용자 녹음과 대화 문장이 임대 서버로 전달되므로, 운영 터널은 [개인정보 처리방침](../../backend/src/main/resources/templates/legal/privacy.html)의 처리 위탁 표에 해당 업체가 들어간 뒤에만 켭니다. 그 전에는 개발 터널만 켭니다.

## 서버 조건

| 항목 | STT·합성 | vLLM |
| --- | --- | --- |
| GPU | 메모리 24GB 이상 한 장(STT 묶음 약 10GB, 합성 약 5GB). 기준 카드 RTX 4090 | 메모리 80GB 이상 한 장(모델 약 54GB + 요청 처리 공간). 지금과 같은 속도는 H200 141GB, 차선은 H100 80GB |
| 디스크 | 서버를 멈춰도 남는 디스크 20GB 이상 | 90GB 이상(모델 약 54GB, vLLM 환경 약 15GB) |

- 드라이버: NVIDIA 드라이버 570 이상(SSAFY 서버가 570에서 CUDA 12.9 판 vLLM을 씁니다), `nvidia-smi`가 동작하는 이미지. 운영체제는 Ubuntu 22.04 또는 24.04입니다.
- 네트워크: 바깥으로 나가는 SSH(22)와 HTTPS. 들어오는 포트는 필요 없습니다. 서버를 빌린 직후 `nc -vz k15a305.p.ssafy.io 22`로 SSH가 나가는지 먼저 확인합니다.
- 위치: 배포 서버(서울)와 가까울수록 요청마다 붙는 왕복 시간이 줄어듭니다. 한국·일본 리전을 고릅니다.

컨테이너로 빌려주는 서비스(RunPod 등)는 서버를 멈추면 컨테이너 디스크가 지워집니다. 남는 볼륨(예: `/workspace`)을 홈으로 지정해 설치·실행합니다.

```bash
export HOME=/workspace
```

## 옮겨 올 파일

저장소에 없는 파일은 SSAFY GPU 서버에서 같은 경로로 옮깁니다. 토큰과 키는 채팅·저장소·Jira에 붙이지 않습니다.

| 파일 | 경로 (홈 기준) | 필요한 서비스 | 설명 |
| --- | --- | --- | --- |
| 터널 키 | `stt/gpu_stt/tunnel_key`, `stt/gpu_stt/known_hosts` | 모두 | `a305-stt-tunnel` 계정 키와 배포 서버 호스트 키 |
| STT 모델 | `stt/stt_finetune/checkpoints/turbo-lr1e4/ct2/` | STT | 자체 학습 모델(약 1.6GB). 학습 데이터는 옮기지 않습니다. |
| STT 토큰 | `stt/gpu_stt/token` | STT | 워커의 `STT_GPU_TOKEN`과 같은 값 |
| 합성 토큰 | `tts/gpu_tts/token` | 합성 | 워커의 `TTS_GPU_TOKEN`과 같은 값 |
| LoRA 어댑터 | `guard/models/guard-qwen3.8-27b-r16/`, `guard/models/npc-qwen3.8-27b-r16/` | vLLM | 무례 판정·NPC 대답 어댑터(각 약 435MB) |
| vLLM 키 | `guard/vllm_token` | vLLM | 워커의 `GUARD_GPU_TOKEN`과 같은 값 |

SSAFY 서버에서 필요한 파일만 한 파일로 묶습니다. 묶음은 권한 `600`으로 만들고 옮긴 뒤 지웁니다.

```bash
(umask 077 && tar -C ~ -cf ~/gpu_rental_files.tar \
  stt/gpu_stt/tunnel_key stt/gpu_stt/known_hosts \
  stt/stt_finetune/checkpoints/turbo-lr1e4/ct2 stt/gpu_stt/token tts/gpu_tts/token \
  guard/models/guard-qwen3.8-27b-r16 guard/models/npc-qwen3.8-27b-r16 guard/vllm_token)
```

임대 서버 홈에서 풉니다.

```bash
tar -C "$HOME" -xf gpu_rental_files.tar && rm gpu_rental_files.tar
```

## 설치

저장소의 `ai/` 폴더를 임대 서버에 올린 뒤 필요한 스크립트를 실행합니다. 두 스크립트 모두 다시 실행해도 됩니다.

```bash
sh ai/gpu_rental/setup.sh       # STT·합성
sh ai/gpu_rental/setup_vllm.sh  # 무례 판정·NPC 대답 vLLM
```

`setup.sh`

- SSAFY 서버와 같은 폴더(`~/stt`, `~/tts`)에 STT·합성 가상환경을 만들고, SSAFY 서버와 같은 버전의 패키지를 설치합니다.
- STT의 CUDA 라이브러리(cuBLAS·cuDNN)는 pip 패키지로 받고, 실행기 `~/stt/run.sh`가 그 경로를 지정합니다. SSAFY 서버의 `~/stt/gpu5.sh`를 대신하며 0번 GPU를 씁니다.
- Kokoro 모델을 받아 SSAFY 서버 파일과 해시가 같은지 확인합니다.

`setup_vllm.sh`

- `~/guard`에 SSAFY 서버와 같은 vLLM(0.32.0, CUDA 12.9 판)과 PyTorch를 설치합니다.
- Qwen3.8-27B를 SSAFY 서버에서 서비스 중인 판(커밋 `1d4bf0f2`)으로 받습니다. 약 54GB라 받는 데 몇 분 걸립니다. `serve.sh`가 인터넷 없이 받은 판만 읽도록 그 판을 기본 판으로 고정합니다.
- 홈 디스크 여유가 90GB보다 적으면 받기 전에 멈춥니다.

두 스크립트 모두 서버·감시 스크립트를 제자리에 복사하고, 옮겨 올 파일이 빠졌으면 경로를 출력합니다.

## 전환

같은 터널 입구는 한 서버만 잡을 수 있습니다. SSAFY 서버의 터널이 남아 있으면 임대 서버 터널은 30초마다 실패하고, 요청은 계속 SSAFY 서버로 갑니다.

1. SSAFY 서버에서 자동 시작 훅을 끄고 옮길 서비스를 내립니다. 훅이 켜져 있으면 Jupyter가 다시 시작될 때 SSAFY 쪽 감시 스크립트가 다시 뜹니다. `stop.sh`에는 옮길 서비스(`stt`·`tts`·`guard`)를 적습니다. 인자가 없으면 `stt tts`입니다.

   ```bash
   mv ~/.jupyter/jupyter_server_config.py ~/.jupyter/jupyter_server_config.py.off
   sh ai/gpu_rental/stop.sh guard   # 예: vLLM만 옮길 때
   ```

   SSAFY 서버에 저장소 `ai/` 폴더가 없으면 `stop.sh`를 그 서버에 올려 실행합니다.

2. 임대 서버에서 켤 터널의 파일을 만들고 감시 스크립트를 띄웁니다. `start.sh`는 설치된 서비스만 띄웁니다.

   | 서비스 | 개발 터널 | 운영 터널 |
   | --- | --- | --- |
   | STT | 기본으로 켜짐(`~/stt/gpu_stt/dev_tunnel_disabled`가 있으면 꺼짐) | `~/stt/gpu_stt/prod_tunnel_enabled` |
   | 합성 | `~/tts/gpu_tts/tunnel_enabled` | `~/tts/gpu_tts/prod_tunnel_enabled` |
   | vLLM | `~/guard/tunnel_enabled` | `~/guard/prod_tunnel_enabled` |

   ```bash
   touch ~/guard/tunnel_enabled   # 예: vLLM 개발 터널
   sh ai/gpu_rental/start.sh
   ```

   - vLLM은 0번 GPU에서 지금 서비스와 같은 약 71GB를 쓰도록 띄웁니다. 메모리가 100GB 미만인 카드(H100 80GB 등)는 메모리 비율을 0.9, 그보다 큰 카드는 0.5로 정합니다. 다른 값을 쓰려면 `GUARD_GPU_MEMORY`를 지정하고 실행합니다.

3. 아래 [확인](#확인)을 실행합니다. STT·합성은 약 1분, vLLM은 모델을 읽고 예열하는 데 약 4분이 걸립니다. 그동안 워커는 각 서비스의 대체 경로로 처리합니다.

임대 서버(파드)가 다시 시작되면 `start.sh`만 다시 실행합니다. Jupyter 자동 시작 훅은 임대 서버에 없습니다.

## 확인

STT·합성:

```bash
curl -s http://127.0.0.1:18431/health
curl -s http://127.0.0.1:18433/health
curl -s -o /tmp/check.wav -w 'synth %{http_code} %{time_total}s\n' -H "Authorization: Bearer $(cat ~/tts/gpu_tts/token)" \
  -d '{"phonemes": "hˈɛloʊ ðˈɛɹ.", "voice": "af_heart", "speed": 1.0}' http://127.0.0.1:18433/v1/synth
curl -s -w '\nstt %{http_code} %{time_total}s\n' -H "Authorization: Bearer $(cat ~/stt/gpu_stt/token)" \
  -F file=@/tmp/check.wav http://127.0.0.1:18431/v1/audio/transcriptions
```

- 합성 서버의 `/health`에 `"device": "GPU"`와 `CUDAExecutionProvider`가 있어야 합니다.
- 합성한 소리를 STT가 받아 적은 `text`가 "Hello there."에 가까워야 합니다.

vLLM:

```bash
curl -s -o /dev/null -w 'health %{http_code}\n' http://127.0.0.1:18432/health
curl -s -H "Authorization: Bearer $(cat ~/guard/vllm_token)" http://127.0.0.1:18432/v1/models | grep -o '"id":"[^"]*"'
```

- `/health`가 200이고, 모델 목록에 `Qwen/Qwen3.8-27B`와 어댑터 `guard-qwen3.8-27b-r16`·`npc-qwen3.8-27b-r16`이 있어야 합니다.

공통:

- `tail -n 3 ~/stt/gpu_stt/tunnel.log ~/tts/gpu_tts/tunnel.log ~/guard/tunnel.log`의 마지막 줄에 `remote port forwarding failed`가 있으면 SSAFY 서버 터널이 아직 남아 있는 것입니다. 전환 1단계를 다시 확인합니다.
- 게임에서 대화한 뒤 `~/guard/serve-service.log`에 `POST /v1/chat/completions`가 기록되면 워커가 임대 서버의 vLLM을 쓰는 것입니다. STT·합성 기본 로그에는 시작·실패만 남으므로, 요청 기록이 필요하면 `GPU_LOG_LEVEL=DEBUG`를 지정하고 `start.sh`를 실행합니다.

## 처음 옮길 때 배포 서버에서 확인할 것

터널 계정은 키와 터널 입구만 제한하며 접속 주소는 제한하지 않는 것을 전제로 합니다. 다음 중 하나라도 있으면 배포 서버 담당자가 임대 서버 주소를 허용해야 합니다.

- `/home/a305-stt-tunnel/.ssh/authorized_keys`의 키 줄에 `from=` 제한이 있는 경우
- 배포 서버 방화벽이 SSH(22)를 특정 주소에서만 받는 경우

## 되돌리기

임대 서버에서 옮겼던 서비스를 `stop.sh`로 내린 뒤, SSAFY 서버에서 훅을 다시 켜고 해당 감시 스크립트를 띄웁니다.

```bash
sh ai/gpu_rental/stop.sh stt tts guard   # 임대 서버
```

```bash
mv ~/.jupyter/jupyter_server_config.py.off ~/.jupyter/jupyter_server_config.py
setsid nohup ~/stt/gpu_stt/supervise.sh >> ~/stt/gpu_stt/supervise.log 2>&1 &
setsid nohup ~/tts/gpu_tts/supervise.sh >> ~/tts/gpu_tts/supervise.log 2>&1 &
setsid nohup ~/guard/supervise.sh >> ~/guard/supervise.log 2>&1 &
```
