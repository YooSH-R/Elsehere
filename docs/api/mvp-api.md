# MVP API 명세 초안

MVP 와이어프레임(화면 01~19)의 화면 요소와 사용자 동작을 서버 API로 나눈 초안입니다. 데이터 구조는 [ERD v3.3](../erd.dbml), 수용 조건은 [요구사항명세서](../A305_기획안/04_AI용_요구사항명세서.md)를 따릅니다. 이 문서는 초안이며 팀 리뷰를 거쳐 확정합니다.

- `[미정]`은 팀 결정이 필요한 항목, `[제안]`은 작성자 제안입니다.
- MVP에서 뺀 기능은 7절에 모았습니다. 와이어프레임에 화면이 없는 기능(설정, 프로필 수정, NPC 호감도 조회)도 API를 만들지 않았습니다.
- 대화 턴은 REST로 작성했습니다. 스트리밍을 선택하면 요청·응답의 의미와 식별자는 유지하고 전송 방식만 바꿉니다.

## 1. 공통 규칙

### 1.1 형식

- 모든 API는 `/api/v1` 아래에 있으며, 이 문서의 경로는 `/api/v1`을 포함한 전체 경로로 적습니다.
- 요청·응답 본문은 UTF-8 JSON이며 필드명은 camelCase입니다. DB 컬럼(snake_case)과의 변환은 백엔드 DTO에서 합니다.
- 시각은 시간대를 포함한 ISO 8601(`2026-10-21T14:00:00+09:00`), 날짜는 `YYYY-MM-DD`입니다.
- 장면·NPC·사물 같은 마스터 데이터는 환경마다 달라지지 않는 코드로 가리킵니다: `sceneCode`(`scenes.code`), `npcCode`(`npcs.code`), `objectKey`(`world_objects.unity_key`). 사용자 데이터는 서버가 발급한 숫자 ID로 가리킵니다.
- 목록은 커서 방식으로 나눕니다. 요청은 `?cursor=&size=`, 응답은 `{ "items": [...], "nextCursor": "..." }`이며 마지막 페이지의 `nextCursor`는 `null`입니다.

### 1.2 인증

- 소셜 로그인으로 사용자를 식별합니다(`users.provider`, `users.provider_uid`). 제공자는 Google 하나이며, 경로의 `{provider}`는 `google`입니다.
- 로그인 상태는 서버 세션에 두고 HttpOnly 세션 쿠키로 유지합니다. nginx로 Unity Web 빌드와 API를 같은 출처에서 제공하며, 세션 쿠키는 `SameSite=Lax`입니다.
- 요청 위조를 막기 위해 상태를 바꾸는 요청(POST·PUT·PATCH·DELETE)에는 `X-Requested-With` 헤더를 붙입니다. 값은 자유이며, 헤더가 없으면 `403 CSRF_HEADER_REQUIRED`입니다.
- 개발 환경(local 프로필)에서는 소셜 로그인 없이 `POST /api/v1/dev/login`에 `{ "userId": 1 }`을 보내 그 사용자로 로그인할 수 있습니다. 다른 환경에는 이 API가 없습니다.
- 처음 로그인한 사용자는 가입 정보(닉네임, 캐릭터, 약관)를 제출해야 사용자 계정이 만들어집니다. 그 전까지는 가입 관련 API와 BOOT-01만 호출할 수 있고, 나머지는 `403 SIGNUP_REQUIRED`입니다.
- 인증이 필요한 API를 인증 없이 호출하면 `401 UNAUTHENTICATED`입니다.
- 서버는 모든 조회·변경에서 요청한 사용자의 데이터인지 검사합니다. 다른 사용자의 데이터는 존재 여부를 드러내지 않도록 `404 NOT_FOUND`로 응답합니다.

### 1.3 요청 ID

AI를 호출하는 요청과 중복 반영되면 안 되는 요청은 본문(multipart는 폼 필드)에 `requestId`(UUID)를 담습니다.

- 클라이언트는 사용자 동작마다 새 `requestId`를 만들고, 네트워크 오류로 다시 보낼 때는 같은 값을 사용합니다.
- 서버는 같은 사용자·`requestId`·작업 종류의 처리 결과가 있으면 다시 처리하지 않고 저장된 결과를 돌려주며, 사용량도 한 번만 차감합니다.
- 같은 `requestId`로 다른 내용을 보내면 `409 REQUEST_ID_CONFLICT`입니다.

### 1.4 AI 사용량

사용량은 토큰으로 셉니다. 대화 도중 토큰이 떨어져 대화가 갑자기 끊기지 않도록 대화 단위로 예약합니다.

1. **대화 시작(CONV-01)**: 한 턴의 예상 토큰과 짧은 피드백에 쓸 토큰을 합친 양이 남아 있어야 시작합니다. 시작하면 피드백용 토큰을 미리 예약해 둡니다(`user_ai_usage.reserved_tokens`). 부족하면 대화를 시작하지 않고 `AI_QUOTA_EXHAUSTED`입니다.
2. **대화 중(CONV-03)**: 턴마다 예상 토큰을 예약하고 AI를 호출한 뒤 실제 토큰으로 정산합니다(`ai_requests`). 피드백 예약분을 빼고 남은 양이 다음 턴에 부족하면 AI를 호출하지 않습니다. 대신 NPC가 미리 작성한 작별 대사("앗, 급한 일이 생겼어요. 다음에 또 얘기해요!")로 대화를 끝냅니다.
3. **대화 종료(CONV-08)**: 예약해 둔 토큰으로 짧은 피드백을 만듭니다. 토큰이 떨어져 끝난 대화도 피드백을 받을 수 있습니다.

- 서버 쪽 실패로 결과를 전달하지 못하면 사용자 한도에서 차감하지 않고, 실제 쓴 토큰은 비용 근거로 기록합니다.
- 클라이언트의 버튼 활성화와 관계없이 최종 허용 여부는 서버가 판단합니다.
- AI 호출 기록(`ai_requests`)은 호출 한 번이 한 행입니다. 한 턴에서 같은 `requestId`로 무례 판정·의도 판정·NPC 대답을 부르므로 `kind`(`STT`, `GUARD`, `JUDGE`, `NPC`, `TTS`, `HINT`, `MEANING`, `FEEDBACK`)로 구분합니다. 한도 예약은 턴 단위로 한 번 하고, 행별 실제 사용량으로 정산합니다.
- 음성 인식(STT)은 토큰이 아니라 음성 길이로 사용량이 나옵니다. 길이는 `ai_requests.audio_ms`에 기록하고, 사용자 한도에서는 초당 토큰으로 환산해 차감합니다.
- 튜토리얼 음성 인식(CONV-02 `purpose: TUTORIAL`)은 사용자 한도에서 차감하지 않고 비용 근거만 기록합니다.
- 한 턴의 예상 토큰, 피드백 예약량, 한도 수치, 갱신 주기, STT의 초당 토큰 환산 비율은 서버 설정값이며 `[미정]`입니다.

### 1.5 오류 응답

```json
{
  "error": {
    "code": "CONVERSATION_ALREADY_ACTIVE",
    "message": "진행 중인 대화가 있습니다.",
    "details": { "activeConversationId": 55 }
  }
}
```

AI 제한 오류는 화면 19의 "계속 쓸 수 있어요" 목록에 쓸 기능 목록을 함께 돌려줍니다. `resumeAt`이 `null`이면 갱신 시점이 정책상 정해지지 않았다는 뜻이며 영구 차단이 아닙니다.

```json
{
  "error": {
    "code": "AI_QUOTA_EXHAUSTED",
    "message": "AI 사용량을 모두 사용했습니다. 마을 탐험, 사물 카드, 단어장은 계속할 수 있습니다.",
    "details": {
      "aiStatus": "AI_EXHAUSTED",
      "allowedFeatures": ["WORLD_EXPLORE", "OBJECT_CARD", "VOCABULARY", "SAVED_RECORDS"],
      "resumeAt": null
    }
  }
}
```

| 코드 | HTTP | 의미 |
| --- | --- | --- |
| `VALIDATION_FAILED` | 400 | 필수값 누락, 길이·형식 오류 |
| `UNAUTHENTICATED` | 401 | 로그인 필요 |
| `SIGNUP_REQUIRED` | 403 | 가입 정보 미제출 |
| `CSRF_HEADER_REQUIRED` | 403 | 상태를 바꾸는 요청에 `X-Requested-With` 헤더 없음 |
| `AI_RESTRICTED` | 403 | 운영·안전상 AI 제한 |
| `NOT_FOUND` | 404 | 대상이 없거나 다른 사용자의 데이터 |
| `ALREADY_SIGNED_UP` | 409 | 이미 가입한 사용자의 가입 정보 제출 |
| `NICKNAME_TAKEN` | 409 | 다른 사용자가 쓰고 있는 닉네임 |
| `LEVEL_TEST_NOT_IN_PROGRESS` | 409 | 이미 끝난 레벨 테스트에 응답·완료·그만하기 |
| `REQUEST_ID_CONFLICT` | 409 | 같은 `requestId`로 다른 내용 전송 |
| `CONVERSATION_ALREADY_ACTIVE` | 409 | 진행 중인 다른 대화가 있음 |
| `CONVERSATION_NOT_ACTIVE` | 409 | 이미 끝난 대화에 발화 전송 |
| `SCENE_CLOSED` | 409 | 열리지 않은 장소(`is_open = false`) |
| `TUTORIAL_NOT_IN_PROGRESS` | 409 | 튜토리얼 전 단계(`LANG`, `LEVEL`) 사용자의 튜토리얼 완료·건너뛰기 |
| `AUDIO_TOO_LARGE` | 413 | 녹음 크기 초과(3MB) |
| `AUDIO_FORMAT_UNSUPPORTED` | 415 | 지원하지 않는 녹음 형식 |
| `STT_NO_SPEECH` | 422 | 무음 또는 인식 실패 |
| `VOCAB_SOURCE_MISMATCH` | 422 | 저장하려는 단어가 출처(사물 카드, 대사 단어 풀이, 피드백 단어장 후보)에 없음 |
| `AI_QUOTA_EXHAUSTED` | 429 | 사용자 AI 한도 소진 |
| `AI_RATE_LIMITED` | 429 | 요청 속도 제한 |
| `AI_RESPONSE_INVALID` | 502 | 제한 횟수만큼 재시도해도 AI 응답 형식 오류 |
| `AI_TEMPORARILY_UNAVAILABLE` | 503 | 외부 AI 장애·시간 초과 |
| `INTERNAL_ERROR` | 500 | 그 밖의 서버 오류 |

