# AI API 명세 (Spring ↔ AI 워커)

AI 기능 전용 명세입니다. 프론트엔드↔백엔드 API는 [MVP API 명세](mvp-api.md)가 기준이고, 이 문서는 그 문서를 바꾸지 않습니다.
대신 백엔드 API의 각 기능이 어떤 AI API를 쓰는지(0절)와, 백엔드 명세에 없는 AI 쪽 규칙(0.1절)을 여기에 둡니다.

이 문서는 `ai/worker`의 **현재 코드 기준** 계약입니다. 코드와 문서가 다르면 버그로 보고, 둘 중 하나를 고친 뒤 이 문서를 갱신합니다. 요청 형식의 원본은 [schemas.py](../../ai/worker/app/schemas.py)입니다.

- AI 워커는 **상태를 저장하지 않습니다.** 게임 상태·전환 규칙·가격·크레딧·기록은 Spring이 갖고, 필요한 것을 매 요청에 담아 보냅니다.
- AI가 돌려주는 의도는 **제안**입니다. 상태를 바꿀지는 Spring이 현재 상태에서 다시 검증해 정합니다.
- Unity는 AI 워커를 직접 부르지 않습니다. 모든 호출은 Spring을 거칩니다.

## 0. 백엔드 API와의 대응

AI API의 칸 이름은 snake_case이고, 백엔드 API의 camelCase로 바꾸는 것은 Spring이 합니다.

| 백엔드 API | AI API | 대응 |
|---|---|---|
| ONB-08·09 난이도, CONV-01 `levelApplied` | 모든 대화 요청의 `level` | 그대로 넘김 (MVP는 `1`) |
| CONV-01 대화 시작 · 첫 인사 | (백엔드 `[미정]`) | 미리 작성한 인사를 쓰면 AI 호출 없음. AI로 만들면 `/v1/npc/reply`에 인사 지시(`instruction`)와 `suggest_words: true` |
| CONV-01·03 `suggestWords` | `/v1/npc/reply`의 `words` 이벤트 (또는 `/v1/suggest`) | `words` 그대로. 작별 대사 턴은 부르지 않음(`null`) |
| CONV-02 음성 인식 | `POST /v1/stt` | `sttRawText` ← `text`, `durationMs` ← `audio_s`×1000. `text: ""`이면 `STT_NO_SPEECH`. 413 `audio_too_large` → `AUDIO_TOO_LARGE`, 415 `audio_format_unsupported` → `AUDIO_FORMAT_UNSUPPORTED` |
| CONV-03 발화 전송 | `POST /v1/judge`와 `POST /v1/guard`를 동시에 → (Spring 규칙) → `POST /v1/npc/reply` | 판정의 `intent.ids`를 Spring 규칙이 검증해 상태·미션을 확정(`ai_candidates`). 무례 판정은 `/v1/guard`(2.2.1절). `npcTurn.text` ← `done.text` |
| CONV-03 `npcTurn.speech` | `done.audio_full_id` → `GET /v1/audio/{id}` | 턴 전체 음성 하나를 받아 저장(`npc_audio_key`). `null`이면 `speech.status: FAILED` |
| CONV-04 뜻 보기 | `POST /v1/meaning` | `meaningKo` ← `meaning_ko`, `glossary[].meaningKo` ← `glossary[].meaning_ko` |
| CONV-05 힌트 | `POST /v1/hint` | `hintText` ← `say_this[0].en` (`say_this[0].ko`·`keywords`는 3단계 힌트용으로 남음) |
| CONV-06 다시 듣기 | (없음) | CONV-03에서 저장한 턴 전체 음성 |
| CONV-07 NPC 음성 다시 만들기 | `POST /v1/tts` | `text` ← 대사 전문, `voice` ← `npc.tts_voice`, `speed` ← `npc.tts_speed` × 난이도 배율(1.1절) |
| CONV-08 대화 종료 피드백 | `POST /v1/feedback` (+ `POST /v1/memory`) | `ai/feedback` 계약 0.3.0. CONV-08 형식 변환 위치는 `ai/feedback` 담당과 정함 |
| FB-04 모범 음성 | `POST /v1/tts` | |
| 1.5 오류 응답 | 워커 오류 코드 | `provider_limit` → `AI_RATE_LIMITED`, `provider_unavailable`·`timeout` → `AI_TEMPORARILY_UNAVAILABLE`, `invalid_output` → `AI_RESPONSE_INVALID` |

