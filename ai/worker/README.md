# AI 워커 (FastAPI)

Spring만 부르는 내부 서버입니다. **게임 상태를 바꾸는 권한은 Spring에만 있고**, AI 워커는 판단 재료(판정)와 말(NPC 대답)·소리(음성)만 만듭니다.
상태를 저장하지 않으므로 필요한 것(NPC·지금 상태·최근 대화·기억 카드)은 Spring이 매 요청에 담아 보냅니다.

- AI API 명세(요청·응답·오류, 백엔드 API와의 대응): [docs/api/ai-worker.md](../../docs/api/ai-worker.md)
- 피드백 입력 변환·NPC 기억·담당 구분: [피드백·기억 연동](../../docs/api/npc-moderation-integration.md)
- 기억 카드 생성·교체·실패 유지·다음 방문 전달: [기억 카드 연동](../../docs/api/npc-memory-integration.md)
- 피드백 종료·저장·재조회와 요청·응답 예시: [피드백 연동](../../docs/api/feedback-integration.md)
- 배포: Spring과 같은 서버에서 **별도 프로세스(컨테이너)** 로 실행합니다. 포트(기본 8020)는 외부에 열지 않고 Spring만 부릅니다.
  Java인 Spring 안에 넣지 않는 이유는 음성 합성(Kokoro)과 로컬 무례 판정기가 Python 라이브러리이고, CPU·메모리를 많이 써서 따로 두는 편이 Spring에 영향을 주지 않기 때문입니다.
- 프롬프트와 판정 규칙은 웹 프로토타입(브랜치 `prototype/S15P31A305-55-leejunghoon`의 `prototype/mvp_web`)에서 회귀 테스트로 다듬은 것을 옮겼습니다.

## 실행

```bash
cd ai/worker
python -m venv .venv
.venv/Scripts/python -m pip install -r requirements.txt
.venv/Scripts/python -m pip install -r requirements-tts.txt     # 선택: 음성 합성(Kokoro). 없으면 tts: false로 알림
.venv/Scripts/python -m pip install -r requirements-guard.txt   # 선택: 로컬 무례 판정기(CPU torch). 켜는 것은 GUARD_LOCAL_MODEL
AI_WORKER_TOKEN=<Spring과 같은 값> .venv/Scripts/python -m uvicorn app.main:app --port 8020
```

키는 환경 변수 또는 저장소 루트 `.env`(Git 제외)에서 읽습니다. 배포에서는 컨테이너 환경 변수로 넣습니다.
대화 종료 피드백은 같은 저장소의 `ai/feedback` 모듈을 불러 쓰므로, 배포할 때 `ai/worker`와 `ai/feedback`을 함께 둡니다 (추가 의존성은 pydantic뿐이고 워커 의존성에 이미 들어 있음).