- 지원하지 않는 메서드(405)·형식(415)처럼 표에 없는 요청 오류는 그 HTTP 상태와 상태 이름을 `code`로 돌려줍니다(예: `METHOD_NOT_ALLOWED`). 클라이언트 구현 오류이므로 화면에 따로 안내하지 않습니다.
- `INTERNAL_ERROR`의 `message`에는 내부 오류 내용을 담지 않습니다. 원인은 서버 로그에만 남깁니다.
- 화면 19의 세 상황은 서로 다른 코드로 구분합니다. 사용량 소진은 `AI_QUOTA_EXHAUSTED`, 응답 실패는 `AI_TEMPORARILY_UNAVAILABLE`·`AI_RESPONSE_INVALID`이며, 마이크 권한 거부와 장치 없음은 브라우저에서 판단하므로 서버 오류 코드가 없습니다.
- 대화 도중 토큰이 떨어지거나 무례한 말이 쌓인 경우는 오류가 아니라 NPC 대사로 대화가 끝나는 정상 응답입니다(1.4, CONV-03). `AI_RESTRICTED`는 무례 누적에 쓰지 않습니다.
- 무음·인식 실패(`STT_NO_SPEECH`)는 응답 실패와 별도로 "다시 녹음" 안내를 보여줍니다.
- 사용량 소진의 HTTP 상태 코드는 `[미정]`입니다. `[제안]` 429를 쓰고 `code`로 속도 제한과 구분합니다.

## 2. 화면별 API

각 화면에 표시되는 데이터와 버튼이 어느 API를 쓰는지 정리합니다. API의 요청·응답은 4절에 있습니다.

### 2.1 가입·초기 설정

| 화면 | 표시·동작 | API |
| --- | --- | --- |
| 01 사이트 접속 | 접속 직후 로그인·가입·초기 설정 상태로 다음 화면 결정 | BOOT-01 |
| | [시작하기], [로그인] | AUTH-01 |
| 02 회원가입·로그인 | [Google로 계속하기] | AUTH-01, AUTH-02 |
| | 처음 가입: 약관 목록·버전 | AUTH-03 |
| | 닉네임 [중복 확인] | AUTH-06 |
| | 닉네임, 캐릭터 기본형(F/M), 약관 동의 → [계속하기] | AUTH-04 |
| 03 배울 언어 선택 | 언어 카드(선택 가능·준비 중) | ONB-01 |
| | [다음] | ONB-02 |
| 04 레벨 테스트 | 첫 화면 [레벨 테스트 진행하기] (MVP는 클라이언트에서 비활성) | ONB-03 |
| | 첫 화면 [건너뛰기] | ONB-07 |
| | 문항, "문항 3 / N" 진행률, [다음] (응답 저장), 마지막 문항 뒤 결과 | ONB-04, ONB-05 |
| | 문항을 푸는 도중 [그만하기] | ONB-06 |
| 05 NPC AI 난이도 설정 | "레벨 테스트 추천" 배지, 단계 카드(선택 가능·추후 제공) | ONB-08 |
| | [설정 완료] | ONB-09 |

### 2.2 집 튜토리얼·행인 길 안내

튜토리얼(06~12)은 미리 작성한 대본으로 진행합니다. 대본과 NPC 음성은 Unity 콘텐츠로 두고, 서버는 음성 인식, 튜토리얼 완료·건너뛰기 기록만 맡습니다. `[제안]`

| 화면 | 표시·동작 | API |
| --- | --- | --- |
| 06 집에서 튜토리얼 시작 | "튜토리얼 1/5" 진행 표시와 안내 문구 | 없음(클라이언트) |
| | [튜토리얼 건너뛰기] | TUT-02 |
| 07 키 조작 안내 | 조작 안내와 "해 보기" 체크 | 없음(클라이언트) |
| 08 가구 호버 실습 | 집의 사물 카드(영어, 뜻, 예문, 예문 뜻) | WORLD-02(`HOME`) |
| | 발음 듣기 | WORLD-04 |
| | [단어장에 저장] | VOCAB-02(`OBJECT`) |
| | [알아요 · 다시 안 띄우기] | WORLD-03 |
| | 숨긴 카드는 사물을 클릭하면 다시 보임 | 없음(WORLD-02의 카드 정보 사용) |
| 09 미션 소개·외출 | 첫 미션 안내(자동으로 시작, 수락 버튼 없음) | 없음(튜토리얼 대본) |
| | 현관문으로 나가 거리로 이동 | WORLD-05 |
| 10 행인 NPC 선제 접근 | 행인이 다가와 말을 걸고 바로 대화 시작(튜토리얼에서는 지나가기 없음) | 없음(튜토리얼 대본) |
| 11 길 안내 튜토리얼 대화 | 대본의 NPC 대사와 내가 말할 대사 | 없음(튜토리얼 대본) |
| | 녹음 → 음성 인식 → 대본과 비교 | CONV-02(`purpose: TUTORIAL`) |
| 12 튜토리얼 피드백 | 피드백이 이렇게 제공된다는 예시 설명 | 없음(튜토리얼 대본) |
| | [마을로 가기] | TUT-01 |

### 2.3 마을·미션·자유 대화

| 화면 | 표시·동작 | API |
| --- | --- | --- |
| 13 마을·새로운 미션 | HUD의 [미션]으로 여는 오늘의 미션 목록(진행 중·완료), 갱신 시각 | MIS-01 |
| | 화면 위 미션 표시(진행 중인 미션 제목) | MIS-01 |
| 14 시설 이동·NPC 접근 | 건물 출입 | WORLD-05 |
| | 건물 안 NPC 이름표 | WORLD-02 |
| | [E 대화하기] | CONV-01(`initiatedBy: USER`) |
| | 튜토리얼 뒤 거리 행인의 말걸기 → [대화하기] / [지금은 괜찮아요] | CONV-01(`initiatedBy: NPC`) / 없음 |
| 15 NPC AI 자유 대화 | 대화 기록(텍스트) | CONV-01, CONV-03 응답(새로고침 후에는 CONV-11) |
| | 녹음 → 전사 확인 → [다시 녹음] / [보내기] | CONV-02, CONV-03 |
| | 마이크를 쓸 수 없을 때 [텍스트로 입력] | CONV-03 |
| | [다시 듣기], [뜻 보기], [힌트] | CONV-06, CONV-04, CONV-05 |
| | [대화 종료 (Esc)] | CONV-08 |
| | 토큰이 떨어지면 NPC 작별 대사로 대화 종료 | CONV-03 응답 |
| 16 대화 후 짧은 피드백 | NPC·장소·발화 수, 미션 완료, 요약, 잘했어요·아쉬워요, 개선 표현 | CONV-08 응답, FB-01 |
| | 생성 실패 시 [다시 만들기], [대화 기록 보기] | FB-02, CONV-10 |
| | [상세 보기], 단어장 후보 단어 [단어장에 저장] | FB-03, VOCAB-02 |

### 2.4 피드백·단어장·상태

| 화면 | 표시·동작 | API |
| --- | --- | --- |
| 17 상세 피드백 | 발화별 내 발화, 더 자연스러운 표현, 이유, 숙어와 뜻 | FB-03(처음), FB-01(다시 열 때) |
| | [모범 음성 듣기] | FB-04 |
| | [단어장에 저장] | VOCAB-02(`FEEDBACK`) |
| 18 단어장 | 전체 개수, 외운 단어 포함 여부, 최근 저장순 목록 (유형 필터는 MVP에서 숨김) | VOCAB-01 |
| | 상세: 단어 뜻·저장한 문장과 문장 뜻·저장 출처·메모 | VOCAB-01 항목 |
| | [메모], [외웠어요] | VOCAB-03 |
| | [단어장에서 삭제] | VOCAB-04 |
| | 지난 대화 탭 | CONV-09, CONV-10 |
| 19 AI 한도·오류 상태 | AI 사용량 소진 안내와 계속 쓸 수 있는 기능 | AI-01, 1.5 오류 응답 |
| | 응답 실패 [다시 시도] | 같은 `requestId`로 실패한 요청 다시 전송 |
| | 마이크 사용 불가 [텍스트로 입력] | 없음(클라이언트) |

## 3. API 목록

"AI" 열은 외부 AI를 새로 호출하는지 나타냅니다. 이미 만든 결과를 돌려줄 때는 호출하지 않습니다.

| ID | 메서드·경로 | 설명 | AI | 화면 |
| --- | --- | --- | --- | --- |
| BOOT-01 | `GET /api/v1/bootstrap` | 로그인·가입·초기 설정 상태와 AI 상태 조회 | - | 01, 13 |
| AUTH-01 | `GET /api/v1/auth/login/{provider}` | 소셜 로그인 시작(제공자로 리다이렉트) | - | 01, 02 |
| AUTH-02 | `GET /api/v1/auth/callback/{provider}` | 제공자 콜백, 기존 사용자 로그인 또는 가입 대기 | - | 02 |
| AUTH-03 | `GET /api/v1/auth/signup` | 가입 대기 정보와 약관 목록 | - | 02 |
| AUTH-04 | `POST /api/v1/auth/signup` | 가입 정보 제출, 사용자 생성 | - | 02 |
| AUTH-05 | `POST /api/v1/auth/logout` | 로그아웃 | - | 화면 없음 |
| AUTH-06 | `GET /api/v1/nicknames/availability` | 닉네임 사용 가능 여부 확인 | - | 02 |
| ONB-01 | `GET /api/v1/languages` | 학습 언어 목록 | - | 03 |
| ONB-02 | `PUT /api/v1/me/languages/{languageCode}` | 학습 언어 선택 | - | 03 |
| ONB-03 | `POST /api/v1/level-tests` | 레벨 테스트 시작, 문항 조회 | - | 04 |
| ONB-04 | `PUT /api/v1/level-tests/{testId}/answers/{itemCode}` | 문항 응답 저장 | - | 04 |
| ONB-05 | `POST /api/v1/level-tests/{testId}/complete` | 채점·추천 레벨 (MVP는 초급 고정) | - | 04 |
| ONB-06 | `POST /api/v1/level-tests/{testId}/quit` | 테스트 그만하기 | - | 04 |
| ONB-07 | `POST /api/v1/level-tests/skip` | 테스트 건너뛰기 | - | 04 |
| ONB-08 | `GET /api/v1/me/languages/{languageCode}` | 추천 레벨과 선택 가능한 난이도 | - | 05 |
| ONB-09 | `PUT /api/v1/me/languages/{languageCode}/level` | 대화 난이도 선택 | - | 05 |
| TUT-01 | `POST /api/v1/me/tutorial/complete` | 튜토리얼 완료 | - | 12 |
| TUT-02 | `POST /api/v1/me/tutorial/skip` | 튜토리얼 건너뛰기 | - | 06 |
| WORLD-01 | `GET /api/v1/scenes` | 장소 목록·개방 여부 | - | 14 |
| WORLD-02 | `GET /api/v1/scenes/{sceneCode}` | 장소의 NPC와 사물 카드 | - | 08, 14 |
| WORLD-03 | `PUT /api/v1/me/object-prefs/{objectKey}` | 사물 카드 자동 표시 끄기·다시 켜기 | - | 08 |
| WORLD-04 | `GET /api/v1/vocab/{vocabId}/pronunciation` | 미리 만든 사물 단어 발음 음성 | - | 08 |
| WORLD-05 | `PUT /api/v1/me/last-scene` | 현재 장소 저장(재접속 위치) | - | 09, 14 |
| MIS-01 | `GET /api/v1/missions` | 오늘의 미션과 진행 상태(자동 시작) | - | 13 |
| CONV-01 | `POST /api/v1/conversations` | 대화 시작, NPC 첫 대사 | `[미정]` | 14 |
| CONV-02 | `POST /api/v1/speech/transcriptions` | 녹음 전사(STT) | STT | 11, 15 |
| CONV-03 | `POST /api/v1/conversations/{conversationId}/turns` | 발화 전송, NPC 응답과 추천 단어 | LLM, TTS | 15 |
| CONV-04 | `POST /api/v1/turns/{turnId}/meaning` | NPC 대사 한국어 뜻 | LLM | 15 |
| CONV-05 | `POST /api/v1/turns/{turnId}/hint` | 추천 문장 | LLM | 15 |
| CONV-06 | `GET /api/v1/turns/{turnId}/speech` | 만들어 둔 NPC 음성 재생 | - | 15 |
| CONV-07 | `POST /api/v1/turns/{turnId}/speech` | NPC 음성 다시 생성 | TTS | 15 |
| CONV-08 | `POST /api/v1/conversations/{conversationId}/end` | 대화 종료, 짧은 피드백 | LLM | 16 |
| CONV-09 | `GET /api/v1/conversations` | 지난 대화 목록 | - | 18 |
| CONV-10 | `GET /api/v1/conversations/{conversationId}` | 대화 상세(턴 포함) | - | 16, 18 |
| CONV-11 | `GET /api/v1/conversations/active` | 진행 중인 대화 조회 | - | 15 |
| FB-01 | `GET /api/v1/conversations/{conversationId}/feedback` | 짧은 피드백·상세 평가 조회 | - | 16, 17 |
| FB-02 | `POST /api/v1/conversations/{conversationId}/feedback` | 짧은 피드백 다시 만들기 | LLM | 16 |
| FB-03 | `POST /api/v1/conversations/{conversationId}/detailed-feedback` | 상세 평가 만들기 | LLM | 16, 17 |
| FB-04 | `POST /api/v1/turn-feedback/{turnFeedbackId}/model-speech` | 모범 음성 | TTS | 17 |
| VOCAB-01 | `GET /api/v1/vocab` | 단어장 목록·개수 | - | 18 |
| VOCAB-02 | `POST /api/v1/vocab` | 단어 저장 | - | 08, 15, 16, 17 |
| VOCAB-03 | `PATCH /api/v1/vocab/{userVocabId}` | 메모, 외웠어요 표시 | - | 18 |
| VOCAB-04 | `DELETE /api/v1/vocab/{userVocabId}` | 단어장에서 삭제 | - | 18 |
| AI-01 | `GET /api/v1/ai/status` | AI 이용 상태·기능별 가능 여부 | - | 13, 19 |

