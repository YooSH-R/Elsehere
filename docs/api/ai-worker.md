# AI 워커 API 계약 (Spring ↔ AI 워커)

이 문서는 `ai/worker`의 **현재 코드 기준** 계약입니다. 코드와 문서가 다르면 버그로 보고, 둘 중 하나를 고친 뒤 이 문서를 갱신합니다. 요청 형식의 원본은 [schemas.py](../../ai/worker/app/schemas.py)입니다.

- AI 워커는 **상태를 저장하지 않습니다.** 게임 상태·전환 규칙·가격·크레딧·기록은 Spring이 갖고, 필요한 것을 매 요청에 담아 보냅니다.
- AI가 돌려주는 의도는 **제안**입니다. 상태를 바꿀지는 Spring이 현재 상태에서 다시 검증해 정합니다.
- Unity는 AI 워커를 직접 부르지 않습니다. 모든 호출은 Spring을 거칩니다.

## 1. 공통

| 항목 | 내용 |
|---|---|
| 인증 | 모든 요청에 `X-Internal-Token` (환경 변수 `AI_WORKER_TOKEN`과 같은 값). `/health`만 예외 |
| 추적 | `X-Request-Id`(없으면 워커가 만듦, 응답 헤더로 되돌려 줌), `X-User-Id`, `X-Conv-Id` — 워커 로그에 남아 Spring 턴 기록과 잇는 열쇠 |
| 형식 | 요청·응답 JSON (UTF-8). 예외: `/v1/stt`는 multipart, `/v1/npc/reply`는 SSE, `/v1/audio`·`/v1/tts`는 WAV |
| 사용량 | 모델을 부른 응답은 `usage` 배열을 담습니다. 크레딧 차감·원장은 Spring이 이것으로 합니다 (2절 끝) |

**HTTP 오류 형식** — 요청 전체가 실패한 경우만 이 형식입니다.

```json
{"error": {"code": "provider_unavailable", "message": "모든 모델이 응답하지 않음 (...)"}}
```

| code | HTTP | 뜻 | Spring 처리 |
|---|---|---|---|
| `unauthorized` | 401 | 내부 토큰 틀림 | 설정 오류. 재시도하지 않음 |
| `invalid_request` | 400 | 요청 형식 오류 (메시지에 어느 칸인지) | 재시도하지 않음 |
| `provider_limit` | 429 | 공급자 한도 (대체 모델까지 소진) | 잠시 뒤 재시도 안내 |
| `provider_unavailable` | 503 | 공급자 오류·응답 없음 (대체 모델까지 실패) | 재시도 안내 |
| `invalid_output` | 502 | 모델 출력이 정해진 형식이 아님 (힌트 등) | 응답 형식 오류로 안내 (`AI_RESPONSE_INVALID`) |
| `tts_unavailable` | 503 | 음성 합성 모델 없음 | 자막만 |
| `not_found` | 404 | 음성 조각 없음·보관 시간 지남, 없는 주소 | 음성이면 자막만 |
| `method_not_allowed` | 405 | 주소는 있지만 HTTP 메서드가 다름 | 호출 코드 오류 |
| `internal_error` | 500 | 워커 프로그램 오류 (원인은 워커 로그) | 재시도 안내, 워커 로그 확인 |

**부분 실패** — `/v1/judge`는 일부가 실패해도 **HTTP 200**으로 돌려주고 실패한 칸에 오류를 적습니다(2.2절). `/v1/feedback`은 실패도 200 안의 `feedback.status: failed`로 알립니다(2.7절). NPC 대답은 SSE `error` 이벤트로 알립니다(2.3절).

**시간 상한** (워커 환경 변수, 재시도·대체 모델 포함)