## 0.1 AI 쪽 규칙 (백엔드 명세에 없는 것)

- **무례 판정**: `/v1/guard`의 `guard.abusive`가 `true`면 무례, `false`면 정상, **`null`이면 판정 못 함**(무례로 세지 않음, 2.2.1절). 무례를 몇 번에 어떻게 제재할지는 Spring 규칙이 정합니다. 누적·퇴장 규칙과 NPC 지시문은 [무례 처리 합의안](rudeness-handling.md)에 있습니다. NPC가 먼저 잘못한 장면의 맞대응(정당방위)은 MVP 장소에 그런 장면이 없어 MVP에서 제외합니다(합의안 4절).
- **번역 카드**: 학습자 말에 한글이 섞이면 `/v1/judge`가 `translation.english`를 줍니다. 화면에는 `english`가 있고 `/v1/guard`의 `abusive`가 `false`(무례가 아님이 확정)일 때만 보여 줍니다. 무례하거나 판정 못 한(`null`) 말은 보여 주지 않습니다(욕을 영어로 가르치지 않도록).
- **행동 묘사**: `*...*`는 지우고 말만 판정합니다. 행동 묘사만 있으면 `dropped: true`이고 턴으로 세지 않습니다.
- **난이도**: 1.1절 `level` 표. 판정에는 영향이 없습니다.
- **한 턴 구조**: NPC 한 턴은 `turnId` 하나, 대답 한 줄(난이도에 따라 1~3문장)입니다. 문장으로 쪼개 저장하지 않습니다(뜻 보기·힌트·추천 단어가 턴 단위).
  - 음성은 두 가지입니다. 문장 조각(`audio` 이벤트)은 바로 재생용이고, 다시 듣기용은 `done`의 턴 전체 음성(`audio_full_id`) 하나입니다.
  - 추천 단어는 `suggest_words: true`면 대답이 끝난 뒤 남은 음성 합성과 동시에 만들어 `words` 이벤트로 옵니다. Spring이 따로 부르지 않아도 됩니다.
  - 전송: Spring이 `done`까지 받은 뒤 한 번에 응답(REST)해도 되고, `delta`·`audio`·`words`를 Unity로 바로 중계(스트리밍)해도 됩니다. 워커 계약은 같습니다. 첫 음성까지의 시간은 스트리밍이 짧습니다.

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
| `audio_too_large` | 413 | 녹음이 3MB를 넘음 (`/v1/stt`) | `AUDIO_TOO_LARGE` |
| `audio_format_unsupported` | 415 | 지원하지 않는 녹음 형식 (`/v1/stt`) | `AUDIO_FORMAT_UNSUPPORTED` |
| `provider_limit` | 429 | 공급자 한도 (대체 모델까지 소진) | 잠시 뒤 재시도 안내 |
| `provider_unavailable` | 503 | 공급자 오류·응답 없음 (대체 모델까지 실패), 공급자 응답 형식이 다름(JSON 아님·`choices` 없음, 스트림 data 줄이 깨짐) | 재시도 안내 |
| `invalid_output` | 502 | 모델 출력이 정해진 형식이 아님 (힌트 등) | 응답 형식 오류로 안내 (`AI_RESPONSE_INVALID`) |
| `tts_unavailable` | 503 | 음성 합성 모델 없음 | 자막만 |
| `not_found` | 404 | 음성 조각 없음·보관 시간 지남, 없는 주소 | 음성이면 자막만 |
| `method_not_allowed` | 405 | 주소는 있지만 HTTP 메서드가 다름 | 호출 코드 오류 |
| `internal_error` | 500 | 워커 프로그램 오류 (원인은 워커 로그) | 재시도 안내, 워커 로그 확인 |

**부분 실패** — `/v1/guard`는 판정이 실패해도 **HTTP 200**에 `abusive: null`로 돌려줍니다(2.2.1절). `/v1/judge`는 일부가 실패해도 **HTTP 200**으로 돌려주고 실패한 칸에 오류를 적습니다(2.2절). `/v1/feedback`은 실패도 200 안의 `feedback.status: failed`로 알립니다(2.7절). NPC 대답은 SSE `error` 이벤트로 알립니다(2.3절).