## 4. API 상세

### 4.1 접속·가입

#### BOOT-01 `GET /api/v1/bootstrap`

접속 직후 한 번 호출해 첫 화면을 정합니다. 로그인하지 않았으면 `401 UNAUTHENTICATED`이며 화면 01을 보여줍니다.

```json
{
  "authState": "SIGNED_IN",
  "user": {
    "userId": 1,
    "nickname": "Sun",
    "avatarType": "F",
    "currentLanguage": "en-US",
    "onboardingStep": "DONE"
  },
  "lastSceneCode": "TOWN",
  "activeConversationId": null,
  "aiStatus": { "status": "AI_AVAILABLE" },
  "progress": { "savedVocabCount": 12, "runningMissionCount": 2 }
}
```

| 상태 | 보여줄 화면 |
| --- | --- |
| `authState: SIGNUP_REQUIRED` | 02 가입 정보 입력. 이때 `user` 이하 필드는 없습니다. |
| `onboardingStep: LANG` | 03 배울 언어 선택 |
| `onboardingStep: LEVEL` | 04 레벨 테스트. ONB-08의 `levelTest.status`가 `DONE`, `QUIT`, `SKIPPED`면 05 |
| `onboardingStep: TUTORIAL` | 06 튜토리얼 시작. 튜토리얼 중간에 나갔다 오면 처음부터 다시 진행합니다. |
| `onboardingStep: DONE` | `lastSceneCode` 장소. 없으면 마을 |

- `activeConversationId`가 있으면 CONV-11 규칙에 따라 처리합니다.
- `aiStatus`의 형식은 AI-01과 같으며 HUD의 "AI 사용 가능" 표시에 씁니다.
- 값이 없는 필드(`lastSceneCode` 등)는 응답에 담지 않습니다. 클라이언트는 필드가 없을 때와 `null`일 때를 같게 처리합니다.
- 세션의 로그인 사용자가 DB에 없으면(탈퇴 등) 세션의 로그인 상태를 비우고 `401 UNAUTHENTICATED`로 응답합니다.

#### AUTH-01·02 소셜 로그인

- AUTH-01 `GET /api/v1/auth/login/google`은 Google 로그인 화면으로 리다이렉트합니다. 클라이언트는 API 호출이 아니라 페이지 이동으로 엽니다.
- AUTH-02 `GET /api/v1/auth/callback/google`은 Google이 인증을 마치고 돌려보내는 주소입니다. Google 콘솔의 승인된 리디렉션 URI에 환경마다 이 주소를 등록합니다. `(provider, providerUid)`로 사용자를 찾으며, `providerUid`는 Google 계정 고유 ID(OIDC `sub`)입니다.
  - 가입한 사용자: 로그인 처리 후 클라이언트로 리다이렉트합니다.
  - 처음 로그인한 사용자: 사용자를 만들지 않고 제공자 정보(`provider`, `providerUid`, 이메일)를 가입 대기 상태로 세션에 보관한 뒤 클라이언트로 리다이렉트합니다. `users.nickname`, `users.avatar_type`이 필수라 가입 정보를 받기 전에는 사용자를 만들 수 없습니다. 가입 대기 상태는 로그인 상태가 아닙니다.
- 리다이렉트할 클라이언트 주소는 서버 설정값(`CLIENT_URL`, 기본값 `/`)입니다. 로그인이 실패하거나 사용자가 Google 동의를 취소하면 같은 주소에 `?loginError=true`를 붙여 보냅니다. 클라이언트는 그 뒤 BOOT-01로 다음 화면을 정합니다.

#### AUTH-03 `GET /api/v1/auth/signup`

화면 02의 가입 정보 입력에 필요한 약관 목록을 돌려줍니다. 가입 대기 상태가 아니면 `401 UNAUTHENTICATED`입니다.

```json
{
  "email": "user@example.com",
  "consents": [
    { "consentType": "TERMS", "version": "2026-10", "required": true, "title": "서비스 이용약관", "url": "/terms/2026-10" },
    { "consentType": "PRIVACY", "version": "2026-10", "required": true, "title": "개인정보 처리방침", "url": "/privacy/2026-10" },
    { "consentType": "AGE14", "version": "2026-10", "required": true, "title": "만 14세 이상입니다", "url": null },
    { "consentType": "VOICE", "version": "2026-10", "required": false, "title": "음성 처리 동의", "url": "/voice/2026-10" }
  ]
}
```

- 약관 버전과 문서 주소는 서버 설정값입니다. `VOICE` 동의의 필수 여부와 음성 보관 기간은 `[미정]`이며, 정해지기 전까지 선택 약관으로 둡니다.

#### AUTH-04 `POST /api/v1/auth/signup`

```json
// 요청
{
  "nickname": "Sun",
  "avatarType": "F",
  "consents": [
    { "consentType": "TERMS", "version": "2026-10" },
    { "consentType": "PRIVACY", "version": "2026-10" },
    { "consentType": "AGE14", "version": "2026-10" }
  ]
}

// 응답 201
{ "user": { "userId": 1, "nickname": "Sun", "avatarType": "F", "currentLanguage": null, "onboardingStep": "LANG" } }
```

- 사용자(`users`)와 약관 동의(`user_consents`)를 한 트랜잭션으로 만들고 세션을 로그인 상태로 바꿉니다. 로그인하면서 세션 ID를 새로 발급하고 가입 대기 정보를 지웁니다.
- `nickname`은 NPC가 부르는 이름입니다.
  - 한글 완성형(가~힣), 영문, 숫자만 씁니다. 공백, 특수문자, 이모지, 자음·모음만 있는 글자는 쓸 수 없습니다.
  - 2자 이상이고, 한글은 2, 영문·숫자는 1로 세어 합계 16 이하입니다. 한글만 쓰면 2~8자, 영문·숫자만 쓰면 2~16자입니다.
  - 규칙에 맞지 않으면 `VALIDATION_FAILED`입니다.
  - 다른 사용자가 쓰고 있으면 `409 NICKNAME_TAKEN`입니다. 대소문자를 구분해 `Sun`과 `sun`은 다른 닉네임으로 봅니다. `[제안]`
  - 금칙어 처리 여부는 `[미정]`입니다.
- `avatarType`은 `F`, `M`입니다.
- 현재 버전과 다른 버전에 동의한 항목은 동의하지 않은 것으로 봅니다. 선택 약관도 현재 버전에 동의한 경우에만 저장합니다.
- 필수 약관이 빠졌거나 버전이 현재 버전과 다르면 `VALIDATION_FAILED`이며 `details.missingConsents`에 빠진 약관을 담습니다(예: `["AGE14"]`).
- 가입 대기 상태가 아니면 `401 UNAUTHENTICATED`, 이미 로그인한 사용자이거나 같은 Google 계정으로 이미 가입했으면 `409 ALREADY_SIGNED_UP`입니다.
- `users.current_language`는 필수 컬럼이라 언어를 고르기 전에는 기본값 `en-US`로 저장합니다. 응답의 `currentLanguage`는 ONB-02 전까지 `null`로 보여줍니다. `[제안]`

#### AUTH-05 `POST /api/v1/auth/logout`

- 세션을 끝내고 `204 No Content`를 돌려줍니다. 와이어프레임에는 로그아웃 버튼이 없으며 설정 화면을 설계할 때 배치합니다.
- 로그인 여부와 관계없이 `204`입니다. 가입 대기 중인 사용자가 다른 Google 계정으로 바꾸려 할 때도 이 API로 가입 대기 정보를 지웁니다.
- POST만 받습니다. 다른 상태 변경 요청처럼 `X-Requested-With` 헤더가 필요합니다.

#### AUTH-06 `GET /api/v1/nicknames/availability`

화면 02에서 가입 정보를 제출하기 전에 닉네임을 쓸 수 있는지 확인합니다. 이후 닉네임 변경 화면을 만들면 같은 API를 씁니다.

```json
// GET /api/v1/nicknames/availability?nickname=Sun
{ "nickname": "Sun", "available": false }
```

- 가입 대기 상태이거나 로그인한 사용자만 호출할 수 있습니다. 둘 다 아니면 `401 UNAUTHENTICATED`입니다.
- 닉네임 규칙(AUTH-04)에 맞지 않으면 `VALIDATION_FAILED`입니다. 규칙에 맞고 다른 사용자가 쓰고 있지 않으면 `available: true`입니다.
- 확인한 닉네임을 예약하지 않습니다. 확인한 뒤 제출하기 전에 다른 사용자가 먼저 가입하면 AUTH-04가 `409 NICKNAME_TAKEN`을 돌려줍니다.
- 한글 닉네임은 URL 인코딩해 보냅니다.

### 4.2 초기 설정

#### ONB-01 `GET /api/v1/languages`

```json
[
  { "languageCode": "en-US", "name": "English (US)", "description": "영어 · 미국식 발음", "available": true }
]
```

- 화면 03의 카드입니다. `available: false`인 언어는 "준비 중" 카드로 보여줍니다. MVP에서 선택 가능한 언어는 `en-US`뿐이며 언어 범위·표시 이름은 `[미정]`입니다.