| 변수 | 기본 | 적용 |
|---|---|---|
| `CALL_TIMEOUT_S` | 8초 | 모델 호출 **한 번**. 넘으면 끊고 재시도·대체 모델 |
| `HEDGE_AFTER_S` | 3초 | 한 번에 받는 호출이 이만큼 답이 없으면 같은 요청을 하나 더 보내 먼저 온 답을 씀 (NPC 스트림은 제외). 공급자가 혼잡할 때 응답 없이 멈춘 호출을 기다리지 않기 위함. 평소 응답(gms-luna 판정 p99 약 2.5~3.9초)보다 길게 잡아 평소에는 거의 더 보내지 않음 |
| `JUDGE_BUDGET_S` | 5초 | 판정의 의도·무례·번역 **각각**. 넘으면 그 칸만 `timeout` 오류 |
| `REPLY_FIRST_BUDGET_S` | 5초 | NPC 대답 **첫 글자까지**. 넘으면 SSE `error`(`timeout`). 말하기 시작한 대답은 끊지 않음 |

Spring 쪽의 턴 전체 제한(STT + 판정 + 대답)은 Spring이 정합니다. 워커 상한보다 길게 잡아야 워커의 오류 응답을 받을 수 있습니다.

**공통 객체** — 여러 요청에 같은 형식으로 들어갑니다.

`npc`

| 칸 | 형식 | 뜻 |
|---|---|---|
| `id` | 문자열 (필수) | NPC 코드 |
| `name` | 문자열 (필수) | 이름 (프롬프트에 씀) |
| `role` | 문자열 (필수) | 역할 한 줄 (예: "the barista at Maple Cafe.") |
| `facts` | 문자열 | 메뉴·가격·성격 등 NPC가 아는 사실 |
| `scene` | 문자열 | 장면 설명 (요청의 `scene`이 비면 이것) |
| `tts_voice` | 문자열 (기본 `af_heart`) | Kokoro 목소리 이름 |
| `tts_speed` | 수 0.5~2.0 (기본 1.0) | 이 NPC의 타고난 말 속도. 실제 속도는 여기에 난이도 배율을 곱함 |

`history`의 한 줄

| 칸 | 형식 | 뜻 |
|---|---|---|
| `who` | `npc` 또는 `me` (필수) | 말한 사람 (`me`는 학습자) |
| `text` | 문자열 (필수, 최대 1000자) | 말한 내용 |
| `english` | 문자열 또는 `null` | 학습자 줄의 영어 번역 (판정의 `translation.english`) |
| `rude` | 불 (기본 `false`) | 학습자 줄의 무례 판정이 `true`였는지 (`null`은 `false`로) |

`level` — 대화 난이도(백엔드 `levelApplied`). `1`·`2`·`3` 중 하나이고 기본은 `2`입니다. MVP는 `1`만 보냅니다. 판정(`/v1/judge`)에는 영향이 없습니다.

| 단계 | NPC 대답 | 말 속도 배율 | 힌트·추천 단어·단어 풀이 수준 |
|---|---|---|---|
| `1` | 초보자에게 말하듯 천천히, 쉬운 일상 단어. 한 문장(많아야 두 문장), 문장마다 14단어 미만, 속어·관용구 없음 | ×0.85 | 아주 쉽고 짧게 (CEFR A1-A2) |
| `2` | 자연스러운 일상 미국 영어(줄임말·흔한 관용구). 한두 문장, 모두 25단어 미만 | ×1.0 | 쉽고 자연스럽게 (CEFR A2-B1) |
| `3` | 원래 말투 그대로(속어·관용구·구동사). 두세 문장, 45단어 미만 | ×1.06 | 자연스럽게, 흔한 관용구 가능 (CEFR B1-B2) |

## 2. 엔드포인트

### 2.1 `GET /health` (토큰 불필요)

```json
{"ok": true, "tts": true, "profile": "groq", "models": {"intent": "...", "guard": "...", "npc": "...", "stt": "..."}, "guard_local": null}
```

`profile`은 수집 기록의 `profile` 칸에 그대로 남깁니다. `guard_local`은 로컬 무례 판정기를 켰을 때만 `{"model": "v2-mix", "t_lo": 0.05, "t_hi": 0.95}`이고, 끄면(기본) `null`입니다.

### 2.2 `POST /v1/judge` — 의도·무례·번역

학습자 한 줄을 받아 의도·무례 판정·번역(한글이 있을 때만)을 **동시에** 돌립니다.

요청: `npc`, `scene`(비면 `npc.scene`), `state`(지금 상태: `id`, `desc`, `intents`, `slots`, `current`, `more_intents`), `history`(최대 40줄), `text`(최대 500자, 공백만이면 400 `invalid_request`).