**시간 상한** (워커 환경 변수, 재시도·대체 모델 포함)

| 변수 | 기본 | 적용 |
|---|---|---|
| `CALL_TIMEOUT_S` | 8초 | 모델 호출 **한 번**. 넘으면 끊고 재시도·대체 모델 |
| `HEDGE_AFTER_S` | 3초 | 한 번에 받는 호출이 이만큼 답이 없으면 같은 요청을 하나 더 보내 먼저 온 답을 씀 (NPC 스트림은 제외). 공급자가 혼잡할 때 응답 없이 멈춘 호출을 기다리지 않기 위함. 평소 응답(gms-luna 판정 p99 약 2.5~3.9초)보다 길게 잡아 평소에는 거의 더 보내지 않음 |
| `STREAM_HEDGE_AFTER_S` | 2.5초 | NPC 대답 스트림의 **첫 글자**가 이만큼 안 오면 같은 요청 스트림을 하나 더 열어 먼저 글자를 낸 쪽을 씀(진 쪽은 닫음). GMS 스트리밍이 가끔 첫 글자를 10초 넘게 늦게 보내는 것 대비. 평소 첫 글자는 1.7초 안 |
| `WORDS_BUDGET_S` | 3초 | NPC 대답 스트림 안 추천 단어(`words`). 넘으면 `words: null`, `error: "timeout"`으로 넘기고 `done`을 보냄 |
| `JUDGE_BUDGET_S` | 5초 | `/v1/judge`의 의도·번역 **각각**. 넘으면 그 칸만 `timeout` 오류 |
| `GUARD_BUDGET_S` | 5초 | `/v1/guard` 무례 판정 (로컬 판정기 + LLM). 넘으면 `abusive: null`, `error_code: timeout` |
| `REPLY_FIRST_BUDGET_S` | 5초 | NPC 대답 **첫 글자까지**. 넘으면 SSE `error`(`timeout`). 말하기 시작한 대답은 끊지 않음 |
| `STT_FIRST_TIMEOUT_S` | 8초 | 음성 인식 뒤에 다른 공급자가 있을 때 첫 공급자 호출. 넘으면 다음 공급자로. 마지막 공급자와 공급자가 하나뿐일 때는 30초 |
| `STT_NEXT_TIMEOUT_S` | 8초 | 음성 인식 공급자가 셋 이상일 때 중간 공급자 호출 |

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

### 1.1 대화 난이도 `level`

`level` — 대화 난이도(백엔드 `levelApplied`). `1`·`2`·`3` 중 하나이고 기본은 `2`입니다. MVP는 `1`만 보냅니다. 판정(`/v1/judge`)에는 영향이 없습니다.

| 단계 | NPC 대답 | 말 속도 배율 | 힌트·추천 단어·단어 풀이 수준 |
|---|---|---|---|
| `1` | 초보자에게 말하듯 천천히, 쉬운 일상 단어. 한 문장(많아야 두 문장), 문장마다 14단어 미만, 속어·관용구 없음 | ×0.85 | 아주 쉽고 짧게 (CEFR A1-A2) |
| `2` | 자연스러운 일상 미국 영어(줄임말·흔한 관용구). 한두 문장, 모두 25단어 미만 | ×1.0 | 쉽고 자연스럽게 (CEFR A2-B1) |
| `3` | 원래 말투 그대로(속어·관용구·구동사). 두세 문장, 45단어 미만 | ×1.06 | 자연스럽게, 흔한 관용구 가능 (CEFR B1-B2) |

## 2. 엔드포인트

### 2.1 `GET /health` (토큰 불필요)

```json
{"ok": true, "tts": true, "tts_phoneme_timing": true, "profile": "groq", "models": {"intent": "...", "guard": "...", "npc": "...", "stt": "..."}, "guard_local": null}
```

`tts_phoneme_timing`은 워밍업에서 음성과 일치하는 발음 시간표까지 검증했는지 나타냅니다. 음성 엔진 유무인 `tts`와 구분합니다. 배포 이미지의 `TTS_REQUIRE_TIMING=true`에서는 시간표를 만들 수 없으면 시작에 실패합니다.