#### ONB-02 `PUT /api/v1/me/languages/{languageCode}`

- `user_language_profiles`를 만들고 `users.current_language`를 바꿉니다. 처음 선택하면 `onboardingStep`이 `LEVEL`로 넘어갑니다.
- 응답은 `{ "languageCode": "en-US", "onboardingStep": "LEVEL" }`입니다.
- ONB-01 목록에 없거나 `available: false`인 언어는 `VALIDATION_FAILED`입니다.
- 이미 고른 적 있는 언어를 다시 고르면 학습 설정(추천·선택 난이도)을 새로 만들지 않고 학습 언어만 바꿉니다. 언어 선택 단계를 지난 사용자의 `onboardingStep`은 바뀌지 않습니다.

#### ONB-03~07 레벨 테스트

레벨 테스트 방식은 `[미정]`입니다. MVP는 흐름만 만들어 두고 실제 레벨 판정은 하지 않습니다.

- 문항은 서버 설정 데이터의 en-US 예시 선택형 5개입니다. 문항 원본을 둘 테이블이 ERD에 없으므로 `itemCode`로 문항을 구분합니다.
- 결과 레벨은 점수와 상관없이 초급(1)으로 고정합니다.
- 화면 04의 첫 화면에는 [레벨 테스트 진행하기]와 [건너뛰기]를 둡니다. MVP에서는 클라이언트가 [진행하기]를 비활성("준비 중")으로 막아 두므로 실제로는 ONB-07만 씁니다. ONB-03~06은 막아 둔 버튼을 열면 바로 쓸 수 있게 만들어 둔 API입니다.

```json
// ONB-03 POST /api/v1/level-tests  요청 ([진행하기]를 누를 때)
{ "languageCode": "en-US" }

// 응답 201
{
  "testId": 9,
  "status": "IN_PROGRESS",
  "totalItems": 5,
  "items": [
    {
      "itemCode": "EN_L1_001",
      "type": "CHOICE",
      "instruction": "빈칸에 알맞은 말을 고르세요.",
      "question": "Can I ______ a coffee, please?",
      "choices": ["have", "has", "having", "had"]
    }
  ]
}
```

- ONB-03은 [진행하기]를 누를 때 새 테스트(`IN_PROGRESS`)를 만들고 학습 설정의 `last_test_id`를 이 테스트로 바꿉니다. 정답은 응답에 담지 않습니다.
- 진행 중인 테스트가 있어도 다시 시작하면 새 테스트를 만듭니다. 중간에 나갔다 오면 이어서 풀지 않고 처음부터 다시 풉니다.
- 화면 04의 "문항 3 / N"과 진행률은 `totalItems`와 현재 문항 순서로 그립니다.
- ONB-04 `PUT /api/v1/level-tests/{testId}/answers/{itemCode}`는 `{ "answer": "have" }`를 저장하고 `204`를 돌려줍니다. 정답 여부도 함께 기록하며, 같은 문항에 다시 답하면 덮어씁니다. 없는 문항이면 `404 NOT_FOUND`입니다.
- ONB-05는 `{ "testId": 9, "status": "DONE", "score": 3, "resultLevel": 1 }`을 돌려주고 `recommended_level`을 1로 저장합니다. `score`는 맞힌 문항 수이며, 답하지 않은 문항은 틀린 것으로 셉니다. 말하기 문항처럼 AI 채점이 필요해지면 AI 사용량 검사 대상이 됩니다.
- ONB-06 [그만하기]는 일부 응답 후 멈춘 기록(`QUIT`)입니다. `{ "testId": 9, "status": "QUIT" }`를 돌려주고 추천 레벨은 바꾸지 않습니다.
- 응답·완료·그만하기는 진행 중인 테스트에만 할 수 있습니다. 이미 끝난 테스트면 `409 LEVEL_TEST_NOT_IN_PROGRESS`, 다른 사용자의 테스트면 `404 NOT_FOUND`입니다.
- 고른 적 없는 언어로 시작하거나 건너뛰면 `404 NOT_FOUND`입니다.
- 문항 하나만 건너뛰는 기능의 필요 여부는 `[미정]`입니다.

```json
// ONB-07 POST /api/v1/level-tests/skip  요청
{ "languageCode": "en-US" }

// 응답 201
{ "testId": 10, "status": "SKIPPED" }
```

- ONB-07 [건너뛰기]는 테스트를 시작하기 전에 누르므로 테스트 ID 없이 학습 언어만 보냅니다. 테스트를 치르지 않고 넘어간 기록(`SKIPPED`)을 만들고 `last_test_id`를 이 기록으로 바꿉니다. 추천 레벨 없이 화면 05로 갑니다.

#### ONB-08 `GET /api/v1/me/languages/{languageCode}`

```json
{
  "languageCode": "en-US",
  "recommendedLevel": 1,
  "selectedLevel": null,
  "levelTest": { "testId": 9, "status": "DONE" },
  "levels": [
    { "level": 1, "name": "초급", "available": true, "description": "천천히, 쉬운 단어와 짧은 문장으로 말해요." },
    { "level": 2, "name": "중급", "available": false, "description": null },
    { "level": 3, "name": "고급", "available": false, "description": null }
  ]
}
```

- 화면 05의 "레벨 테스트 추천" 배지는 `recommendedLevel`이 있을 때만 보여줍니다.
- 난이도는 초급·중급·고급 3단계이고 MVP는 초급만 고를 수 있습니다. `available: false`인 단계는 "추후 제공" 카드입니다. 단계 이름과 설명은 서버 설정값이며, 중급·고급의 설명과 단계별 차이는 `[미정]`입니다.
- `levelTest`는 마지막으로 시작하거나 건너뛴 테스트(`last_test_id`)입니다. BOOT-01의 `onboardingStep: LEVEL` 사용자는 이 상태가 `DONE`, `QUIT`, `SKIPPED`면 화면 05, 없거나 `IN_PROGRESS`면 화면 04로 갑니다.
- `selectedLevel`은 ONB-09 전까지 `null`입니다. DB에는 `selected_level`이 필수라 프로필을 만들 때 1로 저장하고, ONB-09 처리 여부는 `onboardingStep`으로 판단합니다. `[제안]`
- 레벨 테스트 기록이 없으면 `levelTest`는 응답에 담지 않습니다.
- 고른 적 없는 언어면 `404 NOT_FOUND`입니다.

#### ONB-09 `PUT /api/v1/me/languages/{languageCode}/level`

```json
{ "selectedLevel": 1 }
```

- MVP는 1단계만 허용합니다. 다른 값은 `VALIDATION_FAILED`입니다.
- 처음 설정하면 `onboardingStep`이 `TUTORIAL`로 넘어가고 응답에 `onboardingStep`을 담습니다. 응답은 `{ "languageCode": "en-US", "selectedLevel": 1, "onboardingStep": "TUTORIAL" }`입니다.
- 튜토리얼 단계 이후에 다시 설정하면 난이도만 바꾸고 `onboardingStep`은 그대로 둡니다. 고른 적 없는 언어면 `404 NOT_FOUND`입니다.

### 4.3 튜토리얼

튜토리얼(화면 06~12)은 미리 작성한 대본으로 진행합니다. 대본의 NPC 대사·음성과 내가 말할 대사는 Unity 콘텐츠이며, 튜토리얼 대화는 `conversations`에 기록하지 않습니다. `[제안]`

- 미션은 자동으로 시작되어 수락 버튼이 없습니다(화면 09).
- 행인은 튜토리얼 대본에 따라 반드시 말을 겁니다. 지나가거나 거절할 수 없습니다(화면 10).
- 튜토리얼 대화에는 추천 단어, 힌트, 대화 종료 버튼이 없습니다. 대본의 대사를 모두 말해야 끝납니다(화면 11).
- 사용자가 대본의 대사를 말하면 CONV-02(`purpose: TUTORIAL`)로 음성을 인식하고, 대본과 맞는지는 클라이언트가 비교합니다. 비교 기준(키워드 포함 여부 등)은 클라이언트 콘텐츠에서 정합니다.
- 튜토리얼 피드백(화면 12)은 실제 AI 피드백이 아니라 "대화가 끝나면 이렇게 피드백을 받아요"를 보여주는 예시 설명입니다.

#### TUT-01 `POST /api/v1/me/tutorial/complete`

```json
// 요청 본문 없음

// 응답 200
{ "onboardingStep": "DONE" }
```

- 화면 12의 [마을로 가기]에서 호출합니다. `onboardingStep`을 `DONE`으로 바꿉니다.
- 튜토리얼 전 단계(`LANG`, `LEVEL`)면 `409 TUTORIAL_NOT_IN_PROGRESS`이며 단계를 바꾸지 않습니다. 이미 `DONE`인 사용자가 다시 호출하면 같은 응답을 돌려줍니다.
- 마지막 장소(`last_scene_id`)는 바꾸지 않습니다. BOOT-01에 `lastSceneCode`가 없으면 클라이언트가 마을에서 시작합니다.

#### TUT-02 `POST /api/v1/me/tutorial/skip`

- 화면 06의 [튜토리얼 건너뛰기]에서 호출합니다. 요청·응답과 단계 규칙은 TUT-01과 같습니다. `onboardingStep`을 `DONE`으로 바꾸고 마을에서 시작합니다.
- 건너뛴 사용자에게 튜토리얼을 다시 볼 방법을 줄지는 `[미정]`입니다.

### 4.4 장소·사물

#### WORLD-01 `GET /api/v1/scenes`

- `[{ "sceneCode": "CAFE", "name": "모퉁이 카페", "unityKey": "CafeScene", "isOpen": true }]` 형식입니다. 화면 14에서 들어갈 수 있는 건물을 표시할 때 씁니다. MVP는 카페·호텔을 포함해 모든 장소가 열려 있으며, 이후 준비 중인 건물은 `isOpen: false`로 내려갑니다.
- `unityKey`는 클라이언트가 불러올 Unity 씬 이름입니다. 목록은 집, 마을, 카페, 호텔 순입니다.

#### WORLD-02 `GET /api/v1/scenes/{sceneCode}`

```json
{
  "sceneCode": "HOME",
  "name": "집",
  "unityKey": "HomeScene",
  "isOpen": true,
  "npcs": [],
  "objects": [
    {
      "objectKey": "sofa",
      "vocab": {
        "vocabId": 31,
        "type": "WORD",
        "text": "sofa",
        "meaningKo": "소파",
        "example": "I'm sitting on the sofa.",
        "exampleKo": "소파에 앉아 있어요."
      },
      "hasPronunciation": true,
      "saved": false,
      "hoverHidden": false
    }
  ]
}
```

거리(`TOWN`)나 카페(`CAFE`)의 NPC는 이렇게 내려옵니다.

```json
"npcs": [
  { "npcCode": "TOWN_PASSERBY", "name": "Ben", "npcType": "PASSERBY", "roleLabel": "행인", "canInitiate": true, "approachLine": "Hi there! Nice weather today, isn't it?" },
  { "npcCode": "CAFE_STAFF", "name": "Mia", "npcType": "STAFF", "roleLabel": "카페 직원", "canInitiate": false, "approachLine": null }
]
```