응답:

```json
{
  "text": "one latte please",
  "dropped": false,
  "notice": null,
  "intent": {"id": "order_items", "ids": ["order_items"], "slots": {"items": [{"name": "latte", "iced": false, "qty": 1}], "choice": "none"}, "error": null},
  "guard": {"abusive": false, "error": null, "error_code": null},
  "translation": null,
  "usage": [{"step": "intent", "model": "...", "input_tokens": 512, "output_tokens": 20, "latency_s": 0.41, "fallback": false}]
}
```

| 칸 | 규칙 |
|---|---|
| `text` | 행동 묘사(`*...*`)를 지운 말. 판정·대답·기록에는 이것을 씀 |
| `dropped` | `true`면 행동 묘사만 있었던 것. 모델을 부르지 않았고 `intent`·`guard`·`translation`은 `null`. **턴으로 세지 않고** `notice`를 보여 줌 |
| `notice` | 학습자에게 보여 줄 한국어 안내 (행동 묘사를 지웠을 때) |
| `intent.ids` | 말한 순서대로 최대 3개. Spring이 **하나씩 현재 상태에서 다시 검증**해 적용. `id`는 `ids[0]` |
| `intent.slots` | 요청 `state.slots`에 있는 칸만. 상품은 `catalog` 안의 이름만, `iced`는 `iced_ok`에 있을 때만 |
| `intent.error` | 의도 판정 실패 시 메시지, 이때 `id`는 `unclear`. **상태·금액을 바꾸지 말고 다시 물을 것** |
| `translation` | 한글이 섞였을 때만 `{"english", "show", "error"}`. `show`는 무례가 아님이 **확정**(`abusive: false`)일 때만 `true` |

**`guard` — 무례 판정**

| `abusive` | `error_code` | 뜻 | Spring 처리 |
|---|---|---|---|
| `true` | `null` | 무례 | 무례 누적 |
| `false` | `null` | 무례 아님 | — |
| `null` | `empty` | 판정 모델이 빈 답을 냄 | **무례로 세지 않음.** 대화 계속. 기록에 남김 |
| `null` | `unparsed` | yes/no가 아닌 답 (거절 등) | 위와 같음. 심한 말에 모델이 거절하는 경우가 있어 건수를 따로 봄 |
| `null` | `timeout` | `JUDGE_BUDGET_S` 초과 | 위와 같음 |
| `null` | `provider_unavailable` / `provider_limit` | 공급자 실패 | 위와 같음 |

판정 모델의 빈 답·거절과 공급자 장애는 무례도 정상도 아닌 `null`입니다. **`null`을 `false`나 `true`처럼 다루지 마세요.** 턴 기록에는 `null`과 `error_code`를 그대로 남깁니다.

**로컬 판정기를 켰을 때 (단계적 구조, 기본 꺼짐)**

워커에 `GUARD_LOCAL_MODEL`을 주면 한글이 든 입력은 로컬 KcELECTRA가 먼저 점수 `score`(0~1, 확률 아님)를 냅니다. `score ≥ t_hi`면 무례, `score ≤ t_lo`면 아님으로 **판정용 LLM을 부르지 않고** 끝납니다. 그 사이 점수, 한글이 없는 입력, 로컬 실패는 지금처럼 LLM이 판정합니다. 이때 `guard`에 칸 세 개가 붙습니다(꺼져 있으면 붙지 않음).

```json
"guard": {"abusive": true, "error": null, "error_code": null, "source": "local", "score": 0.9912, "local_error": null}
```

| 칸 | 뜻 |
|---|---|
| `source` | `local`(로컬이 판정, `usage`에 `guard` 단계 없음) 또는 `llm` |
| `score` | 로컬 점수. 한글이 없거나 로컬이 실패했으면 `null`. LLM이 판정했어도 남김 |
| `local_error` | 로컬 판정기 오류 (이때 판정은 LLM으로 이어 감) |