`profile`은 수집 기록의 `profile` 칸에 그대로 남깁니다. `guard_local`은 로컬 무례 판정기를 켰을 때만 `{"model": "v2-mix", "t_lo": 0.05, "t_hi": 0.95}`이고, 끄면(기본) `null`입니다.

### 2.2 `POST /v1/judge` — 의도·번역

학습자 한 줄을 받아 의도와 번역(한글이 있을 때만)을 **동시에** 돌립니다. 무례 판정은 하지 않습니다. 같은 줄로 `/v1/guard`(2.2.1절)를 동시에 불러 주세요.

요청: `npc`, `scene`(비면 `npc.scene`), `state`(지금 상태: `id`, `desc`, `intents`, `slots`, `current`, `more_intents`), `history`(최대 40줄), `text`(최대 500자, 공백만이면 400 `invalid_request`).

응답:

```json
{
  "text": "one latte please",
  "dropped": false,
  "notice": null,
  "intent": {"id": "order_items", "ids": ["order_items"], "slots": {"items": [{"name": "latte", "iced": false, "qty": 1}], "choice": "none"}, "error": null},
  "translation": null,
  "usage": [{"step": "intent", "model": "...", "input_tokens": 512, "output_tokens": 20, "latency_s": 0.41, "fallback": false}]
}
```

| 칸 | 규칙 |
|---|---|
| `text` | 행동 묘사(`*...*`)를 지운 말. 판정·대답·기록에는 이것을 씀 |
| `dropped` | `true`면 행동 묘사만 있었던 것. 모델을 부르지 않았고 `intent`·`translation`은 `null`. **턴으로 세지 않고** `notice`를 보여 줌 |
| `notice` | 학습자에게 보여 줄 한국어 안내 (행동 묘사를 지웠을 때) |
| `intent.ids` | 말한 순서대로 최대 3개. Spring이 **하나씩 현재 상태에서 다시 검증**해 적용. `id`는 `ids[0]` |
| `intent.slots` | 요청 `state.slots`에 있는 칸만. 상품은 `catalog` 안의 이름만, `iced`는 `iced_ok`에 있을 때만 |
| `intent.error` | 의도 판정 실패 시 메시지, 이때 `id`는 `unclear`. **상태·금액을 바꾸지 말고 다시 물을 것** |
| `translation` | 한글이 섞였을 때만 `{"english", "error"}`. 화면에 보여 줄지는 `/v1/guard` 결과로 Spring이 정함(0.1절 번역 카드) |

### 2.2.1 `POST /v1/guard` — 무례 판정만

학습자 한 줄의 무례 여부를 판정합니다. 한 턴에서 `/v1/judge`와 같은 `text`·`history`로 **동시에** 부릅니다.

요청: `npc`, `history`(최대 40줄, 판정은 마지막 4줄·로컬 판정기는 마지막 NPC 줄만 봄), `text`(최대 500자, 공백만이면 400 `invalid_request`).

응답:

```json
{"text": "you idiot, hurry up", "dropped": false, "guard": {"abusive": true, "error": null, "error_code": null}, "usage": [{"step": "guard", "model": "...", "input_tokens": 230, "output_tokens": 1, "latency_s": 0.9, "fallback": false}]}
```

| 칸 | 규칙 |
|---|---|
| `text` | 행동 묘사(`*...*`)를 지운 말. `/v1/judge`의 `text`와 같은 값 |
| `dropped` | `true`면 행동 묘사만 있었던 것. 모델을 부르지 않고 `guard: null`, `usage: []` |
| `guard` | 아래 표. 판정 실패·시간 초과(`GUARD_BUDGET_S`)도 **HTTP 200**에 `abusive: null`과 `error_code` |

**`guard` — 무례 판정**

| `abusive` | `error_code` | 뜻 | Spring 처리 |
|---|---|---|---|
| `true` | `null` | 무례 | 무례 누적 |
| `false` | `null` | 무례 아님 | — |
| `null` | `empty` | 판정 모델이 빈 답을 냄 | **무례로 세지 않음.** 대화 계속. 기록에 남김 |
| `null` | `unparsed` | yes/no가 아닌 답 (거절 등) | 위와 같음. 심한 말에 모델이 거절하는 경우가 있어 건수를 따로 봄 |
| `null` | `timeout` | `GUARD_BUDGET_S` 초과 | 위와 같음 |
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