- `npcCode`는 Unity 씬의 NPC에 넣은 코드와 같습니다. 튜토리얼 행인은 Unity 대본 콘텐츠라 서버 NPC가 아닙니다.
- `npcs`, `objects`의 순서에는 의미가 없습니다. 클라이언트는 `npcCode`, `objectKey`로 찾습니다.
- 장소에 들어갈 때 그 장소의 사물 카드를 한 번에 받습니다. MVP는 장소마다 사물이 10개 안팎이라 응답이 작습니다.
- 맵이 커지면 사물을 한 번에 받지 않고 캐릭터 주변 사물만 받도록 바꿉니다. 사물 위치는 Unity 씬에 있으므로, 클라이언트가 주변 사물의 `objectKey`를 모아 요청하는 방식을 우선 검토합니다. `[제안]`
- 화면 08의 사물 카드는 `vocab`과 `saved`, `hoverHidden`으로 그립니다. 미리 등록한 어휘라 AI를 호출하지 않습니다.
- `hoverHidden: true`인 사물은 호버로 카드를 띄우지 않고, 사물을 클릭하면 카드를 보여줍니다.
- `exampleKo`(예문 뜻)는 `vocabularies.example_ko`입니다. 예문을 저장 전부터 보여줄지는 `[미정]`입니다(5.2).
- `approachLine`(`npcs.approach_line`)은 튜토리얼 뒤 거리 행인의 말걸기 말풍선입니다. 대화를 시작하기 전에 보여주므로 미리 작성한 대사입니다. 말걸기 발동 조건(거리, 반복 간격, 횟수)은 클라이언트 콘텐츠 설정입니다.
- `roleLabel`은 `npcs.role_label`입니다. 이름표 아래에 보여주는 역할 이름(행인, 카페 직원)입니다.
- NPC의 `persona`, `voice_id`는 서버 내부 정보라 응답하지 않습니다.
- 열리지 않은 장소는 `isOpen: false`이며, 그 장소의 NPC와 대화를 시작하면 `SCENE_CLOSED`입니다.

#### WORLD-03 `PUT /api/v1/me/object-prefs/{objectKey}`

```json
// 요청
{ "hoverHidden": true }

// 응답 200
{ "objectKey": "sofa", "hoverHidden": true }
```

- 화면 08의 [알아요 · 다시 안 띄우기]는 `true`입니다. 숨긴 뒤에는 사물을 클릭해야 카드가 보이며, 카드에서 자동 표시를 다시 켜면 `false`로 바꿉니다.
- `objectKey`는 WORLD-02 사물 카드의 `objectKey`(`world_objects.unity_key`)입니다.
- 설정은 사용자·사물마다 하나입니다. 처음 바꿀 때 만들고, 다시 보내면 값만 바꿉니다. 같은 값을 여러 번 보내도 결과가 같습니다.
- `hoverHidden`이 없으면 `400 VALIDATION_FAILED`, 없는 사물이면 `404 NOT_FOUND`입니다.

#### WORLD-04 `GET /api/v1/vocab/{vocabId}/pronunciation`

- 화면 08의 발음 듣기 버튼이 쓰는 음성 파일(`audio/mpeg` 등)을 돌려줍니다. `hasPronunciation: false`인 단어는 버튼을 숨깁니다.
- 발음 음성은 사물 어휘를 등록할 때 TTS로 한 번 만들어 파일로 저장합니다. 요청할 때 AI를 호출하지 않으므로 AI 사용량이 소진돼도 들을 수 있습니다. `[제안]`
- 음성 파일 자체는 DB가 아니라 파일 저장소(서버 디스크 또는 오브젝트 스토리지)에 두고, DB에는 파일 키(`vocabularies.pronunciation_audio_key`)만 저장합니다.

#### WORLD-05 `PUT /api/v1/me/last-scene`

```json
// 요청
{ "sceneCode": "CAFE" }

// 응답 200
{ "sceneCode": "CAFE" }
```

- 화면 09의 외출, 화면 14의 건물 출입처럼 장소를 옮길 때 호출합니다. 재접속하면 BOOT-01의 `lastSceneCode`에서 시작합니다. 장소 안의 정확한 위치 복원은 `[미정]`입니다.
- `sceneCode`가 없거나 비어 있으면 `400 VALIDATION_FAILED`, 없는 장소면 `404 NOT_FOUND`, 열리지 않은 장소(`is_open = false`)면 `409 SCENE_CLOSED`입니다. 오류가 나면 마지막 장소를 바꾸지 않습니다.

### 4.5 미션

미션은 자동으로 시작됩니다. 수락 버튼이 없고, 사용자가 미션에 맞는 행동을 하면 서버가 완료로 판정합니다. 한 대화에서 여러 미션을 동시에 완료할 수 있습니다.

#### MIS-01 `GET /api/v1/missions`

```json
{
  "date": "2026-10-21",
  "resetsAt": "2026-10-22T00:00:00+09:00",
  "missions": [
    {
      "missionId": 3,
      "title": "카페에서 음료 주문하기",
      "description": "카페 직원에게 원하는 음료를 영어로 주문해 보세요.",
      "sceneCode": "CAFE",
      "npcCode": "CAFE_STAFF",
      "npcName": "Mia",
      "userMissionId": 12,
      "status": "RUN",
      "completedAt": null
    },
    {
      "missionId": 5,
      "title": "카페 직원에게 추천 메뉴 묻기",
      "description": "카페 직원에게 오늘의 추천 메뉴를 물어보세요.",
      "sceneCode": "CAFE",
      "npcCode": "CAFE_STAFF",
      "npcName": "Mia",
      "userMissionId": 13,
      "status": "DONE",
      "completedAt": "2026-10-21T14:05:00+09:00"
    }
  ]
}
```

- 오늘 처음 조회하면 서버가 오늘의 일일 미션을 `user_missions`에 `RUN` 상태로 만듭니다. 같은 날 다시 조회해도 새로 만들지 않습니다.
- 화면 13의 미션 목록은 HUD의 [미션]으로 엽니다. 화면 위 미션 표시는 `status: RUN`인 미션의 제목입니다. 미션 이름과 내용은 예시입니다.
- 열리지 않은 장소의 미션은 목록에 넣지 않습니다. MVP의 일일 미션은 카페와 호텔에 하나씩입니다. 튜토리얼 미션은 Unity 대본 콘텐츠라 DB에 없습니다.
- 미션 상세 화면(지도, 자세한 설명)은 MVP에 넣지 않습니다(7절).
- 완료 판정 기준(`keywords`, `intent_rule`)은 서버 내부 정보라 응답하지 않습니다.
- MVP의 미션 완료 행동은 대상 NPC와의 대화입니다. 대화 외 행동(장소 방문, 단어 저장 등)으로 완료하는 미션이 필요한지는 `[미정]`입니다.
- 일일 미션의 날짜는 MVP에서 `Asia/Seoul` 기준으로 고정하며, 자정에 바뀝니다. 미션 보상은 `[미정]`입니다.

### 4.6 대화

#### CONV-01 `POST /api/v1/conversations`

```json
// 요청
{ "requestId": "6f1c2a7e-4b8d-4b7a-9a52-1f0f3d2c9e11", "npcCode": "CAFE_STAFF", "initiatedBy": "USER" }

// 응답 201
{
  "conversationId": 58,
  "status": "ACTIVE",
  "levelApplied": 1,
  "npc": { "npcCode": "CAFE_STAFF", "name": "Mia", "roleLabel": "카페 직원" },
  "runningMissions": [{ "userMissionId": 12, "title": "카페에서 음료 주문하기" }],
  "openingTurn": {
    "turnId": 100,
    "seq": 1,
    "speaker": "NPC",
    "text": "Hi! What can I get for you?",
    "suggestWords": ["latte", "iced", "please"],
    "speech": { "status": "READY", "url": "/api/v1/turns/100/speech" }
  }
}
```

- 화면 14의 [E 대화하기]는 `initiatedBy: USER`, 튜토리얼 뒤 거리 행인의 말걸기에 응답하면 `initiatedBy: NPC`입니다. 행인 말걸기를 거절하거나 무시하면 서버를 호출하지 않습니다.
- `initiatedBy: NPC`면 이미 보여준 `approachLine`을 첫 NPC 턴으로 저장하며 AI를 호출하지 않습니다.
- `initiatedBy: USER`일 때 직원의 첫 인사를 미리 작성할지 AI로 만들지는 `[미정]`입니다.
- `runningMissions`는 이 NPC가 대상인 진행 중인 미션이며 상단의 "미션 · …" 표시에 씁니다.
- `levelApplied`는 사용자의 `selected_level`입니다.
- 1.4의 규칙대로 한 턴과 피드백에 쓸 토큰이 남아 있어야 시작하며, 피드백용 토큰을 예약합니다. 부족하면 `AI_QUOTA_EXHAUSTED`입니다.
- 진행 중인 대화가 있으면 `CONVERSATION_ALREADY_ACTIVE`를 `activeConversationId`와 함께 돌려줍니다.
- AI를 쓸 수 없는 상태에서는 행인 말걸기를 발생시키지 않습니다. `[제안]` 클라이언트가 BOOT-01·AI-01의 상태로 판단하고, 최종 확인은 이 API에서 합니다.

#### CONV-02 `POST /api/v1/speech/transcriptions`

`multipart/form-data`로 녹음을 보내고 전사만 받습니다. 발화 턴은 만들지 않습니다.

| 필드 | 설명 |
| --- | --- |
| `requestId` | 요청 ID |
| `purpose` | `TURN`(자유 대화 발화) 또는 `TUTORIAL`(튜토리얼 대본 대사) |
| `conversationId` | `TURN`일 때 진행 중인 대화 ID. `TUTORIAL`이면 보내지 않습니다. |
| `audio` | 녹음 파일 |

```json
// 응답 200
{ "sttRawText": "Medium size, please", "durationMs": 2300 }
```

- 화면 15의 전사 확인에 `sttRawText`를 보여줍니다. 인식이 틀렸으면 [다시 녹음]으로 다시 말하고, 맞으면 [보내기]로 CONV-03에 보냅니다. 전사 내용을 직접 고치는 기능은 넣지 않았습니다(6절).
- 화면 11의 튜토리얼에서는 결과를 대본의 대사와 비교하는 데 씁니다. 튜토리얼 음성 인식은 사용자 한도에서 차감하지 않습니다(1.4).
- 무음·인식 실패는 `STT_NO_SPEECH`입니다.
- 인식 대상은 영어입니다. 한국어로 말하면 거르거나 번역하지 않고 한글 그대로 전사합니다. 그 문장의 번역은 CONV-03이 맡습니다.
- 녹음 형식은 `webm`, `ogg`, `mp4`, `wav`, `mpeg`이며 Unity는 16kHz 모노 16비트 WAV로 보냅니다. `audio` 파일 부분에 `Content-Type`(예: `audio/wav`)을 붙입니다.
- 녹음은 최대 3MB입니다. 서버는 AI를 호출하기 전에 크기와 형식을 확인해 `AUDIO_TOO_LARGE`(413), `AUDIO_FORMAT_UNSUPPORTED`(415)로 거절합니다. 녹음 길이(최대 15초, 무음이 3초 넘게 이어지면 자동 종료)는 클라이언트 녹음 규칙으로 지킵니다. `durationMs`는 인식 뒤에 알 수 있으므로 거절 기준으로 쓰지 않습니다.
- 받은 음성은 전사 후 보관하지 않습니다. `VOICE` 동의를 받은 경우의 보관 여부·기간은 `[미정]`입니다.

