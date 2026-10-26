# AI 워커 (FastAPI)

Spring만 부르는 내부 서버입니다. **게임 상태를 바꾸는 권한은 Spring에만 있고**, AI 워커는 판단 재료(판정)와 말(NPC 대답)·소리(음성)만 만듭니다.
상태를 저장하지 않으므로 필요한 것(NPC·지금 상태·최근 대화·기억 카드)은 Spring이 매 요청에 담아 보냅니다.

- AI API 명세(요청·응답·오류, 백엔드 API와의 대응): [docs/api/ai-worker.md](../../docs/api/ai-worker.md)
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
| `STT_FIRST_TIMEOUT_S` | 8 | 대체가 있을 때 첫 음성 인식 호출의 상한 (대체 쪽과 대체가 없을 때는 30초) |
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

## NPC 인격 모델 (자동 생성)

`app/npc_profile_models.py`는 백엔드와 함께 쓰는 공통 인격 스키마([`npc_shared_schemas_v2.json`](../../docs/api/npc-ai-contracts/npc_shared_schemas_v2.json))에서 만든 Pydantic 모델입니다. 직접 고치지 말고 스키마를 고친 뒤 다시 만듭니다. 커밋한 파일이 지금 스키마로 만든 결과와 다르면 테스트가 실패합니다.

```bash
.venv/Scripts/python ../tools/generate_npc_profile_models.py
```

생성 모델은 스키마 규칙 중 활성 NPC의 `voiceId` 필수(조건 규칙), 목록 중복 금지, 생략할 수 있는 칸의 `null` 금지를 검사하지 않고, `"1"` 같은 글을 숫자·참거짓으로 바꿔 받습니다. 이 규칙을 원본 스키마로 검사하는 단계는 아직 없습니다.

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
- `tests/test_stt.py`: 음성 인식 — 프롬프트 되풀이 거르기·장면 단어, 녹음 크기(413)·형식(415)·코덱 매개변수, Groq 실패(429·5xx·키 오류·응답 없음) 시 GMS whisper-1 대체·녹음 자체 오류는 안 넘김·첫 호출 짧은 상한
- `tests/test_guard_local.py`: 무례 판정 단계적 구조. 가짜 판정기로 확신 구간·중간 구간·영어·로컬 실패·LLM 실패, 기준값 설정 검사 (torch 없이 돎)
- `tests/test_hedge.py`: 늦은 호출에 같은 요청 하나 더 보내기, 오류 응답 뒤 남은 요청 기다리기, 판정 시간 상한, NPC 스트림 제외
- `tests/test_contract.py`: 모델 출력이 깨졌을 때(JSON 객체가 아님, 칸 없음)와 빈 문장·없는 주소·프로그램 오류에도 계약의 실패 형식을 지키는지
- `tests/test_learning_aids.py`: 뜻·단어 풀이(대사에 없는 말 버리기, 3개 제한), 추천 단어(정리·중복·3개), 형식 오류 502, 난이도별 NPC 지시문·대답 길이·말 속도·힌트 수준
- `tests/test_feedback.py`: `ai/feedback`의 예시 입력·출력으로 피드백 계약(완료·발화 없음·공급자 실패·출력 오류·입력 오류), 피드백 전용 시간 상한과 같은 요청 더 보내기 끔
- `tests/test_stream_hedge.py`: NPC 스트림의 첫 글자(또는 응답 머리)가 늦으면 같은 요청 스트림 하나 더, 이긴 쪽 글자만 이어짐, 진 쪽 닫기, 둘 다 늦으면 첫 글자 상한
- `tests/test_turn.py`: 한 턴 구조 — 조각을 이어 붙인 턴 전체 음성, 조각 실패·음성 없음이면 `null`, 스트림 안의 추천 단어(`words`)와 그 실패
- `tests/test_audio_store.py`: 음성 보관소의 전체 크기 상한
- `tests/test_profiles.py`: `LLM_PROFILE` 하나로 주소·키·모델이 바뀌는지, GMS 구성에서 대화는 GMS·음성 인식은 Groq로 가는지, 음성 인식 대체(`STT_FALLBACK`), GPT-5 요청 칸
- `tests/test_npc_profile_models.py`: 커밋한 인격 모델이 지금 스키마로 다시 만든 것과 같은지, 계약 예시를 Python 이름으로 읽고 계약 JSON으로 되돌리기, 스키마가 막는 값(모르는 키·빈 글·정해진 값 밖·길이·개수) 거부

프롬프트 문장의 품질(모델이 무엇을 고르는지)은 이 테스트로 보지 않습니다. 실제 모델을 부르는 회귀 테스트는 프로토타입 브랜치의 `prototype/mvp_web/tests/replay.py`에 있습니다.
