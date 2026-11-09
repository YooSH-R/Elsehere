# 임대 GPU 서버 (예비)

SSAFY GPU 서버를 쓸 수 없을 때 STT([`ai/gpu_stt`](../gpu_stt/README.md))와 음성 합성([`ai/gpu_tts`](../gpu_tts/README.md))을 옮겨 돌릴 임대 GPU 서버의 설치·전환 절차입니다. SSAFY 서버는 여러 팀이 같이 쓰는 공용 서버라 사용이 제한되거나, 다른 팀이 5번 GPU 메모리를 차지해 우리 서버가 뜨지 못할 수 있습니다.

- 옮기는 서비스는 STT와 음성 합성뿐입니다. 무례 판정 vLLM은 옮기지 않으며, GPU 판정이 없으면 워커가 프로필 모델로 판정합니다([무례 판정](../guard/README.md)).
- 임대 서버도 SSAFY 서버와 같은 터널 계정(`a305-stt-tunnel`), 터널 입구, 토큰을 씁니다. 그래서 워커 환경변수와 배포 서버 설정은 바꾸지 않습니다([GPU 터널 네트워크](../../docs/gpu-worker-network.md)).
- 서버는 127.0.0.1에서만 요청을 받고 임대 서버가 배포 서버로 터널을 겁니다. 임대 서버에서 공개 포트를 열지 않습니다.

## 서버 조건

| 항목 | 기준 |
| --- | --- |
| GPU | 메모리 24GB 이상 한 장. STT 묶음 처리에 약 10GB, 합성에 약 5GB를 씁니다. 판단 기준 카드는 RTX 4090입니다. |
| 드라이버 | CUDA 12.8을 지원하는 NVIDIA 드라이버(570 이상). `nvidia-smi`가 동작하는 이미지 |
| 운영체제 | Ubuntu 22.04 또는 24.04 |
| 네트워크 | 바깥으로 나가는 SSH(22)와 HTTPS. 들어오는 포트는 필요 없습니다. |
| 디스크 | 서버를 멈춰도 남는 디스크 20GB 이상 |

컨테이너로 빌려주는 서비스(RunPod 등)는 서버를 멈추면 컨테이너 디스크가 지워집니다. 남는 볼륨(예: `/workspace`)을 홈으로 지정해 설치·실행합니다.

```bash
export HOME=/workspace
```

## 옮겨 올 파일

저장소에 없는 파일은 SSAFY GPU 서버에서 같은 경로로 옮깁니다. 토큰과 키는 채팅·저장소·Jira에 붙이지 않습니다.

| 파일 | 경로 (홈 기준) | 설명 |
| --- | --- | --- |
| STT 모델 | `stt/stt_finetune/checkpoints/turbo-lr1e4/ct2/` | 자체 학습 모델(약 1.6GB). 학습 데이터는 옮기지 않습니다. |
| STT 토큰 | `stt/gpu_stt/token` | 워커의 `STT_GPU_TOKEN`과 같은 값 |
| 합성 토큰 | `tts/gpu_tts/token` | 워커의 `TTS_GPU_TOKEN`과 같은 값 |
| 터널 키 | `stt/gpu_stt/tunnel_key`, `stt/gpu_stt/known_hosts` | `a305-stt-tunnel` 계정 키와 배포 서버 호스트 키 |

SSAFY 서버에서 한 파일로 묶습니다. 묶음은 권한 `600`으로 만들고 옮긴 뒤 지웁니다.

```bash
(umask 077 && tar -C ~ -czf ~/gpu_rental_files.tgz \
  stt/stt_finetune/checkpoints/turbo-lr1e4/ct2 \
  stt/gpu_stt/token stt/gpu_stt/tunnel_key stt/gpu_stt/known_hosts tts/gpu_tts/token)
```

임대 서버 홈에서 풉니다.

```bash
tar -C "$HOME" -xzf gpu_rental_files.tgz && rm gpu_rental_files.tgz
```

## 설치

저장소의 `ai/` 폴더를 임대 서버에 올린 뒤 실행합니다. 다시 실행해도 됩니다.

```bash
sh ai/gpu_rental/setup.sh
```

- SSAFY 서버와 같은 폴더(`~/stt`, `~/tts`)에 STT·합성 가상환경을 만들고, SSAFY 서버와 같은 버전의 패키지를 설치합니다.
- STT의 CUDA 라이브러리(cuBLAS·cuDNN)는 pip 패키지로 받고, 실행기 `~/stt/run.sh`가 그 경로를 지정합니다. SSAFY 서버의 `~/stt/gpu5.sh`를 대신하며 0번 GPU를 씁니다.
- Kokoro 모델을 받아 SSAFY 서버 파일과 해시가 같은지 확인합니다.
- 서버·감시 스크립트를 제자리에 복사하고, 옮겨 올 파일이 빠졌으면 경로를 출력합니다.

