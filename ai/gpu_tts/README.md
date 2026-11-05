# GPU 서버 음성 합성

AI 워커의 NPC 음성(Kokoro-82M)을 SSAFY GPU 서버(5번 GPU)에서 합성하는 서버입니다. 워커는 이 서버를 먼저 부르고, 실패·시간 초과면 지금처럼 CPU에서 합성합니다. 워커 설정은 [AI 워커 README](../worker/README.md)의 `TTS_GPU_*`, 계약은 [AI API 명세](../../docs/api/ai-worker.md) 1절 시간 상한을 따릅니다.

배포 서버의 CPU 2코어에서는 대사 하나에 약 2~3초(가끔 20초)가 걸려 NPC 대답 음성이 빠지거나 늦었습니다. GPU 서버에서는 같은 대사가 약 0.4~0.6초입니다(S15P31A305-370).

## 구조

- 워커가 읽을 글을 다듬고(`app/speech_text.py`) 발음 사전을 거쳐 음소로 바꾼 뒤(`app/pronunciation.py`) `POST /v1/synth`로 보냅니다. 이 서버는 음소를 소리로 바꾸기만 하므로 읽기 규칙은 워커 한 곳에만 있습니다.
- 모델은 fp32 `kokoro-v1.0.onnx`를 CUDA로 돌립니다. 워커의 int8 모델은 GPU에서 양자화 연산이 CPU로 넘어가 오히려 느립니다. 그래서 GPU와 CPU 합성의 소리가 아주 조금 다를 수 있습니다.
- cuDNN 알고리즘 탐색은 `HEURISTIC`으로 둡니다. onnxruntime 기본값(`EXHAUSTIVE`)은 문장 길이가 바뀔 때마다 탐색을 다시 해서 대사 하나에 약 3초가 걸렸습니다.
- 127.0.0.1에서만 받고 `Authorization: Bearer <토큰>`이 맞아야 응답합니다. 학원 방화벽 때문에 배포 서버에서 GPU 서버로 들어올 수 없어, GPU 서버가 배포 서버로 거는 역방향 SSH 터널(개발 `172.19.0.1:18433`, 운영 `172.20.0.1:18433`)로만 들어옵니다. STT·무례 판정과 같은 방식입니다.
- 받은 음소는 로그에 남기지 않고 소리 길이·합성 시간만 남깁니다.
- 여러 사람이 동시에 말하면 문장 조각 요청이 몰리므로 한 세션으로 동시에 8개까지 합성합니다(워커 한 대가 동시에 보내는 합성 수, 곧 asyncio 기본 스레드 풀 크기 `CPU 수 + 4`와 비슷). 한 번에 하나씩일 때는 동시 15요청(5명 × 조각 3)에 약 6초가 걸려 워커가 CPU로 넘겼고, 동시 합성으로 바꾼 뒤에는 15요청 약 2~2.6초, 60요청(20명분) 약 4.4~6.1초입니다(S15P31A305-486, GPU 5번을 다른 서비스와 같이 쓰는 상태라 측정마다 흔들림). 합성 중 GPU 메모리를 약 5GB 더 씁니다.
- kokoro-onnx는 목소리 파일(`voices-v1.0.bin`, npz)을 요청마다 같은 zip 핸들로 읽어 동시에 읽으면 `BadZipFile`이 납니다. 서버는 시작할 때 목소리를 모두 메모리로 읽어 둡니다.

| 요청 | 응답 |
|---|---|
| `GET /health` | `{"ok": true, "model": "kokoro-v1.0.onnx", "device": "GPU"}` |
| `POST /v1/synth` `{"phonemes", "voice", "speed"}` | `audio/wav` (24kHz 16비트 모노). 토큰이 틀리면 401, 형식이 틀리면 400 |

## 설치 (GPU 서버, 처음 한 번)

```bash
mkdir -p ~/tts/models ~/tts/gpu_tts && cd ~/tts
uv venv --python 3.12 .venv
uv pip install --python .venv/bin/python kokoro-onnx==0.6.1
uv pip uninstall --python .venv/bin/python onnxruntime        # kokoro-onnx가 끌어온 CPU용과 GPU용이 같은 폴더를 써서 지움
uv pip install --python .venv/bin/python 'onnxruntime-gpu[cuda,cudnn]==1.23.2'
R=https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0
for f in kokoro-v1.0.onnx voices-v1.0.bin; do curl -sSL -o models/$f $R/$f; done
sha256sum models/voices-v1.0.bin   # bca610b8…(infra/docker/ai-worker.Dockerfile과 같아야 함)
```

이 폴더의 `server.py`·`supervise.sh`를 `~/tts/gpu_tts/`에 올리고, 토큰을 만듭니다(권한 600). 같은 값을 워커의 `TTS_GPU_TOKEN`으로 씁니다.

```bash
python3 -c "import secrets; print(secrets.token_urlsafe(32))" > ~/tts/gpu_tts/token && chmod 600 ~/tts/gpu_tts/token
```

## 실행

```bash
setsid nohup ~/tts/gpu_tts/supervise.sh > ~/tts/gpu_tts/supervise.log 2>&1 &
```

- 감시 스크립트가 30초마다 합성 서버와 터널을 확인하고, 죽어 있으면 다시 띄웁니다. 서버는 시작할 때 몇 문장을 순서대로, 그리고 동시 합성 수(8)만큼 동시에 미리 합성해 첫 호출 준비를 끝내 둡니다. 동시 예열이 없으면 다시 띄운 직후 처음 몰린 요청들이 다 같이 5~13초 걸렸습니다. 예열에는 약 13초가 더 들어 시작까지 약 1분입니다.
- 개발 터널은 `~/tts/gpu_tts/tunnel_enabled`, 운영 터널은 `~/tts/gpu_tts/prod_tunnel_enabled` 파일이 있을 때만 겁니다. 배포 서버 담당자가 `a305-stt-tunnel` 계정에 해당 입구 전달과 워커 컨테이너 방화벽을 허용한 뒤에 만듭니다([GPU 터널 네트워크](../../docs/gpu-worker-network.md)).
- JupyterHub 세션이 꺼지면(서버 점검·재부팅) 감시 스크립트와 합성 서버도 같이 꺼집니다. 그동안 워커는 CPU 합성으로 넘어가므로 음성은 계속 나옵니다. 로그인 후 위 명령으로 다시 띄웁니다.
- 공용 서버라 `server.py`가 onnxruntime을 불러오기 전에 5번 GPU만 보이게 정합니다. 다른 번호를 쓰지 않습니다.

## 확인

```bash
curl -s http://127.0.0.1:18433/health
curl -s -o /tmp/a.wav -w '%{http_code} %{time_total}\n' -H "Authorization: Bearer $(cat ~/tts/gpu_tts/token)" \
  -d '{"phonemes": "hˈɛloʊ ðˈɛɹ.", "voice": "af_heart", "speed": 1.0}' http://127.0.0.1:18433/v1/synth
```