요청: `npc`, `memory`(선택: `visits`, `last_order`, `card`), `history`(최대 40줄), `text`(학습자 원문, 최대 500자), `instruction`(Spring 규칙이 정한 이번 대답에서 할 일, 최대 1000자), `tts`(기본 `true`), `level`(기본 `2`), `suggest_words`(기본 `false`, 추천 단어를 스트림으로), `goal`(추천 단어용: 지금 상태에서 학습자가 할 일, 최대 300자). **한 턴의 대답은 난이도에 따라 한 문장에서 세 문장까지**이며 한 줄로 옵니다.

응답은 `text/event-stream`이며 이벤트 순서는 다음과 같습니다.

```
delta*  →  audio*  →  words?  →  done | error
```

`delta`와 `audio`는 섞여서 옵니다. `words`는 `suggest_words: true`이고 대답이 성공했을 때만 `done` 바로 앞에 한 번 옵니다. 마지막은 항상 `done` 또는 `error` 중 하나입니다.

| 이벤트 | data | 뜻 |
|---|---|---|
| `delta` | `{"text": "Got"}` | 글자 조각. 받는 대로 자막에 붙임 |
| `audio` | `{"seq": 0, "text": "Got it, one latte for you.", "audio_id": "9f3a...", "duration_s": 1.8, "tts": true}` | 읽을 조각. `seq` 순서대로 재생. **WAV가 아니라 `audio_id`** — `GET /v1/audio/{audio_id}`로 받음. `tts: false`면 `audio_id: null`(합성 실패·늦음) → 그 조각은 자막만 |
| `words` | `{"words": ["for here", "to go", "please"], "error": null}` | 추천 단어 (`/v1/suggest`와 같은 규칙). 실패하면 `words: null`, `error`에 `provider_unavailable`·`provider_limit`·`timeout`(`WORDS_BUDGET_S` 초과)·`invalid_output`·`internal_error`. 대답은 그대로 성공. **대답이 `error`로 끝나면 `words`는 오지 않음** |
| `done` | `{"text": "...", "first_token_s": 0.62, "total_s": 2.1, "audio_full_id": "7c1e...", "audio_full_s": 3.4, "usage": [...]}` | **생성 완료**. 클라이언트 재생 완료가 아님. `text`가 대답 전문. `audio_full_id`는 모든 조각을 순서대로 이어 붙인 **턴 전체 음성**(다시 듣기용, `GET /v1/audio/{id}`). 조각 하나라도 합성하지 못했거나 `tts: false`면 `null` → `/v1/tts`로 다시 만듦. `usage`에 추천 단어(`suggest`)도 포함 |
| `error` | `{"code": "provider_unavailable", "message": "...", "usage": [...]}` | 생성 실패. 이미 보낸 `delta`·`audio`는 유효. `code`는 `provider_unavailable`·`provider_limit`·`timeout` |

- 대답은 한 줄입니다. 모델이 줄을 바꿔 다른 사람 대사를 쓰면 버립니다.
- 빈 대답이면 `"Sorry, could you say that again?"`로 대신합니다(`done`).
- Spring이 연결을 끊으면 워커도 모델 호출을 멈춥니다.
- 조각 하나의 합성이 `TTS_CHUNK_BUDGET_S`(기본 2.5초)를 넘으면 그 조각은 `tts: false`입니다.
- 턴 전체 음성도 워커 메모리에 보관 시간(600초) 동안만 있으니, Spring은 `done`을 받은 뒤 바로 받아 저장합니다.

### 2.4 `GET /v1/audio/{audio_id}`

`audio/wav`를 돌려줍니다. 보관 시간은 600초(설정 `audio_ttl_s`, 환경 변수로는 아직 못 바꿈)이며 **워커 프로세스 메모리**에 있습니다. 전체 크기가 `AUDIO_MAX_MB`(기본 200MB)를 넘으면 오래된 것부터 버립니다. 워커를 여러 대 띄우면 같은 대답의 음성은 그 대답을 만든 워커에서 받아야 합니다. 없으면 404 `not_found`입니다.

Unity가 음성을 받을 때도 Spring이 인증한 뒤 이 주소에서 받아 중계합니다.

### 2.5 `POST /v1/stt` — 음성 인식 (multipart)