## 전환

같은 터널 입구는 한 서버만 잡을 수 있습니다. SSAFY 서버의 터널이 남아 있으면 임대 서버 터널은 30초마다 실패하고, 요청은 계속 SSAFY 서버로 갑니다.

1. SSAFY 서버에서 자동 시작 훅을 끄고 STT·합성을 내립니다. 훅이 켜져 있으면 Jupyter가 다시 시작될 때 SSAFY 쪽 감시 스크립트가 다시 뜹니다. 훅을 끄면 무례 판정 vLLM의 자동 시작도 함께 꺼집니다.

   ```bash
   mv ~/.jupyter/jupyter_server_config.py ~/.jupyter/jupyter_server_config.py.off
   sh ai/gpu_rental/stop.sh
   ```

   SSAFY 서버에 저장소 `ai/` 폴더가 없으면 `stop.sh` 내용을 터미널에 그대로 실행합니다.

2. 임대 서버에서 터널을 켜고 감시 스크립트를 띄웁니다. STT 개발 터널은 항상 걸리고, 합성 개발 터널과 두 서비스의 운영 터널은 아래 파일이 있을 때 걸립니다.

   ```bash
   touch ~/tts/gpu_tts/tunnel_enabled ~/tts/gpu_tts/prod_tunnel_enabled ~/stt/gpu_stt/prod_tunnel_enabled
   sh ai/gpu_rental/start.sh
   ```

3. 아래 [확인](#확인)을 실행합니다. 감시 스크립트가 서버를 띄우고 예열을 마치기까지 약 1분이 걸립니다. 그동안 워커는 STT를 GMS·Groq로, 합성을 CPU로 대신합니다.

임대 서버(파드)가 다시 시작되면 `start.sh`만 다시 실행합니다. Jupyter 자동 시작 훅은 임대 서버에 없습니다.

## 확인

```bash
curl -s http://127.0.0.1:18431/health
curl -s http://127.0.0.1:18433/health
curl -s -o /tmp/check.wav -w 'synth %{http_code} %{time_total}s\n' -H "Authorization: Bearer $(cat ~/tts/gpu_tts/token)" \
  -d '{"phonemes": "hˈɛloʊ ðˈɛɹ.", "voice": "af_heart", "speed": 1.0}' http://127.0.0.1:18433/v1/synth
curl -s -w '\nstt %{http_code} %{time_total}s\n' -H "Authorization: Bearer $(cat ~/stt/gpu_stt/token)" \
  -F file=@/tmp/check.wav http://127.0.0.1:18431/v1/audio/transcriptions
tail -n 3 ~/stt/gpu_stt/tunnel.log ~/tts/gpu_tts/tunnel.log
```

- 합성 서버의 `/health`에 `"device": "GPU"`와 `CUDAExecutionProvider`가 있어야 합니다.
- 합성한 소리를 STT가 받아 적은 `text`가 "Hello there."에 가까워야 합니다.
- `tunnel.log` 마지막 줄에 `remote port forwarding failed`가 있으면 SSAFY 서버 터널이 아직 남아 있는 것입니다. 1단계를 다시 확인합니다.
- 게임에서 대화한 뒤 `~/stt/gpu_stt/server.log`·`~/tts/gpu_tts/server.log`에 요청 시각이 기록되면 워커가 임대 서버를 쓰는 것입니다. 기본 로그에는 시작·실패만 남으므로, 요청 기록이 필요하면 `GPU_LOG_LEVEL=DEBUG`를 지정하고 `start.sh`를 실행합니다.

## 처음 옮길 때 배포 서버에서 확인할 것

터널 계정은 키와 터널 입구만 제한하며 접속 주소는 제한하지 않는 것을 전제로 합니다. 다음 중 하나라도 있으면 배포 서버 담당자가 임대 서버 주소를 허용해야 합니다.

- `/home/a305-stt-tunnel/.ssh/authorized_keys`의 키 줄에 `from=` 제한이 있는 경우
- 배포 서버 방화벽이 SSH(22)를 특정 주소에서만 받는 경우

## 되돌리기

임대 서버에서 `sh ai/gpu_rental/stop.sh`를 실행한 뒤, SSAFY 서버에서 훅을 다시 켜고 감시 스크립트를 띄웁니다.

```bash
mv ~/.jupyter/jupyter_server_config.py.off ~/.jupyter/jupyter_server_config.py
setsid nohup ~/stt/gpu_stt/supervise.sh >> ~/stt/gpu_stt/supervise.log 2>&1 &
setsid nohup ~/tts/gpu_tts/supervise.sh >> ~/tts/gpu_tts/supervise.log 2>&1 &
```