#### CONV-03 `POST /api/v1/conversations/{conversationId}/turns`

```json
// 요청
{
  "requestId": "0d4f6a0b-2a7c-4e3f-8d0e-5b9a1c2d3e4f",
  "inputType": "VOICE",
  "text": "Medium size, please",
  "sttRawText": "Medium size, please"
}

// 응답 200
{
  "userTurn": {
    "turnId": 104,
    "seq": 4,
    "speaker": "USER",
    "text": "Medium size, please",
    "rude": false,
    "translation": null
  },
  "npcTurn": {
    "turnId": 105,
    "seq": 5,
    "speaker": "NPC",
    "text": "Great! Is that for here or to go?",
    "suggestWords": ["for here", "to go", "please"],
    "speech": { "status": "READY", "url": "/api/v1/turns/105/speech" }
  },
  "conversation": { "status": "ACTIVE", "endReason": null, "rudeCount": 0, "rudeLimit": 3 },
  "missionUpdates": []
}
```

- `text`는 사용자가 보낸 최종 문장입니다. `inputType`이 `VOICE`면 STT 결과를 그대로 `text`와 `sttRawText`로 보내고, 마이크를 쓸 수 없어 텍스트로 입력하면 `TEXT`로 보냅니다.
- 영어, 한국어, 한영 혼용, 미완성 발화를 모두 받습니다. 최대 길이는 서버 설정값이며 넘으면 `VALIDATION_FAILED`입니다.
- 추천 단어(`suggestWords`)는 NPC 응답과 함께 만들어 대사 아래에 보여줍니다.
- 같은 `requestId`로 다시 보내면 턴을 새로 만들지 않고 이전 응답을 그대로 돌려줍니다(`turns.request_id`). 화면 19의 [다시 시도]가 이 규칙을 씁니다.
- TTS가 실패해도 응답은 성공으로 돌려주고 `speech.status`를 `FAILED`로 표시합니다. 클라이언트는 자막을 보여주고 CONV-07로 다시 시도합니다.
- AI가 미션 완료나 대화 종료를 제안하면 서버가 검증해 확정합니다(`ai_candidates`). 확정된 결과만 `missionUpdates`와 `conversation.status`에 반영합니다. 한 턴에서 여러 미션이 함께 완료될 수 있습니다.
- 서버는 한 턴에서 무례 판정과 의도·번역 판정을 같은 문장으로 동시에 요청하고 둘 다 기다린 뒤 NPC 대답을 만듭니다.

**학습자 말 번역** — `userTurn.translation`은 학습자 말의 영어 번역 `{ "english": "Medium size, please." }`입니다. 번역이 있고 무례로 판정되지 않은 턴에만 담고, 그 밖에는 `null`입니다. 한국어나 한영 혼용으로 말했을 때 "영어로는 이렇게 말해요" 카드에 씁니다. 표시 위치는 Unity와 정합니다.

**무례 판정** — 학습자 말이 무례한지(욕설·위협·모욕·조롱·하대) 턴마다 판정하고, 대화 하나 안에서 셉니다.

| 판정 | 이번 턴 | NPC 대답 | 대화 |
| --- | --- | --- | --- |
| 무례, `rudeCount`가 `rudeLimit`보다 작음 | `rude: true`, `rudeCount` +1. 미션·대화 상태에 반영하지 않음 | 무례하다고 차분하게 지적 | 계속 |
| 무례, `rudeCount`가 `rudeLimit`에 닿음 | 위와 같음 | 더는 이야기하지 않겠다고 말함 | `ENDED_BY_NPC`, `endReason: RUDE` |
| 무례 아님 | `rude: false` | 평소대로 | 계속 |
| 판정 실패 | `rude: false`. 무례로 세지 않고 평소대로 처리 | 평소대로 | 계속 |

- `rudeLimit`은 서버 설정값(3)입니다. 새 대화는 0부터 셉니다.
- 판정 결과와 실패 이유는 `turns.guard_abusive`, `turns.guard_error_code`에 남깁니다. 판정 실패를 무례나 무례 아님으로 바꿔 저장하지 않습니다.
- NPC가 먼저 잘못한 장면(정당방위)의 예외는 사건 장면을 만들 때 정합니다(6절).

```json
"missionUpdates": [
  { "userMissionId": 12, "title": "카페에서 음료 주문하기", "status": "DONE" },
  { "userMissionId": 13, "title": "카페 직원에게 추천 메뉴 묻기", "status": "DONE" }
]
```

대화 도중 토큰이 떨어지면(1.4) AI를 호출하지 않고 NPC의 작별 대사로 대화를 끝냅니다. 오류가 아니라 정상 응답입니다.

```json
{
  "userTurn": {
    "turnId": 110,
    "seq": 10,
    "speaker": "USER",
    "text": "Can I also get a muffin?",
    "rude": false,
    "translation": null
  },
  "npcTurn": {
    "turnId": 111,
    "seq": 11,
    "speaker": "NPC",
    "text": "Oh, something urgent just came up. Let's talk next time!",
    "suggestWords": null,
    "speech": { "status": "READY", "url": "/api/v1/turns/111/speech" }
  },
  "conversation": {
    "status": "ENDED_BY_NPC",
    "endReason": "QUOTA_EXHAUSTED",
    "rudeCount": 0,
    "rudeLimit": 3
  },
  "missionUpdates": []
}
```

- 작별 대사와 음성은 NPC마다 미리 만들어 둡니다(`npcs.farewell_line`, `npcs.farewell_audio_key`). 클라이언트는 대사를 보여준 뒤 CONV-08로 피드백을 받습니다.

무례한 말이 `rudeLimit`만큼 쌓이면 NPC가 대화를 끝냅니다. 이것도 오류가 아니라 정상 응답입니다.

```json
{
  "userTurn": { "turnId": 120, "seq": 6, "speaker": "USER", "text": "...", "rude": true, "translation": null },
  "npcTurn": {
    "turnId": 121,
    "seq": 7,
    "speaker": "NPC",
    "text": "I don't want to talk like this anymore. Please leave.",
    "suggestWords": null,
    "speech": { "status": "READY", "url": "/api/v1/turns/121/speech" }
  },
  "conversation": { "status": "ENDED_BY_NPC", "endReason": "RUDE", "rudeCount": 3, "rudeLimit": 3 },
  "missionUpdates": []
}
```

- `endReason`(`conversations.end_reason`)은 `NPC_ENDED`(AI가 제안한 자연스러운 종료), `QUOTA_EXHAUSTED`, `RUDE`(무례 누적)입니다. 대화가 `ACTIVE`가 아니면 `CONVERSATION_NOT_ACTIVE`입니다.

#### CONV-04 `POST /api/v1/turns/{turnId}/meaning`

```json
// 요청
{ "requestId": "..." }

// 응답 200
{
  "turnId": 105,
  "meaningKo": "좋아요! 매장에서 드실 건가요, 가져가실 건가요?",
  "glossary": [{ "text": "to go", "meaningKo": "포장" }]
}
```

- 화면 15의 [뜻 보기]입니다. NPC 턴에만 요청할 수 있습니다.
- 한 번 만든 뜻은 저장하고(`turns.meaning_ko`, `turns.glossary`) 다시 요청하면 AI를 호출하지 않습니다.

#### CONV-05 `POST /api/v1/turns/{turnId}/hint`

```json
// 요청
{ "requestId": "..." }

// 응답 200
{ "turnId": 105, "hintText": "For here, please." }
```

- 화면 15의 [힌트]를 누르면 추천 단어 아래에 추천 문장을 보여줍니다. 사용자가 답해야 하는 NPC 턴에 요청합니다.
- 만든 문장은 저장해 다시 쓰고(`turns.hint_text`), 처음 열면 `hint_opened_at`을 기록합니다.
- 자유 대화의 힌트는 추천 단어·추천 문장 2단계입니다. 요구사항명세서의 3단계 힌트(키워드 → 문장 시작 → 예문)와 다릅니다(5.2).

#### CONV-06·07 NPC 음성

- CONV-06 `GET /api/v1/turns/{turnId}/speech`는 만들어 둔 음성 파일을 돌려줍니다. [다시 듣기]는 이 API를 쓰며 TTS를 새로 만들지 않습니다.
- CONV-07 `POST /api/v1/turns/{turnId}/speech`는 TTS가 실패했을 때 `{ "requestId": "..." }`로 다시 만듭니다. 이미 음성이 있으면 새로 만들지 않고 CONV-06 주소를 돌려줍니다.
- NPC 음성 파일 키는 `turns.npc_audio_key`에 저장합니다. 앱 Compose는 환경별 영구 볼륨을 백엔드의 `/app/data/npc-audio`에 연결하며, 저장 위치와 배포 시 보존 기준은 [NPC 음성 저장 운영 문서](../npc-audio-storage.md)를 따릅니다. 보관 기간·자동 삭제·용량 상한은 `[미정]`입니다.
- 내가 말한 문장을 TTS로 다시 들려주는 기능은 필요성을 함께 검토합니다(6절).

#### CONV-08 `POST /api/v1/conversations/{conversationId}/end`

```json
// 요청
{ "requestId": "...", "skipFeedback": false }

// 응답 200
{
  "conversationId": 58,
  "status": "ENDED_BY_USER",
  "endReason": "USER_ENDED",
  "endedAt": "2026-10-21T14:05:00+09:00",
  "npc": { "npcCode": "CAFE_STAFF", "name": "Mia", "roleLabel": "카페 직원" },
  "sceneCode": "CAFE",
  "userTurnCount": 4,
  "feedback": {
    "status": "DONE",
    "summary": "아이스 라떼 주문에 성공했어요",
    "reviews": {
      "good": ["원하는 메뉴와 사이즈를 분명하게 말했어요."],
      "improve": ["I want 대신 I'd like를 쓰면 더 공손해요."]
    },
    "suggestion": { "userText": "I want 아이스 라떼.", "betterText": "I'd like an iced latte, please." },
    "errorCode": null
  },
  "completedMissions": [
    { "userMissionId": 12, "title": "카페에서 음료 주문하기" },
    { "userMissionId": 13, "title": "카페 직원에게 추천 메뉴 묻기" }
  ]
}
```