| 칸 | 내용 |
|---|---|
| `file` | 녹음. 파일 부분의 `Content-Type`이 `audio/webm`·`audio/ogg`·`audio/mp4`·`audio/wav`·`audio/x-wav`·`audio/mpeg` 중 하나여야 합니다(`;codecs=...` 같은 매개변수는 붙어도 됨). 그 밖의 형식이나 `Content-Type`이 없으면(`application/octet-stream` 포함) 415 `audio_format_unsupported`, 3MB를 넘으면 413 `audio_too_large`, 비면 400 `invalid_request`입니다. 형식이 맞지 않으면 STT 공급자를 부르지 않습니다 |
| `scene_words` | 장면 단어(메뉴·상품 이름). JSON 배열 또는 쉼표로 구분한 글. 최대 30개 사용 |

응답: `{"text": "...", "latency_s": 0.41, "audio_s": 2.9, "usage": [{"step": "stt", "model": "...", "audio_s": 2.9, "latency_s": 0.41, "fallback": false}]}`

- 무음일 때 Whisper가 프롬프트를 되풀이한 결과는 `text: ""`로 거릅니다. 빈 결과면 다시 말하기를 안내합니다.
- GMS 구성(기본 `gms-luna`)은 Groq Whisper(`whisper-large-v3-turbo`)로 인식하고, Groq가 한도(429)·서버 오류·키 오류·응답 없음이면 GMS `whisper-1`로 한 번 더 인식합니다. 이때 `usage`의 `model`은 `whisper-1`, `fallback: true`입니다. 녹음 자체를 공급자가 받지 않은 경우(400·413·415·422)는 넘기지 않습니다. 대체 쪽도 실패하면 429/503이고, 메시지에 두 오류가 함께 적힙니다.
- 대체가 있으면 Groq 호출은 `STT_FIRST_TIMEOUT_S`(8초) 안에 답이 없을 때 넘깁니다. 응답의 `latency_s`는 실패한 첫 호출까지 포함한 전체 시간입니다. GMS `whisper-1`은 Groq보다 느립니다(6.5초 음성 기준 약 1.9초, Groq 약 0.4초).
- 워커 환경 변수 `STT_CHAIN`으로 공급자 순서를 정할 수 있습니다(예: SSAFY GPU 서버의 자체 학습 모델 `gpu:turbo-lr1e4` → GMS `whisper-1` → Groq). 앞 공급자가 한도·오류·응답 없음이면 다음으로 넘기고, 녹음 자체 오류는 넘기지 않습니다. `usage`의 `model`은 실제로 인식한 모델이고, 첫 공급자가 아니면 `fallback: true`입니다. 모두 실패하면 마지막 공급자의 오류 코드(429/503)이고 메시지에 공급자별 오류가 함께 적힙니다.
- 같은 공급자가 연속으로 `STT_BREAKER_FAILURES`(3)번 실패하면 `STT_BREAKER_SKIP_S`(60초) 동안 건너뛰고 다음 공급자로 바로 보냅니다. 마지막 공급자는 건너뛰지 않습니다. 응답 형식은 같습니다.
- 음성은 음성 인식 공급자(Groq, SSAFY GMS를 거친 OpenAI, `STT_CHAIN`에 `gpu`가 있으면 SSAFY GPU 서버)로 전송됩니다. 사용자 동의 화면에 이 점을 알려야 합니다.

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
- 시간 상한은 `FEEDBACK_TIMEOUT_S`(기본 60초)이며 **재시도·대체 모델을 포함한 전체 시간**입니다. 넘으면 `feedback.status: failed`, `problem.code: AI_TIMEOUT`. 길게 생성하는 호출이라 같은 요청 더 보내기(`HEDGE_AFTER_S`)를 쓰지 않습니다.
- 백엔드 CONV-08 형식으로의 변환(예: `summary` ← `result.conversation_summary.text_ko`, `reviews.good`·`improve` ← `result.review_tags`의 `label_ko`, `suggestion` ← `result.summary_suggestion_id`가 가리키는 제안)은 `ai/feedback` 담당과 백엔드가 정합니다.

### 2.7.1 `POST /v1/memory` — NPC 기억 카드

요청: `npc`, `outcome`(대화 결과 한 줄), `history`(최대 200줄, 학습자 줄의 `english`·`rude` 포함), `memory_card`(이전 카드).

