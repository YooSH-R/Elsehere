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
| `KOKORO_MODEL`, `KOKORO_VOICES` | (없음) | `kokoro-v1.0.int8.onnx`, `voices-v1.0.bin` 경로 ([kokoro-onnx 배포 파일](https://github.com/thewh1teagle/kokoro-onnx/releases)) |
| `TTS_CHUNK_BUDGET_S` | 2.5 | NPC 대답 한 조각 합성이 이보다 늦으면 그 조각은 `tts: false` |
| `GUARD_LOCAL_MODEL` | (없음, 꺼짐) | 로컬 무례 판정기 폴더 (KcELECTRA 학습 결과 `best/`). 주면 한글 입력의 확신 구간은 판정용 LLM 없이 판정. `requirements-guard.txt` 필요, 없거나 못 읽으면 경고만 남기고 전부 LLM |
| `GUARD_LOCAL_T_LO`, `GUARD_LOCAL_T_HI` | (모델을 주면 필수) | 로컬 점수 ≤ T_LO → 아님, ≥ T_HI → 무례, 그 사이는 LLM. 학습에 쓰지 않은 보정용 데이터에서 고른 값 |

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
- `tests/test_guard.py`: 무례 판정 `/v1/guard` — yes/no 해석, 행동 묘사만 있는 말, 빈 입력, 시간 상한(`GUARD_BUDGET_S`), 공급자 실패, 로컬 판정기 연결
- `tests/test_stt.py`: 음성 인식 — 프롬프트 되풀이 거르기·장면 단어, 녹음 크기(413)·형식(415)·코덱 매개변수, Groq 실패(429·5xx·키 오류·응답 없음) 시 GMS whisper-1 대체·녹음 자체 오류는 안 넘김·첫 호출 짧은 상한, `STT_CHAIN` 순서(GPU 서버 → GMS → Groq, 자리별 상한, 모두 실패 시 공급자별 오류), 연속 실패한 공급자 잠시 건너뛰기·성공하면 초기화·마지막 공급자는 안 건너뜀
- `tests/test_guard_local.py`: 무례 판정 단계적 구조. 가짜 판정기로 확신 구간·중간 구간·영어·로컬 실패·LLM 실패, 기준값 설정 검사 (torch 없이 돎)
- `tests/test_hedge.py`: 늦은 호출에 같은 요청 하나 더 보내기, 오류 응답 뒤 남은 요청 기다리기, 판정 시간 상한, NPC 스트림 제외
- `tests/test_contract.py`: 모델 출력이 깨졌을 때(JSON 객체가 아님, 칸 없음)와 빈 문장·없는 주소·프로그램 오류에도 계약의 실패 형식을 지키는지
- `tests/test_learning_aids.py`: 뜻·단어 풀이(대사에 없는 말 버리기, 3개 제한), 추천 단어(정리·중복·3개), 형식 오류 502, 난이도별 NPC 지시문·대답 길이·말 속도·힌트 수준
- `tests/test_feedback.py`: `ai/feedback`의 예시 입력·출력으로 피드백 계약(완료·발화 없음·공급자 실패·출력 오류·입력 오류), 피드백 전용 시간 상한과 같은 요청 더 보내기 끔
- `tests/test_stream_hedge.py`: NPC 스트림의 첫 글자(또는 응답 머리)가 늦으면 같은 요청 스트림 하나 더, 이긴 쪽 글자만 이어짐, 진 쪽 닫기, 둘 다 늦으면 첫 글자 상한
- `tests/test_turn.py`: 한 턴 구조 — 조각을 이어 붙인 턴 전체 음성, 조각 실패·음성 없음이면 `null`, 스트림 안의 추천 단어(`words`)와 그 실패
- `tests/test_audio_store.py`: 음성 보관소의 전체 크기 상한
- `tests/test_profiles.py`: `LLM_PROFILE` 하나로 주소·키·모델이 바뀌는지, GMS 구성에서 대화는 GMS·음성 인식은 Groq로 가는지, 음성 인식 대체(`STT_FALLBACK`)·순서(`STT_CHAIN`, gpu 주소·토큰 필수, 잘못된 값)·건너뛰기 설정, GPT-5 요청 칸
- `tests/test_npc_chat.py`: 터미널 대화 도구(`ai/tools/npc_chat.py`)의 크레딧 계산(입력·출력 단가, 단가 모르는 모델, 같은 요청 더 보냄 표시), 첫 대사는 시스템 프롬프트만 보내고 기억, 이력을 붙인 대답과 대화 기록, 공급자 실패 때 기록 유지, GMS 키만으로 실행
- `tests/test_npc_profile_models.py`: 커밋한 인격 모델이 지금 스키마로 다시 만든 것과 같은지, 계약 예시를 Python 이름으로 읽고 계약 JSON으로 되돌리기, 스키마가 막는 값(모르는 키·빈 글·정해진 값 밖·길이·개수) 거부
- `tests/test_npc_configs.py`: 원본 스키마 검사 — 계약 예시와 서비스 NPC 파일(`ai/npcs`) 통과, 활성 NPC의 `voiceId`, 생성 모델이 놓치는 규칙(중복·`null`·형변환)을 어긴 위치로 알려 주기, 여러 오류를 한 번에. NPC 파일 합치기 — 파일 이름 순, 파일끼리 `npcId` 중복, 파일 이름을 붙인 스키마 오류, JSON이 아닌 파일, 빈 폴더(스키마의 배열 규칙), 명령줄 검사만·`--out` 저장(BOM 없음)·문제가 있으면 저장 안 함. 서비스 상황 — `npcId`로 모으기, 연결 없음·없는 NPC·없는 파일·형식 위반·장소 불일치, `--scenarios-out` 저장, 서비스 NPC 3명의 상황, 백엔드 생성 파일이 원본과 같은지
- `tests/test_npc_prompt.py`: NPC 시스템 프롬프트 — 인격 부분(프로필 칸이 스키마 순서대로 한 줄씩, 생략·빈 칸은 줄째로 빠짐, Big Five 수치는 설정됐을 때만 상대값으로, 값 안의 줄바꿈이 새 부분을 열지 못함), 다섯 부분 순서(규칙·인격·상황·학습 수준·이번 턴), 주어진 사실만, 학습 수준 6단계 → 규칙 3묶음, 첫 대사는 시스템 프롬프트만·응답 턴은 이력 역할과 사용자 발화 마지막(계약 추론 요청 예시로)

프롬프트 문장의 품질(모델이 무엇을 고르는지)은 이 테스트로 보지 않습니다. 실제 모델을 부르는 회귀 테스트는 프로토타입 브랜치의 `prototype/mvp_web/tests/replay.py`에 있습니다.

## NPC 발음 시간표와 립싱크

`kokoro-onnx==0.6.1`의 `create_timed()`를 사용해 같은 합성 호출에서 음성과 발음별 시간을 얻습니다. 별도의 음성 재생성, 외부 분석 API, 문장별 입모양 수작업은 필요하지 않습니다. `KOKORO_MODEL`은 **duration 출력이 포함된** [공식 model-files-v1.1 배포](https://github.com/thewh1teagle/kokoro-onnx/releases/tag/model-files-v1.1)의 `kokoro-v1.0.int8.onnx`, `KOKORO_VOICES`는 해당 배포의 `voices-v1.0.bin`을 가리켜야 합니다. 파일명이 같은 이전 모델에는 duration 출력이 없을 수 있습니다. 시작 로그에 `model has no duration output` 경고가 있으면 정확한 립싱크를 사용할 수 없습니다. 모델은 배포 시 준비하며 요청 중에 다운로드하지 않습니다.

`app/speech_timing.py`는 영어 IPA를 Unity 공통 음소 이름으로 변환합니다. `/aɪ/` 등 이중모음은 하나의 구간으로 묶어 Unity에서 입이 연속 변형되게 합니다. 합성 시 얻은 시각은 무음 제거·문장 쉼이 반영된 최종 오디오 기준입니다. 기존 얼굴의 한계 때문에 일부 자음은 가까운 입모양을 사용하며, 실제 혀·치아 전체의 조음 모델은 아닙니다.

시간표는 WAV의 선택적 `a305` RIFF chunk에 UTF-8 JSON으로 저장합니다. PCM 자체는 바꾸지 않으며, `version: 2`, `provider: "kokoro-en-US"`, 전체 `duration`(초), PCM 바이트의 `pcmSha256`, `phonemes` 배열(`start`, `end`, `phoneme`)을 담습니다. 대사 원문은 넣지 않습니다. Spring은 WAV 바이트 전체를 저장·반환하므로 기존 `/v1/tts`, `/v1/audio/{id}`, `/api/v1/turns/{id}/speech` 경로와 인증을 그대로 사용합니다. Unity는 같은 파일에서 PCM과 시간표를 읽고 해시·길이를 검사합니다.

캐시는 음성과 시간표를 함께 보관합니다. 여러 음성 조각을 합칠 때 `join_wavs()`는 각 조각의 실제 샘플 길이만큼 시간을 이동하고 합친 PCM의 해시를 다시 계산합니다. 한 조각이라도 시간표가 없으면 완전한 립싱크 데이터로 표시하지 않습니다. 기존 저장 WAV에는 시간표가 없으므로 소리는 재생되지만 정확한 입모양은 적용되지 않습니다. 이를 이유로 과거 음성을 자동 재생성하지 않습니다.