- 화면 16의 짧은 피드백을 그립니다. "Mia · 카페 · 발화 4번"은 `npc`, `sceneCode`, `userTurnCount`, "미션 완료"는 `completedMissions`, 개선 표현 상자는 `suggestion`입니다.
- `summary`는 `conversation_feedback.summary`, `reviews`와 `suggestion`은 `conversation_feedback.reviews` JSON(`{ good, improve, suggestion }`)에 저장합니다.
- 피드백은 대화를 시작할 때 예약한 토큰으로 만듭니다(1.4). 토큰이 떨어져 NPC가 대화를 끝낸 경우에도 피드백을 받습니다.
- [대화 종료 (Esc)]와 오류 화면의 [대화 종료]에서 호출합니다. 어떤 오류 상태에서도 종료할 수 있습니다.
- NPC가 대화를 끝낸 경우(`ENDED_BY_NPC`)에도 피드백을 받으려면 호출합니다. 이때 상태는 바꾸지 않고 피드백만 만듭니다.
- `skipFeedback: true`면 피드백을 만들지 않고 `feedback.status`를 `SKIPPED`로 기록하며 예약한 토큰을 풉니다. 와이어프레임에는 피드백 건너뛰기 버튼이 없으며 배치 여부는 `[미정]`입니다.
- 피드백을 만들 수 없으면(생성 실패) 대화는 정상 종료하고 `feedback.status: FAILED`, `feedback.errorCode`에 원인 코드를 담습니다. 화면 16은 이때 가짜 피드백 대신 [다시 만들기]와 [대화 기록 보기]를 보여줍니다(FB-02, CONV-10).
- 사용자 발화가 없는 대화의 피드백 생성 여부는 `[미정]`입니다.

#### CONV-09·10 지난 대화

- CONV-09 `GET /api/v1/conversations?npcCode=&cursor=&size=`는 화면 18의 지난 대화 탭 목록입니다. 최근 시작 순으로 `{ conversationId, npc, sceneCode, status, endReason, startedAt, endedAt, userTurnCount, feedbackStatus }`를 돌려줍니다.
- CONV-10은 대화 정보와 모든 턴(`seq` 순서)의 텍스트, 저장된 뜻·추천 단어·추천 문장을 돌려줍니다. 화면 15의 텍스트 대화 기록과 화면 16의 [대화 기록 보기]가 이 형식을 씁니다. 저장된 결과만 조회하므로 AI 사용량이 소진돼도 이용할 수 있습니다.

#### CONV-11 `GET /api/v1/conversations/active`

- 진행 중인 대화가 있으면 CONV-10과 같은 형식, 없으면 `204 No Content`입니다. 대화 중 새로고침했을 때 화면 15의 대화 기록을 다시 그립니다.
- 새로고침 뒤 이어서 진행할지 종료 처리할지는 `[미정]`입니다. 일정 시간 동안 요청이 없는 대화는 서버가 `EXPIRED`로 닫고 예약한 토큰을 풉니다. 시간 값은 서버 설정값입니다.

### 4.7 피드백

#### FB-01 `GET /api/v1/conversations/{conversationId}/feedback`

- CONV-08 응답의 `npc`, `sceneCode`, `userTurnCount`, `feedback`, `completedMissions`에 `detailedFeedback`을 더해 돌려줍니다. AI를 호출하지 않습니다.
- 상세 평가가 아직 없으면 `detailedFeedback`은 빈 배열입니다. 항목 형식은 FB-03과 같습니다.

#### FB-02 `POST /api/v1/conversations/{conversationId}/feedback`

- 화면 16의 [다시 만들기]입니다. `{ "requestId": "..." }`로 짧은 피드백을 다시 만듭니다. `feedback.status`가 `FAILED`일 때만 만들고, `DONE`이면 저장된 피드백을 돌려줍니다.
- 예약해 둔 토큰이 남아 있으면 그것으로 만들고, 없으면 일반 AI 사용량 검사를 거칩니다.

#### FB-03 `POST /api/v1/conversations/{conversationId}/detailed-feedback`

```json
// 요청
{ "requestId": "..." }

// 응답 200
{
  "conversationId": 58,
  "items": [
    {
      "turnFeedbackId": 33,
      "turnId": 102,
      "userText": "I want 아이스 라떼.",
      "betterText": "I'd like an iced latte, please.",
      "reason": "주문할 때는 I'd like가 더 공손해요. 아이스 라떼는 iced latte라고 해요.",
      "idiom": null,
      "hasModelSpeech": false
    },
    {
      "turnFeedbackId": 34,
      "turnId": 104,
      "userText": "Medium size, please.",
      "betterText": "A medium, please.",
      "reason": "사이즈만 말할 때는 관사와 함께 짧게 말해요.",
      "idiom": { "text": "for here or to go", "meaningKo": "매장에서 드실 건가요, 가져가실 건가요?" },
      "hasModelSpeech": false
    }
  ]
}
```

- 화면 16의 [상세 보기]에서 처음 호출하고 화면 17을 그립니다. 사용자 턴마다 한 항목이며, 이미 만들었으면 다시 만들지 않고 저장된 평가를 돌려줍니다.
- STT 오인식을 발음 오류로 평가하지 않습니다. 평가 기준은 사용자가 보낸 `text`입니다.
- `idiom.text`는 `turn_feedback.idiom`, `idiom.meaningKo`는 `turn_feedback.idiom_meaning_ko`입니다.
- 상세 평가를 대화 종료 때 함께 만들지 상세 보기를 누를 때 만들지는 `[미정]`이며, 초안은 상세 보기를 누를 때 만듭니다.

#### FB-04 `POST /api/v1/turn-feedback/{turnFeedbackId}/model-speech`

- 화면 17의 [모범 음성 듣기]입니다. `{ "requestId": "..." }`로 `betterText`의 모범 음성을 만들고 `{ "url": "..." }`를 돌려줍니다.
- 한 번 만든 음성은 `turn_feedback.model_audio_key`로 다시 사용하며 새로 만들지 않습니다.

### 4.8 단어장

MVP는 **단어 하나만** 저장합니다. 숙어·문장은 저장하지 않으며 숙어는 이후 검토합니다(7절). 단어장에는 단어의 뜻과 함께, 그 단어를 저장한 **문장 전체와 문장 뜻**을 보여줍니다.

예: Mia가 "Would you like an iced latte?"라고 했고 사용자가 `latte`를 저장하면, 단어장에는 `latte` = "라떼"와 함께 "Would you like an iced latte?" = "아이스 라떼 드릴까요?"가 보입니다.

#### VOCAB-01 `GET /api/v1/vocab`

`?includeMemorized=false&cursor=&size=`

```json
{
  "counts": { "all": 12 },
  "items": [
    {
      "userVocabId": 21,
      "vocab": {
        "vocabId": 77,
        "type": "WORD",
        "text": "latte",
        "meaningKo": "라떼"
      },
      "context": {
        "text": "Would you like an iced latte?",
        "meaningKo": "아이스 라떼 드릴까요?"
      },
      "memo": "주문할 때 쓰기",
      "memorized": false,
      "savedAt": "2026-10-21T14:06:00+09:00",
      "source": { "sourceType": "NPC_LINE", "npcName": "Mia", "conversationId": 58 }
    }
  ],
  "nextCursor": null
}
```

- "외운 표현도 보기"는 `includeMemorized`입니다. 목록은 최근 저장순입니다. `counts.all`은 현재 `includeMemorized` 조건을 적용한 개수입니다.
- 저장하는 것이 단어뿐이라 화면 18의 유형 필터(단어·숙어·문장)는 MVP에서 보여주지 않습니다. `vocabularies.type`은 숙어를 검토할 때를 위해 그대로 둡니다.
- `vocab.meaningKo`는 단어 하나의 뜻(`vocabularies.meaning_ko`), `context`는 저장할 때의 문장과 문장 뜻(`user_vocab.context_text`, `context_meaning_ko`)입니다. 같은 단어라도 사용자마다 저장한 문장이 다르므로 `user_vocab`에 둡니다.
- 상세의 "Mia와의 대화" 같은 출처 표시는 `source`로 그립니다. 출처 대화는 `user_vocab.source_conversation_id`, 출처 NPC는 그 대화의 NPC입니다. 사물에서 저장한 단어는 `npcName`, `conversationId`가 `null`입니다.
- `memo`(`user_vocab.memo`)는 사용자가 남긴 메모입니다. 뜻은 수정하지 않습니다.

#### VOCAB-02 `POST /api/v1/vocab`

저장 출처마다 요청 형식이 다릅니다. `text`는 저장할 단어 하나입니다.

```json
// 화면 08: 사물 카드에서 저장
{ "requestId": "...", "sourceType": "OBJECT", "objectKey": "sofa" }

// 화면 15: NPC 대사 뜻 보기(CONV-04)의 단어 풀이에서 저장
{ "requestId": "...", "sourceType": "NPC_LINE", "turnId": 105, "text": "latte" }

// 화면 16·17: 피드백의 단어장 후보에서 저장
{ "requestId": "...", "sourceType": "FEEDBACK", "conversationId": 58, "text": "order" }
```

저장할 수 있는 단어와 뜻·문장을 가져오는 곳은 출처마다 정해져 있습니다. 저장할 때 AI나 외부 번역 API를 호출하지 않습니다.

| 출처 | 저장할 수 있는 단어 | 단어 뜻 | 문장 · 문장 뜻 |
| --- | --- | --- | --- |
| `OBJECT` | 사물 카드의 단어 | `vocabularies.meaning_ko` (시드) | 사물 카드 예문 `example` · `example_ko` (없으면 `null`) |
| `NPC_LINE` | 그 턴의 CONV-04 단어 풀이(`glossary`) 중 한 단어인 것 | `glossary[].meaningKo` | NPC 대사 전문 · CONV-04 대사 뜻 |
| `FEEDBACK` | 그 대화 피드백의 단어장 후보(`vocab_candidates`) 중 `kind: word`인 것 | 후보의 `meaning_ko` | 후보의 `example_en` · `example_ko` |

- 표에 없는 단어, 두 단어 이상인 표현, 아직 뜻 보기(CONV-04)를 하지 않은 대사의 단어는 저장하지 않고 `422 VOCAB_SOURCE_MISMATCH`를 돌려줍니다. 클라이언트는 표의 단어에만 저장 버튼을 보여줍니다.
- 새로 저장하면 `201`, 이미 저장한 단어면 처음 저장한 항목(문장 포함)을 그대로 `200`으로 돌려줍니다. 응답 형식은 VOCAB-01의 항목에 `created` 필드를 더한 것입니다.
- 사전에 없는 단어는 `vocabularies`에 `type: WORD`로 새로 만듭니다(`source`는 `NPC` 또는 `FEEDBACK`). 이미 있는 단어는 기존 뜻을 그대로 씁니다.
- `NPC_LINE`은 `source_turn_id`에 `turnId`를, `source_conversation_id`에 그 턴의 대화를 기록합니다. `FEEDBACK`은 `source_conversation_id`에 요청의 `conversationId`를 기록합니다.
- 이 규칙은 대화 쪽이 CONV-04의 단어 풀이와 피드백의 단어장 후보를 저장해 두는 것을 전제로 합니다.

#### VOCAB-03·04 메모·외웠어요·삭제

- VOCAB-03 `PATCH /api/v1/vocab/{userVocabId}`는 `{ "memo": "...", "memorized": true }` 중 보낸 필드만 바꿉니다. 화면 18의 [메모], [외웠어요]입니다. `memorized: true`면 `memorized_at`을 기록하고, `false`면 지웁니다. 메모는 최대 길이를 서버 설정값으로 둡니다.
- VOCAB-04는 사용자 단어장에서만 지우고 공용 어휘(`vocabularies`)는 지우지 않습니다. 성공하면 `204 No Content`입니다.

### 4.9 AI 이용 상태

#### AI-01 `GET /api/v1/ai/status`

```json
{
  "status": "AI_AVAILABLE",
  "capabilities": {
    "canStartConversation": true,
    "canUseSpeech": true,
    "canGenerateFeedback": true
  },
  "quota": null,
  "resumeAt": null
}
```