응답: `{"memory_card": {"name": "", "claims": [], "events": [], "attitude": "neutral", "struggled": []}, "usage": [...]}`

- `memory_card`: `name`(최대 40자), `claims`·`events`·`struggled`(각 최대 5개, 항목당 100자), `attitude`(`friendly`·`neutral`·`wary`). 연락처(전화번호·이메일)는 지웁니다.
- 공급자 실패는 429/503, 출력이 JSON 객체가 아니면 502 `invalid_output`. **실패하면 이전 카드를 그대로 둡니다.**
- 카드의 다섯 필드는 모두 필요합니다. 누락·추가 필드, 잘못된 자료형·문자열이 아닌 배열 원소, 중복 JSON 키·비표준 상수도 502 `invalid_output`입니다. 연락처는 길이 제한 적용 전에 검사합니다.
- 생성 지연만으로 같은 기억 요청을 병렬 추가 전송하지 않습니다(`hedge=False`). 공급자 실패의 기존 재시도·대체 모델 처리는 유지됩니다.
- 성공 카드는 전체 교체 결과이며 이전 목록을 합쳐 철회 내용을 복원하지 않습니다. 다음 방문의 `memory.card`로 전달하는 방법은 [기억 카드 연동](npc-memory-integration.md)을 참고합니다.
- `history`의 `rude`는 판정이 `abusive: true`였던 줄에만 `true`로 보냅니다(`null`은 `false`).
- `rude: true` 사용자 발화는 원문과 생성 번역 대신 `[RUDE: content withheld]` 표식으로 전달합니다. 확정 사건은 원문 인용 없이 요약할 수 있으나, 위협·횟수·제재·종료를 추측하지 않습니다. 다른 발화에 무례 표식이 없다는 것만으로 정상 판정을 확정하지 않습니다.
- 무례 원문을 이름·프로필·선호·학습할 표현으로 저장하지 않는 프롬프트 규칙을 적용합니다. 출력의 의미 정확도까지 결정론적으로 보장하는 필터는 아니며 실제 모델 품질 검토가 별도로 필요합니다.

### 2.8 `POST /v1/tts` — 한 문장 합성

요청: `{"text": "...", "voice": "af_heart", "speed": 1.0}`(text 1~400자, speed 0.5~2.0). 응답은 `audio/wav`이고, 헤더 `X-Audio-S`(길이)·`X-Cache`(hit/miss)·`X-Synth-S`(합성 시간)가 붙습니다. 모델이 없으면 503 `tts_unavailable`입니다.

## 3. 사용량 (`usage`)

모델 호출 한 번마다 항목 하나가 생깁니다.

| 칸 | 뜻 |
|---|---|
| `step` | `intent`·`guard`·`translate`·`npc`·`hint`·`suggest`·`meaning`·`feedback`·`memory`·`stt` |
| `model` | **실제로 쓴** 모델 (대체 모델이면 그 이름) |
| `fallback` | 요청한 모델이 아닌 대체 모델을 썼는지 |
| `hedged` | (있을 때만 `true`) 답이 늦어 같은 요청을 하나 더 보냈음(NPC 대답은 첫 글자 기준). 토큰은 먼저 온 답의 것만 기록되고, 늦은 쪽 사용량은 알 수 없음 (크레딧이 조금 더 나갔을 수 있음) |
| `input_tokens`, `output_tokens` | 공급자가 알려 준 토큰 수 (STT는 없음, 대신 `audio_s`) |
| `latency_s` | 그 호출에 걸린 시간 |
| `first_token_s` | NPC 대답만: 첫 글자까지 |

**알려진 누락:** 시간 상한으로 끊긴 호출, 응답을 받기 전에 실패한 호출은 `usage`에 남지 않을 수 있습니다. 실제 청구는 공급자 사용량 화면과 대조합니다.

## 4. 알려진 한계

- 오류 응답(429·502·503 등)에는 `usage`가 없습니다. 실패 전에 쓴 토큰은 공급자 사용량 화면과 대조합니다.
- 판정은 Spring이 연결을 끊어도 끝까지 실행됩니다(NPC 대답만 멈춤).
- 같은 요청을 두 번 보내면 모델도 두 번 부릅니다. 중복 반영 방지는 Spring이 요청 id로 합니다.