`abusive`·`error_code`의 뜻과 Spring 처리는 위 표와 같습니다. 기록에는 `source`와 `score`를 함께 남겨 주세요(호출 감소와 자동 구간 품질을 이걸로 셉니다). 서비스에서 켜는 것은 학습에 쓰지 않은 보정 데이터로 `t_lo`·`t_hi`를 고르고, 미리 정한 평가 기준(품질은 LLM 단독과 비슷하면서 판정용 LLM 호출 감소)을 넘은 뒤입니다. 그 전에는 켜지 않습니다.

이진 판정이라 위협·성희롱 같은 **세부 유형은 구분하지 않습니다.** NPC가 먼저 잘못한 장면에서의 맞대응(정당방위)처럼 장면에 따라 봐줘야 하는 경우는, 유형을 돌려주는 계약이 생기기 전까지 Spring 규칙에서 자동 제재를 빼 주세요.

### 2.3 `POST /v1/npc/reply` — NPC 대답 (SSE)

요청: `npc`, `memory`(선택: `visits`, `last_order`, `card`), `history`(최대 40줄), `text`(학습자 원문, 최대 500자), `instruction`(Spring 규칙이 정한 이번 대답에서 할 일, 최대 1000자), `tts`(기본 `true`), `level`(기본 `2`). **한 턴의 대답은 난이도에 따라 한 문장에서 세 문장까지**이며 한 줄로 옵니다.

응답은 `text/event-stream`이며 이벤트 순서는 다음과 같습니다.

```
delta*  →  audio*  →  done | error
```

`delta`와 `audio`는 섞여서 옵니다. 마지막은 항상 `done` 또는 `error` 중 하나입니다.

| 이벤트 | data | 뜻 |
|---|---|---|
| `delta` | `{"text": "Got"}` | 글자 조각. 받는 대로 자막에 붙임 |
| `audio` | `{"seq": 0, "text": "Got it, one latte for you.", "audio_id": "9f3a...", "duration_s": 1.8, "tts": true}` | 읽을 조각. `seq` 순서대로 재생. **WAV가 아니라 `audio_id`** — `GET /v1/audio/{audio_id}`로 받음. `tts: false`면 `audio_id: null`(합성 실패·늦음) → 그 조각은 자막만 |
| `done` | `{"text": "...", "first_token_s": 0.62, "total_s": 2.1, "usage": [...]}` | **생성 완료**. 클라이언트 재생 완료가 아님. `text`가 대답 전문 |
| `error` | `{"code": "provider_unavailable", "message": "...", "usage": [...]}` | 생성 실패. 이미 보낸 `delta`·`audio`는 유효. `code`는 `provider_unavailable`·`provider_limit`·`timeout` |

- 대답은 한 줄입니다. 모델이 줄을 바꿔 다른 사람 대사를 쓰면 버립니다.
- 빈 대답이면 `"Sorry, could you say that again?"`로 대신합니다(`done`).
- Spring이 연결을 끊으면 워커도 모델 호출을 멈춥니다.
- 조각 하나의 합성이 `TTS_CHUNK_BUDGET_S`(기본 2.5초)를 넘으면 그 조각은 `tts: false`입니다.

### 2.4 `GET /v1/audio/{audio_id}`

`audio/wav`를 돌려줍니다. 보관 시간은 600초(설정 `audio_ttl_s`, 환경 변수로는 아직 못 바꿈)이며 **워커 프로세스 메모리**에 있습니다. 워커를 여러 대 띄우면 같은 대답의 음성은 그 대답을 만든 워커에서 받아야 합니다. 없으면 404 `not_found`입니다.

Unity가 음성을 받을 때도 Spring이 인증한 뒤 이 주소에서 받아 중계합니다.

### 2.5 `POST /v1/stt` — 음성 인식 (multipart)

| 칸 | 내용 |
|---|---|
| `file` | 녹음 (최대 3MB, 비면 400). `audio/webm`·`ogg`·`mp4`·`wav`·`mpeg` |
| `scene_words` | 장면 단어(메뉴·상품 이름). JSON 배열 또는 쉼표로 구분한 글. 최대 30개 사용 |

응답: `{"text": "...", "latency_s": 0.41, "audio_s": 2.9, "usage": [{"step": "stt", "model": "...", "audio_s": 2.9, "latency_s": 0.41, "fallback": false}]}`

