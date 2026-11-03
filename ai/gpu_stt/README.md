# GPU 서버 음성 인식

AI 워커의 음성 인식(STT)을 SSAFY GPU 서버(5번 GPU)에서 자체 학습 모델(Whisper large-v3-turbo LoRA, `turbo-lr1e4`)로 받아 적는 서버입니다. 워커는 `STT_CHAIN` 순서대로 이 서버를 먼저 부르고, 실패·시간 초과면 다음 공급자(GMS·Groq)로 넘깁니다. 워커 설정은 [AI 워커 README](../worker/README.md)의 `STT_GPU_*`·`STT_CHAIN`을 따릅니다.

## 구조

- 워커가 Groq를 부르는 OpenAI 호환 형식(`POST /v1/audio/transcriptions`, `verbose_json`)을 그대로 받습니다. 언어는 `ko` 고정에 워커가 보내는 장면 단어 프롬프트를 씁니다(게임 녹음 평가에서 가장 좋았던 설정).
- faster-whisper(CTranslate2) float16으로 돌리고, 받아 적기는 한 번에 하나씩입니다. 한 건 약 0.2초라 동시 10건이면 마지막 요청이 약 5초, 20건이면 약 12초를 기다립니다(S15P31A305-511 실측). `num_workers=4`로 동시에 받아 적게도 해 봤지만 같은 GPU에서는 처리량이 늘지 않아 쓰지 않습니다.
- 파이썬 기본 HTTP 서버의 접속 대기열(5)로는 동시에 몰린 연결이 끊겨(동시 40건 중 2건), 대기열을 64로 늘렸습니다.
- 127.0.0.1에서만 받고 `Authorization: Bearer <토큰>`이 맞아야 응답합니다. 학원 방화벽 때문에 GPU 서버가 배포 서버로 거는 역방향 SSH 터널(`172.19.0.1:18431`)로만 들어옵니다. 합성([`ai/gpu_tts`](../gpu_tts/README.md))·무례 판정과 같은 방식입니다.
- 받아 적은 문장은 로그에 남기지 않고 소리 길이·처리 시간·단어 수만 남깁니다.

| 요청 | 응답 |
|---|---|
| `GET /health` | `{"ok": true, "model": "turbo-lr1e4", "language": "ko"}` |
| `POST /v1/audio/transcriptions` (multipart `file`, 선택 `prompt`) | `verbose_json` (`text`, `language`, `duration`, `segments`). 토큰이 틀리면 401, 읽을 수 없는 녹음은 400, 3MB 넘으면 413 |

## 실행 (GPU 서버)

이 폴더의 `server.py`·`supervise.sh`를 `~/stt/gpu_stt/`에 둡니다. 모델은 `~/stt/stt_finetune/checkpoints/turbo-lr1e4/ct2`, 실행 환경은 `~/stt/.venv`, `~/stt/gpu5.sh`가 5번 GPU만 보이게 하고 그 환경의 파이썬으로 실행합니다. 토큰은 `~/stt/gpu_stt/token`(권한 600)이고 같은 값을 워커의 `STT_GPU_TOKEN`으로 씁니다.

```bash
setsid nohup ~/stt/gpu_stt/supervise.sh > ~/stt/gpu_stt/supervise.log 2>&1 &
```

- 감시 스크립트가 30초마다 STT 서버와 터널을 확인해 죽어 있으면 다시 띄우고, 30분마다 Jupyter API를 불러 JupyterHub 유휴 종료(24시간)를 막습니다.
- `server.py`를 바꾼 뒤에는 실행 중인 서버 프로세스(`pgrep -f gpu_stt/server.py`)를 끝내면 감시 스크립트가 30초 안에 새 코드로 다시 띄웁니다. 다시 뜨는 동안 워커는 다음 공급자로 넘깁니다.
- JupyterHub 세션이 꺼지면(서버 점검·재부팅) 감시 스크립트와 서버도 같이 꺼집니다. 로그인 후 위 명령으로 다시 띄웁니다.
