# AI 워커 (FastAPI)

Spring만 부르는 내부 서버입니다. **게임 상태를 바꾸는 권한은 Spring에만 있고**, AI 워커는 판단 재료(판정)와 말(NPC 대답)·소리(음성)만 만듭니다.
상태를 저장하지 않으므로 필요한 것(NPC·지금 상태·최근 대화·기억 카드)은 Spring이 매 요청에 담아 보냅니다.

프로토타입(`prototype/mvp_web/server.py`)에서 회귀 테스트로 다듬은 판정·NPC·피드백·기억 카드 코드를 옮긴 것입니다.
API 명세(백엔드와 합의용): [AI 워커 API 명세 (Spring 연동)](https://claude.ai/code/artifact/a7ba0d38-3931-45fa-9230-1dd175160d0f)

## 실행

```bash
cd ai/worker
python -m venv .venv
.venv/Scripts/python -m pip install -r requirements.txt
.venv/Scripts/python -m pip install -r requirements-tts.txt     # 선택: 음성 합성(Kokoro). 없으면 tts: false로 알림
.venv/Scripts/python -m pip install -r requirements-guard.txt   # 선택: 로컬 무례 판정기(CPU torch). 켜는 것은 GUARD_LOCAL_MODEL
AI_WORKER_TOKEN=<Spring과 같은 값> .venv/Scripts/python -m uvicorn app.main:app --port 8020
```

| 환경 변수 | 기본값 | 뜻 |
|---|---|---|
| `AI_WORKER_TOKEN` | (필수) | Spring과 공유하는 내부 토큰. 모든 요청의 `X-Internal-Token` |
| `LLM_PROFILE` | `gms-luna` | 모델 구성 이름 ([`llm_profiles.json`](llm_profiles.json): `gms-luna`(GPT-6 Luna), `gms-sol`(GPT-6.1 SOL), `groq`). 기본값은 json의 `default_profile`. 역할별 모델·공급자 주소·키 이름·대체 모델이 한 번에 바뀜 |
| `GROQ_API_KEY` / `GMS_KEY` | (구성에 따라 필수) | 공급자 키 (이름은 json의 `key_env`). 개발할 때는 저장소 루트 `.env`에서 읽음. GMS 구성도 음성 인식은 Groq라 둘 다 필요 |
| `LLM_API_KEY`, `LLM_BASE_URL` | (구성 값) | 구성의 기본 공급자 키·주소를 덮어쓰기 (예전 방식 호환) |
| `MODEL_NPC` `MODEL_INTENT` `MODEL_GUARD` `MODEL_TRANSLATE` `MODEL_HINT` `MODEL_FEEDBACK` `MODEL_MEMORY` `MODEL_STT` | (구성 값) | 역할 하나만 모델 바꾸기 (그 역할의 공급자 그대로) |
| `MODEL_FALLBACK` | (구성 값) | 대체 모델 사슬 `a=b,c=d` |
| `LLM_PROFILES` | `llm_profiles.json` | 구성 파일 경로 (프로토타입 `prototype/mvp_web`도 같은 파일을 읽음) |
| `CALL_TIMEOUT_S` | 8 | 모델 호출 하나의 상한. 넘으면 끊고 재시도·대체 모델 |
| `HEDGE_AFTER_S` | 3 | 한 번에 받는 호출(판정·번역·힌트 등, NPC 스트림 제외)이 이만큼 답이 없으면 같은 요청을 하나 더 보내 먼저 온 답을 씀. 0이면 끔. 측정은 `ai/tools/llm_latency.py` |
| `KOKORO_MODEL`, `KOKORO_VOICES` | (없음) | `kokoro-v1.0.int8.onnx`, `voices-v1.0.bin` 경로 (받는 곳은 `prototype/mvp_web/README.md`) |
| `TTS_CHUNK_BUDGET_S` | 2.5 | NPC 대답 한 조각 합성이 이보다 늦으면 그 조각은 `tts: false` |
| `GUARD_LOCAL_MODEL` | (없음, 꺼짐) | 로컬 무례 판정기 폴더 (`train_clf.py`가 남긴 `runs/<이름>/best`). 주면 한글 입력의 확신 구간은 판정용 LLM 없이 판정. `requirements-guard.txt` 필요, 없거나 못 읽으면 경고만 남기고 전부 LLM |
| `GUARD_LOCAL_T_LO`, `GUARD_LOCAL_T_HI` | (모델을 주면 필수) | 로컬 점수 ≤ T_LO → 아님, ≥ T_HI → 무례, 그 사이는 LLM. `prototype/guard_clf/cascade_eval.py`로 보정용 데이터에서 고른 값 |

## API

| API | 언제 | 응답 |
|---|---|---|
| `POST /v1/judge` | 한 턴의 처음: 번역·무례·의도를 동시에 판정 (상태는 안 바꿈) | JSON |
| `POST /v1/npc/reply` | Spring이 상태를 바꾼 뒤 NPC 대답 | SSE: `delta` → `audio`(seq 순서) → `done` / `error` |
| `GET /v1/audio/{id}` | 스트림의 음성 조각 (10분 보관) | audio/wav |
| `POST /v1/tts` | 모범 발화·단어 발음 같은 단독 합성 | audio/wav |
| `POST /v1/stt` | 녹음 인식 (multipart `file`, 선택 `scene_words`) | JSON |
| `POST /v1/hint` | 학습자가 💡를 누른 때 | JSON |
| `POST /v1/conversation/end` | 대화가 끝났을 때: 피드백 + 기억 카드 | JSON |
| `GET /health` | 상태 확인 (토큰 없이) | JSON |

- 모든 응답(스트림은 마지막 `done`)에 `usage`: 단계·실제로 쓴 모델·입출력 토큰·시간·대체 여부. Spring은 이걸로 크레딧을 차감합니다.
  단가는 `llm_profiles.json`의 `prices_per_1m_tokens_usd` (프로토타입 `cost_report.py`가 같은 표로 턴·세션당 비용을 계산).

### 구성 바꾸기

```bash
# 기본은 gms-luna (.env에 GMS_KEY, 음성 인식용 GROQ_API_KEY). 다른 구성은 LLM_PROFILE로
LLM_PROFILE=gms-sol AI_WORKER_TOKEN=<값> .venv/Scripts/python -m uvicorn app.main:app --port 8020
curl -s localhost:8020/health        # "profile": "gms-sol", "models": {...}
```

GMS 키는 팀 공용 크레딧입니다. json에 없는 모델(예전 `gms-high`의 GPT-5.4, `gms-low`의 GPT-4o-mini 등)은 쓰지 않고, `gms-sol`은 `gms-luna`보다 크레딧이 약 20배 드니 꼭 필요할 때만 씁니다.
GPT-5·GPT-6 계열 요청 칸(`max_completion_tokens`, temperature 없음, `reasoning_effort`)은 json의 `model_params`로 정해져 코드 수정 없이 바뀝니다.
어느 구성을 쓸지는 프로토타입 회귀 테스트(`python tests/replay.py --profile gms-luna --npc`)로 통과율·지연·비용을 재고 정합니다.
- 오류는 `{"error": {"code", "message"}}`: `invalid_request` 400, `unauthorized` 401, `provider_limit` 429, `provider_unavailable` 503, `tts_unavailable` 503.
  판정·대화 종료는 일부가 실패해도 200으로 이어 갑니다 (의도 → `unclear`, 무례 → `false`, 기억 카드 → `null` + `errors`).
- 추적: `X-Request-Id`(없으면 만들어 응답 헤더로 돌려줌), `X-User-Id`, `X-Conv-Id`를 요청마다 로그 한 줄(JSON)로 남깁니다.

### 한 줄에 행동 여러 개 (`state.more_intents`, 응답 `intent.ids`)

"to go. cash"(포장+결제), "here. bye"(결제+작별)처럼 한 줄에 행동이 여러 개일 수 있다. Spring은 지금 상태의 의도(`intents`)와 함께
같은 장소 다음 단계의 의도를 `more_intents`로 보내고, 응답의 `intent.ids`(말한 순서)를 **차례로 적용**한다.
앞 행동으로 바뀐 상태에 맞지 않는 의도는 건너뛰고, 대화가 끝나는 상태에 이르면 멈춘다. `intent.id`는 첫 행동 (예전 형식 호환).
규칙은 프로토타입 `apply_intent()`와 같다.

### 판정 요청의 `state.slots` (장소 데이터 형식)

장소 파일(`prototype/mvp_web/places/*.json`)에서 Spring이 만듭니다. 사는 장소가 아니면 생략합니다.

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

## 테스트

```bash
.venv/Scripts/python -m pytest -q       # 모델 호출 없음 (가짜 공급자), 2초
```

가짜 공급자(`tests/conftest.py`)가 시스템 프롬프트로 단계를 알아보고 정해 둔 답을 돌려줍니다.
무례 판정 단계적 구조(`tests/test_guard_local.py`): 가짜 판정기로 확신 구간·중간 구간·영어·로컬 실패·LLM 실패, 기준값 설정 검사 (torch 없이 돎).
구성 전환(`tests/test_profiles.py`): `LLM_PROFILE` 하나로 주소·키·모델이 바뀌는지, GMS 구성에서 대화는 GMS·음성 인식은 Groq로 가는지, GPT-5 요청 칸.
시간 상한·재시도·대체 모델·하루 한도 건너뛰기·형식 강제 실패 재시도, 판정 값 정리, SSE 순서·느린 음성 대체, 기억 카드 정리, 음성 인식 프롬프트 되풀이 거르기를 봅니다.
프롬프트 문장의 품질(모델이 무엇을 고르는지)은 프로토타입 회귀 테스트(`prototype/mvp_web/tests/replay.py`)가 봅니다. 프롬프트를 바꾸면 거기서 먼저 확인합니다.

## 측정 (2026-10-14, Groq 무료 키, i7-13700H CPU, 약국 한 턴)

| 단계 | 시간 |
|---|---|
| 판정 (번역·무례·의도 동시) | 1.05초 |
| NPC 첫 글자 | 0.27초 |
| 첫 음성 조각 (1.4초 분량) | 1.32초 |
| 대화 종료 (피드백·기억 카드 동시) | 1.26초 |

같은 PC에서 3D 화면이 켜져 있어 Kokoro가 평소(한 문장 0.44~1.16초)보다 느렸습니다. 배포 서버 CPU에서 같은 대사로 다시 재야 합니다.