- 무음일 때 Whisper가 프롬프트를 되풀이한 결과는 `text: ""`로 거릅니다. 빈 결과면 다시 말하기를 안내합니다.
- 대체 모델이 없습니다. 실패하면 바로 429/503입니다.
- 음성은 외부 음성 인식 서비스(지금 Groq)로 전송됩니다. 사용자 동의 화면에 이 점을 알려야 합니다.

### 2.6 `POST /v1/hint`

요청: `npc`, `scene`, `goal`, `history`, `level`(기본 `2`). 응답: `{"say_this": [{"en", "ko"} 최대 2개], "keywords": [{"en", "ko"} 최대 3개], "usage": [...]}`. 공급자 실패는 429/503, 출력이 이 형식이 아니면 502 `invalid_output`입니다.

### 2.6.1 `POST /v1/suggest` — 추천 단어 (백엔드 `suggestWords`)

학습자가 다음 대답에 쓸 만한 영어 단어·짧은 구를 3개까지 만듭니다. NPC 대답이 끝난 뒤(`done`의 `text`를 `history` 끝에 붙여) 부릅니다.

요청: `npc`, `scene`(비면 `npc.scene`), `goal`(지금 상태에서 학습자가 할 일, 비면 자연스러운 대답, 최대 300자), `history`(마지막 NPC 줄이 대답할 대상, 최대 40줄), `level`(기본 `2`).

응답: `{"words": ["for here", "to go", "please"], "usage": [...]}`

- 단어 또는 1~3단어 구, 문자열 목록, 한국어 없음. 앞뒤 공백을 지우고 대소문자를 무시한 중복을 뺀 뒤 3개까지.
- 출력에 쓸 단어가 하나도 없으면 502 `invalid_output`. 공급자 실패는 429/503.
- 토큰이 떨어져 작별 대사로 끝나는 턴처럼 추천 단어가 필요 없으면 부르지 않습니다(백엔드는 `suggestWords: null`).

### 2.6.2 `POST /v1/meaning` — NPC 대사 뜻과 단어 풀이 (백엔드 CONV-04)

요청: `npc`, `text`(NPC가 한 말, 최대 1000자, 공백만이면 400), `level`(기본 `2`, 단어 풀이에 무엇을 고를지).

응답:

```json
{"meaning_ko": "좋아요! 매장에서 드실 건가요, 가져가실 건가요?", "glossary": [{"text": "to go", "meaning_ko": "포장"}], "usage": [...]}
```

- `meaning_ko`: 말투를 살린 한국어 뜻 (가게 직원은 해요체, 친구 같은 말투는 반말). 괄호 풀이 없이 번역만.
- `glossary`: 대사에서 학습자가 모를 만한 말(속어·관용구·구동사·핵심 단어) **최대 3개**. `text`는 대사에 **그대로 있는** 말만 남깁니다(대소문자 무시). 쉬운 대사면 빈 목록.
- 출력이 이 형식이 아니거나 `meaning_ko`가 비면 502 `invalid_output`. 같은 대사는 Spring이 저장해 다시 부르지 않습니다.

### 2.7 `POST /v1/feedback` — 대화 종료 피드백 (ai/feedback 계약 0.3.0)

대화가 끝나면 학습 피드백을 만듭니다. **요청·응답 형식은 `ai/feedback` 모듈의 계약(0.3.0)을 그대로 따릅니다.**
워커는 그 모듈(`generate_feedback`)을 부르고, 모델 호출만 워커 구성의 `feedback` 역할(재시도·대체 모델 포함)로 합니다.

- 요청 본문: 모듈의 입력(`ai/feedback/schemas/feedback.input.schema.json`) — `schema_version: "0.3.0"`, `request_id`, `scope`, `turns[]`, `context` 등. 계약과 다르면 400 `invalid_request`.
- 응답: `{"feedback": <모듈 출력>, "usage": [...]}`. 모듈 출력 형식은 `ai/feedback/schemas/feedback.output.schema.json`이며 `status`로 나뉩니다.

