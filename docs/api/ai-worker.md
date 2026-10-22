# AI 워커 API 계약 (Spring ↔ AI 워커)

이 문서는 `ai/worker`의 **현재 코드 기준** 계약입니다(2026-10-20). 코드와 문서가 다르면 버그로 보고, 둘 중 하나를 고친 뒤 이 문서를 갱신합니다. 요청 형식의 원본은 [schemas.py](../../ai/worker/app/schemas.py)입니다.

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
| `tts_unavailable` | 503 | 음성 합성 모델 없음 | 자막만 |
| `not_found` | 404 | 음성 조각 없음·보관 시간 지남 | 자막만 |

**부분 실패** — `/v1/judge`와 `/v1/conversation/end`는 일부가 실패해도 **HTTP 200**으로 돌려주고, 실패한 칸에 오류를 적습니다(2.2, 2.6절). NPC 대답은 SSE `error` 이벤트로 알립니다(2.3절).

**시간 상한** (워커 환경 변수, 재시도·대체 모델 포함)

| 변수 | 기본 | 적용 |
|---|---|---|
| `CALL_TIMEOUT_S` | 8초 | 모델 호출 **한 번**. 넘으면 끊고 재시도·대체 모델 |
| `JUDGE_BUDGET_S` | 5초 | 판정의 의도·무례·번역 **각각**. 넘으면 그 칸만 `timeout` 오류 |
| `REPLY_FIRST_BUDGET_S` | 5초 | NPC 대답 **첫 글자까지**. 넘으면 SSE `error`(`timeout`). 말하기 시작한 대답은 끊지 않음 |

Spring 쪽의 턴 전체 제한(STT + 판정 + 대답)은 Spring이 정합니다. 워커 상한보다 길게 잡아야 워커의 오류 응답을 받을 수 있습니다.

## 2. 엔드포인트

### 2.1 `GET /health` (토큰 불필요)

```json
{"ok": true, "tts": true, "profile": "groq", "models": {"intent": "...", "guard": "...", "npc": "...", "stt": "..."}}
```

`profile`은 수집 기록의 `profile` 칸에 그대로 남깁니다.

### 2.2 `POST /v1/judge` — 의도·무례·번역

학습자 한 줄을 받아 의도·무례 판정·번역(한글이 있을 때만)을 **동시에** 돌립니다.

요청: `npc`, `scene`(비면 `npc.scene`), `state`(지금 상태: `id`, `desc`, `intents`, `slots`, `current`, `more_intents`), `history`(최대 40줄), `text`(최대 500자).

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
| `intent.ids` | 말한 순서대로 최대 3개. Spring이 **하나씩 현재 상태에서 다시 검증**해 적용. `id`는 `ids[0]`(예전 형식 호환) |
| `intent.slots` | 요청 `state.slots`에 있는 칸만. 상품은 `catalog` 안의 이름만, `iced`는 `iced_ok`에 있을 때만 |
| `intent.error` | 의도 판정 실패 시 메시지, 이때 `id`는 `unclear`. **상태·금액을 바꾸지 말고 다시 물을 것** |
| `translation` | 한글이 섞였을 때만 `{"english", "show", "error"}`. `show`는 무례가 아님이 **확정**(`abusive: false`)일 때만 `true` |

**`guard` — 무례 판정 (2026-10-20 변경)**

| `abusive` | `error_code` | 뜻 | Spring 처리 |
|---|---|---|---|
| `true` | `null` | 무례 | 무례 누적 |
| `false` | `null` | 무례 아님 | — |
| `null` | `empty` | 판정 모델이 빈 답을 냄 | **무례로 세지 않음.** 대화 계속. 기록에 남김 |
| `null` | `unparsed` | yes/no가 아닌 답 (거절 등) | 위와 같음. 심한 말에 모델이 거절하는 경우가 있어 건수를 따로 봄 |
| `null` | `timeout` | `JUDGE_BUDGET_S` 초과 | 위와 같음 |
| `null` | `provider_unavailable` / `provider_limit` | 공급자 실패 | 위와 같음 |

예전에는 빈 답·거절을 `true`, 공급자 실패를 `false`로 돌려줬습니다. 이제는 둘 다 `null`입니다. **`null`을 `false`처럼 다루지 마세요.** 수집 기록에는 `guard: null`과 `guard_error`로 남깁니다.