| 환경 변수 | 기본값 | 뜻 |
|---|---|---|
| `AI_WORKER_TOKEN` | (필수) | Spring과 공유하는 내부 토큰. 모든 요청의 `X-Internal-Token` |
| `LLM_PROFILE` | `gms-luna` | 모델 구성 이름 ([`llm_profiles.json`](llm_profiles.json): `gms-luna`(GPT-6 Luna), `gms-sol`(GPT-6.1 SOL), `groq`). 기본값은 json의 `default_profile`. 역할별 모델·공급자 주소·키 이름·대체 모델이 한 번에 바뀜 |
| `GROQ_API_KEY` / `GMS_KEY` | (구성에 따라 필수) | 공급자 키 (이름은 json의 `key_env`). GMS 구성도 음성 인식은 Groq라 둘 다 필요. 배포하면 서버 키 하나의 한도를 모든 사용자가 나눠 씀 |
| `STT_FALLBACK` | (구성 값, GMS 구성은 `gms:whisper-1`) | 음성 인식이 한도·오류·응답 없음일 때 한 번 더 부를 `공급자:모델`. 빈 값이면 끔. 녹음 자체 오류(400·413·415·422)는 넘기지 않음 |
| `STT_FIRST_TIMEOUT_S` | 8 | 뒤에 다른 공급자가 있을 때 첫 음성 인식 호출의 상한 (마지막 공급자와 공급자가 하나뿐일 때는 30초) |
| `STT_CHAIN` | (없음) | 음성 인식 공급자 순서 `공급자:모델,공급자:모델` (예: `gpu:turbo-lr1e4,gms:whisper-1,groq:whisper-large-v3-turbo`). 앞 공급자가 한도·오류·응답 없음이면 다음으로 넘김. 정하면 구성의 음성 인식과 `STT_FALLBACK` 대신 이 순서를 씀 |
| `STT_GPU_BASE_URL` / `STT_GPU_TOKEN` | (`STT_CHAIN`에 `gpu`가 있으면 필수) | SSAFY GPU 서버에서 도는 자체 학습 STT의 주소와 토큰. 학원 방화벽 때문에 GPU 서버가 배포 서버로 거는 역방향 SSH 터널 입구(예: `http://172.19.0.1:18431/v1`)를 가리킴 |
| `STT_NEXT_TIMEOUT_S` | 8 | 순서 중간 공급자(처음·마지막 아님)의 상한 |
| `STT_BREAKER_FAILURES` / `STT_BREAKER_SKIP_S` | 3 / 60 | 같은 음성 인식 공급자가 연속으로 이만큼 실패하면 이 시간(초) 동안 건너뛰고 다음 공급자로 바로 감. 마지막 공급자는 건너뛰지 않음. 횟수가 0이면 끔 |
| `LLM_API_KEY`, `LLM_BASE_URL` | (구성 값) | 구성의 기본 공급자 키·주소를 덮어쓰기 |
| `MODEL_NPC` `MODEL_INTENT` `MODEL_GUARD` `MODEL_TRANSLATE` `MODEL_HINT` `MODEL_FEEDBACK` `MODEL_MEMORY` `MODEL_STT` | (구성 값) | 역할 하나만 모델 바꾸기 (그 역할의 공급자 그대로) |
| `MODEL_FALLBACK` | (구성 값) | 대체 모델 사슬 `a=b,c=d` |
| `LLM_PROFILES` | `llm_profiles.json` | 구성 파일 경로 |
| `CALL_TIMEOUT_S` | 8 | 모델 호출 하나의 상한. 넘으면 끊고 재시도·대체 모델 |
| `HEDGE_AFTER_S` | 3 | 한 번에 받는 호출(판정·번역·힌트 등, NPC 스트림 제외)이 이만큼 답이 없으면 같은 요청을 하나 더 보내 먼저 온 답을 씀. 0이면 끔. 측정은 [`ai/tools/llm_latency.py`](../tools/llm_latency.py) |
| `STREAM_HEDGE_AFTER_S` | 2.5 | NPC 대답 스트림의 첫 글자가 이만큼 안 오면 같은 요청 스트림을 하나 더 열어 먼저 글자를 낸 쪽을 씀. 0이면 끔. 측정은 `ai/tools/llm_latency.py --mode stream` |
| `FEEDBACK_TIMEOUT_S` | 60 | 대화 종료 피드백의 전체 상한 (재시도·대체 모델 포함, 같은 요청 더 보내기 없음). 넘으면 `AI_TIMEOUT` |
| `AUDIO_MAX_MB` | 200 | 음성 조각·턴 전체 음성 보관소의 전체 크기 상한 (넘으면 오래된 것부터 버림) |
| `WORDS_BUDGET_S` | 3 | NPC 대답 스트림 안 추천 단어의 상한. 넘으면 `words: null`(timeout)로 넘기고 `done`을 보냄 |
| `JUDGE_BUDGET_S` | 5 | `/v1/judge`의 의도·번역 각각의 상한 (재시도·대체 모델 포함) |
| `GUARD_BUDGET_S` | 5 | `/v1/guard` 무례 판정의 상한 (로컬 판정기 + LLM 재시도·대체 모델 포함). 넘으면 `abusive: null`(timeout) |
| `REPLY_FIRST_BUDGET_S` | 5 | NPC 대답 첫 글자까지의 상한 |
| `RESPOND_BUDGET_S` | 30 | NPC 추론 API(`/internal/ai/npc/respond`) 요청 하나의 상한 (재시도·대체 모델 포함, 계약 10절). 넘으면 504 `AI_TIMEOUT` |
| `KOKORO_MODEL`, `KOKORO_VOICES` | (없음, 배포 이미지는 `/opt/kokoro/` 안 파일) | `kokoro-v1.0.int8.onnx`, `voices-v1.0.bin` 경로 ([kokoro-onnx 배포 파일](https://github.com/thewh1teagle/kokoro-onnx/releases)) |
| `KOKORO_THREADS` | 0 (onnxruntime 기본, 보이는 코어 수) | 음성 합성 스레드 수. 컨테이너처럼 CPU 상한이 있으면 상한과 같게 (앱 Compose는 2). 상한보다 많으면 CPU 할당량에 걸려 몇 배 느려짐 |
| `TTS_REQUIRE_TIMING` | `false` (배포 이미지 `true`) | 모델 워밍업에서 검증된 발음 시간표가 없으면 워커 시작 실패 |
| `TTS_CHUNK_BUDGET_S` | 로컬 2.5, 앱 Compose 8 | NPC 대답 한 조각 합성이 이보다 늦으면 그 조각은 `tts: false` |
| `GUARD_LOCAL_MODEL` | (없음, 꺼짐) | 로컬 무례 판정기 폴더 (KcELECTRA 학습 결과 `best/`). 주면 한글 입력의 확신 구간은 판정용 LLM 없이 판정. `requirements-guard.txt` 필요, 없거나 못 읽으면 경고만 남기고 전부 LLM |
| `GUARD_LOCAL_T_LO`, `GUARD_LOCAL_T_HI` | (모델을 주면 필수) | 로컬 점수 ≤ T_LO → 아님, ≥ T_HI → 무례, 그 사이는 LLM. 학습에 쓰지 않은 보정용 데이터에서 고른 값 |
| `GUARD_GPU_MODEL` | (없음, 꺼짐) | SSAFY GPU 서버 vLLM에 올린 자체 학습 무례 판정 모델 이름(예: `guard-qwen3.8-27b-r16`). 주면 이 모델을 먼저 한 번 부르고, 실패·시간 초과면 구성의 `guard` 모델로 넘김 |
| `GUARD_GPU_BASE_URL` / `GUARD_GPU_TOKEN` | (`GUARD_GPU_MODEL`을 주면 필수) | GPU 서버 vLLM의 주소와 키. STT처럼 GPU 서버가 거는 역방향 SSH 터널 입구(STT와 다른 포트, 예: `http://172.19.0.1:18432/v1`) |
| `GUARD_GPU_TIMEOUT_S` | 2 | GPU 서버 판정 한 번의 상한. 넘으면 재시도 없이 구성의 `guard` 모델로 |
| `GUARD_BREAKER_FAILURES` / `GUARD_BREAKER_SKIP_S` | 3 / 60 | GPU 서버 판정이 연속으로 이만큼 실패하면 이 시간(초) 동안 건너뛰고 구성의 `guard` 모델로 바로 감. 횟수가 0이면 끔 |

## API

| API | 언제 | 응답 |
|---|---|---|
| `POST /v1/judge` | 한 턴의 처음: 의도·번역을 동시에 판정 (상태는 안 바꿈) | JSON |
| `POST /v1/guard` | 한 턴의 처음: 무례 판정 (`/v1/judge`와 동시에) | JSON |
| `POST /v1/npc/reply` | Spring이 상태를 바꾼 뒤 NPC 대답 | SSE: `delta` → `audio`(seq 순서) → (`words`) → `done`(턴 전체 음성 `audio_full_id`) / `error` |
| `GET /v1/audio/{id}` | 스트림의 음성 조각 (10분 보관) | audio/wav |
| `POST /v1/tts` | 모범 발화·단어 발음 같은 단독 합성 | audio/wav |
| `POST /v1/stt` | 녹음 인식 (multipart `file`, 선택 `scene_words`) | JSON |
| `POST /v1/hint` | 학습자가 💡를 누른 때 | JSON |
| `POST /v1/suggest` | NPC 대답이 끝난 뒤: 학습자가 쓸 추천 단어 3개 | JSON |
| `POST /v1/meaning` | [뜻 보기]: NPC 대사의 한국어 뜻과 단어 풀이 | JSON |
| `POST /v1/feedback` | 대화가 끝났을 때: 학습 피드백 (`ai/feedback` 계약 0.3.0, 그 모듈을 부름) | JSON |
| `POST /v1/memory` | 대화가 끝났을 때: NPC 기억 카드 갱신 | JSON |
| `GET /health` | 상태 확인 (토큰 없이) | JSON |

- 모든 응답(스트림은 마지막 `done`)에 `usage`: 단계·실제로 쓴 모델·입출력 토큰·시간·대체 여부. Spring은 이걸로 크레딧을 차감합니다.
  단가는 `llm_profiles.json`의 `prices_per_1m_tokens_usd`.
- 오류는 `{"error": {"code", "message"}}`: `invalid_request` 400, `unauthorized` 401, `not_found` 404, `method_not_allowed` 405, `provider_limit` 429, `internal_error` 500, `invalid_output` 502(모델 출력 형식 오류), `provider_unavailable` 503, `tts_unavailable` 503.
  판정·대화 종료는 일부가 실패해도 200으로 이어 갑니다 (의도 → `unclear`, 무례 → `abusive: null` 미판정, 기억 카드 → `null` + `errors`).
- 추적: `X-Request-Id`(없으면 만들어 응답 헤더로 돌려줌), `X-User-Id`, `X-Conv-Id`를 요청마다 로그 한 줄(JSON)로 남깁니다. 대화 원문은 로그에 남기지 않습니다.

### 구성 바꾸기

```bash
# 기본은 gms-luna (GMS_KEY, 음성 인식용 GROQ_API_KEY). 다른 구성은 LLM_PROFILE로
LLM_PROFILE=gms-sol AI_WORKER_TOKEN=<값> .venv/Scripts/python -m uvicorn app.main:app --port 8020
curl -s localhost:8020/health        # "profile": "gms-sol", "models": {...}
```

GMS 키는 팀 공용 크레딧입니다. json에 없는 모델은 쓰지 않고, `gms-sol`은 `gms-luna`보다 크레딧이 약 20배 드니 꼭 필요할 때만 씁니다.
GPT-5·GPT-6 계열 요청 칸(`max_completion_tokens`, temperature 없음, `reasoning_effort`)은 json의 `model_params`로 정해져 코드 수정 없이 바뀝니다.

### 한 줄에 행동 여러 개 (`state.more_intents`, 응답 `intent.ids`)

"to go. cash"(포장+결제), "here. bye"(결제+작별)처럼 한 줄에 행동이 여러 개일 수 있습니다. Spring은 지금 상태의 의도(`intents`)와 함께
같은 장소 다음 단계의 의도를 `more_intents`로 보내고, 응답의 `intent.ids`(말한 순서)를 **차례로 적용**합니다.
앞 행동으로 바뀐 상태에 맞지 않는 의도는 건너뛰고, 대화가 끝나는 상태에 이르면 멈춥니다. `intent.id`는 첫 행동입니다.

### 판정 요청의 `state.slots` (장소 데이터 형식)

Spring이 장소 데이터(상품 목록·선택지)로 만듭니다. 사는 장소가 아니면 생략합니다.

```json
"slots": {
  "items":  {"catalog": ["americano", "latte", "muffin"], "iced_ok": ["americano", "latte"],
             "catalog_word": "menu", "vague_words": "'coffee' or 'drink'"},
  "option": {"slot": "choice", "labels": ["here", "to_go"], "prompt": "where they will have it"}
}
```

- 약국처럼 아이스가 없으면 `iced_ok`를 비우고(판정 형식에서 `iced` 칸이 사라짐), 선택지가 없으면 `option`을 뺍니다.
- 응답 `intent.slots`는 이 범위 안으로 정리됩니다: 목록에 없는 상품은 버리고, `iced_ok`가 아닌 상품의 `iced`는 `false`, 선택지는 `labels` 또는 `"none"`.
  수량 상한·가격 계산은 Spring 규칙이 합니다.

## NPC와 터미널에서 대화해 보기

NPC 설정이나 공통 시스템 프롬프트(`app/npc_prompt.py`)를 고친 뒤 실제 모델과 바로 주고받아 볼 때 씁니다. 추론 계약의 요청과 같은 모양으로 프롬프트를 만들어,
NPC가 첫 대사를 먼저 하고 이후 한 줄마다 대답합니다. 턴마다 입력·출력 토큰, 크레딧, 첫 글자·전체 시간, 실제로 쓴 모델을 보여 주고, 끝내면(`/quit` 또는 Ctrl+Z, Enter) 대화 전체 합계가 나옵니다.

```bash
.venv/Scripts/python ../tools/npc_chat.py --npc ../npcs/CAFE_STAFF.json --scenario ../npcs/scenarios/cafe_counter.json            # 학습 수준 A2(초급)
.venv/Scripts/python ../tools/npc_chat.py --npc ../npcs/CAFE_STAFF.json --scenario ../npcs/scenarios/cafe_counter.json --level B1
```

- `--npc`는 공통 인격 스키마의 `NpcConfiguration` JSON(NPC마다 하나)이고, 대화 전에 원본 스키마로 검사합니다. 서비스 NPC 설정은 `ai/npcs/<npcId>.json`에 둡니다.
- `--scenario`는 추론 계약의 `scenario`(`scenarioId`·`locationId`·`situation`·`facts`) JSON입니다. 실제 상황은 백엔드가 추론 요청에 담아 보내고, `ai/npcs/scenarios/`의 파일은 대화 확인용입니다.
- 실제 모델을 부르므로 팀 공용 GMS 크레딧을 씁니다. 키는 GMS 키(`GMS_KEY` 또는 `GMS_API_KEY`, 환경 변수 또는 저장소 루트 `.env`)만 있으면 되고, 음성 인식용 Groq 키는 필요 없습니다.
- 크레딧은 `llm_profiles.json`의 단가로 계산하며, 같은 요청을 하나 더 보낸 턴은 진 쪽 토큰이 빠져 실제 차감이 더 클 수 있다고 표시합니다.
- 입력을 파이프로 넘기면 줄마다 한 턴씩 말합니다 (`Get-Content lines.txt | .venv/Scripts/python ../tools/npc_chat.py`).

## NPC 인격 모델 (자동 생성)

`app/npc_profile_models.py`는 백엔드와 함께 쓰는 공통 인격 스키마([`npc_shared_schemas_v2.json`](../../docs/api/npc-ai-contracts/npc_shared_schemas_v2.json))에서 만든 Pydantic 모델입니다. 직접 고치지 말고 스키마를 고친 뒤 다시 만듭니다. 커밋한 파일이 지금 스키마로 만든 결과와 다르면 테스트가 실패합니다.

```bash
.venv/Scripts/python ../tools/generate_npc_profile_models.py
```

생성 모델은 스키마 규칙 중 활성 NPC의 `voiceId` 필수(조건 규칙), 목록 중복 금지, 생략할 수 있는 칸의 `null` 금지를 검사하지 않고, `"1"` 같은 글을 숫자·참거짓으로 바꿔 받습니다. 그래서 NPC 설정은 [`ai/tools/npc_configs.py`](../tools/npc_configs.py)의 `schema_errors()`로 원본 스키마 그대로 한 번 더 검사합니다. 어긴 곳마다 위치(`$.npcProfile.values`)와 이유를 모두 돌려줍니다.

NPC 설정 파일(NPC마다 하나)을 검사하고 백엔드 전달용 배열(`NpcConfigurationList`)로 합칠 때는 같은 도구를 명령줄로 씁니다.

```bash
.venv/Scripts/python ../tools/npc_configs.py ../npcs                                # 검사만
.venv/Scripts/python ../tools/npc_configs.py ../npcs --out npc_configurations.json  # 검사하고 합친 배열 저장
```

파일마다 스키마 검사, 파일끼리 `npcId` 중복, NPC 수(스키마의 1~1000)를 보고, 문제가 있으면 배열을 저장하지 않고 실패 코드 1로 끝납니다. 결과는 셸의 `>` 대신 `--out`으로 저장합니다. Windows PowerShell 5.1의 `>`는 파일 앞에 BOM을 붙여 JSON을 읽을 때 오류가 납니다.

### 백엔드 시드로 내보내기

아래 두 생성 파일은 백엔드가 `npcs`의 인격·이름·목소리·상황을 채울 때 쓸 내용 파일입니다. 백엔드가 이 파일을 읽는 연결은 아직 없고 별도 작업에서 붙입니다. 원본은 `ai/npcs`이고, 생성 파일은 직접 고치지 않습니다. `ai/npcs`를 고쳤으면 저장소 최상위에서 다시 만듭니다.

```bash
ai/worker/.venv/Scripts/python ai/tools/npc_configs.py ai/npcs --out backend/src/main/resources/content/npc-configurations.json --scenarios-out backend/src/main/resources/content/npc-scenarios.json
```

- `npc-configurations.json`: NPC 설정 배열(계약 `NpcConfigurationList`).
- `npc-scenarios.json`: `npcId` → 서비스 대화의 상황(추론 계약의 `scenario`). 인격과 따로 관리하므로 NPC마다 어느 상황 파일을 쓸지는 [`ai/npcs/scenarios/service_scenarios.json`](../npcs/scenarios/service_scenarios.json)에 적습니다. 상황은 이 Worker의 추론 요청 모델(`app/npc_respond.py`의 `Scenario`)로 검사하고, 상황 장소(`locationId`)가 NPC 기본 장소(`defaultLocationId`)와 같은지도 봅니다.
- 생성 파일이 원본과 다르면 `tests/test_npc_configs.py`가 실패합니다.

## NPC 생활 규칙과 하루 시뮬레이션

NPC마다 정하는 것은 `ai/npcs/life/<npcId>.json`의 MBTI와 역할뿐이고, 행동·생활 값·일과는 `ai/npcs/rules/`의 공통 규칙으로 계산합니다(`ai/tools/npc_derive.py`, 설계는 [docs/npc-life-design.md](../../docs/npc-life-design.md)). 규칙 숫자를 바꾼 뒤에는 LLM 없이 하루를 돌려 봅니다.

```bash
.venv/Scripts/python ../tools/npc_simulate.py   # 서비스 NPC 3명의 하루(행동 조각·부탁·행동·정시 욕구)와 알아봄·기억 곡선
```

## NPC 인격 카드 생성

성격을 만드는 일은 NPC를 만들 때 한 번 하고, 결과인 인격 카드는 대화할 때마다 그대로 씁니다(설계는 [docs/npc-life-design.md](../../docs/npc-life-design.md)의 "인격 카드 생성").

- 손으로 쓴 인격 카드(`ai/npcs/<npcId>.json`)에는 생활 파일의 MBTI로 계산한 성격 5축과 뽑힌 행동 조각이 자동 칸으로 들어갑니다. 행동 조각이나 생활 파일을 바꿨으면 다시 쓰고 백엔드 전달물도 다시 만듭니다(위 "백엔드 시드로 내보내기").
- 새 NPC는 재료 파일(`ai/npcs/rules/persona-ingredients.json`)에서 규칙으로 뼈대를 만들고, `--narrate`를 주면 LLM이 사연(배경 이야기·겪은 일)만 씁니다. 결과는 초안(`isActive` false, `voiceId` null)이라 사람이 보고 `ai/npcs`로 옮긴 뒤 상황 파일을 연결하고 목소리를 정해 켭니다.
- 조합 규칙은 `app/npc_persona.py` 한 곳에 있고, 어드민 랜덤 생성은 같은 규칙을 워커 API로 부릅니다(`/v1/npc/persona/generate`·`/derive`, [계약](../../docs/api/ai-worker.md) 2.9절). 배포 이미지에는 `ai/npcs/rules`가 함께 들어갑니다.

```bash
.venv/Scripts/python ../tools/npc_derive.py ../npcs                                       # 손으로 쓴 인격 카드의 자동 칸 다시 쓰기
.venv/Scripts/python ../tools/npc_generate.py GEN_CAFE_01 INFP-A STAFF CAFE --out drafts            # 규칙 뼈대만 (LLM 없음)
.venv/Scripts/python ../tools/npc_generate.py GEN_CAFE_01 INFP-A STAFF CAFE --out drafts --narrate  # 사연까지 (.env 구성의 NPC 대답 모델)
```

## NPC 관계·기분

사용자 × NPC 관계(알아봄 0~100, 호감 −100~100)는 공통 규칙(`ai/npcs/rules/world-rules.json`)으로 `app/npc_relationship.py`가 계산하고, 저장은 백엔드가 합니다(설계는 [docs/npc-life-design.md](../../docs/npc-life-design.md) 4·5·8절).

- 매 턴 의도 판정(`/v1/judge`)이 같은 호출에서 학습자 줄의 대화 사건(`events`)도 고릅니다. 백엔드는 줄마다 저장해 `history[].events`로 보냅니다([계약](../../docs/api/ai-worker.md) 2.2절).
- 무례 판정(`/v1/guard`)이 무례라고 하면 응답의 `affinity_delta`(NPC 성격에 따른 감점)를 백엔드가 무례 저장과 함께 호감에 더합니다(2.2.1절).
- 대화가 끝나면 백엔드가 `/v1/npc/relationship`을 부르고, 돌려받은 변화량(`recognition_delta`·`affinity_delta`)을 저장값에 더합니다. 모델을 부르지 않고 줄 사건으로 계산하며, 이미 깎은 무례는 다시 세지 않습니다(2.7.2절).
- 다음 대답 요청(`/v1/npc/reply`)에 저장한 관계와 지난 게임 날 수를 `relationship`으로 실으면, 알아봄·호감·이번 대화의 기분(짜증·좋은 기분)을 말로 풀어 지시문에 넣습니다. 얼굴을 알아봐야 평범한 기억을 떠올립니다.
- 알아보는 속도와 사건 반응 크기는 요청의 인격 카드(성격 5축과 카드의 행동 조각)로 정합니다.

## 음성 합성 왕복 평가

NPC 음성이 문장을 제대로 읽는지 봅니다. 고정 평가 문장([`ai/tools/tts_roundtrip_set.jsonl`](../tools/tts_roundtrip_set.jsonl), 가격·시각·별표·한글·구어 철자·외국어·이름·메뉴 등 태그)을 NPC 대답 스트림과 같은 조각으로 Kokoro가 읽게 하고, 로컬 Whisper로 다시 받아써 틀린 곳을 찾습니다. Kokoro와 faster-whisper는 가상환경이 달라 단계를 나눕니다. 외부 API는 부르지 않으며, 결과(`ai/tools/results/`)는 저장소에 올리지 않습니다.

```bash
ai/worker/.venv/Scripts/python ai/tools/tts_roundtrip.py synth --model <kokoro-v1.0.int8.onnx> --voices <voices-v1.0.bin>   # CPU
<faster-whisper·transformers 환경>/python ai/tools/tts_roundtrip.py asr      # GPU, 게임 테스트 중에는 돌리지 않음
<faster-whisper·transformers 환경>/python ai/tools/tts_roundtrip.py report   # 태그별 WER·틀린 곳 → report.md
```

- 목소리는 문장 NPC 설정의 `voiceId`, 속도는 `--speed`(기본 1.0, 난이도 1·3은 0.85·1.06)입니다.
- Whisper 영어 정규화로 숫자·축약 표기를 맞추고, 곡선 따옴표와 붙여 쓰기·띄어 쓰기 차이(haircut ↔ hair cut)는 틀린 것으로 세지 않습니다.
- Whisper는 어색한 발음도 맞게 받아쓰기 쉽습니다. 예를 들어 읽을 글 다듬기(`app/speech_text.py`)를 넣기 전 Kokoro는 `$4`를 "dollar four"로 읽었는데, Whisper는 이를 `$4`로 받아썼습니다. 그래서 틀린 곳이 없다고 바르게 읽었다는 뜻은 아닙니다. 틀린 곳이 나온 문장은 wav를 직접 들어 확인합니다.

## 검사

```bash
.venv/Scripts/python -m pip install -r requirements-dev.txt
.venv/Scripts/ruff check . ../tools --config pyproject.toml          # 규칙 검사 (Ruff 기본 권장 규칙 + import 정렬)
.venv/Scripts/ruff format --check . ../tools --config pyproject.toml # 포맷 확인 (고칠 때는 --check 빼고)
```

## 테스트

```bash
.venv/Scripts/python -m pytest -q       # 모델 호출 없음 (가짜 공급자), 몇 초
```

가짜 공급자(`tests/conftest.py`)가 시스템 프롬프트로 단계를 알아보고 정해 둔 답을 돌려줍니다.

- `tests/test_api.py`: 시간 상한·재시도·대체 모델·하루 한도 건너뛰기·형식 강제 실패 재시도, 판정 값 정리, SSE 순서·느린 음성 대체, 기억 카드 정리
- `tests/test_guard.py`: 무례 판정 `/v1/guard` — yes/no 해석, 행동 묘사만 있는 말, 빈 입력, 시간 상한(`GUARD_BUDGET_S`), 공급자 실패, 로컬 판정기 연결, GPU 서버 판정 모델 → 구성 모델 대체·건너뛰기, 무례일 때만 호감 감점(`affinity_delta`)과 성격(신경성)에 따른 크기
- `tests/test_stt.py`: 음성 인식 — 프롬프트 되풀이 거르기·장면 단어, 녹음 크기(413)·형식(415)·코덱 매개변수, Groq 실패(429·5xx·키 오류·응답 없음) 시 GMS whisper-1 대체·녹음 자체 오류는 안 넘김·첫 호출 짧은 상한, `STT_CHAIN` 순서(GPU 서버 → GMS → Groq, 자리별 상한, 모두 실패 시 공급자별 오류), 연속 실패한 공급자 잠시 건너뛰기·성공하면 초기화·마지막 공급자는 안 건너뜀
- `tests/test_guard_local.py`: 무례 판정 단계적 구조. 가짜 판정기로 확신 구간·중간 구간·영어·로컬 실패·LLM 실패, 기준값 설정 검사 (torch 없이 돎)
- `tests/test_hedge.py`: 늦은 호출에 같은 요청 하나 더 보내기, 오류 응답 뒤 남은 요청 기다리기, 판정 시간 상한, NPC 스트림 제외
- `tests/test_contract.py`: 모델 출력이 깨졌을 때(JSON 객체가 아님, 칸 없음)와 빈 문장·없는 주소·프로그램 오류에도 계약의 실패 형식을 지키는지
- `tests/test_learning_aids.py`: 뜻·단어 풀이(대사에 없는 말 버리기, 3개 제한), 추천 단어(정리·중복·3개), 형식 오류 502, 난이도별 NPC 지시문·대답 길이·말 속도·힌트 수준
- `tests/test_feedback.py`: `ai/feedback`의 예시 입력·출력으로 피드백 계약(완료·발화 없음·공급자 실패·출력 오류·입력 오류), 피드백 전용 시간 상한과 같은 요청 더 보내기 끔
- `tests/test_stream_hedge.py`: NPC 스트림의 첫 글자(또는 응답 머리)가 늦으면 같은 요청 스트림 하나 더, 이긴 쪽 글자만 이어짐, 진 쪽 닫기, 둘 다 늦으면 첫 글자 상한
- `tests/test_turn.py`: 한 턴 구조 — 조각을 이어 붙인 턴 전체 음성, 조각 실패·음성 없음이면 `null`, 스트림 안의 추천 단어(`words`)와 그 실패
- `tests/test_audio_store.py`: 음성 보관소의 전체 크기 상한
- `tests/test_speech_text.py`: 음성으로 읽을 글 다듬기(`app/speech_text.py`) — 가격(`$13` → 13 dollars, 센트·단수·쉼표), 시각(`10:00` → 10 o'clock, `9:05` → 9 oh 5, a.m.·p.m.), 별표, 한글은 자막으로만(남은 문장 부호 정리), 평범한 영어는 그대로, 읽을 게 없으면 빈 글. Kokoro가 다듬은 글을 엔진에 넘기고, 읽을 게 없으면 엔진 없이 같은 형식의 짧은 무음
- `tests/test_tts_roundtrip.py`: 음성 합성 왕복 평가(`ai/tools/tts_roundtrip.py`)의 채점 — 곡선 따옴표는 곧은 것으로 바꿔 정규화, 대소문자·문장 부호 무시, 붙여 쓰기·띄어 쓰기 차이, 이어서 틀린 단어를 한 덩어리로, 태그별 WER(단어 수 가중), 평가 문장 파일(id 중복·NPC·태그)
- `tests/test_profiles.py`: `LLM_PROFILE` 하나로 주소·키·모델이 바뀌는지, GMS 구성에서 대화는 GMS·음성 인식은 Groq로 가는지, 음성 인식 대체(`STT_FALLBACK`)·순서(`STT_CHAIN`, gpu 주소·토큰 필수, 잘못된 값)·건너뛰기 설정, 음성 합성 스레드 수(`KOKORO_THREADS`), GPT-5 요청 칸
- `tests/test_npc_chat.py`: 터미널 대화 도구(`ai/tools/npc_chat.py`)의 크레딧 계산(입력·출력 단가, 단가 모르는 모델, 같은 요청 더 보냄 표시), 첫 대사는 시스템 프롬프트만 보내고 기억, 이력을 붙인 대답과 대화 기록, 공급자 실패 때 기록 유지, GMS 키만으로 실행
- `tests/test_npc_profile_models.py`: 커밋한 인격 모델이 지금 스키마로 다시 만든 것과 같은지, 계약 예시를 Python 이름으로 읽고 계약 JSON으로 되돌리기, 스키마가 막는 값(모르는 키·빈 글·정해진 값 밖·길이·개수) 거부
- `tests/test_npc_configs.py`: 원본 스키마 검사 — 계약 예시와 서비스 NPC 파일(`ai/npcs`) 통과, 활성 NPC의 `voiceId`, 생성 모델이 놓치는 규칙(중복·`null`·형변환)을 어긴 위치로 알려 주기, 여러 오류를 한 번에. NPC 파일 합치기 — 파일 이름 순, 파일끼리 `npcId` 중복, 파일 이름을 붙인 스키마 오류, JSON이 아닌 파일, 빈 폴더(스키마의 배열 규칙), 명령줄 검사만·`--out` 저장(BOM 없음)·문제가 있으면 저장 안 함. 서비스 상황 — `npcId`로 모으기, 연결 없음·없는 NPC·없는 파일·형식 위반·장소 불일치, `--scenarios-out` 저장, 서비스 NPC 3명의 상황, 백엔드 생성 파일이 원본과 같은지
- `tests/test_npc_derive.py`: 자동 계산(`ai/tools/npc_derive.py`) — MBTI 네 글자·-T/-A 꼬리를 성격 5축으로(대소문자 무관, 형식이 아니면 거부), 연결 규칙의 직선 계산, Mia의 계산값, 행동 조각 뽑기(성격 쪽만·범주당 하나·같은 씨앗 같은 결과·충돌 제외·맞는 조각이 적으면 적게), 배수 합치기(곱하기, 기억 세기 보너스는 더해 최대 2), 생활 파일에서 한 번에 계산, 역할의 일과 틀에 기본 장소를 넣은 일과(지금은 각자 기본 장소 안에서만), 세 NPC의 최종 값이 인격 글의 성격 방향을 재현하는지, 인격 카드 자동 칸(성격 5축을 스키마 칸 순서로, 손으로 쓴 규칙 뒤 행동 조각, 다시 계산해도 중복 없음, 원본 불변, 서비스 인격 파일 동기화, 명령은 생활 파일 있는 NPC만 고침)
- `tests/test_npc_persona_api.py`: 인격 카드 조합 API(`/v1/npc/persona/generate`·`/derive`) — 필수 입력(npcId·역할·장소)만으로 초안과 고른 MBTI, 같은 요청 같은 결과, 고정 입력 유지, DB 후보와 다른 NPC 이름, 만들 수 없는 요청(장소에 직업 없음·틀린 MBTI·고를 이름 없음·필요한 쪽 성격 특징 없음)은 400, 사연은 사연 칸만·모델을 어긴 사연은 502, MBTI를 바꾼 카드의 자동 칸만 다시 계산, 모델 밖 카드는 400
- `tests/test_npc_generate.py`: 인격 카드 생성(`app/npc_persona.py`, 명령 `ai/tools/npc_generate.py`) — MBTI 없이 고르기, 고정 칸 유지·한 칸 고정이 다른 칸을 바꾸지 않음, 고정 직업의 나이대, 후보 목록은 준 목록만 바꿈, 재료 파일 검사(스키마 위치, 축 양쪽 특징 2개 이상, 없는 나이대, 특징 이름 중복), 같은 id 같은 NPC·다른 id 다른 NPC, MBTI 16 × 직원(카페·호텔)·행인 48가지가 공통 인격 스키마·생활 파일 스키마 통과, 초안 표시, 축마다 기운 쪽 특징, 반대 쪽·다른 역할 재료 없음, 직업·나이대, 쓰는 이름 제외, 말투·사회적 행동 규칙, 자동 칸, 장소에 직업이 없으면 오류, 명령이 두 파일을 씀, LLM은 사연 칸만 쓰고(가짜 공급자) 카드와 금지 사항을 받으며 스키마 밖 사연·JSON이 아닌 출력은 거절
- `tests/test_npc_simulate.py`: 하루 시뮬레이션(`ai/tools/npc_simulate.py`) — 욕구 감소 배수, 가장 급한 욕구를 채울 행동, 근무 중에는 일만, 다른 역할의 행동 제외, 부탁 발생(하루 한 번)·자기 장소 심부름 제외·퇴근 중 제외. 서비스 데이터로 Jake의 근무 중 머핀 부탁·Mia는 부탁 없음·Ben의 오후 말 걸기, 튜토리얼을 마치기 전 안내 NPC(Ben)는 생활·부탁 없음(다른 NPC는 평소대로), 알아봄 단계에 닿는 대화 수, 기억 떠올림, 명령줄 출력
- `tests/test_npc_life_rules.py`: NPC 생활·관계 공통 규칙(`ai/npcs/rules/world-rules.json`, 설계는 [docs/npc-life-design.md](../../docs/npc-life-design.md)) — 원본 스키마 통과, 빠진 욕구·없는 욕구·감정·사건, 범위 밖 값을 어긴 위치로 알려 주기, 행동에 쓰는 사물이 그 장소에 배치된 사물 단어인지, 역할별 일과 틀이 하루를 채우는지와 일과표의 빈 시간·겹침·자정 넘김. NPC별 생활 파일(`ai/npcs/life/<npcId>.json`) — 서비스 NPC 3명이 입력 노드(MBTI·역할)만 가짐, 스키마가 막는 값(손으로 적는 생활 값 포함), 파일 이름과 `npcId`, 인격 파일과의 짝, 스키마를 어긴 파일은 스키마 오류만, 튜토리얼 안내 NPC는 Ben 하나. 행동 조각 모음(`ai/npcs/rules/behaviors.json`) — 원본 스키마 통과, 스키마가 막는 값, id 중복, 없는 조각·자기 자신과의 충돌 표시, 성격 축 양쪽마다 고를 조각 수
- `tests/test_npc_prompt.py`: NPC 시스템 프롬프트 — 인격 부분(프로필 칸이 스키마 순서대로 한 줄씩, 생략·빈 칸은 줄째로 빠짐, Big Five 수치는 설정됐을 때만 상대값으로, 값 안의 줄바꿈이 새 부분을 열지 못함), 다섯 부분 순서(규칙·인격·상황·학습 수준·이번 턴), 주어진 사실만, 학습 수준 6단계 → 규칙 3묶음, 첫 대사는 시스템 프롬프트만·응답 턴은 이력 역할과 사용자 발화 마지막(계약 추론 요청 예시로)
- `tests/test_npc_reply_profile.py`: 게임 대화 경로(`/v1/npc/reply`)의 인격 — 인격이 없으면 지금 지시문 그대로, 백엔드 인격 설정(`content/npc-configurations.json`)의 3명이 대답의 Character가 됨, 다섯 부분 순서, 인격이 있어도 장소·사실·기억·난이도·백엔드 지시가 들어감, 난이도 규칙이 말투를 정하지 않음, 스키마 밖 인격은 400, 힌트 등 다른 요청도 같은 npc를 받음
- `tests/test_npc_relationship.py`: 관계 계산(`app/npc_relationship.py`) — 알아봄은 여유 날 뒤 하루씩 줄고 대화마다 갈수록 덜 오름(NPC 배수), 인상은 사건 한 번씩·무례 제외·목록 밖 버림, 무례 한 번의 감점(인상 × 따라가는 비율 × 반응 크기)과 같은 값의 기본값, 호감은 인상 일부만 따라가고 끝에서 느려짐, 더할 정수 변화량, 오래 안 오면 알아봄 변화량이 음수, 처음 만남, 인격 카드로 계산한 배수가 생활 파일로 계산한 값과 같음, 지금 상태(처음 봄·본 적 있음·단골, 오래 안 오면 다시 낯섦, 좋아함·싫어함, 사적인 이야기, 무례 뒤 짜증과 성격별 지속, 사과하면 짜증이 줄어듦, 칭찬 뒤 좋은 기분, 잡담은 이어질 때만, 무례 줄의 다른 사건 버림, 함께면 더 큰 기분만), 잡담·사적인 질문은 호감을 움직이지 않음, 정수로 저장해도 호감이 쌓이는 대화 수
- `tests/test_npc_reply_relationship.py`: 대답 경로의 관계 — 두 지시문(인격·한 줄 역할) 모두 처음 봄, 알아보지 못하면 평범한 기억 숨김, 본 적 있음·단골의 기억, 오래 안 오면 다시 낯섦, 강한 일은 얼굴을 몰라도 기억(싫어해도), 좋아함·싫어함, 호감 문장이 카드 태도를 대신함, 가까운 단골만 자기 이야기, 무례 뒤 짜증, 줄 사건의 칭찬 뒤 좋은 기분, 숫자를 넣지 않음, 관계가 없으면 지금 기억 문장 그대로, 범위 밖 값은 400
- `tests/test_npc_relationship_api.py`: 관계 갱신 API(`/v1/npc/relationship`) — 줄 사건과 규칙으로 변화량(모델 호출 없음), 처음 만남, 같은 사건은 대화당 한 번, 무례 줄은 호감 계산에서 빠지고(이중 감점 방지) 그 줄의 다른 사건도 버림, 목록 밖·줄 사건으로 온 RUDE 버림, 학습자가 말하지 않은 대화는 400
- `tests/test_judge_events.py`: 의도 판정의 줄 사건 — 같은 호출에서 `events`를 고름(판정 형식의 목록), 무례·목록 밖·중복 버림, `events`가 없거나 목록이 아니면 `[]`, 의도 판정 실패면 `[]`, 행동 묘사만이면 `null`, 지시문에 사건 정의 가이드 v1.0

프롬프트 문장의 품질(모델이 무엇을 고르는지)은 이 테스트로 보지 않습니다. 실제 모델을 부르는 회귀 테스트는 프로토타입 브랜치의 `prototype/mvp_web/tests/replay.py`에 있습니다.

## NPC 발음 시간표와 립싱크

`kokoro-onnx==0.6.1`의 `create_timed()`로 음성과 발음 순서를 얻고, `pocketsphinx==5.0.4`의 로컬 음향 모델로 해당 발음 순서를 최종 PCM에 정렬합니다. TTS 내부 duration은 실제 발음 경계와 어긋날 수 있어 그대로 재생 시간표로 사용하지 않습니다. 별도의 음성 재생성, 외부 분석 API, 문장별 입모양 수작업은 필요하지 않습니다. `KOKORO_MODEL`은 **duration 출력이 포함된** [공식 model-files-v1.1 배포](https://github.com/thewh1teagle/kokoro-onnx/releases/tag/model-files-v1.1)의 `kokoro-v1.0.int8.onnx`, `KOKORO_VOICES`는 해당 배포의 `voices-v1.0.bin`을 가리켜야 합니다. 파일명이 같은 이전 모델에는 duration 출력이 없을 수 있습니다. 시작 로그에 `model has no duration output` 경고가 있으면 정확한 립싱크를 사용할 수 없습니다. 모델은 배포 시 준비하며 요청 중에 다운로드하지 않습니다.

`app/speech_timing.py`는 영어 IPA를 Unity 공통 음소 이름으로 변환합니다. `/aɪ/` 등 이중모음은 하나의 구간으로 묶어 Unity에서 입이 연속 변형되게 합니다. `app/speech_alignment.py`가 같은 최종 PCM을 두 번 분석해 단어·음소 경계를 구합니다. 단어 사이 공백을 무음으로 강제하지 않고 음향 모델이 찾은 무음만 사용합니다. 발음 순서가 누락·변경되거나 시간 범위가 잘못되면 해당 시간표를 폐기하고 음성만 반환하며 경고를 기록합니다. 원래 음성의 속도·샘플은 변경하지 않습니다. 정렬은 음향 모델의 추정이므로 모든 경계의 정확성을 보장하지 않으며 빠른 연결 발음은 실제 음성과 대조해야 합니다. 기존 얼굴의 한계 때문에 일부 자음은 가까운 입모양을 사용하며, 실제 혀·치아 전체의 조음 모델은 아닙니다.

시간표는 WAV의 선택적 `a305` RIFF chunk에 UTF-8 JSON으로 저장합니다. PCM 자체는 바꾸지 않으며, `version: 2`, `provider: "kokoro-en-US"`, 전체 `duration`(초), PCM 바이트의 `pcmSha256`, `phonemes` 배열(`start`, `end`, `phoneme`)을 담습니다. 대사 원문은 넣지 않습니다. Spring은 WAV 바이트 전체를 저장·반환하므로 기존 `/v1/tts`, `/v1/audio/{id}`, `/api/v1/turns/{id}/speech` 경로와 인증을 그대로 사용합니다. Unity는 같은 파일에서 PCM과 시간표를 읽고 해시·길이를 검사합니다.

캐시는 음성과 시간표를 함께 보관합니다. 여러 음성 조각을 합칠 때 `join_wavs()`는 각 조각의 실제 샘플 길이만큼 시간을 이동하고 합친 PCM의 해시를 다시 계산합니다. 한 조각이라도 시간표가 없으면 완전한 립싱크 데이터로 표시하지 않습니다. 기존 저장 WAV에는 시간표가 없으므로 소리는 재생되지만 정확한 입모양은 적용되지 않습니다. 이를 이유로 과거 음성을 자동 재생성하지 않습니다.

읽을 내용이 없는 음성 조각은 실제 무음 길이의 휴지 시간표를 함께 보관합니다. 앞뒤 음성을 다시 듣기용 WAV로 합칠 때 나머지 발음 시간표와 시간 오프셋을 유지하며, 자막 전처리 후 실제로 합성한 문장을 기준으로 음소 시간을 만듭니다.

### 배포 모델 준비와 상태 확인

앱 Docker 이미지는 `requirements-tts.txt`의 Kokoro·PocketSphinx와 duration 출력 모델을 포함합니다. PocketSphinx에 포함된 영어 음향 모델을 사용하며 요청 중 외부 분석 API를 호출하지 않습니다. 합성 후 CPU 정렬 시간이 추가되므로 배포 환경에서 전체 응답 시간을 측정해야 합니다. `ai/tools/prepare_kokoro.py --directory <모델 전용 폴더>`는 공식 배포의 두 파일을 SHA256으로 확인합니다. 같은 이름의 잘못된 기존 파일은 덮어쓰지 않고 실패하므로 새 폴더를 지정해야 합니다. 요청 중 다운로드는 하지 않습니다.

이미지 빌드와 워커 시작 때 `Ready.`를 CPU로 합성해 음성과 일치하는 시간표까지 검사합니다. `/health`의 `tts`는 음성 엔진 존재 여부, `tts_phoneme_timing`은 워밍업의 시간표 검증 결과입니다. 배포 이미지의 `TTS_REQUIRE_TIMING=true`에서는 모델 누락·구형 모델·시간표 변환 실패를 정상 시작으로 처리하지 않습니다. 기존 로컬 환경은 이 옵션을 생략하면 음성 없는 개발을 계속할 수 있습니다.

앱 Compose의 합성 조각 대기 상한은 8초이며 환경 변수로 조정합니다. CPU 합성 시간이 이전 2.5초를 넘는 문장도 있어 필요한 음성 조각이 일찍 버려지지 않도록 둔 상한입니다. 8초 응답 속도를 보장한다는 의미는 아닙니다. `AI_WORKER_CPUS`와 `AI_WORKER_MEMORY`의 기본은 2 CPU·1GiB이며, 배포 대상에서 여러 동시 대화의 지연·메모리·조각 누락을 측정한 후 조정해야 합니다. Spring의 단독 TTS 요청 제한은 기존 30초를 유지합니다.