| `feedback.status` | 뜻 | Spring 처리 |
|---|---|---|
| `completed` | `result`에 요약·의사소통 평가·리뷰 태그·발화별 평가·단어장 후보 | 백엔드 형식으로 변환해 저장 (아래) |
| `insufficient_evidence` | 평가할 확정 학습자 발화가 없음 (모델을 부르지 않음) | 피드백 없이 종료 |
| `failed` | `problem.code`: `AI_TIMEOUT`·`AI_PROVIDER_ERROR`·`INVALID_MODEL_OUTPUT` | `feedback.status: FAILED`, `errorCode`에 그대로 |

- 공급자 실패·출력 형식 오류도 HTTP는 200이고 `feedback.status: failed`입니다(모듈이 판단). 사용량 한도는 Spring이 먼저 확인하고 부릅니다.
- 시간 상한은 `FEEDBACK_TIMEOUT_S`(기본 60초)이며, 길게 생성하는 호출이라 같은 요청 더 보내기(`HEDGE_AFTER_S`)를 쓰지 않습니다.
- 백엔드 CONV-08 형식으로의 변환(예: `summary` ← `result.conversation_summary.text_ko`, `reviews.good`·`improve` ← `result.review_tags`의 `label_ko`, `suggestion` ← `result.summary_suggestion_id`가 가리키는 제안)은 `ai/feedback` 담당과 백엔드가 정합니다.

### 2.7.1 `POST /v1/memory` — NPC 기억 카드

요청: `npc`, `outcome`(대화 결과 한 줄), `history`(최대 200줄, 학습자 줄의 `english`·`rude` 포함), `memory_card`(이전 카드).

응답: `{"memory_card": {"name": "", "claims": [], "events": [], "attitude": "neutral", "struggled": []}, "usage": [...]}`

- `memory_card`: `name`(최대 40자), `claims`·`events`·`struggled`(각 최대 5개, 항목당 100자), `attitude`(`friendly`·`neutral`·`wary`). 연락처(전화번호·이메일)는 지웁니다.
- 공급자 실패는 429/503, 출력이 JSON 객체가 아니면 502 `invalid_output`. **실패하면 이전 카드를 그대로 둡니다.**
- `history`의 `rude`는 판정이 `abusive: true`였던 줄에만 `true`로 보냅니다(`null`은 `false`).

### 2.8 `POST /v1/tts` — 한 문장 합성

요청: `{"text": "...", "voice": "af_heart", "speed": 1.0}`(text 1~400자, speed 0.5~2.0). 응답은 `audio/wav`이고, 헤더 `X-Audio-S`(길이)·`X-Cache`(hit/miss)·`X-Synth-S`(합성 시간)가 붙습니다. 모델이 없으면 503 `tts_unavailable`입니다.

## 3. 사용량 (`usage`)

모델 호출 한 번마다 항목 하나가 생깁니다.

| 칸 | 뜻 |
|---|---|
| `step` | `intent`·`guard`·`translate`·`npc`·`hint`·`suggest`·`meaning`·`feedback`·`memory`·`stt` |
| `model` | **실제로 쓴** 모델 (대체 모델이면 그 이름) |
| `fallback` | 요청한 모델이 아닌 대체 모델을 썼는지 |
| `hedged` | (있을 때만 `true`) 답이 늦어 같은 요청을 하나 더 보냈음. 토큰은 먼저 온 답의 것만 기록되고, 늦은 쪽 사용량은 알 수 없음 (크레딧이 조금 더 나갔을 수 있음) |
| `input_tokens`, `output_tokens` | 공급자가 알려 준 토큰 수 (STT는 없음, 대신 `audio_s`) |
| `latency_s` | 그 호출에 걸린 시간 |
| `first_token_s` | NPC 대답만: 첫 글자까지 |

**알려진 누락:** 시간 상한으로 끊긴 호출, 응답을 받기 전에 실패한 호출은 `usage`에 남지 않을 수 있습니다. 실제 청구는 공급자 사용량 화면과 대조합니다.

## 4. 알려진 한계

- 오류 응답(429·502·503 등)에는 `usage`가 없습니다. 실패 전에 쓴 토큰은 공급자 사용량 화면과 대조합니다.
- 판정은 Spring이 연결을 끊어도 끝까지 실행됩니다(NPC 대답만 멈춤).
- 같은 요청을 두 번 보내면 모델도 두 번 부릅니다. 중복 반영 방지는 Spring이 요청 id로 합니다.