이진 판정이라 위협·성희롱 같은 **세부 유형은 구분하지 않습니다.** 장면별 예외(라벨 가이드 5절의 정당방위)는 유형을 돌려주는 계약이 생기기 전까지 자동 제재에서 빼 주세요.

### 2.3 `POST /v1/npc/reply` — NPC 대답 (SSE)

요청: `npc`, `memory`(선택: `visits`, `last_order`, `card`), `history`, `text`(학습자 원문), `instruction`(Spring 규칙이 정한 이번 대답에서 할 일, 최대 1000자), `tts`(기본 `true`).

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
- 음성은 외부 음성 인식 서비스(지금 Groq)로 전송됩니다(동의 화면 문구, [수집 계약](../ai-data-collection.md) 4절).

### 2.6 `POST /v1/hint`

요청: `npc`, `scene`, `goal`, `history`. 응답: `{"say_this": [최대 2], "keywords": [최대 3], "usage": [...]}`. 실패하면 429/503입니다.

### 2.7 `POST /v1/conversation/end` — 피드백과 기억 카드

요청: `npc`, `scenario_title`, `goal_reached`, `outcome`, `history`(최대 200줄, 학습자 줄의 `english`·`rude` 포함), `memory_card`(이전 카드).

응답:

```json
{"feedback": {"good": "...", "improve": {"line": 3, "better": "...", "why": "..."}, "lines": [...]},
 "memory_card": {"name": "", "claims": [], "events": [], "attitude": "neutral", "struggled": []},
 "errors": {"feedback": null, "memory": null}, "usage": [...]}
```

하나가 실패하면 그 칸만 `null`이고 `errors`에 이유가 들어갑니다. 기억 카드가 `null`이면 **이전 카드를 그대로 둡니다.** 기억 카드에 연락처는 저장하지 않습니다.

`history`의 `rude`는 판정이 `abusive: true`였던 줄에만 `true`로 보냅니다(`null`은 `false`).

### 2.8 `POST /v1/tts` — 한 문장 합성

요청: `{"text": "...", "voice": "af_heart", "speed": 1.0}`(text 1~400자, speed 0.5~2.0). 응답은 `audio/wav`이고, 헤더 `X-Audio-S`(길이)·`X-Cache`(hit/miss)·`X-Synth-S`(합성 시간)가 붙습니다. 모델이 없으면 503 `tts_unavailable`입니다.

## 3. 사용량 (`usage`)

모델 호출 한 번마다 항목 하나가 생깁니다.

| 칸 | 뜻 |
|---|---|
| `step` | `intent`·`guard`·`translate`·`npc`·`hint`·`feedback`·`memory`·`stt` |
| `model` | **실제로 쓴** 모델 (대체 모델이면 그 이름) |
| `fallback` | 요청한 모델이 아닌 대체 모델을 썼는지 |
| `input_tokens`, `output_tokens` | 공급자가 알려 준 토큰 수 (STT는 없음, 대신 `audio_s`) |
| `latency_s` | 그 호출에 걸린 시간 |
| `first_token_s` | NPC 대답만: 첫 글자까지 |

**알려진 누락:** 시간 상한으로 끊긴 호출, 응답을 받기 전에 실패한 호출은 `usage`에 남지 않을 수 있습니다. 실제 청구는 공급자 사용량 화면과 대조합니다.

## 4. 알려진 한계

- 힌트의 모델 출력이 JSON으로 읽히지 않으면 현재 500을 돌려줍니다(형식 강제 모드라 드묾).
- 판정은 Spring이 연결을 끊어도 끝까지 실행됩니다(NPC 대답만 멈춤).
- 같은 요청을 두 번 보내면 모델도 두 번 부릅니다. 중복 반영 방지는 Spring이 요청 id로 합니다.

## 5. 변경 이력

| 날짜 | 변경 |
|---|---|
| 2026-10-20 | 무례 판정 실패를 `abusive: null` + `error_code`로 분리. 판정·대답 첫 글자 시간 상한(`JUDGE_BUDGET_S`, `REPLY_FIRST_BUDGET_S`)과 `timeout` 코드 추가. 이 문서를 저장소에 둠 (이전: Claude Docs 링크) |