| `status` | 의미 |
| --- | --- |
| `AI_AVAILABLE` | 새 AI 요청 가능 |
| `AI_EXHAUSTED` | 사용자 한도 소진 |
| `AI_TEMPORARILY_UNAVAILABLE` | 외부 AI 장애 |
| `AI_RESTRICTED` | 운영·안전상 제한 |

- HUD의 "AI 사용 가능" 표시와 화면 19의 사용량 소진 안내에 씁니다. `canStartConversation`은 한 턴과 피드백에 쓸 토큰이 남아 있는지(1.4)를 반영합니다.
- 클라이언트는 접속할 때(BOOT-01), 대화가 토큰 소진으로 끝났을 때, AI 요청이 거절됐을 때 상태를 갱신합니다. 소진 상태에서는 AI가 필요한 버튼을 비활성화하고, 탐험·사물 카드·단어장·지난 기록은 계속 제공합니다.
- `quota`는 한도 정책이 정해진 경우에만 `{ "periodStart": "2026-10-21", "usedTokens": 1200, "remainingTokens": 8800 }` 형식으로 채웁니다. 화면 19의 "갱신 시점"은 `resumeAt`이며, 정해지지 않았으면 `[정책 미정]`으로 표시합니다.

## 5. ERD와의 관계

### 5.1 이 명세에 맞춰 ERD v3.1~v3.3에 반영한 사항

| 항목 | 관련 API | ERD 변경 |
| --- | --- | --- |
| 튜토리얼 기록 | TUT-01, 02 | 튜토리얼은 `users.onboarding_step`만으로 관리합니다. `conversations.mode`, `missions.type`을 지우고 `user_missions`의 튜토리얼 행(`mission_date` NULL)을 없앴습니다. |
| 미션 자동 시작 | MIS-01 | `user_missions.accepted_at`을 지우고, `status`는 `RUN`·`DONE`만 씁니다. `mission_date`는 다시 필수입니다. |
| 중요도 삭제 | VOCAB-01, 02 | `user_vocab.importance`와 중요도 인덱스를 지우고, 최근 저장순 인덱스(`user_id`, `saved_at DESC`)를 두었습니다. |
| 뜻 수정 → 메모 | VOCAB-03 | `user_vocab.custom_meaning`을 `memo`로 바꿨습니다. |
| 예문 뜻 | WORLD-02 | `vocabularies.example_ko`를 추가했습니다. |
| 발음 음성 | WORLD-04 | `vocabularies.pronunciation_audio_key`를 추가했습니다. |
| 행인 첫 말·작별 대사 | WORLD-02, CONV-03 | `npcs.approach_line`, `approach_audio_key`, `farewell_line`, `farewell_audio_key`를 추가했습니다. |
| 대화 종료 이유 | CONV-03, 08 | `conversations.end_reason`(`USER_ENDED`, `NPC_ENDED`, `QUOTA_EXHAUSTED`, `EXPIRED`)을 추가했습니다. |
| 숙어 뜻 | FB-03 | `turn_feedback.idiom_meaning_ko`를 추가했습니다. |
| 짧은 피드백 개선 표현 | CONV-08 | `conversation_feedback.reviews` JSON을 `{ good, improve, suggestion }` 구조로 정했습니다. |
| NPC 음성 보관 | CONV-06, 07 | `turns.npc_audio_key`를 추가했습니다. 보관 기간은 `[미정]`입니다. |
| 무례 판정 (v3.2) | CONV-03 | `turns.guard_abusive`(null이면 판정 실패), `turns.guard_error_code`, `conversations.rude_count`를 추가하고 `end_reason`에 `RUDE`를 더했습니다. |
| 학습자 말 번역 (v3.2) | CONV-03 | `turns.translation_en`을 추가했습니다. 번역 카드와 다음 요청의 대화 기록, 피드백에 씁니다. |
| AI 호출 기록 단위 (v3.2) | 1.4, CONV-02·03 | `ai_requests.kind`를 기능 단위(`STT`, `GUARD`, `JUDGE`, `NPC`, `TTS`, `HINT`, `MEANING`, `FEEDBACK`)로 나눴습니다. 한 턴의 여러 AI 호출이 같은 `request_id`를 써도 겹치지 않습니다. |
| STT 사용량 (v3.2) | 1.4, CONV-02 | `ai_requests.audio_ms`를 추가했습니다. 한도는 초당 토큰으로 환산해 차감합니다. |
| NPC 역할 이름 (v3.2) | WORLD-02, CONV | `npcs.role_label`을 추가했습니다. `persona`는 AI 프롬프트용이라 화면 표시용 값을 따로 둡니다. |
| 단어장 출처 대화 (v3.2) | VOCAB-01, 02 | `user_vocab.source_conversation_id`를 추가했습니다. 짧은 피드백(화면 16)은 턴 없이 대화 단위로 저장하므로 `source_turn_id`만으로는 출처 대화를 찾을 수 없습니다. |
| 단어장 문장 (v3.2) | VOCAB-01, 02 | `user_vocab.context_text`, `context_meaning_ko`를 추가했습니다. 단어를 저장한 문장과 문장 뜻입니다. |
| 닉네임 중복 불가 (v3.3) | AUTH-04, 06 | `users.nickname`에 unique 제약을 추가했습니다. 확인 API를 거쳐도 동시에 가입하면 같은 닉네임이 들어갈 수 있어 DB 제약으로 막습니다. |
| 미션 코드 (v3.3) | MIS-01 | `missions.code`를 추가했습니다. 시드를 반복 실행할 때 같은 미션을 찾는 고정 키이며, Unity 미션 콘텐츠의 키와 같습니다. |
| 호텔 개방 (v3.3) | WORLD-01·02, MIS-01 | 기능명세서 F-21·F-22에 맞춰 호텔을 열고(`scenes.is_open = true`) 호텔 직원 NPC(`HOTEL_STAFF`)와 호텔 미션을 시드에 추가했습니다. 컬럼 변경은 없습니다. |

### 5.2 아직 정할 사항

| 항목 | 관련 API | 내용 |
| --- | --- | --- |
| 예문 표시 시점 | WORLD-02 | 사물 카드에 예문을 저장 전부터 보여줄지(와이어프레임), 저장 후에만 보여줄지 정합니다. |
| 사물 저장 출처 | VOCAB-01 | 사물에서 저장한 표현은 어느 사물에서 저장했는지 기록하지 않습니다. 화면에 사물 이름이 필요하면 `user_vocab`에 사물 ID를 추가합니다. |
| 레벨 테스트 문항 | ONB-03~05 | 문항 원본 테이블이 없어 서버 설정 데이터로 둡니다. MVP는 en-US 예시 선택형 5문항입니다. |
| 힌트 단계 | CONV-05 | 자유 대화 힌트는 추천 단어·추천 문장 2단계로 요구사항명세서의 3단계와 다릅니다. 요구사항명세서와 다른 점으로 기록합니다. |

## 6. 정할 사항

| 항목 | 관련 API | 선택지 |
| --- | --- | --- |
| 한 턴 예상 토큰·피드백 예약량·한도·갱신 주기·STT 초당 토큰 환산 비율 | CONV-01, 02, 03, 08, AI-01 | 서버 설정값으로 두고 정책 결정 후 반영 |
| 작별 대사 내용 | CONV-03 | NPC별 대사와 음성 |
| 번역 카드 표시 위치 | CONV-03 | 화면 15의 어디에 "영어로는 이렇게 말해요" 카드를 둘지 Unity와 정함 |
| 무례 판정의 정당방위 예외 | CONV-03 | NPC가 먼저 잘못한 사건 장면에서는 무례로 세지 않음. 사건 장면을 만들 때 장소 데이터에 표시 |
| 튜토리얼 다시 보기 | TUT-02 | 건너뛴 사용자에게 제공 여부 |
| 전사 직접 수정 | CONV-02, 03 | 다시 녹음만(초안) / 수정 허용. 요구사항명세서 REQ-12는 텍스트 수정을 포함합니다. |
| 내 발화 다시 듣기 | CONV-06 | 내가 보낸 문장을 TTS로 들려줄지 |
| 대화 턴 통신 방식 | CONV-03 | REST(초안) / 스트리밍 |
| 직원 첫 인사 생성 방식 | CONV-01 | 미리 작성 / AI 생성 |
| NPC 음성 응답 방식 | CONV-03, 06 | 파일 주소(초안) / 본문 포함 |
| 음성 보관 동의·기간 | CONV-02, AUTH-04 | 보관 안 함 / 동의 시 기간 한정 보관 |
| 상세 평가 생성 시점 | CONV-08, FB-03 | 종료 시 / 상세 보기 선택 시(초안) |
| 피드백 건너뛰기 버튼 | CONV-08 | 배치 여부 |
| 사용자 발화 없는 대화의 피드백 | CONV-08 | 만들지 않음 / 만듦 |
| 대화 중 새로고침 | CONV-11 | 이어서 진행 / 종료 처리 |
| NPC 대사 저장 버튼 | VOCAB-02 | 화면 15에 배치 여부 |
| 미션 완료 행동 | MIS-01 | 대화만(초안) / 장소 방문·단어 저장 등 추가 |
| 레벨 테스트 방식·문항 건너뛰기 | ONB-03~07 | 선택형 / 말하기 포함, 채점 기준, 문항 단위 건너뛰기 여부. MVP는 예시 문항·초급 고정으로 흐름만 두고 [진행하기]를 막아 둡니다. |
| 일일 미션 보상 | MIS-01 | 보상 여부 (날짜 기준은 MVP에서 `Asia/Seoul`로 확정) |
| 맵 확장 시 사물 조회 | WORLD-02 | 주변 사물 `objectKey` 묶음 조회 `[제안]` / 서버 반경 조회 |
| 사용량 소진 HTTP 상태 코드 | 1.5 | 429 `[제안]` / 403 |
| 설정 화면 | AUTH-05 | 로그아웃, 프로필 수정, 음성 동의 변경 등 화면 설계 |

## 7. MVP에서 뺀 기능

| 기능 | 화면 | 비고 |
| --- | --- | --- |
| 미션 수락·포기 | 09, 13 | 미션은 자동 시작 |
| 미션 상세 화면(지도, 설명) | 13 | 미션을 누르거나 상세 보기를 선택했을 때 여는 화면으로 이후 설계 |
| 튜토리얼 대화의 추천 단어·힌트·대화 종료 | 11 | 대본 진행이라 필요 없음 |
| 상세 피드백의 내 음성 듣기 | 17 | 음성 보관 정책과 함께 이후 검토 |
| 상세 피드백의 다시 말하기 | 17 | 이후 검토 |
| 단어장 중요도 | 18 | 목록은 최근 저장순 |
| 숙어·문장 저장, 단어장 유형 필터 | 15~18 | MVP는 단어 하나만 저장. 숙어 저장 방식(뜻 출처, 문장 표시)은 이후 검토 |
| 회상 복습 | 18 | 요구사항명세서 REQ-17·18(정답 기록, 5회 정답 시 카드 숨김)과 다른 점으로 기록 |
| NPC 호감도 조회, 설정, 프로필 수정 | - | 와이어프레임에 화면 없음 |
