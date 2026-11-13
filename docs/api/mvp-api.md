# MVP API 명세 초안

MVP 와이어프레임(화면 01~19)의 화면 요소와 사용자 동작을 서버 API로 나눈 초안입니다. 데이터 구조는 [ERD v3.7](../erd.dbml), 수용 조건은 [요구사항명세서](../A305_기획안/04_AI용_요구사항명세서.md)를 따릅니다. 이 문서는 초안이며 팀 리뷰를 거쳐 확정합니다.

- `[미정]`은 팀 결정이 필요한 항목, `[제안]`은 작성자 제안입니다.
- MVP에서 뺀 기능은 7절에 모았습니다. NPC 호감도는 두 곳에서 조회합니다. 대화 중인 NPC의 호감도는 대화 시작·발화 전송 응답(CONV-01·03의 `conversation.affinity`)에 실어 대화 화면에 보여 주고, 끝까지 대화한 NPC의 호감도는 휴대폰 [이웃]의 마을 NPC 목록(WORLD-08)으로 보여 줍니다. 호감도 설정과 NPC 프로필 수정은 MVP에서 뺐습니다. 집에서 고르는 화면 테마(USER-01), 마이페이지의 닉네임 변경(USER-04), 휴대폰 [이웃](WORLD-08)은 와이어프레임 밖에서 추가한 기능입니다.
- 대화 턴은 REST로 작성했습니다. 스트리밍을 선택하면 요청·응답의 의미와 식별자는 유지하고 전송 방식만 바꿉니다.
- 종료한 대화의 표현으로 다시 연습할 상황을 보관하는 API는 [상황 복습장](review-workbook.md)에 정리합니다.

## 1. 공통 규칙

### 1.1 형식

- 모든 API는 `/api/v1` 아래에 있으며, 이 문서의 경로는 `/api/v1`을 포함한 전체 경로로 적습니다.
- 요청·응답 본문은 UTF-8 JSON이며 필드명은 camelCase입니다. DB 컬럼(snake_case)과의 변환은 백엔드 DTO에서 합니다.
- 시각은 시간대를 포함한 ISO 8601(`2026-10-21T14:00:00+09:00`), 날짜는 `YYYY-MM-DD`입니다.
- 장면·NPC·사물 같은 마스터 데이터는 환경마다 달라지지 않는 코드로 가리킵니다: `sceneCode`(`scenes.code`), `npcCode`(`npcs.code`), `wordKey`(`vocabularies.word_key`, Unity 공용 단어 키). 사용자 데이터는 서버가 발급한 숫자 ID로 가리킵니다.
- 목록은 커서 방식으로 나눕니다. 요청은 `?cursor=&size=`, 응답은 `{ "items": [...], "nextCursor": "..." }`이며 마지막 페이지의 `nextCursor`는 `null`입니다.

### 1.2 인증

- 소셜 로그인 또는 게스트로 시작하기(AUTH-07)로 사용자를 식별합니다(`users.provider`, `users.provider_uid`). 소셜 로그인 제공자는 Google 하나이며, 경로의 `{provider}`는 `google`입니다. 게스트는 `provider`가 `GUEST`입니다.
- 로그인 상태는 서버 세션에 두고 HttpOnly 세션 쿠키로 유지합니다. nginx로 Unity Web 빌드와 API를 같은 출처에서 제공하며, 세션 쿠키는 `SameSite=Lax`입니다.
- 세션은 요청이 24시간 없으면 끝나고, 쿠키도 24시간 유지돼 브라우저를 닫았다 열어도 이어집니다. 게스트(AUTH-07)는 2시간이며, 가입해 회원이 되면(AUTH-04) 24시간입니다. 서버를 다시 시작(배포)하면 로그인이 풀립니다. 끝난 뒤의 요청은 `401 UNAUTHENTICATED`입니다.
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
- 튜토리얼·레벨 테스트 음성 인식(CONV-02 `purpose: TUTORIAL`·`LEVEL_TEST`)은 사용자 한도에서 차감하지 않고 비용 근거만 기록합니다.
- 한 턴의 예상 토큰, 피드백 예약량, 한도 수치, 갱신 주기, STT의 초당 토큰 환산 비율은 서버 설정값이며 `[미정]`입니다.
- 게스트(AUTH-07)는 이와 별도로 계정마다 시작할 수 있는 NPC 대화 수를 제한합니다(CONV-01).

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

AI 제한 오류는 화면 19의 "계속 쓸 수 있어요" 목록에 쓸 기능 목록을 함께 돌려줍니다. `resumeAt`이 `null`이면 갱신 시점이 정책상 정해지지 않았다는 뜻이며 영구 차단이 아닙니다. 다만 게스트의 대화 횟수 제한(CONV-01)은 다시 채워지지 않습니다.

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
| `UNAUTHENTICATED` | 401 | 로그인 필요. 로그인 세션은 서버 메모리에 있어 서버 재배포나 오래 쉬면 사라집니다. 장소 화면에서 이 응답을 받으면 클라이언트는 "접속이 끊겼어요" 안내를 띄우고 처음 화면(로그인)으로 돌려보냅니다. |
| `ADMIN_LOGIN_FAILED` | 401 | 운영자 어드민 로그인(`POST /api/v1/admin/login`)의 아이디나 비밀번호가 틀림. 무엇이 틀렸는지는 알리지 않음. [운영자 어드민](../admin.md) 참고 |
| `SIGNUP_REQUIRED` | 403 | 가입 정보 미제출 |
| `CSRF_HEADER_REQUIRED` | 403 | 상태를 바꾸는 요청에 `X-Requested-With` 헤더 없음 |
| `VOICE_CONSENT_REQUIRED` | 403 | 음성 처리(`VOICE`)에 동의하지 않은 사용자의 음성 인식(CONV-02). 글자로 입력하거나 휴대폰 › 설정 › 선택 동의에서 동의를 켭니다. |
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
| `MISSION_NOT_READY` | 409 | 첫날 미션에서 카페 주문·대화 종료·현재 장소 등 휴식 조건 미충족 |
| `REVIEW_IN_PROGRESS` | 409 | 연습 중인 카드는 종료한 뒤 삭제 |
| `REVIEW_NOT_READY` | 409 | 대화가 진행 중이거나 DONE 피드백의 서로 다른 복습 표현이 2개 미만 |
| `AUDIO_TOO_LARGE` | 413 | 녹음 크기 초과(3MB) |
| `AUDIO_FORMAT_UNSUPPORTED` | 415 | 지원하지 않는 녹음 형식 |
| `STT_NO_SPEECH` | 422 | 무음 또는 인식 실패 |
| `VOCAB_SOURCE_MISMATCH` | 422 | 저장하려는 단어가 출처(사물 카드, 대사 단어 풀이, 피드백 단어장 후보·상황 표현·고친 문장)에 없음 |
| `AI_QUOTA_EXHAUSTED` | 429 | 사용자 AI 한도 소진, 게스트 대화 횟수 소진 |
| `AI_RATE_LIMITED` | 429 | 요청 속도 제한 |
| `ADMIN_LOGIN_LOCKED` | 429 | 운영자 어드민 아이디로 연속 5번 틀려 15분 동안 로그인을 막음. [운영자 어드민](../admin.md) 참고 |
| `AI_RESPONSE_INVALID` | 502 | 제한 횟수만큼 재시도해도 AI 응답 형식 오류 |
| `AI_TEMPORARILY_UNAVAILABLE` | 503 | 외부 AI 장애·시간 초과 |
| `INTERNAL_ERROR` | 500 | 그 밖의 서버 오류 |

- 지원하지 않는 메서드(405)·형식(415)처럼 표에 없는 요청 오류는 그 HTTP 상태와 상태 이름을 `code`로 돌려줍니다(예: `METHOD_NOT_ALLOWED`). 클라이언트 구현 오류이므로 화면에 따로 안내하지 않습니다.
- `INTERNAL_ERROR`의 `message`에는 내부 오류 내용을 담지 않습니다. 원인은 서버 로그에만 남깁니다.
- 같은 `code`라도 상황에 맞는 안내가 따로 있으면 `message`가 다를 수 있습니다(예: 게스트 대화 횟수 소진, CONV-01). 클라이언트는 `code`로 처리를 나누고 `message`는 그대로 보여 줍니다.
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
| | [게스트로 시작하기] → 캐릭터 기본형(F/M) 선택 → [시작하기]. 02~05를 거치지 않고 튜토리얼로 갑니다. 선택 화면에 대화할 수 있는 횟수를 보여 줍니다. | AUTH-08, AUTH-07 |
| | 게스트 세션이 남은 채 다시 접속(`user.guest: true`)하면 마지막 장소로 가기 전에 [Google로 가입하고 이어 하기]·[게스트로 계속하기]를 고르게 합니다. | BOOT-01, AUTH-01 |
| | 게스트 세션에서 이미 가입한 Google 계정으로 로그인해 돌아오면(`?existingAccount=true`) 게스트 기록을 합치지 않고 그 계정으로 들어왔다고 알립니다. | AUTH-02 |
| 02 회원가입·로그인 | [Google로 계속하기] | AUTH-01, AUTH-02 |
| | 처음 가입 1단계(약관 동의): 약관 목록·버전, [전체 동의], 필수 약관에 모두 동의하면 [다음]. 화면 안에서만 다음 단계로 넘어가고 서버를 부르지 않습니다. | AUTH-03 |
| | 처음 가입 2단계(가입 정보): 닉네임 [중복 확인] | AUTH-06 |
| | 2단계: 닉네임, 캐릭터 기본형(F/M) → [가입하기]. 1단계에서 고른 동의를 함께 보냅니다. [뒤로]는 동의를 유지한 채 1단계로 돌아갑니다. | AUTH-04 |
| | 게스트가 가입: 캐릭터는 게스트 때 것을 쓰므로 고르지 않고 닉네임·약관만 받은 뒤, 03~05 없이 게스트 때 단계(튜토리얼·마지막 장소)로 갑니다. | AUTH-03, AUTH-04 |
| 03 배울 언어 선택 | 언어 카드(선택 가능·준비 중) | ONB-01 |
| | [다음] | ONB-02 |
| 04 레벨 테스트 | 첫 화면 [시작하기] | ONB-03 |
| | 첫 화면 [건너뛰기] | ONB-07 |
| | 문항, "문항 3 / N" 진행률, [다음] (응답 저장), 마지막 문항 뒤 결과 | ONB-04, ONB-05 |
| | 듣기 문항 음성 | ONB-10 |
| | 말하기 문항 녹음 → 음성 인식 | CONV-02(`purpose: LEVEL_TEST`) |
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
| | [숨기기] | WORLD-03 |
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
| | 왼쪽 기다림 카드: NPC가 건네기로 한 것의 남은 시간, 준비 알림, X로 닫기 | CONV-08 응답 `followUp`, CONV-13(새로고침·씬 이동 뒤), CONV-14(닫기) |
| | 준비되면 그 NPC가 부름 → [대화하기] | CONV-01(`initiatedBy: NPC`, 응답 `deliveredFollowUpId`) |
| 15 NPC AI 자유 대화 | 대화 기록(텍스트) | CONV-01, CONV-03 응답(새로고침 후에는 CONV-11) |
| | 녹음 → 전사 확인 → [다시 녹음] / [보내기] | CONV-02, CONV-03 |
| | 마이크를 쓸 수 없을 때 [텍스트로 입력] | CONV-03 |
| | 대사 아래 추천 단어 | CONV-12(대사를 받은 뒤 따로) |
| | [다시 듣기], [뜻 보기], [힌트] | CONV-06, CONV-04, CONV-05 |
| | [뜻 보기]로 받은 대사의 단어 풀이(단어·숙어)를 누르면 그 단어 근처 호버 카드에 뜻과 [단어장에 저장] | CONV-04 응답, VOCAB-02(`NPC_LINE`) |
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
| | 휴대폰 메뉴의 [대화 노트] 창: 장소 탭과 목록, 대화마다 [피드백 보기]로 저장된 피드백과 대화 기록, [다시 연습]으로 상황 복습 | CONV-09, FB-01, CONV-10, 상황 복습장 |
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
| AUTH-05 | `POST /api/v1/auth/logout` | 로그아웃 | - | 마이페이지 |
| AUTH-06 | `GET /api/v1/nicknames/availability` | 닉네임 사용 가능 여부 확인 | - | 02 |
| AUTH-07 | `POST /api/v1/auth/guest` | 게스트로 시작하기, 사용자 생성·로그인 | - | 01 |
| AUTH-08 | `GET /api/v1/auth/guest` | 게스트 안내(대화할 수 있는 횟수) | - | 01 |
| ONB-01 | `GET /api/v1/languages` | 학습 언어 목록 | - | 03 |
| ONB-02 | `PUT /api/v1/me/languages/{languageCode}` | 학습 언어 선택 | - | 03 |
| ONB-03 | `POST /api/v1/level-tests` | 레벨 테스트 시작, 문항 조회 | - | 04 |
| ONB-04 | `PUT /api/v1/level-tests/{testId}/answers/{itemCode}` | 문항 응답 저장 | - | 04 |
| ONB-05 | `POST /api/v1/level-tests/{testId}/complete` | 채점·추천 레벨(초급·중급), 말하기 AI 판정 | FEEDBACK | 04 |
| ONB-06 | `POST /api/v1/level-tests/{testId}/quit` | 테스트 그만하기 | - | 04 |
| ONB-07 | `POST /api/v1/level-tests/skip` | 테스트 건너뛰기 | - | 04 |
| ONB-10 | `GET /api/v1/level-tests/items/{itemCode}/speech` | 듣기 문항 음성(WAV) | TTS(문항마다 한 번) | 04 |
| ONB-08 | `GET /api/v1/me/languages/{languageCode}` | 추천 레벨과 선택 가능한 난이도 | - | 05 |
| ONB-09 | `PUT /api/v1/me/languages/{languageCode}/level` | 대화 난이도 선택 | - | 05 |
| TUT-01 | `POST /api/v1/me/tutorial/complete` | 튜토리얼 완료 | - | 12 |
| TUT-02 | `POST /api/v1/me/tutorial/skip` | 튜토리얼 건너뛰기 | - | 06 |
| TUT-03 | `POST /api/v1/me/tutorial/ben-dialogue/complete` | Ben 길 안내의 방문·관계 저장 | - | 11 |
| WORLD-01 | `GET /api/v1/scenes` | 장소 목록·개방 여부 | - | 14 |
| WORLD-02 | `GET /api/v1/scenes/{sceneCode}` | 장소의 NPC와 단어 카드 | - | 08, 14 |
| WORLD-03 | `PUT /api/v1/me/word-prefs/{wordKey}` | 단어 카드 자동 표시 끄기·다시 켜기 | - | 08 |
| WORLD-04 | `GET /api/v1/vocab/{vocabId}/pronunciation` | 미리 만든 사물 단어 발음 음성 | - | 08 |
| WORLD-05 | `PUT /api/v1/me/last-scene` | 현재 장소 저장(재접속 위치) | - | 09, 14 |
| WORLD-06 | `GET /api/v1/me/game-clock` | 사용자별 게임 시각 | - | 화면 없음(월드 장면) |
| WORLD-07 | `POST /api/v1/me/game-clock/beats` | 플레이 중 신호, 게임 시간 흐르게 하기 | - | 화면 없음(월드 장면) |
| WORLD-08 | `GET /api/v1/me/npcs` | 마을 NPC 목록(이웃 열림·정보·호감도·아는 정도) | - | 휴대폰 [이웃] |
| MIS-01 | `GET /api/v1/missions` | 오늘의 미션과 진행 상태(자동 시작) | - | 13 |
| PRC-01 | `GET /api/v1/practice-places` | 장소 연습 목록(가게 12곳 × 상황 3개, 끝낸 상황 표시) | - | 퀘스트 창 [장소 연습] |
| PRC-02 | `POST /api/v1/practice-scenarios/{scenarioId}/conversations` | 장소 연습 대화 시작, 직원 첫 대사 | LLM, TTS | 퀘스트 창 [장소 연습] → 15 |
| PRC-03 | `GET /api/v1/practice-scenarios/{scenarioId}/conversations/{conversationId}/progress` | 장소 연습 대화의 목표 진행 | - | 15 |
| CONV-01 | `POST /api/v1/conversations` | 대화 시작, NPC 첫 대사 | LLM, TTS(말걸기 대사가 없는 NPC) | 14 |
| CONV-02 | `POST /api/v1/speech/transcriptions` | 녹음 전사(STT) | STT | 11, 15 |
| CONV-03 | `POST /api/v1/conversations/{conversationId}/turns` | 발화 전송, NPC 응답 | LLM, TTS | 15 |
| CONV-04 | `POST /api/v1/turns/{turnId}/meaning` | NPC 대사 한국어 뜻 | LLM | 15 |
| CONV-05 | `POST /api/v1/turns/{turnId}/hint` | 추천 문장 | LLM | 15 |
| CONV-06 | `GET /api/v1/turns/{turnId}/speech` | 만들어 둔 NPC 음성 재생 | - | 15 |
| CONV-07 | `POST /api/v1/turns/{turnId}/speech` | NPC 음성 다시 생성 | TTS | 15 |
| CONV-08 | `POST /api/v1/conversations/{conversationId}/end` | 대화 종료, 짧은 피드백 | LLM | 16 |
| CONV-09 | `GET /api/v1/conversations` | 지난 대화 목록 | - | 18 |
| CONV-10 | `GET /api/v1/conversations/{conversationId}` | 대화 상세(턴 포함) | - | 16, 18 |
| CONV-11 | `GET /api/v1/conversations/active` | 진행 중인 대화 조회 | - | 15 |
| CONV-12 | `POST /api/v1/turns/{turnId}/suggest-words` | NPC 대사에 대답할 추천 단어·문장 미리 받기 | LLM | 15 |
| CONV-13 | `GET /api/v1/follow-ups` | NPC가 곧 건네기로 했는데 아직 받지 않은 것 | - | 14 |
| CONV-14 | `POST /api/v1/follow-ups/{followUpId}/dismiss` | 기다림 카드 닫기 | - | 14 |
| FB-01 | `GET /api/v1/conversations/{conversationId}/feedback` | 짧은 피드백·상세 평가 조회 | - | 16, 17 |
| FB-02 | `POST /api/v1/conversations/{conversationId}/feedback` | 짧은 피드백 다시 만들기 | LLM | 16 |
| FB-03 | `POST /api/v1/conversations/{conversationId}/detailed-feedback` | 상세 평가 조회(종료 때 만든 평가) | - | 16, 17 |
| FB-04 | `POST /api/v1/turn-feedback/{turnFeedbackId}/model-speech` | 모범 음성 | TTS | 17 |
| VOCAB-01 | `GET /api/v1/vocab` | 단어장 목록·개수 | - | 18 |
| VOCAB-02 | `POST /api/v1/vocab` | 단어 저장 | - | 08, 15, 16, 17 |
| VOCAB-03 | `PATCH /api/v1/vocab/{userVocabId}` | 메모, 외웠어요 표시 | - | 18 |
| VOCAB-04 | `DELETE /api/v1/vocab/{userVocabId}` | 단어장에서 삭제 | - | 18 |
| AI-01 | `GET /api/v1/ai/status` | AI 이용 상태·기능별 가능 여부 | - | 13, 19 |
| USER-01 | `PUT /api/v1/me/ui-theme` | 화면 테마 저장 | - | 화면 없음(집에서 선택) |
| USER-02 | `GET /api/v1/me/consents` | 선택 동의(음성 처리·AI 개선) 조회 | - | 마이페이지 |
| USER-03 | `PUT /api/v1/me/consents` | 선택 동의 켜기·끄기 | - | 마이페이지 |
| USER-04 | `PUT /api/v1/me/nickname` | 닉네임 변경 | - | 마이페이지 |
| USER-05 | `DELETE /api/v1/me` | 회원 탈퇴(내용·개인정보 삭제) | - | 마이페이지 |

## 4. API 상세

### 4.1 접속·가입

#### BOOT-01 `GET /api/v1/bootstrap`

접속 직후 한 번 호출해 첫 화면을 정합니다. 로그인하지 않았으면 `401 UNAUTHENTICATED`이며 화면 01을 보여줍니다.

요청에 팀 기기 쿠키(`elsehere_team=1`, 웹 템플릿이 `?team=1`로 연 브라우저에 남김)가 있으면 로그인한 계정을 팀 계정(`users.team_account`)으로 표시합니다. 응답은 같고, 팀 계정은 어드민 이용 현황에서 빠집니다([운영자 어드민](../admin.md#이용-현황)).

```json
{
  "authState": "SIGNED_IN",
  "user": {
    "userId": 1,
    "nickname": "Sun",
    "avatarType": "F",
    "currentLanguage": "en-US",
    "onboardingStep": "DONE",
    "uiTheme": "JELLY",
    "voiceConsent": true,
    "guest": false
  },
  "lastSceneCode": "TOWN",
  "activeConversationId": null,
  "aiStatus": {
    "status": "AI_AVAILABLE",
    "capabilities": { "canStartConversation": true, "canUseSpeech": true, "canGenerateFeedback": true }
  },
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
- `aiStatus`의 형식과 판단 기준은 AI-01과 같으며 HUD의 "AI 사용 가능" 표시에 씁니다. 게스트 대화 횟수 제한을 켠 경우(`GUEST_CONVERSATION_LIMIT` 1 이상) 게스트면 남은 대화 수(`remainingConversations`)도 담습니다. 가입 대기 상태(`SIGNUP_REQUIRED`)에는 담지 않습니다.
- `user.uiTheme`은 계정에 저장한 화면 테마(`JELLY`, `STICKER`, `CLOUD`)입니다. 고른 적 없으면 `JELLY`이며 USER-01로 바꿉니다.
- `user.voiceConsent`는 지금 버전의 음성 처리(`VOICE`)에 동의했는지입니다. `false`면 클라이언트는 녹음을 막고 [말하기]를 회색으로 보이며, 누르면 동의 창에서 USER-03으로 동의를 켤 수 있게 합니다. AUTH-04·07 응답의 `user`에도 같은 값이 있으며, USER-03으로 바꾸면 그 결과를 따릅니다.
- `user.guest`는 게스트로 시작한 사용자(AUTH-07)인지입니다. 클라이언트는 게스트에게 마이페이지의 [회원 탈퇴](USER-05)를 보이지 않습니다. AUTH-04 응답은 `false`, AUTH-07 응답은 `true`입니다.
- 값이 없는 필드(`lastSceneCode` 등)는 응답에 담지 않습니다. 클라이언트는 필드가 없을 때와 `null`일 때를 같게 처리합니다.
- 세션의 로그인 사용자가 DB에 없으면(탈퇴 등) 세션의 로그인 상태를 비우고 `401 UNAUTHENTICATED`로 응답합니다.

#### AUTH-01·02 소셜 로그인

- AUTH-01 `GET /api/v1/auth/login/google`은 Google 로그인 화면으로 리다이렉트합니다. 클라이언트는 API 호출이 아니라 페이지 이동으로 엽니다.
- AUTH-02 `GET /api/v1/auth/callback/google`은 Google이 인증을 마치고 돌려보내는 주소입니다. Google 콘솔의 승인된 리디렉션 URI에 환경마다 이 주소를 등록합니다. `(provider, providerUid)`로 사용자를 찾으며, `providerUid`는 Google 계정 고유 ID(OIDC `sub`)입니다.
  - 가입한 사용자: 로그인 처리 후 클라이언트로 리다이렉트합니다.
  - 처음 로그인한 사용자: 사용자를 만들지 않고 제공자 정보(`provider`, `providerUid`, 이메일)를 가입 대기 상태로 세션에 보관한 뒤 클라이언트로 리다이렉트합니다. `users.nickname`, `users.avatar_type`이 필수라 가입 정보를 받기 전에는 사용자를 만들 수 없습니다. 가입 대기 상태는 로그인 상태가 아닙니다.
- 게스트(AUTH-07)로 로그인한 세션에서 Google 로그인하면:
  - 처음 쓰는 Google 계정: 가입 대기 정보에 그 게스트를 함께 남깁니다. 가입(AUTH-04)하면 새 사용자를 만들지 않고 게스트를 회원으로 바꿉니다.
  - 이미 가입한 Google 계정: 그 계정으로 로그인하고 게스트 기록은 합치지 않습니다. 클라이언트 주소에 `?existingAccount=true`를 붙여 보내 화면이 이를 알리게 합니다. 남은 게스트 기록은 게스트 정리 규칙(AUTH-07)을 따릅니다.
  - 로그인이 실패하거나 취소되면 게스트 로그인은 그대로 남습니다.
- 리다이렉트할 클라이언트 주소는 서버 설정값(`CLIENT_URL`, 기본값 `/`)입니다. 로그인이 실패하거나 사용자가 Google 동의를 취소하면 같은 주소에 `?loginError=true`를 붙여 보냅니다. 클라이언트는 그 뒤 BOOT-01로 다음 화면을 정합니다.

#### AUTH-03 `GET /api/v1/auth/signup`

화면 02의 가입 정보 입력에 필요한 약관 목록을 돌려줍니다. 가입 대기 상태가 아니면 `401 UNAUTHENTICATED`입니다.

```json
{
  "email": "user@example.com",
  "consents": [
    { "consentType": "TERMS", "version": "2026-10", "required": true, "title": "서비스 이용약관", "url": "/terms/2026-10" },
    { "consentType": "PRIVACY", "version": "2026-11-09", "required": true, "title": "개인정보 처리방침", "url": "/privacy/2026-11-09" },
    { "consentType": "AGE14", "version": "2026-10", "required": true, "title": "만 14세 이상입니다", "url": null },
    { "consentType": "VOICE", "version": "2026-10", "required": false, "title": "음성 처리 동의", "url": "/voice/2026-10" },
    { "consentType": "TRAINING_TEXT", "version": "2026-10", "required": false, "title": "AI 개선에 내 대화 기록 활용", "url": "/ai-training/2026-10" },
    { "consentType": "TRAINING_VOICE", "version": "2026-10", "required": false, "title": "AI 개선에 내 녹음 음성 활용", "url": "/ai-training/2026-10" }
  ],
  "guest": { "avatarType": "M", "voiceConsent": true }
}
```

- `guest`는 게스트 세션에서 Google 로그인해 가입하는 중일 때만 담습니다(AUTH-02). 게스트 때 고른 캐릭터와 지금 버전의 음성 처리 동의 여부이며, 화면은 캐릭터 고르기를 숨기고 음성 처리 동의를 이 값으로 채웁니다.
- 약관 버전과 문서 주소는 서버 설정값입니다.
- `VOICE`(음성 처리)는 선택 동의입니다. 동의하지 않으면 음성 입력을 쓸 수 없고 글자로 대화하며, 게스트는 시작할 때 고릅니다. 녹음은 인식에만 쓰고 저장하지 않습니다(`TRAINING_VOICE` 동의 제외). 가입 뒤에는 USER-03으로 켜고 끕니다.
- `TRAINING_TEXT`(대화 기록)·`TRAINING_VOICE`(녹음 음성)는 AI 개선(학습·평가)에 쓰는 선택 동의입니다. 가입 뒤에도 USER-03으로 켜고 끕니다. 동의 범위와 문구는 [AI 개선 데이터 활용 기준](../ai-training-data.md)을 따릅니다.
- 문서 주소(`url`)는 같은 출처의 웹 페이지이며 로그인하지 않아도 열립니다. 백엔드가 `templates/legal`의 문서를 그려 주고, 웹 Nginx가 이 경로를 백엔드로 넘깁니다. 지금 버전이 아닌 주소는 `404 NOT_FOUND`입니다. 버전 없는 주소(`/terms`, `/privacy`, `/voice`, `/ai-training`)는 백엔드가 그 문서의 지금 버전 주소로 보내지만(`302`), 웹 Nginx는 버전이 붙은 경로만 백엔드로 넘기므로 배포한 웹에서는 열리지 않습니다. 그래서 로그인 화면과 웹 템플릿의 통계 동의 창은 지금 버전 주소를 직접 씁니다. 문구를 바꾸면 버전을 올리고 문서 주소와 이 두 링크도 함께 바꿉니다. Unity EditMode 시험(`LoginScreenPrefabTests`, `WebGlBuildTests`)이 두 링크가 `ConsentCatalog`의 지금 주소와 같은지 확인합니다. 문서마다 버전이 다를 수 있어(개인정보 처리방침만 2026-11-09), 문서끼리의 링크는 각 문서의 지금 주소를 씁니다. 버전 확인은 가입할 때만 하며, 이미 가입한 사용자에게 다시 동의를 받지는 않습니다.

#### AUTH-04 `POST /api/v1/auth/signup`

```json
// 요청
{
  "nickname": "Sun",
  "avatarType": "F",
  "consents": [
    { "consentType": "TERMS", "version": "2026-10" },
    { "consentType": "PRIVACY", "version": "2026-11-09" },
    { "consentType": "AGE14", "version": "2026-10" }
  ]
}

// 응답 201
{ "user": { "userId": 1, "nickname": "Sun", "avatarType": "F", "currentLanguage": null, "onboardingStep": "LANG", "uiTheme": "JELLY", "voiceConsent": true, "guest": false } }
```

- 사용자(`users`)와 약관 동의(`user_consents`)를 한 트랜잭션으로 만들고 세션을 로그인 상태로 바꿉니다. 로그인하면서 세션 ID를 새로 발급하고 가입 대기 정보를 지웁니다.
- `nickname`은 NPC가 부르는 이름입니다.
  - 한글 완성형(가~힣), 영문, 숫자만 씁니다. 공백, 특수문자, 이모지, 자음·모음만 있는 글자는 쓸 수 없습니다.
  - 2자 이상이고, 한글은 2, 영문·숫자는 1로 세어 합계 16 이하입니다. 한글만 쓰면 2~8자, 영문·숫자만 쓰면 2~16자입니다.
  - 규칙에 맞지 않으면 `VALIDATION_FAILED`입니다.
  - 다른 사용자가 쓰고 있으면 `409 NICKNAME_TAKEN`입니다. 대소문자를 구분해 `Sun`과 `sun`은 다른 닉네임으로 봅니다. `[제안]`
  - 금칙어 처리 여부는 `[미정]`입니다.
- `avatarType`은 `F`, `M`입니다.
- 새 사용자의 화면 테마(`uiTheme`)는 기본값 `JELLY`입니다. 게스트(AUTH-07)도 같습니다.
- 현재 버전과 다른 버전에 동의한 항목은 동의하지 않은 것으로 봅니다. 선택 약관도 현재 버전에 동의한 경우에만 저장합니다.
- 필수 약관이 빠졌거나 버전이 현재 버전과 다르면 `VALIDATION_FAILED`이며 `details.missingConsents`에 빠진 약관을 담습니다(예: `["AGE14"]`).
- 가입 대기 상태가 아니면 `401 UNAUTHENTICATED`, 이미 로그인한 사용자이거나 같은 Google 계정으로 이미 가입했으면 `409 ALREADY_SIGNED_UP`입니다.
- `users.current_language`는 필수 컬럼이라 언어를 고르기 전에는 기본값 `en-US`로 저장합니다. 응답의 `currentLanguage`는 ONB-02 전까지 `null`로 보여줍니다. `[제안]`
- 게스트 세션에서 시작한 가입(AUTH-02)이면 새 사용자를 만들지 않고 그 게스트를 회원으로 바꿉니다.
  - 같은 `userId`라 게스트 때 대화·피드백·저장 단어·NPC 기억·호감도·게임 시간·튜토리얼 기록·마지막 장소가 그대로 이어집니다.
  - `provider`·`providerUid`·이메일은 Google 계정 것으로, 닉네임은 요청 값으로 바꿉니다. 캐릭터는 게스트 때 것을 그대로 쓰며 요청의 `avatarType`은 쓰지 않습니다.
  - 학습 언어와 초기 설정 단계도 게스트 때 그대로라 응답의 `onboardingStep`은 `TUTORIAL`이나 `DONE`입니다. 클라이언트는 03~05를 거치지 않고 BOOT-01로 다음 화면을 정합니다.
  - 동의한 약관을 남기고, 게스트 때 동의했지만 이번에 동의하지 않은 선택 약관(음성 처리)은 철회합니다. 이미 지금 버전에 동의한 약관은 다시 남기지 않습니다.
  - 회원이 됐으므로 게스트 대화 횟수 제한과 게스트 정리는 더 적용하지 않고, 세션 유지 시간은 24시간이 됩니다.
  - 가입 대기 정보의 사용자가 이미 게스트가 아니면(다른 탭에서 먼저 가입 등) 새 사용자를 만듭니다.

#### AUTH-05 `POST /api/v1/auth/logout`

- 세션을 끝내고 `204 No Content`를 돌려줍니다. 와이어프레임에는 로그아웃 버튼이 없어 마이페이지(4.12)의 [로그아웃]이 씁니다. Unity는 로그아웃한 뒤 입구 씬(화면 01)으로 돌아갑니다.
- 로그인 여부와 관계없이 `204`입니다. 가입 대기 중인 사용자가 다른 Google 계정으로 바꾸려 할 때도 이 API로 가입 대기 정보를 지웁니다.
- POST만 받습니다. 다른 상태 변경 요청처럼 `X-Requested-With` 헤더가 필요합니다.

#### AUTH-06 `GET /api/v1/nicknames/availability`

화면 02에서 가입 정보를 제출하기 전에 닉네임을 쓸 수 있는지 확인합니다. 마이페이지의 닉네임 변경(USER-04)은 저장할 때 중복을 검사하므로 이 API를 먼저 부르지 않아도 됩니다.

```json
// GET /api/v1/nicknames/availability?nickname=Sun
{ "nickname": "Sun", "available": false }
```

- 가입 대기 상태이거나 로그인한 사용자만 호출할 수 있습니다. 둘 다 아니면 `401 UNAUTHENTICATED`입니다.
- 닉네임 규칙(AUTH-04)에 맞지 않으면 `VALIDATION_FAILED`입니다. 규칙에 맞고 다른 사용자가 쓰고 있지 않으면 `available: true`입니다.
- 확인한 닉네임을 예약하지 않습니다. 확인한 뒤 제출하기 전에 다른 사용자가 먼저 가입하면 AUTH-04가 `409 NICKNAME_TAKEN`을 돌려줍니다.
- 한글 닉네임은 URL 인코딩해 보냅니다.

#### AUTH-07 `POST /api/v1/auth/guest`

화면 01의 [게스트로 시작하기]입니다. Google 로그인과 가입 화면(02) 없이 캐릭터와 음성 처리 동의(선택)만 골라 새 사용자를 만들고 그 사용자로 로그인합니다.

```json
// 요청
{ "avatarType": "M", "voiceConsent": true }

// 응답 201
{
  "user": { "userId": 3, "nickname": "guest-123456", "avatarType": "M", "currentLanguage": "en-US", "onboardingStep": "TUTORIAL", "uiTheme": "JELLY", "voiceConsent": true, "guest": true }
}
```

- 다른 상태 변경 요청처럼 `X-Requested-With` 헤더가 필요합니다. 응답 형식은 AUTH-04와 같습니다.
- `avatarType`은 `F`, `M`이며 필수입니다. 없으면 `VALIDATION_FAILED`입니다.
- 부를 때마다 새 사용자를 만듭니다. `provider`는 `GUEST`, `providerUid`는 서버가 만든 임의 값이며 이메일은 없습니다. 로그아웃하거나 세션이 끝나면 같은 게스트로 다시 로그인할 수 없습니다.
- 게스트 세션은 요청이 2시간 없으면 끝납니다(가입자는 24시간).
- 게스트 세션에서 Google로 가입하면 이 게스트가 그대로 회원이 되어 기록이 이어집니다(AUTH-02·04). 화면은 대화 횟수를 다 썼을 때와 게스트 세션이 남은 채 다시 접속했을 때 가입을 권합니다.
- 마지막 활동 뒤 24시간이 지나면 게스트가 남긴 내용(대화 원문·NPC 음성·피드백·NPC 기억·NPC가 건네기로 한 것·저장 단어·숨김)을 지웁니다. 사용자·대화·AI 사용량 기록은 내용 없이 남깁니다.
- 닉네임은 서버가 `guest-` 뒤에 겹치지 않는 여섯 자리 숫자를 붙여 정합니다. 회원 닉네임 규칙(AUTH-04)은 `-`를 허용하지 않으므로 회원 닉네임과 겹치지 않습니다.
- 학습 언어와 대화 난이도는 MVP에서 고를 수 있는 값(`en-US`, 초급 1)으로 정하고 초기 설정은 튜토리얼(`TUTORIAL`)부터 시작합니다. 화면 03~05를 거치지 않으며 레벨 테스트 기록은 만들지 않습니다.
- 가입 화면을 거치지 않으므로 가입 약관 동의 기록(`user_consents`)을 남기지 않습니다. `voiceConsent`가 `true`면 지금 버전의 음성 처리(`VOICE`) 동의만 남깁니다. 없거나 `false`면 동의하지 않은 것으로 보며, 나중에 마이페이지(USER-03)에서 켤 수 있습니다.
- NPC 대화는 계정마다 정해진 횟수까지만 시작할 수 있습니다(CONV-01). 시작하기 전에는 AUTH-08로 그 횟수를 보여 줍니다.
- 모든 환경에서 열려 있습니다. 운영 공개 전에 유지 여부와 약관 처리를 다시 정합니다.

#### AUTH-08 `GET /api/v1/auth/guest`

게스트 캐릭터 선택 화면에 보여 줄 안내입니다. 계정을 만들기 전에 부르므로 로그인하지 않아도 됩니다.

```json
// 응답 200
{ "conversationLimit": 0 }
```

- `conversationLimit`은 게스트 한 계정이 할 수 있는 NPC 대화 수로, 서버 설정값(`GUEST_CONVERSATION_LIMIT`, 기본 0)입니다. `0`이면 가입 사용자처럼 제한이 없습니다. 1 이상이면 한 번이라도 말을 주고받은 대화만 셉니다. 시작한 뒤의 남은 횟수는 AI-01·BOOT-01의 `remainingConversations`입니다.
- 화면은 읽지 못해도 캐릭터 선택과 [시작하기]를 막지 않고, 횟수 안내 없이 닉네임 안내만 보입니다. `0`이면 "게스트도 NPC와 마음껏 대화할 수 있어요"로 안내합니다.

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

#### ONB-03~07·10 레벨 테스트

듣기 4문항과 말하기 2문항으로 초급(1)과 중급(2) 중 추천 난이도를 정합니다. 지금 퀘스트가 초급 기준이라, 문항은 그보다 위(B1)를 알아듣고 말할 수 있는지를 봅니다. 문법 문항은 두지 않습니다.

- 문항은 서버 설정 데이터(`LevelTestCatalog`)의 en-US 6개입니다. 문항 원본을 둘 테이블이 ERD에 없으므로 `itemCode`로 문항을 구분합니다.
- 듣기(`LISTEN`): 마을 NPC가 말하는 두 문장 안팎의 대사를 자막 없이 듣고, 한국어 보기 4개 중 뜻을 고릅니다. 음성은 ONB-10으로 받습니다. 대사와 정답은 응답에 담지 않습니다.
- 말하기(`SPEAK`): 상황 카드(`question`)와 말할 정보(`cues`)를 보고 한두 문장으로 말합니다. 녹음은 CONV-02(`purpose: LEVEL_TEST`)로 받아 적고, 받아 적은 말이나 직접 쓴 말을 ONB-04로 저장합니다. 통과 기준은 서버에만 있고 응답에 담지 않습니다.
- 화면 04의 첫 화면에는 [시작하기]와 [건너뛰기]를 둡니다.

```json
// ONB-03 POST /api/v1/level-tests  요청 ([시작하기]를 누를 때)
{ "languageCode": "en-US" }

// 응답 201
{
  "testId": 9,
  "status": "IN_PROGRESS",
  "totalItems": 6,
  "items": [
    {
      "itemCode": "EN_LT_L2",
      "type": "LISTEN",
      "instruction": "잘 듣고 무슨 뜻인지 고르세요.",
      "question": "무슨 뜻일까요?",
      "choices": ["왼쪽 기계는 고장이라 창가 기계를 쓰고, 25센트 동전만 된다", "…", "…", "…"],
      "speaker": "Gus · 빨래방 주인",
      "cues": []
    },
    {
      "itemCode": "EN_LT_S1",
      "type": "SPEAK",
      "instruction": "상황에 맞게 한두 문장으로 말해 보세요.",
      "question": "카페에 가방을 두고 왔어요. 가방이 어떻게 생겼는지 말하고 찾아 달라고 하세요.",
      "choices": [],
      "cues": ["파란 숄더백", "빨간 열쇠고리", "안에 여권 지갑과 책"]
    }
  ]
}
```

- ONB-03은 [시작하기]를 누를 때 새 테스트(`IN_PROGRESS`)를 만들고 학습 설정의 `last_test_id`를 이 테스트로 바꿉니다.
- 진행 중인 테스트가 있어도 다시 시작하면 새 테스트를 만듭니다. 중간에 나갔다 오면 이어서 풀지 않고 처음부터 다시 풉니다.
- 화면 04의 "문항 3 / N"과 진행률은 `totalItems`와 현재 문항 순서로 그립니다.
- ONB-10 `GET /api/v1/level-tests/items/{itemCode}/speech`는 듣기 문항 대사를 그 NPC 목소리로 만든 WAV를 돌려줍니다. 대사는 모든 사용자에게 같으므로 서버가 문항마다 한 번만 만들어 두고 다시 씁니다. 말하기 문항이나 없는 문항이면 `404 NOT_FOUND`입니다.
- ONB-04 `PUT /api/v1/level-tests/{testId}/answers/{itemCode}`는 `{ "answer": "…" }`를 저장하고 `204`를 돌려줍니다. 듣기 문항은 고른 보기를 정답과 비교해 기록하고, 말하기 문항은 답만 저장해 완료할 때 판정합니다. 같은 문항에 다시 답하면 덮어씁니다. 없는 문항이면 `404 NOT_FOUND`입니다.

```json
// ONB-05 POST /api/v1/level-tests/{testId}/complete  응답 200
{
  "testId": 9,
  "status": "DONE",
  "score": 5,
  "resultLevel": 2,
  "listeningScore": 3,
  "speakingPassed": 2,
  "feedback": "설명은 잘했어요. 부탁할 때 Could you…? 같은 표현을 더 연습해 보세요."
}
```

- ONB-05는 답한 말하기 문항을 AI 워커(`/v1/level-test/speaking`, 기록 종류 `FEEDBACK`)에 보내 문항별 통과 여부와 한 줄 피드백을 받습니다. AI를 기다리는 동안 DB 트랜잭션을 잡지 않고, 판정에 실패하면 테스트는 진행 중으로 남아 다시 완료할 수 있습니다. 답한 말하기 문항이 없으면 AI를 부르지 않고 `feedback`을 담지 않습니다.
- 듣기 4개 중 3개 이상을 맞히고 말하기 2개를 모두 통과하면 중급(2), 그 밖은 초급(1)을 추천하고 `recommended_level`에 저장합니다. 답하지 않은 문항은 틀린 것으로 셉니다. `score`는 맞힌 듣기와 통과한 말하기 문항 수(0~6)입니다.
- ONB-06 [그만하기]는 일부 응답 후 멈춘 기록(`QUIT`)입니다. `{ "testId": 9, "status": "QUIT" }`를 돌려주고 추천 레벨은 바꾸지 않습니다.
- 응답·완료·그만하기는 진행 중인 테스트에만 할 수 있습니다. 이미 끝난 테스트면 `409 LEVEL_TEST_NOT_IN_PROGRESS`, 다른 사용자의 테스트면 `404 NOT_FOUND`입니다.
- 고른 적 없는 언어로 시작하거나 건너뛰면 `404 NOT_FOUND`입니다.
- 문항 하나만 건너뛰는 기능은 두지 않습니다. 듣기에서 모르겠으면 아무 보기나 고르고, 말하기는 답하지 않고 넘어가면 통과하지 못한 것으로 셉니다.

```json
// ONB-07 POST /api/v1/level-tests/skip  요청
{ "languageCode": "en-US" }

// 응답 201
{ "testId": 10, "status": "SKIPPED" }
```

- ONB-07 [건너뛰기]는 테스트를 시작하기 전에 누르므로 테스트 ID 없이 학습 언어만 보냅니다. 테스트를 치르지 않고 넘어간 기록(`SKIPPED`)을 만들고 `last_test_id`를 이 기록으로 바꿉니다. 추천 레벨 없이 화면 05로 가고, 초급으로 시작합니다.

#### ONB-08 `GET /api/v1/me/languages/{languageCode}`

```json
{
  "languageCode": "en-US",
  "recommendedLevel": 1,
  "selectedLevel": null,
  "levelTest": { "testId": 9, "status": "DONE" },
  "levels": [
    { "level": 1, "name": "초급", "available": true, "description": "천천히, 쉬운 단어와 짧은 문장으로 말해요." },
    { "level": 2, "name": "중급", "available": true, "description": "자연스러운 일상 영어로, 관용구를 섞어 조금 더 길게 말해요." },
    { "level": 3, "name": "고급", "available": false, "description": null }
  ]
}
```

- 화면 05의 "레벨 테스트 추천" 배지는 `recommendedLevel`이 있을 때만 보여줍니다.
- 난이도는 초급·중급·고급 3단계이고 초급·중급을 고를 수 있습니다. 고급은 잠가 둡니다. `available: false`인 단계는 "추후 제공" 카드입니다. 단계 이름과 설명은 서버 설정값이며, 중급·고급의 설명과 단계별 차이는 `[미정]`입니다.
- `levelTest`는 마지막으로 시작하거나 건너뛴 테스트(`last_test_id`)입니다. BOOT-01의 `onboardingStep: LEVEL` 사용자는 이 상태가 `DONE`, `QUIT`, `SKIPPED`면 화면 05, 없거나 `IN_PROGRESS`면 화면 04로 갑니다.
- `selectedLevel`은 ONB-09 전까지 `null`입니다. DB에는 `selected_level`이 필수라 프로필을 만들 때 1로 저장하고, ONB-09 처리 여부는 `onboardingStep`으로 판단합니다. `[제안]`
- 레벨 테스트 기록이 없으면 `levelTest`는 응답에 담지 않습니다.
- 고른 적 없는 언어면 `404 NOT_FOUND`입니다.

#### ONB-09 `PUT /api/v1/me/languages/{languageCode}/level`

```json
{ "selectedLevel": 1 }
```

- 1(초급)·2(중급)만 허용합니다. 다른 값은 `VALIDATION_FAILED`입니다.
- 고른 난이도는 이후 시작하는 대화의 `levelApplied`가 되어 NPC 대답·힌트·뜻 보기·피드백 난이도로 쓰입니다.
- 처음 설정하면 `onboardingStep`이 `TUTORIAL`로 넘어가고 응답에 `onboardingStep`을 담습니다. 응답은 `{ "languageCode": "en-US", "selectedLevel": 1, "onboardingStep": "TUTORIAL" }`입니다.
- 튜토리얼 단계 이후에 다시 설정하면 난이도만 바꾸고 `onboardingStep`은 그대로 둡니다. 플레이 중에는 마이페이지 [대화 난이도]에서 이 API로 바꾸며, 진행 중인 대화는 시작할 때의 난이도를 그대로 쓰고 다음 대화부터 바뀝니다. 고른 적 없는 언어면 `404 NOT_FOUND`입니다.

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
- Ben 길 안내 방문이 아직 없으면 TUT-03과 같은 관계·기억을 한 번 저장합니다. 이전 클라이언트의 완료와 길 안내 저장 실패를 보완합니다.
- 첫 퀘스트([첫날의 작은 소동](first-day-mission.md))를 시작합니다(`POST /api/v1/missions/first-day/start`와 같은 결과). 튜토리얼 중에 이미 시작했다면 그 진행을 그대로 둡니다.
- 튜토리얼 전 단계(`LANG`, `LEVEL`)면 `409 TUTORIAL_NOT_IN_PROGRESS`이며 단계를 바꾸지 않습니다. 이미 `DONE`인 사용자가 다시 호출하면 같은 응답을 돌려주고 첫 퀘스트를 새로 시작하지 않습니다.
- 마지막 장소(`last_scene_id`)는 바꾸지 않습니다. BOOT-01에 `lastSceneCode`가 없으면 클라이언트가 마을에서 시작합니다.

#### TUT-02 `POST /api/v1/me/tutorial/skip`

- 화면 06의 [튜토리얼 건너뛰기]에서 호출합니다. 요청·응답과 단계 규칙은 TUT-01과 같습니다. `onboardingStep`을 `DONE`으로 바꾸고 마을에서 시작합니다.
- 건너뛴 사용자에게 튜토리얼을 다시 볼 방법을 줄지는 `[미정]`입니다.
- 건너뛰기 자체는 Ben 관계를 만들지 않습니다. 이미 TUT-03으로 저장한 관계는 유지합니다.
- 건너뛴 사용자는 튜토리얼 마지막의 퀘스트 카드 안내를 보지 못하므로, TUT-01과 같이 첫 퀘스트를 시작해 왼쪽 위 퀘스트 카드와 길 안내가 바로 보이게 합니다.

#### TUT-03 `POST /api/v1/me/tutorial/ben-dialogue/complete`

```json
// 요청 본문 없음

// 응답 200 (튜토리얼 진행 중)
{ "onboardingStep": "TUTORIAL" }
```

- 클라이언트가 고정 대본의 Ben 길 안내를 끝까지 마쳤을 때 호출합니다. 전체 튜토리얼 단계는 바꾸지 않습니다.
- Ben(`TOWN_PASSERBY`)의 첫 방문 1회, 알아봄 +20, 도움 호감 +4, 마지막 대화 시각·게임 날짜와 인사·자기소개 및 카페 길 안내 기억을 저장합니다. 실제 대화·발화를 만들거나 AI를 호출하지 않습니다.
- 이미 방문 기록이 있으면 중복 반영하지 않습니다. 네트워크 실패 시 재전송할 수 있습니다. 중간 종료와 대화 전 건너뛰기에는 호출하지 않습니다.
- `LANG`·`LEVEL`이면 `409 TUTORIAL_NOT_IN_PROGRESS`입니다. 이미 `DONE`이면 관계를 올리지 않고 `{ "onboardingStep": "DONE" }`을 돌려줍니다.

### 4.4 장소·사물

#### WORLD-01 `GET /api/v1/scenes`

- `[{ "sceneCode": "CAFE", "name": "모퉁이 카페", "unityKey": "CafeScene", "isOpen": true }]` 형식입니다. 화면 14에서 들어갈 수 있는 건물을 표시할 때 씁니다. MVP는 카페·호텔을 포함해 모든 장소가 열려 있으며, 이후 준비 중인 건물은 `isOpen: false`로 내려갑니다.
- `unityKey`는 클라이언트가 불러올 Unity 씬 이름입니다. 목록은 집, 마을, 카페, 호텔, 드럭스토어, 경찰서(`POLICE_STATION`, `PoliceStationScene`) 순입니다.

#### WORLD-02 `GET /api/v1/scenes/{sceneCode}`

```json
{
  "sceneCode": "HOME",
  "name": "집",
  "unityKey": "HomeScene",
  "isOpen": true,
  "npcs": [],
  "words": [
    {
      "wordKey": "sofa",
      "vocab": {
        "vocabId": 31,
        "type": "WORD",
        "text": "sofa",
        "meaningKo": "소파",
        "example": "He is sitting on the sofa.",
        "exampleKo": "그는 소파에 앉아 있어요."
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
  { "npcCode": "TAXI_DRIVER", "name": "Joe", "npcType": "STAFF", "roleLabel": "택시 기사", "canInitiate": false, "approachLine": null },
  { "npcCode": "POLICE_OFFICER", "name": "Dan", "npcType": "STAFF", "roleLabel": "경찰관", "canInitiate": false, "approachLine": null },
  { "npcCode": "POST_OFFICE_STAFF", "name": "Linda", "npcType": "STAFF", "roleLabel": "우체국 직원", "canInitiate": false, "approachLine": null },
  { "npcCode": "CAFE_STAFF", "name": "Mia", "npcType": "STAFF", "roleLabel": "카페 직원", "canInitiate": false, "approachLine": null }
]
```

- `npcCode`는 Unity 씬의 NPC에 넣은 코드와 같습니다. 튜토리얼 행인은 Unity 대본 콘텐츠라 서버 NPC가 아닙니다.
- 마을(`TOWN`)에는 거리 매장 직원 9명(`POST_OFFICE_STAFF`·`DINER_STAFF`·`BANK_STAFF`·`LAUNDROMAT_STAFF`·`FLOWER_SHOP_STAFF`·`BARBER_STAFF`·`BAKERY_STAFF`·`ARCADE_STAFF`·`HOME_STORE_STAFF`)도 위 Linda처럼 `STAFF`, `canInitiate: false`로 내려옵니다. 마을 행인 Ruby·Ethan·Maya·Helen·Frank·Arthur·Margaret(`TOWN_RETIREE`·`TOWN_STUDENT`·`TOWN_EVENING_WALKER`·`TOWN_SHOPPER`·`TOWN_HANDYMAN`·`TOWN_GRANDFATHER`·`TOWN_GRANDMOTHER`)는 `PASSERBY`이지만 먼저 말을 걸지 않습니다(`canInitiate: false`). 소방서가 아직 없어 마을에서 지내는 소방관 Ray·Rachel·Andre(`FIRE_CAPTAIN`·`FIRE_FIGHTER`·`FIRE_ROOKIE`)도 같은 `PASSERBY`이고 역할 이름(`roleLabel`)만 소방대장·소방관·신입 소방관입니다.
- 한 장소를 교대로 나눠 맡는 NPC도 그 장소의 `npcs`에 함께 내려옵니다. 카페는 Mia·Leo·Zoe(`CAFE_STAFF`·`CAFE_SERVER`·`CAFE_BARISTA`), 호텔은 Jake·Walter·Diego·Owen·Clara(`HOTEL_STAFF`·`HOTEL_CONCIERGE`·`HOTEL_BELLHOP`·`HOTEL_RECEPTIONIST`·`HOTEL_NIGHT_MANAGER`), 드럭스토어는 Alex·Hannah·Samuel(`SHOP_STAFF`·`SHOP_PHARMACIST`·`SHOP_NIGHT_CLERK`), 마을의 경찰서는 Dan·Victor·Kevin·Taylor(`POLICE_OFFICER`·`POLICE_SERGEANT`·`POLICE_PATROL_OFFICER`·`POLICE_ROOKIE`)입니다. 누가 언제 나오는지는 서버가 아니라 AI 규칙에서 내보낸 일과([NPC 생활·관계 설계](../npc-life-design.md) 6절)로 Unity가 정합니다.
- `words`는 장소의 사물에 붙은 공용 단어 카드입니다. 여러 사물이 같은 단어를 쓰면 카드는 하나이며, Unity가 사물의 공용 단어 키(`wordKey`)로 카드를 찾습니다. 장소별 단어 목록은 Unity가 내보낸 `object-word-bindings.json`을 서버 시작 때 `scene_words`에 적재한 것입니다.
- `npcs`, `words`의 순서에는 의미가 없습니다. 클라이언트는 `npcCode`, `wordKey`로 찾습니다.
- 장소에 들어갈 때 그 장소의 단어 카드를 한 번에 받습니다. MVP는 장소마다 단어가 20~40개라 응답이 작습니다.
- 맵이 커지면 단어 카드를 한 번에 받지 않고 캐릭터 주변 사물의 카드만 받도록 바꿉니다. 사물 위치는 Unity 씬에 있으므로, 클라이언트가 주변 사물의 `wordKey`를 모아 요청하는 방식을 우선 검토합니다. `[제안]`
- 화면 08의 사물 카드는 `vocab`과 `saved`, `hoverHidden`으로 그립니다. 미리 등록한 어휘라 AI를 호출하지 않습니다.
- `saved`, `hoverHidden`은 단어 기준입니다. 한 장소에서 저장하거나 숨긴 단어는 그 단어를 쓰는 모든 장소에서 같은 값으로 보입니다.
- `hoverHidden: true`인 단어의 사물은 호버로 카드를 띄우지 않고, 사물을 클릭하면 카드를 보여줍니다.
- `exampleKo`(예문 뜻)는 `vocabularies.example_ko`입니다. 예문을 저장 전부터 보여줄지는 `[미정]`입니다(5.2).
- `approachLine`(`npcs.approach_line`)은 튜토리얼 뒤 거리 행인의 말걸기 말풍선입니다. 대화를 시작하기 전에 보여주므로 미리 작성한 대사입니다. 말걸기 발동 조건(거리, 반복 간격, 횟수)은 클라이언트 콘텐츠 설정입니다.
- `roleLabel`은 `npcs.role_label`입니다. 이름표 아래에 보여주는 역할 이름(행인, 카페 직원)입니다.
- NPC의 `persona`, `voice_id`는 서버 내부 정보라 응답하지 않습니다.
- 열리지 않은 장소는 `isOpen: false`이며, 그 장소의 NPC와 대화를 시작하면 `SCENE_CLOSED`입니다.

#### WORLD-03 `PUT /api/v1/me/word-prefs/{wordKey}`

```json
// 요청
{ "hoverHidden": true }

// 응답 200
{ "wordKey": "sofa", "hoverHidden": true }
```

- 화면 08의 [숨기기]는 `true`입니다. 숨긴 뒤에는 사물을 클릭해야 카드가 보이며, 카드에서 자동 표시를 다시 켜면 `false`로 바꿉니다.
- `wordKey`는 WORLD-02 단어 카드의 `wordKey`(`vocabularies.word_key`)입니다.
- 설정은 사용자·단어마다 하나(`user_word_prefs`)입니다. 그 단어를 쓰는 모든 장소의 사물에 적용됩니다. 처음 바꿀 때 만들고, 다시 보내면 값만 바꿉니다. 같은 값을 여러 번 보내도 결과가 같습니다.
- `hoverHidden`이 없으면 `400 VALIDATION_FAILED`, 없는 단어 키면 `404 NOT_FOUND`입니다.

#### WORLD-04 `GET /api/v1/vocab/{vocabId}/pronunciation`

- 화면 08의 발음 듣기 버튼이 쓰는 음성 파일을 `200`, `Content-Type: audio/wav`로 돌려줍니다. `vocabId`는 WORLD-02 카드의 `vocab.vocabId`입니다. `hasPronunciation: false`이거나 파일 재생에 실패하면 WebGL 클라이언트는 브라우저의 영어 음성 합성을 사용합니다. 파일과 브라우저 음성 합성을 모두 사용할 수 없을 때만 버튼을 숨깁니다.
- 발음 음성은 AI 워커 TTS(`af_heart`, 속도 1.0)로 미리 만들어 서버의 음성 볼륨 하위 폴더 `pronunciation/`에 `{wordKey}.wav`로 저장합니다([NPC 음성 저장 운영](../npc-audio-storage.md)). 관리자가 발음 생성 API(`POST /api/v1/admin/pronunciations`, [운영자 어드민](../admin.md))로 발음 파일이 없는 단어만 만듭니다. 듣기 요청 때는 AI를 호출하지 않으므로 AI 워커가 꺼져 있거나 AI 사용량이 소진돼도 들을 수 있습니다.
- DB에는 파일 이름(`vocabularies.pronunciation_audio_key`)만 저장합니다. Unity 단어 에셋에 발음 키가 있으면 그 값을, 없으면 발음을 만들 때와 서버가 시작할 때 `{wordKey}.wav` 파일이 있는 단어에 그 파일 이름을 넣습니다.
- 없는 어휘이거나, 발음 키가 없거나, 키에 해당하는 파일이 없으면 `404 NOT_FOUND`입니다. 발음 키는 `{wordKey}.wav` 형식만 받으며 다른 형식이면 파일을 읽지 않고 `404 NOT_FOUND`입니다.

#### WORLD-05 `PUT /api/v1/me/last-scene`

```json
// 요청
{ "sceneCode": "CAFE" }

// 응답 200
{ "sceneCode": "CAFE" }
```

- 화면 09의 외출, 화면 14의 건물 출입처럼 장소를 옮길 때 호출합니다. 재접속하면 BOOT-01의 `lastSceneCode`에서 시작합니다. 장소 안의 정확한 위치 복원은 `[미정]`입니다.
- `sceneCode`가 없거나 비어 있으면 `400 VALIDATION_FAILED`, 없는 장소면 `404 NOT_FOUND`, 열리지 않은 장소(`is_open = false`)면 `409 SCENE_CLOSED`입니다. 오류가 나면 마지막 장소를 바꾸지 않습니다.

#### 게임 시간

게임 시간은 **사용자마다 따로, 플레이한 동안만** 흐릅니다. 실제 10분이 게임 하루이고, 새 사용자는 1일차 오전 8시에서 시작합니다. 진행 중인 대화가 있거나 튜토리얼을 마치기 전(온보딩 단계가 `DONE`이 아님)이면 멈춥니다. 서버가 사용자별 누적 플레이 시간(`user_game_clocks`)을 보관하며, NPC 관계의 "마지막 대화 뒤 지난 게임 날 수"도 이 시계로 셉니다([NPC 생활·관계 설계](../npc-life-design.md) 11절).

#### WORLD-06 `GET /api/v1/me/game-clock`

```json
// 응답 200
{ "day": 3, "minuteOfDay": 615, "paused": false }
```

- `day`는 1일차부터 센 게임 날짜, `minuteOfDay`는 하루 중 분(0~1439, 615면 오전 10시 15분)입니다. `paused`는 진행 중인 대화가 있거나 튜토리얼을 마치기 전이라 시간이 멈췄는지입니다.
- 신호를 보낸 적이 없으면 `{ "day": 1, "minuteOfDay": 480, "paused": false }`입니다. 시간을 흐르게 하지 않습니다.

#### WORLD-07 `POST /api/v1/me/game-clock/beats`

```json
// 요청 (생략 가능: 이전 클라이언트와 호환)
{ "interactionPaused": true, "interactionWasPaused": false, "activeSeconds": 25 }

// 응답 200
{ "day": 3, "minuteOfDay": 627, "paused": true }
```

- 월드 장면에 있는 동안 30초마다 보내는 신호입니다. 지난 신호 뒤 흐른 실제 시간만큼 게임 시간을 더하고 지금 게임 시각을 돌려줍니다(실제 1초 = 게임 2.4분).
- 대화를 시작·종료하거나 단어장·피드백·피드백 목록을 열고 닫으면 간격을 기다리지 않고 신호를 보냅니다. `interactionPaused`는 현재 대화·학습 화면 때문에 시간이 멈췄는지, `interactionWasPaused`는 직전 신호 이후 멈춰 있던 구간이 있었는지입니다. 현재 화면이 열려 있거나 서버에 진행 중인 대화·튜토리얼이 있으면 `paused: true`를 돌려줍니다.
- `activeSeconds`는 직전 요청 이후 실제로 시간이 진행한 초입니다. 창을 열기 전까지의 진행 시간은 보존하고, 대화·학습 화면을 보고 있던 시간은 제외합니다. Unity가 멈췄던 시간(브라우저 창이 포커스를 잃거나 탭이 숨은 동안)도 넣지 않습니다(한 프레임은 1초까지만 셈). 서버는 신호 간격·최대 60초로 제한하며, 정지 화면이 열려 있고 직전 신호 이후 새 대화가 시작됐으면 대화 시작 전 구간까지만 더합니다. 튜토리얼 중에는 더하지 않습니다. 현재 정지 여부는 `interactionPaused`를 따르므로 새로고침 뒤 서버에 이어하기용 `ACTIVE` 대화가 남아 있어도 화면을 닫은 상태라면 시계가 재개됩니다. 기존 대화는 종료하거나 다시 차감하지 않습니다. 닫는 신호는 멈춰 있던 구간을 제외하고 현재 상태를 돌려줍니다.
- 본문을 생략하면 두 플래그는 `false`입니다. `activeSeconds`를 생략한 이전 클라이언트는 두 플래그와 서버 대화·튜토리얼 상태로 구간 전체의 멈춤을 판정합니다. 이 경우 신호 사이에 시작·종료된 대화 몇 초의 오차가 생길 수 있습니다.
- 첫 신호는 시계를 시작만 하고 더하지 않습니다. 신호 사이가 60초보다 길면(창을 닫았거나 백그라운드) 60초까지만 더합니다. 튜토리얼을 마치거나 건너뛴(TUT-01·02) 뒤 신호부터 흐릅니다.
- 신호마다 그날(한국 날짜)의 접속을 `user_daily_activities`에 남깁니다. 첫·마지막 신호 시각을 남기고, 신호 사이 실제 시간을 머문 시간으로 더합니다. 머문 시간은 게임 시계가 멈춘 대화·학습 화면과 튜토리얼 중에도 세고, 신호가 60초 넘게 끊긴 사이(창을 닫았거나 숨김)는 넣지 않습니다. 어드민 이용 현황의 활동한 날·머문 시간·재방문에 씁니다([어드민](../admin.md#이용-현황)).
- 두 API 모두 로그인한 사용자만 호출할 수 있으며, 로그인하지 않았으면 `401 UNAUTHENTICATED`입니다. WORLD-07은 `X-Requested-With` 헤더가 필요합니다.

#### WORLD-08 `GET /api/v1/me/npcs`

```json
// 응답 200
{
  "neighborCount": 1,
  "totalCount": 36,
  "npcs": [
    {
      "npcCode": "ARCADE_STAFF",
      "unlocked": true,
      "name": "Max",
      "roleLabel": "오락실 직원",
      "sceneCode": "TOWN",
      "sceneName": "마을",
      "occupation": "attendant at Little Arcade on the main street",
      "interests": ["video games"],
      "affinity": 12,
      "recognition": 30,
      "talkCount": 3,
      "lastTalkedAt": "2026-11-09T14:12:00+09:00"
    },
    {
      "npcCode": "CAFE_BARISTA",
      "unlocked": false,
      "name": null,
      "roleLabel": "카페 직원",
      "sceneCode": "CAFE",
      "sceneName": "모퉁이 카페",
      "occupation": null,
      "interests": [],
      "affinity": null,
      "recognition": null,
      "talkCount": null,
      "lastTalkedAt": null
    }
  ]
}
```

- 휴대폰 [이웃]의 마을 NPC 목록입니다. 와이어프레임 밖에서 추가한 화면입니다. 마을의 모든 NPC(`npcs`)를 담습니다.
- 끝까지 마친 대화가 있는 NPC만 이웃으로 열립니다(`unlocked: true`). 끝까지 마친 대화는 사용자가 한 마디 이상 하고 [대화 종료](`USER_ENDED`)·NPC 마무리(`NPC_ENDED`)·30턴 도달(`TURN_LIMIT`)로 끝낸 대화입니다. 무례 퇴장(`RUDE`)으로 끝났거나 끝내지 않은 대화는 세지 않습니다. 서버 대화 없이 방문으로 세는 튜토리얼 길 안내(TUT)도 끝까지 마친 대화로 봅니다. 게스트도 같은 기준으로 자기 목록을 받습니다.
- 열린 이웃은 이름, 인격 카드(`npcs.profile`)의 직업(`occupation`)·관심사(`background.interests`, 영어)와 관계 값을 담습니다. `affinity`는 지금 호감도(-100~100)이며 대화 화면의 `conversation.affinity`(CONV-01·03)와 같은 값입니다. `recognition`은 알아봄(0~100, 화면 이름은 아는 정도), `talkCount`는 끝낸 대화 수, `lastTalkedAt`은 마지막으로 대화를 끝낸 시각(한국 시각 `+09:00`)입니다.
- 열리지 않은 NPC는 역할(`roleLabel`)과 장소(`sceneCode`, `sceneName`)만 담고 나머지는 `null`(관심사는 `[]`)입니다.
- `roleLabel`은 WORLD-02와 같은 역할 이름(`npcs.role_label`)입니다. `sceneCode`, `sceneName`은 NPC가 일하거나 지내는 장소(`scenes.code`, `scenes.name`)이며 거리 매장 직원은 마을입니다. NPC가 지금 어디서 무엇을 하는지는 서버가 모르므로 화면이 일과(Unity `NpcLife.json`)로 계산합니다.
- `neighborCount`는 열린 이웃 수, `totalCount`는 전체 NPC 수입니다. 열린 이웃이 먼저 마지막으로 대화한 시각이 최근인 순서(같으면 이름순)로 오고, 그 뒤에 열리지 않은 NPC가 장소·역할 순서로 옵니다.
- 로그인하지 않았으면 `401 UNAUTHENTICATED`입니다.

### 4.5 미션

첫날 7단계 연계 여정은 [첫날 연계 미션 계약](first-day-mission.md)의 `/api/v1/missions/first-day`를 사용한다. 튜토리얼을 마치거나 건너뛰면(TUT-01·02) 서버가 시작하고 진행을 영속화한다. 아래 일일 미션 API와 튜토리얼 기본 미션 경로는 유지한다.

첫날을 마친 뒤의 새 여행 이야기는 [마을 여정 계약](town-story.md)의 `/api/v1/missions/town-story`를 사용한다. 첫날의 완료 기록과 새 여정의 진행은 별도로 저장한다.

시연 전용의 별도 진행은 [비 오는 저녁 시연 퀘스트](../demo-rainy-evening.md)의 `/api/v1/missions/showcase`를 사용한다. 기본값은 비활성이다. 전용 시연 환경에서 활성화하고 시작한 사용자에 한해 첫날 상태·방문 응답을 시연 상태로 제공한다. 기존 일반 퀘스트 진행과 잔액은 유지한다.

미션은 자동으로 시작됩니다. 수락 버튼이 없고, 사용자가 미션에 맞는 행동을 하면 서버가 완료로 판정합니다. 한 대화에서 여러 미션을 동시에 완료할 수 있습니다.

#### MIS-01 `GET /api/v1/missions`

```json
{
  "date": "2026-10-21",
  "resetsAt": "2026-10-22T00:00:00+09:00",
  "missions": [
    {
      "missionId": 1,
      "missionCode": "CAFE_SOY_LATTE",
      "title": "나만의 카페라테 주문하기",
      "description": "우유 대신 두유를 넣은 카페라테를 주문해 보세요.",
      "sceneCode": "CAFE",
      "npcCode": "CAFE_STAFF",
      "npcName": "Mia",
      "userMissionId": 12,
      "status": "DONE",
      "completedAt": "2026-10-21T14:05:00+09:00"
    },
    {
      "missionId": 2,
      "missionCode": "HOTEL_CHECK_IN",
      "title": "호텔에 체크인하기",
      "description": "호텔 직원에게 예약을 확인하고 체크인해 보세요.",
      "sceneCode": "HOTEL",
      "npcCode": "HOTEL_STAFF",
      "npcName": "Jake",
      "userMissionId": 13,
      "status": "RUN",
      "completedAt": null
    }
  ]
}
```

- 오늘 처음 조회하면 서버가 오늘의 일일 미션을 `user_missions`에 `RUN` 상태로 만듭니다. 같은 날 다시 조회해도 새로 만들지 않고, 오늘 날짜로 아직 없는 미션만 시작합니다. 하루 중간에 미션이 추가되거나 장소가 열리면 다음 조회에 반영됩니다.
- `missionCode`는 Unity 미션 콘텐츠와 같은 고정 키(`missions.code`)입니다. 클라이언트는 이 코드로 미션을 찾습니다.
- `completedAt`과 `resetsAt`은 한국 시각(`+09:00`)으로 보냅니다. 진행 중인 미션의 `completedAt`은 `null`입니다.
- 화면 13의 미션 목록은 HUD의 [미션]으로 엽니다. 화면 위 미션 표시는 `status: RUN`인 미션의 제목입니다. 미션 목록의 순서에는 의미가 없습니다.
- 열리지 않은 장소의 미션은 목록에 넣지 않습니다. MVP의 일일 미션은 카페와 호텔에 하나씩입니다. 튜토리얼 미션은 Unity 대본 콘텐츠라 DB에 없습니다.
- 미션 상세 화면(지도, 자세한 설명)은 MVP에 넣지 않습니다(7절).
- 완료 판정 기준(`keywords`, `intent_rule`)은 서버 내부 정보라 응답하지 않습니다.
- MVP의 미션 완료 행동은 대상 NPC와의 대화입니다. 대화 외 행동(장소 방문, 단어 저장 등)으로 완료하는 미션이 필요한지는 `[미정]`입니다.
- 일일 미션의 날짜는 MVP에서 `Asia/Seoul` 기준으로 고정하며, 자정에 바뀝니다. 미션 보상은 `[미정]`입니다.

#### 장소 연습 (PRC-01~03)

퀘스트 창의 [장소 연습]입니다. 가게 12곳마다 직원과 연습할 상황이 세 가지(모두 36개) 있고, 상황마다 학습자가 먼저 말해야 하는 목표가 두 개 있습니다. 이야기 퀘스트와 별도로 처음부터 모두 열려 있고 몇 번이든 다시 할 수 있습니다. 코인·도장은 없으며, 상황의 목표를 모두 이룬 적이 있는지만 기록합니다. 상황·목표·직원 지시 콘텐츠는 백엔드 `PracticeScenario`에 있습니다.

##### PRC-01 `GET /api/v1/practice-places`

```json
{
  "places": [
    {
      "placeCode": "DINER",
      "name": "식당",
      "summary": "자리 잡기 · 못 먹는 재료 말하기",
      "npcCode": "DINER_STAFF",
      "npcName": "Rosa",
      "roleLabel": "식당 종업원",
      "sceneCode": "TOWN",
      "doneCount": 1,
      "totalCount": 3,
      "scenarios": [
        {
          "scenarioId": "DINER_TABLE",
          "number": 1,
          "title": "자리 잡기",
          "description": "몇 명인지 말하고 창가 자리를 부탁해요. 자리가 없으면 얼마나 기다리는지 물어요.",
          "done": true,
          "goals": [
            { "goalId": "say_party_window", "title": "인원 말하고 창가 자리 부탁하기", "hint": "몇 명인지 말하고 창가 자리를 부탁하기" },
            { "goalId": "ask_wait_time", "title": "기다리는 시간 묻기", "hint": "얼마나 기다려야 하는지 묻기" }
          ]
        }
      ]
    }
  ]
}
```

- 장소는 `HOTEL`·`CAFE`·`DINER`·`LAUNDRY`·`BANK`·`BARBER`·`FLORIST`·`BAKERY`·`DRUGSTORE`·`HOME_STORE`·`ARCADE`·`POST_OFFICE` 순서이고, 상황은 `number`(1~3) 순서입니다.
- `summary`는 앞의 두 상황 제목을 ` · `로 이은 목록 아래 줄입니다.
- `npcName`·`roleLabel`·`sceneCode`는 그 가게 직원 NPC(`npcs`)의 값입니다.
- `done`은 이 사용자가 그 상황의 목표를 모두 이룬 연습 대화가 하나라도 있는지(`conversations.practice_done_at`), `doneCount`는 그 가게에서 `done`인 상황 수입니다.
- `hint`는 목표를 이루려면 무엇을 말하면 되는지 알려 주는 한국어 안내입니다. 의도 판정 기준(영어)과 직원 지시는 서버 내부 정보라 응답하지 않습니다.

##### PRC-02 `POST /api/v1/practice-scenarios/{scenarioId}/conversations`

```json
// 요청
{ "requestId": "6f1c2a7e-4b8d-4b7a-9a52-1f0f3d2c9e11", "speechSpeed": 1.1 }

// 응답 201: CONV-01과 같은 형식에 practiceScenarioId가 더해집니다.
{ "conversationId": 91, "status": "ACTIVE", "npc": { "npcCode": "DINER_STAFF", "name": "Rosa", "roleLabel": "식당 종업원" }, "openingTurn": { "...": "..." }, "practiceScenarioId": "DINER_TABLE", "...": "..." }
```

- 그 상황 가게의 직원과 학습자가 말을 거는 대화(`initiatedBy: USER`, 근무 중)를 시작합니다. 없는 `scenarioId`면 `404 NOT_FOUND`입니다.
- `speechSpeed`(선택, 0.8~1.5)는 사용자가 고른 NPC 말 빠르기로 CONV-01과 같은 규칙입니다(없으면 `1.1`, 범위 밖은 `VALIDATION_FAILED`).
- 오류와 같은 `requestId` 재전송 처리는 CONV-01과 같습니다(`CONVERSATION_ALREADY_ACTIVE`, `SCENE_CLOSED`, 게스트 대화 횟수 등). 같은 `requestId`를 다른 상황이나 일반·복습 대화 시작에 쓰면 `409 REQUEST_ID_CONFLICT`입니다.
- 직원의 첫 대사는 상황에 맞게 인사하고 요청을 기다립니다. 학습자가 할 일을 직원이 먼저 꺼내지 않습니다. 한 목표에 답할 때도 물은 것만 답하고, 남은 목표의 답(예: 늦은 체크아웃을 물었을 때의 추가 요금)은 학습자가 물을 때까지 말하지 않습니다. 평가는 `ai/tools/practice_early_answer_eval.py`입니다.
- 장소 연습 대화에는 지금 하는 이야기 퀘스트, 이 NPC와의 기억 카드, 건넬 것(`followUp`)을 섞지 않고, 대화 결과를 이야기 퀘스트·호감도·기억에 반영하지 않습니다. 대화 종료(CONV-08)는 일반 대화처럼 피드백을 만들고, 피드백은 상황의 목표를 기준으로 평가합니다. `followUp`은 항상 `null`입니다.
- 발화(CONV-03)는 상황의 남은 목표로 의도를 판정합니다(판정 상태 `practice_<scenarioId>`, 같은 대화의 앞선 학습자 발화를 합쳐 판정). 인식한 목표는 그 대화에 기록하고(`conversations.practice_completed_goals`), 직원은 인식한 요청에 답한 뒤 남은 요청에 빠진 정보만 묻습니다. 목표를 모두 이룬 턴에 끝낸 시각을 남기고 직원이 마무리 대사로 대화를 끝냅니다(`ENDED_BY_NPC`, `NPC_ENDED`).
- 힌트(CONV-05)는 상황에서 아직 이루지 않은 첫 목표를 기준으로 만듭니다.
- 다시 하면 새 대화라 목표를 처음부터 판정합니다. 앞서 끝낸 기록(PRC-01의 `done`)은 그대로 남습니다.

##### PRC-03 `GET /api/v1/practice-scenarios/{scenarioId}/conversations/{conversationId}/progress`

```json
{
  "scenarioId": "DINER_TABLE",
  "goals": [
    { "goalId": "say_party_window", "title": "인원 말하고 창가 자리 부탁하기", "hint": "몇 명인지 말하고 창가 자리를 부탁하기", "completed": true },
    { "goalId": "ask_wait_time", "title": "기다리는 시간 묻기", "hint": "얼마나 기다려야 하는지 묻기", "completed": false }
  ],
  "done": false
}
```

- 이 대화에서 이룬 목표와, 상황의 목표를 모두 이뤘는지(`done`)입니다. 클라이언트는 NPC 대답을 받을 때마다 읽어 목표 체크를 갱신하고, 대화가 끝나면 `done`으로 결과("연습 완료!" 등)를 보여 줍니다.
- 자신의 그 상황 장소 연습 대화만 읽을 수 있습니다. 다른 사용자의 대화, 다른 상황이나 장소 연습이 아닌 대화, 없는 `scenarioId`는 `404 NOT_FOUND`입니다.

### 4.6 대화

#### CONV-01 `POST /api/v1/conversations`

```json
// 요청
{ "requestId": "6f1c2a7e-4b8d-4b7a-9a52-1f0f3d2c9e11", "npcCode": "CAFE_STAFF", "initiatedBy": "USER", "npcOffDuty": false, "speechSpeed": 1.1, "placeCode": "CAFE" }

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
    "suggestWords": null,
    "speech": { "status": "READY", "url": "/api/v1/turns/100/speech" }
  },
  "conversation": { "status": "ACTIVE", "endReason": null, "rudeCount": 0, "rudeLimit": 3, "affinity": 12, "timeLimitAt": "2026-11-05T14:05:00+09:00", "graceUntil": "2026-11-05T14:05:30+09:00", "closing": false, "finalTurnsRemaining": 0 },
  "deliveredFollowUpId": null
}
```

- `conversation`은 CONV-03 응답과 같은 형식의 대화 상태입니다. 대화 화면이 처음부터 무례 경고 칸(`rudeLimit`개)과 이 NPC의 지금 호감도(`affinity`)를 보여 줍니다. 처음 만난 NPC의 호감도는 0입니다. `status`는 위쪽 `status`와 같은 값이며, 위쪽 값은 이전 클라이언트를 위해 남겨 둡니다.
- 화면 14의 [E 대화하기]는 `initiatedBy: USER`, 튜토리얼 뒤 거리 행인의 말걸기에 응답하면 `initiatedBy: NPC`입니다. 행인 말걸기를 거절하거나 무시하면 서버를 호출하지 않습니다.
- `initiatedBy: NPC`면 이미 보여준 `approachLine`을 첫 NPC 턴으로 저장하며 AI를 호출하지 않습니다. 근무 시간이 아닌 NPC(`npcOffDuty: true`)는 예외입니다.
- 이 NPC가 곧 건네기로 한 것(CONV-08 `followUp`)이 준비됐으면, `initiatedBy`·`approachLine`과 관계없이 AI가 그것을 건네는 첫마디("Your iced latte is ready! Here you go—enjoy!")를 만들고 건넨 것으로 표시합니다. 경찰관이면 출동을 다녀와 결과를 알리는 첫마디("I'm back—found your wallet at the diner. Here you go!")입니다. 응답의 `deliveredFollowUpId`가 그 `followUpId`이고, 건넨 것이 없으면 `null`입니다. 클라이언트는 이 값으로 기다림 카드를 지웁니다. 아직 준비되지 않았으면 평소처럼 시작하고 건네지 않습니다.
- `npcOffDuty`(선택, boolean)는 NPC가 맡은 교대 밖이라 일하지 않는 중이면 `true`입니다. 없거나 `null`이면 `false`(근무 중)로 보아, 이 칸을 보내지 않는 클라이언트는 지금처럼 응대 상황으로 대화합니다. 교대는 [NPC 생활·관계 설계](../npc-life-design.md) 6절 "일과와 교대"를 따릅니다.
  - `true`로 시작한 대화는 그대로 저장되어(`conversations.npc_off_duty`) 그 대화의 첫 인사·대답·의도와 무례 판정·힌트·추천 단어·뜻 보기 요청에 응대 상황 대신 쉬는 중에 잡담하는 일상 상황을 보냅니다(워커 요청의 `npc.scene`). NPC는 주문을 받거나 손님을 응대하지 않습니다.
  - 첫 대사는 저장된 말걸기 대사가 아니라 AI가 만든 일상 인사이며, 무엇을 도와줄지 묻지 않습니다. 대화 종료 피드백은 일상 대화로 평가합니다.
  - 사적인 화제를 얼마나 여는지는 근무 중과 같이 인격 카드와 이 NPC와의 알아봄·호감(워커 관계 규칙)을 따릅니다. 근무가 아니라고 처음 만난 학습자에게 사적인 이야기를 더 하지 않습니다.
  - 첫날 미션 단계의 NPC는 근무 시간이 아니어도 미션 지시대로 인사합니다. 미션 NPC가 퇴근한 뒤의 미션 처리는 아직 정하지 않았습니다([NPC 생활·관계 설계](../npc-life-design.md) 11절 "교대와 첫날 미션").
- `speechSpeed`(선택, 0.8~1.5)는 사용자가 마이페이지 [NPC 말 빠르기] 슬라이더로 고른 NPC 음성 빠르기 배율입니다. 첫인사 음성을 이 빠르기로 만들며 대화 난이도와는 상관없습니다. 없거나 `null`이면 `1.1`이고, 범위를 벗어나면 `400 VALIDATION_FAILED`입니다. 값은 기기(브라우저)에 저장하고 서버에는 저장하지 않으므로 CONV-03·CONV-07·복습 대화 시작·장소 연습 시작(PRC-02)에도 매번 보냅니다.
- `npcTravelContext`(선택)는 Unity가 관찰한 이동 사실입니다. `phase`는 `GOING_TO_WORK`·`AT_WORK`·`GOING_HOME`·`LEISURE`·`BREAK`·`RESTING`·`WALKING` 중 하나이며, `routeMeters`(0~2000m), `walkedMeters`(0~10000m), `departureLeadMinutes`(0~1440게임분), `lateMinutes`(0~1440게임분)를 보냅니다. 자유 문장 대신 제한된 상태와 수치를 받고, 이 사실로 만든 상황을 대화에 저장해 이후 대사·힌트·재접속에도 사용합니다. 근무 시각이나 관계 수치를 바꾸는 요청은 아닙니다. 이전 클라이언트와 기존 대화에는 이 값이 없어도 됩니다. 복습 대화에는 현재의 이동 상황을 덧붙이지 않습니다.
- `placeCode`(선택)는 대화가 열린 장소 코드입니다. 말을 거는 플레이어가 NPC 바로 옆에 있으므로 Unity는 플레이어가 있는 곳을 보냅니다. 거리 매장 실내에 들어가 있으면 그 매장(`DINER`·`LAUNDROMAT` 등), 아니면 지금 씬의 장소(`TOWN`·`CAFE`·`SHOP`·`HOTEL`·`POLICE_STATION`·`HOME`)이고, 택시 안 대화는 `TAXI`입니다. 대문자 장소 코드만 받으며(형식이 다르면 `VALIDATION_FAILED`), 서버가 "지금 있는 곳은 Suds Laundromat 안이고, 자기 일터가 아니면 들른 것"이라는 사실로 바꿔 이동 사실과 함께 대화에 저장합니다. 일과에 따라 다른 매장에 들른 NPC가 아직 길이나 일터에 있는 것처럼 말하지 않게 하려는 값입니다. 장소 이름은 백엔드 `ConversationPlace`에 있고 워커의 마을 안내(`TOWN_GUIDE`)와 같게 둡니다. 없거나 서버가 모르는 코드면 위치 사실을 넣지 않습니다. 복습 대화에는 덧붙이지 않습니다.
- 직원처럼 `approachLine`이 없는 NPC는 `initiatedBy`와 관계없이 AI가 첫 인사를 만들고 음성을 합성합니다. 이 NPC와 끝낸 대화가 있으면 기억 카드를 함께 넘겨 다시 만난 인사를 합니다. 미리 작성한 인사로 바꿀지는 `[미정]`입니다(6절).
- 첫 대사의 추천 단어는 응답을 기다리게 하지 않도록 따로 만들므로 `suggestWords`는 `null`입니다. 클라이언트가 대사를 보여 준 뒤 CONV-12로 받습니다.
- 마을 길(`placeCode: TOWN`)에서 플레이어가 먼저 시작하는 일반 대화는 NPC의 성격·친밀도·시각과 이동 상황을 첫 인사에 반영합니다. 한두 단어 인사, 지금 하는 일, 주변 관찰, 친밀도에 맞는 인사를 섞습니다. 같은 사용자와 NPC의 최근 일반 대화 첫 인사 4개를 비교합니다. 대소문자·띄어쓰기·구두점 차이만 있는 문장은 다른 짧은 인사와 그 음성으로 바꿉니다. 첫 인사를 확정한 뒤 `delta`로 글자 전체를 보내고 `conversation`의 저장 음성으로 재생합니다.
- 첫 인사를 만들지 못하면 대화를 만들지 않고 AI 오류 코드(`AI_TEMPORARILY_UNAVAILABLE` 등)를 돌려줍니다.
- `runningMissions`는 이 NPC가 대상인 진행 중인 미션이며 상단의 "미션 · …" 표시에 씁니다.
- 장소 연습(PRC-02)으로 시작한 대화의 응답은 `practiceScenarioId`에 상황 ID를 담습니다. 이 API로 시작한 일반 대화와 복습 대화는 `null`입니다.
- `levelApplied`는 사용자의 `selected_level`입니다.
- 1.4의 규칙대로 한 턴과 피드백에 쓸 토큰이 남아 있어야 시작하며, 피드백용 토큰을 예약합니다. 부족하면 `AI_QUOTA_EXHAUSTED`입니다.
- 게스트 대화 횟수 제한은 기본으로 꺼져 있어(`GUEST_CONVERSATION_LIMIT=0`) 게스트도 가입 사용자처럼 제한이 없습니다. 이 값을 1 이상으로 켜면 게스트(AUTH-07)는 한 번이라도 말을 주고받은 대화 수(끝난 대화 포함)가 그 값에 이르면 대화를 시작하지 않고 `AI_QUOTA_EXHAUSTED`입니다. 말을 걸었다가 한 마디도 주고받지 않고 끝낸 대화는 세지 않습니다. `message`는 "게스트는 NPC와 3번까지 대화할 수 있어요. 마을 탐험, 사물 카드, 단어장은 계속할 수 있어요."입니다(횟수는 설정값). 게스트 한도는 다시 채워지지 않으므로 `resumeAt`을 담지 않습니다. 남은 횟수는 AI-01·BOOT-01의 `remainingConversations`이고, 시작하기 전 한도는 AUTH-08의 `conversationLimit`입니다.
- 가입 사용자는 NPC 대화 횟수 제한이 없습니다. 대화 하나의 길이는 사용자 발화 30턴으로 제한합니다(CONV-03).
- 게스트 한도에 이르러도 진행 중인 대화의 발화와 종료 피드백은 계속 사용할 수 있습니다.
- 진행 중인 대화가 있으면 `CONVERSATION_ALREADY_ACTIVE`를 `activeConversationId`와 함께 돌려줍니다.
- 첫 인사를 만드는 사이 같은 사용자의 다른 시작 요청이 먼저 저장됐으면, 저장 직전에 다시 확인해 `CONVERSATION_ALREADY_ACTIVE`(게스트가 그 요청으로 횟수를 다 썼으면 `AI_QUOTA_EXHAUSTED`)로 거절합니다. 진행 중인 대화는 하나만 남습니다.
- 같은 `requestId`로 다시 보내면(응답 시간 초과 뒤 재시도 등) 대화를 새로 만들지 않고 그 요청으로 시작한 대화의 응답(첫 대사 포함)을 돌려줍니다(`conversations.request_id`). 이때는 `CONVERSATION_ALREADY_ACTIVE`나 게스트 횟수로 거절하지 않고 횟수도 다시 세지 않습니다. 처음 요청이 첫 인사를 기다리는 사이 재요청이 먼저 저장했어도 같습니다. 다른 사용자가 쓴 `requestId`면 `409 REQUEST_ID_CONFLICT`입니다.
- AI를 쓸 수 없는 상태에서는 행인 말걸기를 발생시키지 않습니다. `[제안]` 클라이언트가 BOOT-01·AI-01의 상태로 판단하고, 최종 확인은 이 API에서 합니다.

**스트리밍** — 요청 헤더에 `Accept: text/event-stream, application/json`을 붙이면 AI가 만드는 첫 인사의 글자와 문장 조각 음성을 만들어지는 대로 SSE(`text/event-stream`)로 받습니다. 첫 인사도 대화의 첫 턴이라 이벤트 형식은 CONV-03 **스트리밍**과 같고, 마지막 이벤트만 `conversation`입니다. 요청 본문, 첫 인사 → 대화 저장 순서, 같은 `requestId` 처리는 위와 같습니다. Accept에 `text/event-stream`이 없으면 지금처럼 JSON 한 번(201)으로 응답합니다. 첫 인사 전체와 음성을 다 기다리는 것보다 첫 글자가 먼저 보입니다.

| 이벤트 | data | 뜻 |
| --- | --- | --- |
| `delta` | `{ "text": "Hi!" }` | 첫 인사 글자 조각. CONV-03과 같습니다. |
| `audio` | `{ "seq": 0, "text": "Hi!", "wav": "<base64>" }` | 문장 조각 음성. CONV-03과 같습니다. |
| `conversation` | 위 응답 201과 같은 본문 | 마지막 이벤트. 저장된 대화와 첫 턴(`openingTurn`), [다시 듣기] 주소(`openingTurn.speech`), 대화 상태. |
| `error` | `{ "status": 503, "error": { "code", "message", "details" } }` | 첫 인사를 보내기 시작한 뒤 난 오류. CONV-03과 같습니다. 대화는 저장하지 않으며 같은 `requestId`로 다시 보낼 수 있습니다. |

- 응답 상태는 첫 이벤트를 보낼 때의 200입니다. 첫 인사를 만들기 전에 난 오류(`CONVERSATION_ALREADY_ACTIVE`, `SCENE_CLOSED`, `AI_QUOTA_EXHAUSTED` 등)는 스트림을 시작하기 전이라 지금과 같은 HTTP 상태와 JSON 오류 본문입니다.
- 말걸기 대사로 시작하거나(`approachLine`) 같은 `requestId`로 다시 보내 저장된 대화를 돌려줄 때는 AI를 부르지 않으므로 `conversation` 하나만 옵니다.
- 사용자가 화면을 닫거나 연결이 끊겨도 서버는 첫 인사를 끝까지 받아 대화를 저장합니다. 클라이언트는 `conversation`이나 `error` 없이 스트림이 끝나면 같은 `requestId`로 다시 보내 저장된 대화를 받습니다.
- 조각 음성을 재생한 첫 인사는 `conversation`을 받은 뒤 전체 음성을 다시 자동 재생하지 않습니다. [다시 듣기]는 `openingTurn.speech`의 전체 음성(CONV-06)을 씁니다.
- 복습 대화 시작과 장소 연습 시작(PRC-02)은 아직 JSON 응답만 있습니다.

#### CONV-02 `POST /api/v1/speech/transcriptions`

`multipart/form-data`로 녹음을 보내고 전사만 받습니다. 발화 턴은 만들지 않습니다.

| 필드 | 설명 |
| --- | --- |
| `requestId` | 요청 ID |
| `purpose` | `TURN`(자유 대화 발화), `TUTORIAL`(튜토리얼 대본 대사), `LEVEL_TEST`(레벨 테스트 말하기 답) |
| `conversationId` | `TURN`일 때 진행 중인 대화 ID. `TUTORIAL`·`LEVEL_TEST`면 보내지 않습니다. |
| `audio` | 녹음 파일 |

```json
// 응답 200
{ "sttRawText": "Medium size, please", "durationMs": 2300 }
```

- 화면 15의 전사 확인에 `sttRawText`를 보여줍니다. 인식이 틀렸으면 [다시 녹음]으로 다시 말하고, 맞으면 [보내기]로 CONV-03에 보냅니다. 전사 내용을 직접 고치는 기능은 넣지 않았습니다(6절).
- 화면 11의 튜토리얼에서는 결과를 대본의 대사와 비교하는 데 씁니다. 튜토리얼 음성 인식은 사용자 한도에서 차감하지 않습니다(1.4).
- 무음·인식 실패는 `STT_NO_SPEECH`입니다.
- 인식 대상은 영어입니다. 한국어로 말하면 거르거나 번역하지 않고 한글 그대로 전사합니다. 그 문장의 번역은 CONV-03이 맡습니다.
- 서버는 인식에 철자 힌트를 함께 보냅니다. 학습자 닉네임(게스트 제외), `TURN`이면 대화 NPC 이름·같은 장소 NPC 이름·그 장소의 메뉴·상품 이름(예: 카페의 `Iced Americano`)과 직전 NPC 대사, 그리고 늘 마을·가게 이름(`Maple Town`, `Maple Bakery` 등)입니다. 장소별 단어와 마을 단어는 [`content/speech-words.json`](../../backend/src/main/resources/content/speech-words.json)에 둡니다.
- 녹음 형식은 `webm`, `ogg`, `mp4`, `wav`, `mpeg`이며 Unity는 16kHz 모노 16비트 WAV로 보냅니다. `audio` 파일 부분에 `Content-Type`(예: `audio/wav`)을 붙입니다.
- 녹음은 최대 3MB입니다. 서버는 AI를 호출하기 전에 크기와 형식을 확인해 `AUDIO_TOO_LARGE`(413), `AUDIO_FORMAT_UNSUPPORTED`(415)로 거절합니다. 녹음 길이(최대 15초, 무음이 3초 넘게 이어지면 자동 종료)는 클라이언트 녹음 규칙으로 지킵니다. `durationMs`는 인식 뒤에 알 수 있으므로 거절 기준으로 쓰지 않습니다.
- 받은 음성은 전사 후 보관하지 않습니다. 녹음 음성 활용(`TRAINING_VOICE`)에 동의한 사용자의 대화·튜토리얼 녹음만 인식 결과와 함께 학습용으로 보관합니다. 레벨 테스트(`LEVEL_TEST`) 녹음은 보관하지 않습니다(`training_recordings`, [AI 개선 데이터 활용 기준](../ai-training-data.md)). 무음·인식 실패는 보관하지 않고, 보관에 실패해도 전사 응답은 그대로 돌려줍니다.
- 음성은 외부 음성 인식 공급자로 가므로 지금 버전의 음성 처리(`VOICE`)에 동의한 사용자만 쓸 수 있습니다. 동의하지 않았으면 녹음을 확인하거나 AI를 부르기 전에 `403 VOICE_CONSENT_REQUIRED`로 거절합니다. 클라이언트는 BOOT-01의 `user.voiceConsent`로 미리 녹음을 막고, 회색 [말하기]를 누르면 동의 창(USER-03)을 띄웁니다.

#### CONV-03 `POST /api/v1/conversations/{conversationId}/turns`

```json
// 요청
{
  "requestId": "0d4f6a0b-2a7c-4e3f-8d0e-5b9a1c2d3e4f",
  "inputType": "VOICE",
  "text": "Medium size, please",
  "sttRawText": "Medium size, please",
  "speechSpeed": 1.1
}

// 응답 200
{
  "userTurn": {
    "turnId": 104,
    "seq": 4,
    "speaker": "USER",
    "text": "Medium size, please",
    "rude": false,
    "translation": null,
    "betterExpressions": []
  },
  "npcTurn": {
    "turnId": 105,
    "seq": 5,
    "speaker": "NPC",
    "text": "Great! Is that for here or to go?",
    "suggestWords": null,
    "speech": { "status": "READY", "url": "/api/v1/turns/105/speech" }
  },
  "conversation": { "status": "ACTIVE", "endReason": null, "rudeCount": 0, "rudeLimit": 3, "affinity": 12, "timeLimitAt": "2026-11-05T14:05:00+09:00", "graceUntil": "2026-11-05T14:05:30+09:00", "closing": false, "finalTurnsRemaining": 0 },
  "missionUpdates": [],
  "policeDispatch": null,
  "taxiDestination": null
}
```

- `text`는 사용자가 보낸 최종 문장입니다. `inputType`이 `VOICE`면 STT 결과를 그대로 `text`와 `sttRawText`로 보내고, 마이크를 쓸 수 없어 텍스트로 입력하면 `TEXT`로 보냅니다.
- `speechSpeed`(선택, 0.8~1.5)는 이번 NPC 대답 음성의 빠르기입니다. 규칙은 CONV-01과 같습니다(없으면 `1.1`, 범위 밖은 `VALIDATION_FAILED`).
- 영어, 한국어, 한영 혼용, 미완성 발화를 모두 받습니다. 최대 길이는 서버 설정값이며 넘으면 `VALIDATION_FAILED`입니다.
- 추천 단어는 NPC 응답을 기다리게 하지 않도록 따로 만듭니다. 응답의 `suggestWords`는 `null`이고, 클라이언트가 대사를 보여 준 뒤 CONV-12로 받아 대사 아래에 보여줍니다. 대화가 끝난 턴에는 요청하지 않습니다.
- 같은 `requestId`로 다시 보내면 턴을 새로 만들지 않고 이전 응답을 그대로 돌려줍니다(`turns.request_id`). 화면 19의 [다시 시도]가 이 규칙을 씁니다.
- 시간 제한 대신 대화당 사용자 발화 30턴을 허용합니다. 30번째 발화의 NPC 답변까지 저장한 뒤 `ENDED_BY_NPC`, `endReason: TURN_LIMIT`으로 종료합니다. 무례 누적이나 용건 완료가 먼저 발생하면 그 종료 이유를 유지합니다. 같은 `requestId`의 재시도는 저장된 답변을 돌려줍니다.
- 기존 응답 필드 `timeLimitAt`, `graceUntil`은 null이고 `closing`, `finalTurnsRemaining`은 각각 false와 0입니다.
- 대화 시작·발화 전송은 같은 사용자·`requestId`의 처리 중 재요청도 순서대로 처리하여, 먼저 저장된 결과를 재사용하고 AI를 중복 호출하지 않습니다. 잠금은 현재 단일 백엔드 프로세스 안에서 관리하며 완료·실패 뒤 해제합니다. 실패한 요청은 같은 ID로 다시 시도할 수 있습니다. 워커에 보낼 앞선 대화는 DB에서 최근 40줄만 조회합니다.
- TTS가 실패해도 응답은 성공으로 돌려주고 `speech.status`를 `FAILED`로 표시합니다. 클라이언트는 자막을 보여주고 CONV-07로 다시 시도합니다.
- AI가 미션 완료나 대화 종료를 제안하면 서버가 검증해 확정합니다(`ai_candidates`). 확정된 결과만 `missionUpdates`와 `conversation.status`에 반영합니다. 한 턴에서 여러 미션이 함께 완료될 수 있습니다.
- 서버는 한 턴에서 무례 판정과 의도·번역 판정을 같은 문장으로 동시에 요청하고 둘 다 기다린 뒤 NPC 대답을 만듭니다. 스트리밍 요청에서 판정 결과가 대답 지시문에 들어가지 않는 대화는 대답을 먼저 시작합니다(아래 **스트리밍**).

**경찰 신고** — 경찰서 창구의 경찰관(`POLICE_OFFICER`·`POLICE_SERGEANT`·`POLICE_PATROL_OFFICER`·`POLICE_ROOKIE`)에게 한 말은 의도 판정이 정해진 단어가 아니라 문맥으로 출동할 일(도난·분실·사고·싸움·수상한 사람 등)인지와 장소를 고릅니다. 출동할 일과 장소가 나오면(본길이나 장소를 모르면 본길 순찰) `policeDispatch`에 `{ "placeCode": "BAKERY" }`처럼 출동할 곳을 담고, 경찰관이 직접 순찰차로 간다고 말하며 그 턴에 대화를 마칩니다(`ENDED_BY_NPC`). `placeCode`는 마을 장소 코드(`HOME`·`HOTEL`·`CAFE`·`SHOP`·`POST_OFFICE`·`DINER`·`BANK`·`LAUNDROMAT`·`FLOWER_SHOP`·`BARBER`·`BAKERY`·`PARK`·`ARCADE`·`HOME_STORE`·`ICE_CREAM_PLAZA`)이고, `null`이면 본길 순찰입니다. 신고만 하고 장소를 말하지 않았으면 출동하지 않고 경찰관이 장소를 묻습니다. 비번(`npcOffDuty`) 경찰관과의 대화, 출동하지 않는 턴, 무례로 센 턴에서는 `policeDispatch`가 `null`입니다. 접수 여부와 장소는 사용자 턴에 저장하므로 같은 `requestId`로 다시 보내도 동일한 출동 정보를 돌려줍니다. 클라이언트는 `userTurn.turnId`로 중복 접수를 막고 추가 신고를 순서대로 보관합니다. 경찰관은 앞선 출동이 끝난 뒤 차례로 출동하므로 정확한 출발 시각은 약속하지 않습니다. 판정 상태는 백엔드 `PoliceDesk`, 평가는 `ai/tools/police_report_eval.py`입니다.

**택시 목적지** — 택시 기사(`TAXI_DRIVER`)에게 한 말은 의도 판정이 정해진 단어가 아니라 최근 대화 6줄까지 보고 목적지를 고릅니다. "거기 가 주세요"처럼 앞에서 말한 장소를 가리키거나 "The first one."처럼 기사가 말한 곳에 답해도 알아듣습니다. 택시가 가는 곳으로 가자고 하면 `taxiDestination`에 `{ "placeCode": "DINER" }`처럼 갈 곳을 담고, 기사가 지금 그곳으로 간다고 말합니다. `placeCode`는 택시 목적지 코드(`HOME`·`HOTEL`·`CAFE`·`SHOP`·`POST_OFFICE`·`DINER`·`BANK`·`LAUNDROMAT`·`FLOWER_SHOP`·`BARBER`·`POLICE_STATION`)입니다. 가는 도중에 다른 곳을 말하면 새 목적지를 보냅니다. 택시가 가지 않는 곳(북쪽 빵집·공원·오락실 등, 마을 밖)이면 출발하지 않고 기사가 그곳은 가지 않는다고 말한 뒤 가까운 하차 장소(공원·빵집·오락실은 우체국 등)를 권하고, 어느 곳인지 모르면 갈 수 있는 곳을 알려 주며 묻습니다. 기사가 갈 곳을 권할 때도 택시가 가는 곳 안에서만 고릅니다. 택시에서는 대화가 끝나면 승객이 내리므로 작별·용건 끝냄(`LEAVE`·`TASK_DONE`)으로 대화를 마치지 않고, 도착하면 클라이언트가 대화를 끝냅니다. 쉬는 중(`npcOffDuty`)인 기사와의 대화, 목적지를 말하지 않은 턴, 무례로 센 턴에서는 `taxiDestination`이 `null`입니다. 목적지는 사용자 턴에 저장하므로 같은 `requestId`로 다시 보내도 같은 값을 돌려줍니다. 판정 상태는 백엔드 `TaxiRide`, 평가는 `ai/tools/taxi_destination_eval.py`입니다.

**스트리밍** — 요청 헤더에 `Accept: text/event-stream, application/json`을 붙이면 NPC 대사 글자와 문장 조각 음성을 만들어지는 대로 SSE(`text/event-stream`)로 받습니다. 요청 본문, 판정 → 대답 순서, 턴 저장, 같은 `requestId` 처리는 위와 같습니다. Accept에 `text/event-stream`이 없으면 지금처럼 JSON 한 번으로 응답합니다. 전송→첫 글자·첫 음성이 대답 전체를 기다리는 것보다 짧습니다.

| 이벤트 | data | 뜻 |
| --- | --- | --- |
| `delta` | `{ "text": "Great!" }` | NPC 대사 글자 조각. 받는 대로 대사에 이어 붙입니다. |
| `audio` | `{ "seq": 0, "text": "Great!", "wav": "<base64>" }` | 문장 조각 음성. PCM16 WAV(립싱크 음소 청크 포함)를 base64로 담습니다. `seq` 순서대로 이어 재생합니다. 합성이 늦거나 실패한 조각은 `wav: null`이며 그 조각은 자막만 보입니다. |
| `turn` | 위 응답 200과 같은 본문 | 마지막 이벤트. 저장된 턴 ID, [다시 듣기] 주소(`npcTurn.speech`), 대화 상태, 미션 변화. |
| `error` | `{ "status": 503, "error": { "code", "message", "details" } }` | 대사를 보내기 시작한 뒤 난 오류. `status`는 JSON으로 응답했다면 보냈을 HTTP 상태이고 `error`는 1.5의 공통 오류 본문입니다. 턴은 저장하지 않으며 같은 `requestId`로 다시 보낼 수 있습니다. |

- 의도·무례 판정까지 난 오류(`CONVERSATION_NOT_ACTIVE`, 행동 묘사만 있는 말의 `VALIDATION_FAILED`, `REQUEST_ID_CONFLICT`, 판정 AI 오류 등)는 스트림을 시작하기 전이라 지금과 같은 HTTP 상태와 JSON 오류 본문입니다. 그래서 Accept에 `application/json`도 함께 넣습니다.
- 같은 `requestId`로 다시 보내 저장된 결과를 돌려줄 때는 `turn` 하나만 옵니다.
- 사용자가 화면을 닫거나 연결이 끊겨도 서버는 대답을 끝까지 받아 턴을 저장합니다. 클라이언트는 `turn`이나 `error` 없이 스트림이 끝나면 같은 `requestId`로 다시 보내 저장된 결과를 받습니다(JSON이든 스트림이든 같습니다).
- **대답 먼저 시작**: 판정 결과가 대답 지시문에 들어가지 않는 대화(이 NPC에게 판정할 미션·장소 연습 목표가 없고 경찰 창구·택시가 아닌 대화. 미션 중 미션과 상관없는 NPC와의 자유 대화와 미션을 마친 뒤의 대화도 포함)는 무례·대화 사건이 없다고 보고 NPC 대답을 판정과 동시에 시작합니다. 받은 조각은 판정이 끝날 때까지 보내지 않습니다. 판정 뒤에 정한 대답 요청(행동 묘사를 지운 말, 지시문, 무례·칭찬·사과 반응)이 같으면 그 대답을 보내고, 다르면 먼저 시작한 대답을 버리고 다시 부릅니다. 클라이언트가 받는 이벤트와 순서는 같고 버린 대답의 조각은 오지 않습니다. 버린 만큼 AI 사용량이 늘어 `NPC_EARLY_REPLY=false`로 끌 수 있습니다([백엔드 README](../../backend/README.md#준비)). JSON 응답은 판정을 기다립니다.
- 조각 음성을 재생한 턴은 `turn`을 받은 뒤 턴 전체 음성을 다시 자동 재생하지 않습니다. [다시 듣기]는 `npcTurn.speech`의 턴 전체 음성(CONV-06)을 씁니다.
- 대화 시작(CONV-01)의 첫 인사도 같은 방식으로 받을 수 있습니다(CONV-01 **스트리밍**).

**학습자 영어 표현** — `userTurn.translation`은 학습자 말의 영어 번역 또는 문법 교정문 `{ "english": "Medium size, please." }`입니다. 한국어·한영 혼용은 영어로 번역하고, 잘못된 영어 문법은 원래 의미를 보존해 최소한으로 교정합니다. 생성된 표현이 있으면 무례 판정과 관계없이 대화창 옆 영어 표현 카드에 담습니다(교정문은 "이렇게 말하면 자연스러워요", 번역은 "영어로는 이렇게 말해요"). Unity는 학습자가 한 말과 달라진 낱말에 밑줄을 긋습니다. 정상 구어체·짧은 답·대소문자/구두점만 다른 경우에는 `null`입니다. `userTurn.text`와 전사 원문은 교정문으로 대체하지 않으며 퀘스트·피드백은 계속 원문을 사용합니다. API 필드와 Unity 표시 경로는 유지합니다.

**다른 표현** — `userTurn.betterExpressions`는 학습자가 한 말과 같은 뜻을 다르게 말하는 자연스러운 문장 1~3개입니다(예: `["I'd like to speak Korean."]`). 더 나은 정답이 아니라 골라 쓸 수 있는 다른 표현이며, 맞게 말했을 때도, `translation`이 있을 때(틀린 문장·한국어 섞임)도 줍니다. 학습자가 한 말·`translation`과 같은 문장은 빼고, 의도 판정이 실패하면 빈 목록입니다. 이름은 처음 만들 때의 `betterExpressions`를 그대로 씁니다. `turns.better_expressions`에 남기며 퀘스트·피드백 판정에는 쓰지 않습니다. Unity는 대화창 옆 카드 아래에 [다른 표현 N개 더 보기]로 접어 두고, 펼치면 "이렇게도 말할 수 있어요" 아래에 번호 칸으로 보여 줍니다.

**무례 판정** — 학습자 말이 무례한지(욕설·위협·모욕·조롱·하대) 턴마다 판정하고, 대화 하나 안에서 셉니다.

| 판정 | 이번 턴 | NPC 대답 | 대화 |
| --- | --- | --- | --- |
| 무례, `rudeCount`가 `rudeLimit`보다 작음 | `rude: true`, `rudeCount` +1. 미션·대화 상태에 반영하지 않음 | 무례하다고 차분하게 지적 | 계속 |
| 무례, `rudeCount`가 `rudeLimit`에 닿음 | 위와 같음 | 더는 이야기하지 않겠다고 말함 | `ENDED_BY_NPC`, `endReason: RUDE` |
| 무례 아님 | `rude: false` | 평소대로 | 계속 |
| 판정 실패 | `rude: false`. 무례로 세지 않고 평소대로 처리 | 평소대로 | 계속 |

- `rudeLimit`은 서버 설정값(3)입니다. 새 대화는 0부터 셉니다.
- `conversation.affinity`는 이 턴을 처리한 뒤 이 NPC의 호감도(-100~100)입니다. 무례로 센 턴은 깎인 값이고([무례 처리 합의안](rudeness-handling.md) 3절), 인사·칭찬·사과·용건 끝냄 같은 대화 사건이 있는 턴은 오른 값입니다([AI 워커 명세](ai-worker.md) 2.2절, 같은 사건은 한 대화에서 한 번). 대화 화면은 NPC 이름 옆에 호감도를, 무례 경고 칸에 `rudeCount`/`rudeLimit`를 보여 줍니다. 같은 `requestId`로 다시 보내 이전 응답을 돌려줄 때도 호감도는 지금 값입니다.
- 판정 결과와 실패 이유는 `turns.guard_abusive`, `turns.guard_error_code`에 남깁니다. 판정 실패를 무례나 무례 아님으로 바꿔 저장하지 않습니다. AI 워커에 닿지 못해 판정하지 못한 턴은 `guard_error_code`가 `worker_unavailable`이고 대화는 평소대로 이어집니다.
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
    "rudeLimit": 3,
    "affinity": 12
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
  "conversation": { "status": "ENDED_BY_NPC", "endReason": "RUDE", "rudeCount": 3, "rudeLimit": 3, "affinity": 0 },
  "missionUpdates": []
}
```

- `endReason`(`conversations.end_reason`)은 `NPC_ENDED`, `QUOTA_EXHAUSTED`, `RUDE`, `TURN_LIMIT` 등입니다. 예전 기록에는 `TIME_LIMIT`이 남을 수 있습니다. 대화가 `ACTIVE`가 아니면 `CONVERSATION_NOT_ACTIVE`입니다.

**마무리** — 무례가 아닌 턴에서 서버가 정하면, NPC가 그 대답으로 따뜻하게 마무리하고(새 질문 없음) 대화를 `ENDED_BY_NPC`, `endReason: NPC_ENDED`로 끝냅니다(2026-11-04 결정). 줄 사건은 의도 판정이 고릅니다([AI 워커 명세](ai-worker.md) 2.2절).

| 대화 | 마무리하는 턴 |
| --- | --- |
| 이 NPC에게 남은 미션 목표가 있음(첫날 연계 미션·마을 이야기) | 이 턴으로 그 목표가 모두 끝났을 때, 또는 학습자가 작별했을 때(`LEAVE`, 목표가 남아도 끝내고 다시 말을 걸어 이어 할 수 있음) |
| 그 밖의 대화 | 학습자가 작별했거나(`LEAVE`) 용건을 끝냈다고 했을 때(`TASK_DONE`, 예: "That's all, thanks.") |

- NPC가 끝낸 대화도 CONV-08로 피드백을 받습니다. 기억·관계 갱신도 정상 종료처럼 합니다.

#### CONV-04 `POST /api/v1/turns/{turnId}/meaning`

```json
// 요청
{ "requestId": "..." }

// 응답 200
{
  "turnId": 105,
  "meaningKo": "좋아요! 매장에서 드실 건가요, 가져가실 건가요?",
  "glossary": [
    { "text": "Great", "meaningKo": "좋아요" },
    { "text": "for here", "meaningKo": "매장에서" },
    { "text": "to go", "meaningKo": "포장" }
  ]
}
```

- 화면 15의 [뜻 보기]입니다. NPC 턴에만 요청할 수 있습니다. 사용자 턴과 다른 사용자의 턴은 `404 NOT_FOUND`입니다.
- 한 번 만든 뜻은 저장하고(`turns.meaning_ko`, `turns.glossary`) 다시 요청하면 AI를 호출하지 않습니다. `turns.glossary`는 응답의 `glossary`와 같은 `[{ text, meaningKo }]` JSON입니다.
- 서버는 NPC 턴(CONV-01 첫 대사, CONV-03 대답)을 저장한 직후 이 뜻을 백그라운드로 미리 만들어 저장합니다. 대화 시작·발화 응답은 이를 기다리지 않으며, AI 호출 기록에는 그 대사를 만든 요청 ID로 남습니다. 그래서 학습자가 [뜻 보기]를 누르면 대부분 저장된 뜻을 바로 받습니다. 미리 만드는 중에 요청하면 AI를 다시 부르지 않고 그 결과를 기다리고, 미리 만들지 못했으면 이 요청이 새로 만듭니다. 학습자가 열지 않는 대사에도 뜻을 만들므로 NPC 턴마다 뜻 보기 비용이 듭니다.
- 뜻과 단어 풀이는 AI 워커 `/v1/meaning`이 만들며, 대화 난이도(`levelApplied`)를 함께 보냅니다. `glossary`는 대사에 그대로 있는 내용어(명사·본동사·형용사·부사)와 숙어·구동사·관용 표현·정해진 문장 틀(`Would you like` 등)을 나온 순서대로 최대 25개까지 담고, 이름·숫자와 혼자 있는 기능어는 뺍니다. `just`, `still`처럼 쉬운 말도 빼지 않으며, 숙어 안의 내용어도 따로 담아(`grab a seat`, `grab`, `seat`) 서로 겹칠 수 있습니다. 한글 표기만 되는 외래어의 뜻은 무엇인지 풀어 쓰고 표기는 괄호에 둡니다(`front desk` → `호텔 접수·안내 창구(프런트)`). 그런 말이 없으면 빈 배열입니다.
- AI 호출이 실패하면 1.5의 AI 오류 코드를 돌려주고 아무것도 저장하지 않습니다. 다시 요청하면 새로 만듭니다.

#### CONV-05 `POST /api/v1/turns/{turnId}/hint`

```json
// 요청
{ "requestId": "..." }

// 응답 200
{ "turnId": 105, "hintText": "For here, please.", "hintMeaningKo": "매장에서 먹을게요." }
```

- 화면 15의 [힌트]는 미리 받은 추천 문장을 펼치거나 가립니다. 펼친 문장을 직접 눌렀을 때만 입력창에 넣습니다. CONV-05는 실제 열기 기록을 남기며, CONV-12에서 준비한 문장이 있으면 모델 호출 없이 저장된 결과를 반환합니다. 미리 받기에 실패한 경우에만 문장을 생성합니다. 사용자 턴과 다른 사용자의 턴은 `404 NOT_FOUND`입니다.
- 만든 문장과 뜻은 저장해 다시 쓰고(`turns.hint_text`, `hint_meaning_ko`), 처음 열면 `hint_opened_at`을 기록합니다. 다시 요청해도 처음 연 시각은 바꾸지 않습니다. 뜻은 워커가 같은 힌트 응답에 포함한 한국어를 사용하며 추가 모델 호출을 하지 않습니다. 기존에 영어만 저장한 힌트의 `hintMeaningKo`는 null일 수 있으며, 이를 채우려고 모델을 다시 호출하지 않습니다.
- `hintText`는 AI 워커 `/v1/hint`가 주는 추천 문장 중 첫 문장(가장 쉬운 문장)입니다. 그 NPC 턴까지의 대화·대화 난이도·이 대화에 적용되는 학습 목표로 만듭니다.
- AI 호출이 실패하면 1.5의 AI 오류 코드를 돌려주고, 추천 문장이 하나도 오지 않으면 `502 AI_RESPONSE_INVALID`입니다. 두 경우 모두 저장하지 않습니다.
- 자유 대화의 힌트는 추천 단어·추천 문장 2단계입니다. 요구사항명세서의 3단계 힌트(키워드 → 문장 시작 → 예문)와 다릅니다(5.2).

#### CONV-12 `POST /api/v1/turns/{turnId}/suggest-words`

```json
// 요청
{ "requestId": "..." }

// 응답 200
{
  "turnId": 105,
  "suggestWords": ["to go", "dine in"],
  "hintText": "For here, please.",
  "hintMeaningKo": "매장에서 먹을게요."
}
```

- NPC 대사(CONV-01·03 응답)를 먼저 보여 준 뒤 추천 단어·문장·한국어 뜻을 한 응답으로 받아 저장합니다. 문장은 기본적으로 가리고, [힌트]를 누르면 저장한 문장을 즉시 펼칩니다. 기다리는 동안 입력과 [보내기]를 막지 않습니다. 준비 중에 힌트를 눌러도 같은 요청을 기다리며 문장 생성을 중복 호출하지 않습니다.
- 문장만 생성하지 못하면 단어는 반환하고 `hintText`·`hintMeaningKo`는 null입니다. 미리 저장하는 동안 `hint_opened_at`은 기록하지 않고 실제로 펼칠 때 CONV-05로 최초 열기 시각을 남깁니다. 단어·문장은 각각 기존 워커의 suggest·hint 생성 경로를 사용하며, AI 호출 기록이 충돌하지 않도록 서로 다른 요청 ID를 사용합니다.
- 사용자가 답해야 하는 NPC 턴에 요청합니다. 사용자 턴과 다른 사용자의 턴은 `404 NOT_FOUND`입니다.
- 만든 단어는 저장해 다시 쓰고(`turns.suggest_words`) 다시 요청하면 AI를 호출하지 않습니다. CONV-10 대화 상세에도 아래 규칙으로 고른 단어가 담깁니다.
- 단어 후보는 AI 워커 `/v1/suggest`가 그 NPC 턴까지의 대화·대화 난이도·이 대화에 적용되는 학습 목표로 5개까지 만듭니다. 주어로 시작하거나(`I'm sorry`) `.`·`?`·`!`로 끝나는 문장 같은 말은 워커가 빼되, 그러면 2개보다 적게 남을 때만 다시 넣습니다.
- 응답의 `suggestWords`는 후보에서 3개까지 고릅니다. 같은 응답의 추천 문장(`hintText`)에 그대로 들어 있는 여러 단어 구(`have a good`)는 문장을 잘라 온 말이라 건너뜁니다(대소문자·문장 부호 무시). 한 단어(`bag`)는 추천 문장에 있어도 보냅니다.
- 건너뛴 결과 2개보다 적게 남으면 건너뛴 구를 짧은 것부터 다시 넣어, 후보가 2개 이상이면 추천 단어가 비거나 1개만 보이지 않게 합니다. 고른 단어는 워커가 보낸 순서대로 보내고, 저장한 후보(`turns.suggest_words`)는 그대로 둡니다.
- AI 호출이 실패하면 1.5의 AI 오류 코드를 돌려주고, 단어가 하나도 오지 않으면 `502 AI_RESPONSE_INVALID`입니다. 두 경우 모두 저장하지 않습니다.
- 추천 단어는 자유 대화 힌트의 첫 단계(CONV-05)라 AI 호출 기록(`ai_requests.kind`)은 `HINT`로 남깁니다. 사용량 항목의 단계 이름(`suggest`)으로 힌트 문장과 구분합니다.

#### 저장 공연 `POST /api/v1/conversations/{conversationId}/performance-intent`

- John(`TOWN_JOHN_PARK`)과 진행 중인 본인 소유의 자유 대화에만 사용합니다. 다른 소유자는 404,
  끝난 대화는 `CONVERSATION_NOT_ACTIVE`, 복습은 `REVIEW_IN_PROGRESS`, 다른 NPC는 `VALIDATION_FAILED`입니다.
- 요청: `{"requestId":"UUID","text":"I'd love an encore of that burger beat."}`. 공백이 아닌 500자 이하입니다.
- 응답: `{"intent":"REPLAY_RAP"}` 또는 `{"intent":"CONVERSATION"}`. 사용자 턴·NPC 대사·TTS·번역을 만들지 않습니다.
- 모호한 재생 요청만 분류합니다. 명확한 요청·안내·랩 버튼·뜻 보기는 Unity에 포함된 저장 에셋을 사용합니다.
  `REPLAY_RAP`이면 저장 안내를 재생하고 버튼을 강조하며, `CONVERSATION`만 기존 발화 API로 전달합니다.
- 분류 사용량은 `ai_requests.kind=JUDGE`, 워커 `usage.step=performance_intent`로 기록합니다.
  이 요청은 자동 재시도하거나 결과를 캐시하지 않습니다. 수동 재요청은 다시 모델 비용이 발생할 수 있습니다.
  UI는 분류 중 중복 전송과 종료 후 늦은 결과를 차단하고, 오류일 때 새 대사를 생성하는 대신 버튼을 안내합니다.
- 음성 입력의 STT와 일반 대화의 시작·종료·평가는 기존 경로를 유지합니다.

#### CONV-06·07 NPC 음성

- CONV-06 `GET /api/v1/turns/{turnId}/speech`는 만들어 둔 음성 파일을 돌려줍니다. [다시 듣기]는 이 API를 쓰며 TTS를 새로 만들지 않습니다. 키나 파일이 없거나 보관 기간이 만료됐으면 `404 NOT_FOUND`입니다.
- CONV-07 `POST /api/v1/turns/{turnId}/speech`는 TTS가 실패했을 때 `{ "requestId": "...", "speechSpeed": 1.1 }`로 다시 만듭니다. `speechSpeed`는 CONV-01과 같은 규칙이며, 대화 때의 빠르기가 아니라 지금 고른 빠르기로 만듭니다. 이미 유효한 음성이 있으면 새로 만들지 않고 CONV-06 주소를 돌려줍니다. 정기 정리 전이라도 만료됐거나 저장했던 파일이 없으면 `EXPIRED`로 응답하고 합성하지 않습니다. 다시 만든 음성을 보관 상한 때문에 저장하지 못하면 오류 대신 `speech.status: FAILED`로 응답합니다.
- NPC 음성 파일 키는 `turns.npc_audio_key`에 저장합니다. 앱 Compose는 환경별 영구 볼륨을 백엔드의 `/app/data/npc-audio`에 연결하며, 저장 위치와 배포 시 보존 기준은 [NPC 음성 저장 운영 문서](../npc-audio-storage.md)를 따릅니다.
- 보관 정책: 음성 생성 시각(`turns.npc_audio_created_at`)부터 최대 30일, 개발·운영 각각의 NPC 턴 음성 합계 5,000,000,000바이트까지 둡니다(서버 설정값, 매일 정리에서 맞추므로 정리 사이에는 넘을 수 있음). 기존 음성의 보관 시작 시각은 턴 생성 시각으로 초기화하며 재배포·파일 이전·DB 복원으로 기한을 다시 시작하지 않습니다. 정리 순서와 공용 음성·하위 폴더 제외, 백업·복원 기준은 [NPC 음성 저장 운영](../npc-audio-storage.md#보관삭제-정책)을 따릅니다.
- `speech.status`는 `READY`(음성 있음), `FAILED`(처음 합성에 실패했거나 저장하지 못함, CONV-07로 다시 시도), `EXPIRED`(보관 정책으로 재생 차단, `turns.npc_audio_deleted_at`)입니다. 파일 삭제 실패로 재시도 중인 경우도 `EXPIRED`이며 CONV-07을 불러도 다시 만들지 않습니다. 클라이언트는 [다시 듣기]를 숨기고 자막을 유지합니다.
- 내가 말한 문장을 TTS로 다시 들려주는 기능은 필요성을 함께 검토합니다(6절).

#### CONV-08 `POST /api/v1/conversations/{conversationId}/end`

상황 복습 대화는 `reviewCardId`와 `reviewResult`를 함께 돌려주고 `feedback`, `followUp`, `detailedFeedback`은 null입니다. 추가 AI 평가·기억 갱신 대신 저장된 목표 표현의 실제 발화 근거를 한 번 기록합니다. 일반 대화의 두 복습 필드는 null이며 기존 동작을 유지합니다. 자세한 종료·재조회 계약은 [상황 복습장](review-workbook.md#복습-종료-결과와-다시-보기)을 따릅니다.

장소 연습 대화(PRC-02)는 일반 대화처럼 `feedback`과 `detailedFeedback`을 만들고 상황의 목표를 기준으로 평가합니다. NPC 기억 카드는 갱신하지 않고 `followUp`은 null입니다. 연습을 끝냈는지는 PRC-03의 `done`으로 확인합니다.

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
    "errorCode": null,
    "situationExpressions": [
      {
        "expression": "Can I get that to go?",
        "meaningKo": "포장해 주실 수 있나요?",
        "usageKo": "음료를 가지고 나가고 싶을 때 써요.",
        "exampleEn": "Can I get that to go? I'm in a bit of a hurry.",
        "exampleKo": "포장해 주실 수 있나요? 좀 급해서요."
      }
    ],
    "vocabCandidates": [
      {
        "expression": "latte",
        "meaningKo": "라떼",
        "exampleEn": "I'd like an iced latte.",
        "exampleKo": "아이스 라떼 주세요."
      }
    ]
  },
  "completedMissions": [
    { "userMissionId": 12, "title": "카페에서 음료 주문하기" },
    { "userMissionId": 13, "title": "카페 직원에게 추천 메뉴 묻기" }
  ],
  "followUp": {
    "followUpId": 7,
    "npc": { "npcCode": "CAFE_STAFF", "name": "Mia", "roleLabel": "카페 직원" },
    "sceneCode": "CAFE",
    "item": "iced latte",
    "itemKo": "아이스 라테",
    "readyAt": "2026-10-21T14:05:45+09:00",
    "remainingSeconds": 45,
    "ready": false
  }
}
```

- 화면 16의 짧은 피드백을 그립니다. "Mia · 카페 · 발화 4번"은 `npc`, `sceneCode`, `userTurnCount`, "미션 완료"는 `completedMissions`, 개선 표현 상자는 `suggestion`입니다.
- `summary`는 `conversation_feedback.summary`, `reviews`·`suggestion`·`situationExpressions`·`vocabCandidates`는 `conversation_feedback.reviews` JSON(`{ good, improve, suggestion, situationExpressions, vocabCandidates }`)에 저장합니다. 저장 형식은 응답과 따로 관리하므로 응답 필드 이름을 바꿔도 이미 저장된 피드백을 그대로 읽습니다.
- `situationExpressions`는 이 장면에서 자주 쓰는 관용·추천 표현 0~3개입니다(표현, 뜻, 쓰는 때, 학습 예문과 번역). 사용자 발화 평가와 별개로 화면 16·17의 회화 칸에 보여 줍니다. 이 필드를 넣기 전에 만든 피드백과 `DONE`이 아닌 피드백은 빈 배열입니다. 예문은 학습용이며 사용자가 한 말이 아닙니다.
- `vocabCandidates`는 단어장에 저장할 수 있는 단어(표현, 뜻, 학습 예문과 번역)입니다. 피드백의 단어장 후보(`vocab_candidates`)를 단어·숙어·문장 종류와 상관없이 담습니다. `situationExpressions`의 숙어·문장과 문장별 평가(`detailedFeedback`)의 고친 문장 `betterText`도 VOCAB-02 `FEEDBACK`으로 저장할 수 있습니다. 화면 16·17의 [담기]는 이 목록의 `expression`이나 고친 문장을 VOCAB-02 `FEEDBACK`으로 보냅니다. 이 필드를 넣기 전에 만든 피드백과 `DONE`이 아닌 피드백은 빈 배열입니다.
- 피드백은 대화를 시작할 때 예약한 토큰으로 만듭니다(1.4). 토큰이 떨어져 NPC가 대화를 끝낸 경우에도 피드백을 받습니다.
- [대화 종료 (Esc)]와 오류 화면의 [대화 종료]에서 호출합니다. 어떤 오류 상태에서도 종료할 수 있습니다.
- NPC가 대화를 끝낸 경우(`ENDED_BY_NPC`)에도 피드백을 받으려면 호출합니다. 이때 상태는 바꾸지 않고 피드백만 만듭니다.
- `skipFeedback: true`면 피드백을 만들지 않고 `feedback.status`를 `SKIPPED`로 기록하며 예약한 토큰을 풉니다. 와이어프레임에는 피드백 건너뛰기 버튼이 없으며 배치 여부는 `[미정]`입니다.
- 피드백을 만들 수 없으면(생성 실패) 대화는 정상 종료하고 `feedback.status: FAILED`, `feedback.errorCode`에 원인 코드를 담습니다. 화면 16은 이때 가짜 피드백 대신 [다시 만들기]와 [대화 기록 보기]를 보여줍니다(FB-02, CONV-10).
- 사용자 발화가 없는 대화는 AI를 호출하지 않고 `feedback.status: SKIPPED`, `errorCode: NO_USABLE_USER_TURNS`로 기록합니다. AI 워커가 평가할 근거가 부족하다고 판단한 경우도 워커가 준 이유 코드로 `SKIPPED`입니다.
- 응답은 피드백 생성을 기다립니다(AI 워커 상한 60초). 짧은 피드백과 발화별 상세 평가(FB-03)를 AI 워커 `/v1/feedback` 한 번으로 함께 만들어 저장합니다([피드백 연동](feedback-integration.md)).
- 같은 대화를 다시 종료하면 피드백을 새로 만들지 않고 저장된 피드백을 돌려줍니다.
- 사용자가 말한 대화를 끝내면 NPC 기억 카드를 따로 갱신합니다. 이번 대화가 좋았다고 판단되면 이때 그 NPC의 호감도도 올리며(학습자가 3줄 이상 말한 대화, +3), 오른 값은 다음 대화의 CONV-01 `conversation.affinity`에 보입니다. 응답은 카드 갱신을 기다리지 않으며, 갱신에 실패하면 이전 카드와 호감도를 유지합니다([기억 연동](npc-memory-integration.md)).
- `followUp`은 이 대화에서 NPC가 주문·요청을 받아 지금 만들거나 준비하고 있어 곧 건넬 것입니다(음료·음식·빨래 등). 서버가 신고 접수를 기록한 경찰 대화만 순찰을 다녀와 알릴 해결 결과를 판정합니다(`itemKo` 예: "지갑 수색 결과", 90초, AI 워커 `kind: police_call`). 진열된 물건을 그 자리에서 건네는 가게 직원과 바로 태워 주는 택시 기사와의 대화는 판정하지 않습니다(CONV-13). 건넬 것이 없으면 `null`입니다. 모든 NPC·장소에 같은 규칙으로 AI 워커 `/v1/npc/follow-up`([AI 워커 명세](ai-worker.md) 2.7.3절)이 판정하고, 피드백을 만드는 동안 함께 끝냅니다. 판정에 실패하면 `null`이며 종료·피드백에는 영향이 없습니다.
  - 학습자가 말하지 않았거나 무례로 끝난 대화는 판정하지 않습니다. NPC가 마무리한 대화(`NPC_ENDED`)와 학습자가 끝낸 대화는 같습니다.
  - 준비 시각은 판정한 때부터 기다림 정도만큼 뒤입니다. 음료·간식 45초, 조리한 음식·포장 90초, 세탁·수선 180초(실제 시간)입니다. `remainingSeconds`는 서버 시각으로 센 남은 초(준비됐으면 0)이고, `ready`는 준비됐는지입니다. 클라이언트는 받은 때부터 `remainingSeconds`를 줄여 보이고 자기 시계로 `readyAt`을 다시 계산하지 않습니다.
  - 사용자 × NPC마다 기다리는 것은 하나입니다. 같은 NPC에게 새로 받은 약속이 앞의 것을 바꿉니다.
  - 같은 대화를 다시 종료하면 다시 판정하지 않고 저장한 것을 돌려줍니다.
  - 준비된 뒤 그 NPC와 대화를 시작하면 첫마디로 건넵니다(CONV-01 `deliveredFollowUpId`). 화면 왼쪽 기다림 카드와 NPC가 부르는 동작은 Unity가 합니다.

#### CONV-09·10 지난 대화

- CONV-09 `GET /api/v1/conversations?npcCode=&sceneCode=&placeGroup=&cursor=&size=&feedbackOnly=`는 휴대폰 메뉴의 [대화 노트] 창 목록입니다. 최근 시작 순으로 `{ conversationId, npc, sceneCode, placeName, placeGroup, status, endReason, startedAt, endedAt, userTurnCount, feedbackStatus, feedbackUnread, practiceCount }`를 돌려줍니다.

```json
{
  "items": [
    {
      "conversationId": 58,
      "npc": { "npcCode": "CAFE_STAFF", "name": "Mia", "roleLabel": "카페 직원" },
      "sceneCode": "CAFE",
      "placeName": "카페",
      "placeGroup": "FOOD",
      "status": "ENDED_BY_USER",
      "endReason": "USER_ENDED",
      "startedAt": "2026-10-21T14:00:00+09:00",
      "endedAt": "2026-10-21T14:05:00+09:00",
      "userTurnCount": 4,
      "feedbackStatus": "DONE",
      "feedbackUnread": false,
      "practiceCount": 2
    }
  ],
  "nextCursor": null
}
```

- 목록 규칙은 VOCAB-01과 같습니다. `size` 기본값은 20이고 1~50만 받습니다. `nextCursor`는 이전 페이지 마지막 항목의 `conversationId`이며, 숫자가 아니거나 내 대화가 아닌 커서와 범위를 벗어난 `size`는 `400 VALIDATION_FAILED`입니다. 시작 시각이 같으면 나중에 만든 대화가 먼저입니다.
- `npcCode`를 주면 그 NPC와 나눈 대화만 돌려줍니다. 없는 NPC 코드면 빈 목록입니다.
- `sceneCode`(`CAFE`·`HOTEL`·`TOWN` 등)를 주면 그 장소 씬의 NPC와 나눈 대화만 돌려줍니다. 없는 장소 코드면 빈 목록이고, 비어 있거나 30자를 넘으면 `400 VALIDATION_FAILED`입니다.
- `placeGroup`(`FOOD`·`SHOPPING`·`SERVICES`·`SAFETY`·`DAILY`)을 주면 그 장소 묶음의 대화만 돌려줍니다. 대화 노트의 장소 탭(전체·음식·쇼핑·생활 서비스·안전·일상)이 씁니다. 묶음은 VOCAB-01과 같이 NPC의 실제 근무지(`npcs.place_group`)로 정하고, 근무 시간 밖에 만난 대화는 근무지와 관계없이 `DAILY`입니다. 대문자 이름이 정확히 같지 않으면 `400 VALIDATION_FAILED`입니다.
- `placeName`은 NPC가 일하는 곳(카페·우체국), `placeGroup`은 그 대화의 장소 묶음입니다. 근무 시간 밖에 만난 대화는 `placeName`이 `null`, `placeGroup`이 `DAILY`입니다.
- `feedbackOnly=true`는 완성된(`DONE`) 피드백이 있는 대화만 페이지 크기·커서 적용 전에 조회합니다. Unity 대화 노트는 이 옵션을 사용하며 생성 전·건너뜀·실패 항목을 보이지 않습니다. 생략하면 기존 전체 대화 기록을 조회합니다. `feedbackUnread`는 완료됐으나 아직 열지 않은 피드백인지 나타냅니다.
- 진행 중(`ACTIVE`)이거나 시간이 지나 닫힌 대화도 상태 그대로 담습니다. `userTurnCount`는 사용자가 보낸 발화 수, `feedbackStatus`는 짧은 피드백 상태(`DONE`·`FAILED`·`SKIPPED`)이며 피드백을 만들기 전이면 `null`입니다. `npc`는 CONV-10의 NPC 이름표와 같은 형식입니다. `practiceCount`는 이 대화로 [다시 연습]한 횟수(같은 대화로 만든 복습 카드의 `practiceCount`)이며, 연습하지 않았으면 0입니다.
- 저장된 결과만 조회하므로 AI 사용량이 소진돼도 이용할 수 있습니다. 항목을 누르면 CONV-10으로 대화 기록을 엽니다.
- CONV-10은 대화 정보와 모든 턴(`seq` 순서)의 텍스트, 저장된 뜻·추천 단어·추천 문장을 돌려줍니다. NPC 턴에는 저장된 `glossary: [{ text, meaningKo }]`도 함께 담고, 아직 없거나 사용자 턴이면 빈 배열입니다. 화면 15의 텍스트 대화 기록과 화면 16의 [대화 기록 보기]가 이 형식을 씁니다. 저장된 결과만 조회하므로 AI 사용량이 소진돼도 이용할 수 있습니다. 전체 대화 화면에서 빠진 뜻을 처음 읽을 때만 CONV-04로 생성·저장하며, 다시 열기·단어 호버·터치는 저장된 뜻과 풀이를 사용합니다.

#### CONV-11 `GET /api/v1/conversations/active`

- 진행 중인 대화가 있으면 CONV-10과 같은 형식, 없으면 `204 No Content`입니다. 대화 중 새로고침했을 때 화면 15의 대화 기록을 다시 그립니다.
- 새로고침 뒤 이어서 진행할지 종료 처리할지는 `[미정]`입니다. 일정 시간 동안 요청이 없는 대화는 서버가 `EXPIRED`로 닫고 예약한 토큰을 풉니다. 시간 값은 서버 설정값입니다.

#### CONV-13 `GET /api/v1/follow-ups`

```json
// 응답 200
{
  "items": [
    {
      "followUpId": 7,
      "npc": { "npcCode": "CAFE_STAFF", "name": "Mia", "roleLabel": "카페 직원" },
      "sceneCode": "CAFE",
      "item": "iced latte",
      "itemKo": "아이스 라테",
      "readyAt": "2026-10-21T14:05:45+09:00",
      "remainingSeconds": 12,
      "ready": false
    }
  ]
}
```

- NPC가 곧 건네기로 했는데 아직 받지 않은 것입니다. 항목 형식은 CONV-08의 `followUp`과 같고 `readyAt` 순서입니다. 준비된 것도 건네기 전까지 담깁니다. 없으면 빈 배열입니다.
- 새로고침·씬 이동 뒤 왼쪽 기다림 카드를 다시 그릴 때 씁니다. AI를 부르지 않습니다.
- 진열된 물건을 그 자리에서 건네는 가게 직원(드럭스토어·빵집·꽃집·생활용품점·오락실·우체국·은행·이발소)과 요청하면 바로 태워 주는 택시 기사와의 대화는 기다릴 것이 없어 판정하지 않습니다.

#### CONV-14 `POST /api/v1/follow-ups/{followUpId}/dismiss`

- 학습자가 기다림 카드의 X로 닫은 것입니다. 응답은 204이고 본문이 없습니다.
- 건넨 것처럼 기다리는 목록에서 빠지고, 그 NPC가 다시 부르지 않습니다. 다른 사용자의 것이나 이미 건넨 것이면 아무것도 바꾸지 않습니다.

### 4.7 피드백

대화 종료(CONV-08) 응답은 저장된 문장별 평가를 `detailedFeedback` 배열로 함께 제공합니다. 성공한 피드백을 표시할 때 FB-03을 다시 호출할 필요가 없으며, 피드백을 만들지 않은 경우에는 빈 배열입니다. 기존 피드백의 생성·저장 계약과 FB-03 재조회는 유지합니다.

#### 피드백 미확인 알림

- `GET /api/v1/feedback/unread`는 로그인 사용자의 완료됐으나 읽지 않은 피드백 수를 `{ "count": 3 }`으로 돌려줍니다. 휴대폰·목록 열기나 조회로 읽음 상태를 바꾸지 않습니다.
- `POST /api/v1/conversations/{conversationId}/feedback/read`는 해당 사용자의 피드백을 실제로 연 뒤 보내며 204를 돌려줍니다. 본문은 없고 CSRF 헤더가 필요합니다. 반복해도 한 번만 읽음 처리하며 다른 사용자 대화는 404입니다.
- 미확인 여부는 서버의 `read_at`에 저장합니다. 기존 완료 내역은 마이그레이션 때 읽음으로 두고, 이후 완성된 피드백부터 알림을 누적합니다.

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

- 화면 16의 [상세 보기]에서 호출하고 화면 17을 그립니다. 대화 종료(CONV-08) 때 짧은 피드백과 함께 만든 평가를 돌려주며 AI를 다시 호출하지 않습니다. 피드백이 `FAILED`였다면 FB-02로 다시 만들 때 함께 만듭니다.
- 사용자 턴마다 한 항목입니다. 고칠 것이 없는 적절한 발화는 `betterText`가 `null`입니다.
- STT 오인식을 발음 오류로 평가하지 않습니다. 평가 기준은 사용자가 보낸 `text`입니다.
- `idiom.text`는 `turn_feedback.idiom`, `idiom.meaningKo`는 `turn_feedback.idiom_meaning_ko`입니다.

#### FB-04 `POST /api/v1/turn-feedback/{turnFeedbackId}/model-speech`

- 화면 17의 [모범 음성 듣기]입니다. `{ "requestId": "..." }`로 `betterText`의 모범 음성을 만들고 `{ "url": "..." }`를 돌려줍니다.
- 한 번 만든 음성은 `turn_feedback.model_audio_key`로 다시 사용하며 새로 만들지 않습니다. 저장한 파일이 없어졌으면 다시 만듭니다.
- 같은 항목을 거의 동시에 여러 번 요청해도 모두 성공합니다. 서버는 먼저 붙은 음성 하나만 남기고 늦게 만든 파일은 지웁니다. 합성은 요청 수만큼 일어날 수 있으므로 클라이언트는 요청 중에 버튼을 막습니다.
- `url`은 같은 경로의 `GET /api/v1/turn-feedback/{turnFeedbackId}/model-speech`이며 WAV(`audio/wav`)를 돌려줍니다. 아직 만들지 않았으면 `404 NOT_FOUND`입니다.
- 모범 음성 파일은 마지막 재생 뒤 30일, 환경 전체 5GB까지 보관합니다(서버 설정값, [NPC 음성 저장 운영](../npc-audio-storage.md#모범-음성-보관)). 정리로 지워진 뒤 GET은 `404 NOT_FOUND`입니다. 클라이언트는 [모범 음성 듣기]를 누를 때마다 POST를 먼저 보내고, 받은 `url`로 GET합니다. POST가 파일이 없으면 다시 만듭니다.
- 목소리는 사물 발음(WORLD-04)과 같은 `af_heart`, 속도 1.0으로 고정합니다(서버 설정값). 대화한 NPC의 목소리를 쓰지 않습니다.
- 다른 사용자의 피드백, 없는 피드백, 고칠 표현이 없어 `betterText`가 `null`인 항목은 `404 NOT_FOUND`입니다. 클라이언트는 `betterText`가 있는 항목에만 [모범 음성 듣기]를 보여 줍니다.
- 합성 호출은 `ai_requests`에 `kind: TTS`로 기록합니다. 워커 오류는 1.5 오류 응답으로 알립니다.

### 4.8 단어장

MVP는 **단어와 숙어**를 저장합니다. 숙어는 `to go`처럼 두 단어 이상인 표현이며, NPC 대사의 단어 풀이(VOCAB-02 `NPC_LINE`)와 대화 피드백(`FEEDBACK`)에서 저장합니다. 대화 피드백의 추천 문장·상황 표현처럼 문장 부호로 끝나는 표현은 문장(`SENTENCE`)으로 저장합니다. 단어장에는 단어·숙어의 뜻과 함께, 그것을 저장한 **문장 전체와 문장 뜻**을 보여줍니다.

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
      "source": {
        "sourceType": "NPC_LINE",
        "npcName": "Mia",
        "conversationId": 58,
        "placeName": "카페",
        "placeGroup": "FOOD"
      }
    }
  ],
  "nextCursor": null
}
```

- "외운 표현도 보기"는 `includeMemorized`입니다. 목록은 최근 저장순입니다. `counts.all`은 현재 `includeMemorized` 조건을 적용한 개수입니다.
- 기본값은 `includeMemorized=false`, `size=20`이고 `size`는 1~50만 받습니다. `nextCursor`는 이전 페이지 마지막 항목의 `userVocabId`이며, 클라이언트는 받은 값을 그대로 다음 요청의 `cursor`로 보냅니다. 숫자가 아니거나 내 단어장에 없는 항목을 가리키는 커서와 범위를 벗어난 `size`는 `400 VALIDATION_FAILED`입니다.
- `savedAt`은 한국 시각(`+09:00`)으로 보냅니다. 저장한 문장이 없으면 `context`는 `null`입니다.
- 화면 18의 유형 필터(단어·숙어·문장)는 MVP에서 보여주지 않습니다. `vocab.type`(`vocabularies.type`)은 한 단어면 `WORD`, 숙어면 `IDIOM`입니다.
- `vocab.meaningKo`는 단어·숙어의 뜻(`vocabularies.meaning_ko`), `context`는 저장할 때의 문장과 문장 뜻(`user_vocab.context_text`, `context_meaning_ko`)입니다. 같은 단어라도 사용자마다 저장한 문장이 다르므로 `user_vocab`에 둡니다.
- 상세의 "Mia와의 대화" 같은 출처 표시는 `source`로 그립니다. 출처 대화는 `user_vocab.source_conversation_id`, 출처 NPC는 그 대화의 NPC입니다. 사물에서 저장한 단어는 `npcName`, `conversationId`가 `null`입니다.
- `placeName`·`placeGroup`은 출처 대화 NPC가 일하는 곳과 그 장소 묶음(`npcs.place_name`·`place_group`)입니다. 코드 이름이 아니라 실제 근무지 기준입니다(햄버거 가게 야간 직원 `FIRE_CAPTAIN`은 `햄버거 가게`·`FOOD`). 묶음은 `FOOD`(카페·햄버거 가게·빵집), `SHOPPING`(드럭스토어·꽃집·생활용품점·오락실), `SERVICES`(은행·우체국·빨래방·이발소·호텔·택시), `SAFETY`(경찰서), `DAILY`(거리에서 만난 사람)입니다. 근무 시간 밖에 만난 NPC와의 대화(`conversations.npc_off_duty`)는 일하는 곳의 표현이 아니므로 `placeName`이 `null`, `placeGroup`이 `DAILY`입니다. 사물에서 저장한 단어는 둘 다 `null`입니다.
- 단어장 화면은 목록을 끝까지 받아 [대화 표현 | 사물 단어] 탭(`sourceType`이 `NPC_LINE`·`FEEDBACK`이면 대화 표현)과 대화 표현의 장소 묶음 칩, 탭·칩별 개수를 클라이언트에서 나눕니다. 서버 필터는 두지 않습니다.
- `memo`(`user_vocab.memo`)는 사용자가 남긴 메모입니다. 뜻은 수정하지 않습니다.

#### VOCAB-02 `POST /api/v1/vocab`

저장 출처마다 요청 형식이 다릅니다. `text`는 저장할 단어나 숙어 하나입니다.

```json
// 화면 08: 사물 카드에서 저장
{ "requestId": "...", "sourceType": "OBJECT", "wordKey": "sofa" }

// 화면 15: NPC 대사 뜻 보기(CONV-04)의 단어 풀이에서 저장
{ "requestId": "...", "sourceType": "NPC_LINE", "turnId": 105, "text": "latte" }

// 화면 16·17: 피드백의 단어장 후보에서 저장
{ "requestId": "...", "sourceType": "FEEDBACK", "conversationId": 58, "text": "order" }
```

저장할 수 있는 단어와 뜻·문장을 가져오는 곳은 출처마다 정해져 있습니다. 저장할 때 AI나 외부 번역 API를 호출하지 않습니다.

| 출처 | 저장할 수 있는 단어 | 단어 뜻 | 문장 · 문장 뜻 |
| --- | --- | --- | --- |
| `OBJECT` | 사물 카드의 단어 | `vocabularies.meaning_ko` (시드) | 사물 카드 예문 `example` · `example_ko` (없으면 `null`) |
| `NPC_LINE` | 그 턴의 CONV-04 단어 풀이(`glossary`)에 있는 단어·숙어 | `glossary[].meaningKo` | NPC 대사 전문 · CONV-04 대사 뜻 |
| `FEEDBACK` | 그 대화 피드백의 단어장 후보(CONV-08·FB-01 `feedback.vocabCandidates`)와 "이 상황에서 자주 써요" 표현(`feedback.situationExpressions`)에 있는 것 | 후보·표현의 `meaningKo` | 후보·표현의 `exampleEn` · `exampleKo` |
| `FEEDBACK` (문장 교정) | 그 대화 문장별 평가(FB-03 `detailedFeedback`)의 고친 문장 `betterText`. 한국어로 말한 발화의 영어 문장도 포함 | 그 평가의 `reason` | 없음(`null`) |

- 표에 없는 단어·숙어, 아직 뜻 보기(CONV-04)를 하지 않은 대사의 단어는 저장하지 않고 `422 VOCAB_SOURCE_MISMATCH`를 돌려줍니다. 클라이언트는 표의 단어에만 저장 버튼을 보여줍니다.
- 새로 저장하면 `201`, 이미 저장한 단어면 처음 저장한 항목(문장 포함)을 그대로 `200`으로 돌려줍니다. 응답 형식은 VOCAB-01의 항목에 `created` 필드를 더한 것입니다.
- 사전에 없는 단어는 `vocabularies`에 새로 만듭니다. 한 단어면 `type: WORD`, 두 단어 이상인 숙어면 `type: IDIOM`, 문장 부호로 끝나는 여러 단어 표현이면 `type: SENTENCE`입니다(`source`는 `NPC` 또는 `FEEDBACK`). 이미 있는 단어는 기존 뜻을 그대로 씁니다. 대소문자만 다른 단어(문장 첫머리의 `Latte` 등)는 같은 단어로 봅니다.
- `NPC_LINE`은 `source_turn_id`에 `turnId`를, `source_conversation_id`에 그 턴의 대화를 기록합니다. `FEEDBACK`은 `source_conversation_id`에 요청의 `conversationId`를 기록합니다.
- 이 규칙은 대화 쪽이 CONV-04의 단어 풀이와 피드백의 단어장 후보를 저장해 두는 것을 전제로 합니다.
- 피드백을 만들지 못한 대화와 `vocabCandidates`를 넣기 전에 만든 피드백의 단어는 확인할 후보가 없어 `422 VOCAB_SOURCE_MISMATCH`입니다. 다른 대화의 고친 문장도 같은 오류입니다.
- 같은 말이 단어장 후보·상황 표현과 고친 문장에 모두 있으면 뜻과 예문이 있는 후보·표현으로 저장합니다.
- `OBJECT`의 `wordKey`는 WORLD-02 단어 카드의 `wordKey`입니다.
- `sourceType`이 없거나, `OBJECT`인데 `wordKey`가 비어 있거나, `NPC_LINE`인데 `turnId`·`text`가, `FEEDBACK`인데 `conversationId`·`text`가 비어 있으면 `400 VALIDATION_FAILED`입니다. 없는 단어 키면 `404 NOT_FOUND`입니다.
- `NPC_LINE`의 턴이 없거나, 다른 사용자의 턴이거나, NPC가 아닌 사용자 턴이면 CONV-04와 같이 `404 NOT_FOUND`입니다. `FEEDBACK`의 대화가 없거나 다른 사용자의 대화여도 `404 NOT_FOUND`입니다.
- `requestId`는 형식을 맞추려고 받지만 쓰지 않습니다. 이미 저장한 단어는 처음 항목을 돌려주므로 같은 요청을 다시 보내도 결과가 같습니다.

#### VOCAB-03·04 메모·외웠어요·삭제

- VOCAB-03 `PATCH /api/v1/vocab/{userVocabId}`는 `{ "memo": "...", "memorized": true }` 중 보낸 필드만 바꿉니다. 화면 18의 [메모], [외웠어요]입니다. `memorized: true`면 `memorized_at`을 기록하고(이미 외운 단어면 처음 시각 유지), `false`면 지웁니다. 메모는 최대 200자이며 넘으면 `400 VALIDATION_FAILED`입니다. 응답은 바뀐 VOCAB-01 항목입니다.
- VOCAB-04는 사용자 단어장에서만 지우고 공용 어휘(`vocabularies`)는 지우지 않습니다. 성공하면 `204 No Content`입니다.
- VOCAB-03·04에서 다른 사용자의 항목이나 없는 항목은 `404 NOT_FOUND`입니다.

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
  "remainingConversations": 2,
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
- 현재 구현: 가입 사용자의 토큰 한도(1.4)는 수치가 `[미정]`이라 아직 없고, 게스트의 대화 횟수 제한(CONV-01, 켠 경우)만 반영합니다. 한도 정책이 없으므로 `quota`·`resumeAt`은 응답에 담지 않습니다.

| 사용자 | `status` | `canStartConversation` | `canUseSpeech` | `canGenerateFeedback` | `remainingConversations` |
| --- | --- | --- | --- | --- | --- |
| 가입 사용자, 제한이 꺼진 게스트(기본) | `AI_AVAILABLE` | `true` | `true` | `true` | 담지 않음 |
| 대화 횟수가 남은 게스트 | `AI_AVAILABLE` | `true` | `true` | `true` | 1 이상 |
| 대화 횟수를 다 쓴 게스트 | `AI_EXHAUSTED` | `false` | `true` | `true` | `0` |

- `remainingConversations`는 게스트 대화 횟수 제한을 켠 경우에만 담는, 게스트가 더 시작할 수 있는 NPC 대화 수입니다(CONV-01의 `GUEST_CONVERSATION_LIMIT`에서 한 번이라도 말을 주고받은 대화 수를 뺀 값). 다시 채워지지 않습니다. 화면은 대화 제안과 대화 시작 확인 창에 남은 횟수를 붙이고, `0`이면 행인 말걸기를 멈추고 NPC를 누르거나 택시 버튼 자리의 [가입하고 계속 대화하기]를 누를 때 가입 안내 창을 띄웁니다. 대화가 끝나면 이 값을 다시 받고, `0`이면 그 대화의 피드백을 닫을 때도 가입 안내 창을 띄웁니다. 가입 사용자와 제한이 꺼진 게스트는 담지 않습니다. 게스트 가입 안내 창은 제한과 상관없이 첫 호텔 퀘스트(`CHECK_IN`)를 끝낸 팡파레의 [다음]에서도 띄웁니다.
- 게스트 한도는 새 대화 시작만 막습니다. 마지막 대화에서 말을 주고받는 순간 `AI_EXHAUSTED`가 되지만, 그 대화의 음성 입력과 피드백은 그대로 쓸 수 있으므로 클라이언트는 `status`가 아니라 기능별 값으로 버튼을 켜고 끕니다.
- 외부 AI 장애(`AI_TEMPORARILY_UNAVAILABLE`)와 운영 제한(`AI_RESTRICTED`)은 아직 판단하지 않습니다. 장애는 각 AI 요청의 오류 응답(1.5)으로 알립니다.

### 4.10 화면 테마

#### USER-01 `PUT /api/v1/me/ui-theme`

```json
// 요청
{ "uiTheme": "STICKER" }

// 응답 200
{ "uiTheme": "STICKER" }
```

- 집에서 고른 화면 테마를 계정(`users.ui_theme`)에 저장합니다. 다른 기기에서 접속해도 BOOT-01의 `user.uiTheme`으로 같은 테마를 받습니다.
- `uiTheme`은 `JELLY`, `STICKER`, `CLOUD`입니다. 가입·게스트로 만든 사용자와 이 기능을 넣기 전에 가입한 사용자는 `JELLY`입니다.
- 로그인한 사용자만 호출할 수 있으며, 로그인하지 않았으면 `401 UNAUTHENTICATED`입니다. 다른 상태 변경 요청처럼 `X-Requested-With` 헤더가 필요합니다.
- `uiTheme`이 없거나 `null`이거나 세 값 중 하나가 아니면(대소문자 구분, 예: `sticker`) `400 VALIDATION_FAILED`이며 테마를 바꾸지 않습니다.
- 같은 값을 여러 번 보내도 결과가 같습니다.

### 4.11 선택 동의(음성 처리·AI 개선)

가입 화면(AUTH-03·04)이나 게스트 시작(AUTH-07)에서 받은 선택 동의를 가입 뒤에 보고 바꿉니다. 마이페이지(4.12)에서 씁니다. 음성 처리(`VOICE`)는 음성 입력(CONV-02)에, AI 개선(`TRAINING_TEXT`·`TRAINING_VOICE`)은 [AI 개선 데이터 활용 기준](../ai-training-data.md)의 학습·평가에 쓰는 동의입니다.

#### USER-02 `GET /api/v1/me/consents`

```json
// 응답 200
{
  "consents": [
    { "consentType": "VOICE", "version": "2026-10", "title": "음성 처리 동의", "url": "/voice/2026-10", "agreed": true },
    { "consentType": "TRAINING_TEXT", "version": "2026-10", "title": "AI 개선에 내 대화 기록 활용", "url": "/ai-training/2026-10", "agreed": true },
    { "consentType": "TRAINING_VOICE", "version": "2026-10", "title": "AI 개선에 내 녹음 음성 활용", "url": "/ai-training/2026-10", "agreed": false }
  ]
}
```

- 이 사용자가 바꿀 수 있는 동의만 음성 처리, 대화 기록, 녹음 음성 순서로 돌려줍니다. 게스트에게는 AI 개선 동의를 받지 않으므로 음성 처리만 돌려줍니다. 클라이언트는 받은 목록을 그대로 그립니다.
- `agreed`는 지금 문구 버전(`version`)에 동의했고 철회하지 않았으면 `true`입니다. 옛 버전에 동의했으면 `false`입니다.

#### USER-03 `PUT /api/v1/me/consents`

```json
// 요청
{ "consentType": "TRAINING_VOICE", "agreed": false }

// 응답 200
{ "consentType": "TRAINING_VOICE", "version": "2026-10", "title": "AI 개선에 내 녹음 음성 활용", "url": "/ai-training/2026-10", "agreed": false }
```

- `agreed: true`면 지금 문구 버전으로 동의를 남깁니다. 이미 동의했으면 바뀌는 것이 없습니다.
- `agreed: false`면 동의를 철회합니다(`user_consents.withdrawn_at`).
  - `VOICE`를 철회하면 다음 요청부터 음성 입력(CONV-02)을 쓸 수 없습니다. 녹음은 원래 저장하지 않으므로 지울 것이 없습니다.
  - `TRAINING_VOICE`를 철회하면 그동안 저장한 녹음 파일과 기록을 바로 지웁니다.
  - `TRAINING_TEXT`를 철회하면 대화 기록은 서비스에 필요하므로 지우지 않고 학습용 내보내기에서 뺍니다.
- `consentType`·`agreed`가 없거나 `null`이거나, `consentType`이 `VOICE`·`TRAINING_TEXT`·`TRAINING_VOICE`가 아니면 `400 VALIDATION_FAILED`입니다. 게스트가 AI 개선 동의를 바꾸려 해도 `400 VALIDATION_FAILED`입니다.
- 로그인한 사용자만 호출할 수 있으며 `X-Requested-With` 헤더가 필요합니다.

### 4.12 마이페이지

와이어프레임 밖에서 추가한 화면입니다. 휴대폰의 [내 정보]로 여는 내 정보 창은 BOOT-01의 `user`로 닉네임·캐릭터·배우는 언어를 보여 주고 닉네임 변경(USER-04), 로그아웃(AUTH-05), 회원 탈퇴(USER-05)를 둡니다. 선택 동의는 휴대폰의 [설정]으로 여는 설정 창에 둡니다. 선택 동의(USER-02·03)는 받은 목록대로 켜고 끄는 항목으로 보여 주며, 바꾸지 못하면 원래 상태로 되돌리고 이유를 알립니다.

#### USER-04 `PUT /api/v1/me/nickname`

```json
// 요청
{ "nickname": "선종" }

// 응답 200
{ "nickname": "선종" }
```

- 닉네임 규칙은 가입(AUTH-04)과 같습니다. 규칙에 맞지 않거나 `nickname`이 없으면 `400 VALIDATION_FAILED`이며 바꾸지 않습니다.
- 다른 사용자가 쓰는 닉네임이면 `409 NICKNAME_TAKEN`입니다. AUTH-06으로 미리 확인했어도 저장할 때 다시 검사하며, 동시에 같은 닉네임으로 바꾸려 해도 한 사람만 바뀝니다.
- 지금 쓰는 닉네임을 그대로 보내면 바꾸지 않고 `200`입니다. 게스트도 바꿀 수 있습니다.
- 바꾼 닉네임은 다시 접속하면 BOOT-01의 `user.nickname`으로 내려갑니다.
- 로그인한 사용자만 호출할 수 있으며, 로그인하지 않았으면 `401 UNAUTHENTICATED`입니다. `X-Requested-With` 헤더가 필요합니다.

#### USER-05 `DELETE /api/v1/me`

마이페이지의 [회원 탈퇴]입니다. 확인 창을 거친 뒤 부릅니다. 응답은 본문 없이 `204`입니다.

- 사용자가 남긴 내용을 바로 지웁니다. 범위는 게스트 내용 정리(AUTH-07)와 같습니다: 대화 원문(턴)·NPC 음성·대화 피드백·문장 피드백과 모범 음성·NPC 기억과 관계·NPC가 건네기로 한 것·저장 단어·숨김·레벨 테스트 답·학습용 녹음.
- 개인정보도 지웁니다.
  - 이메일을 비웁니다.
  - 소셜 계정 ID(`provider_uid`)는 `withdrawn-`으로 시작하는 임의 값으로, 닉네임은 `withdrawn-{사용자 ID}`로 바꿉니다. 같은 Google 계정과 같은 닉네임으로 다시 가입할 수 있습니다.
  - 관리자였다면 일반 사용자로 되돌립니다.
- 사용자·대화·AI 호출 기록·미션 진행·동의 기록 같은 뼈대는 남은 행이 가리키므로 누구 것인지 알 수 없는 채로 남깁니다.
- 요청한 세션을 끝냅니다. 클라이언트는 입구 화면(로그인)으로 돌아갑니다. 다른 기기에서 로그인해 둔 세션은 그 기기에서 24시간 동안 요청이 없으면 끝납니다.
- 게스트도 부를 수 있으며 같은 방식으로 지웁니다. 다만 게스트 내용은 AUTH-07 정리 규칙으로 지워지므로, 클라이언트는 게스트(`user.guest`)에게 [회원 탈퇴]를 보이지 않습니다.
- 로그인하지 않았으면 `401 UNAUTHENTICATED`이며 아무것도 지우지 않습니다. `X-Requested-With` 헤더가 필요합니다.

## 5. ERD와의 관계

### 5.1 이 명세에 맞춰 ERD v3.1~v3.7에 반영한 사항

| 항목 | 관련 API | ERD 변경 |
| --- | --- | --- |
| 튜토리얼 기록 | TUT-01, 02 | 튜토리얼은 `users.onboarding_step`만으로 관리합니다. `conversations.mode`, `missions.type`을 지우고 `user_missions`의 튜토리얼 행(`mission_date` NULL)을 없앴습니다. |
| 미션 자동 시작 | MIS-01 | `user_missions.accepted_at`을 지우고, `status`는 `RUN`·`DONE`만 씁니다. `mission_date`는 다시 필수입니다. |
| 중요도 삭제 | VOCAB-01, 02 | `user_vocab.importance`와 중요도 인덱스를 지우고, 최근 저장순 인덱스(`user_id`, `saved_at DESC`)를 두었습니다. |
| 뜻 수정 → 메모 | VOCAB-03 | `user_vocab.custom_meaning`을 `memo`로 바꿨습니다. |
| 예문 뜻 | WORLD-02 | `vocabularies.example_ko`를 추가했습니다. |
| 발음 음성 | WORLD-04 | `vocabularies.pronunciation_audio_key`를 추가했습니다. 값은 미리 만든 발음 파일 이름(`{wordKey}.wav`)입니다. |
| 행인 첫 말·작별 대사 | WORLD-02, CONV-03 | `npcs.approach_line`, `approach_audio_key`, `farewell_line`, `farewell_audio_key`를 추가했습니다. |
| 대화 종료 이유 | CONV-03, 08 | `conversations.end_reason`(`USER_ENDED`, `NPC_ENDED`, `QUOTA_EXHAUSTED`, `RUDE`, `TIME_LIMIT`, `EXPIRED`)을 추가했습니다. |
| 숙어 뜻 | FB-03 | `turn_feedback.idiom_meaning_ko`를 추가했습니다. |
| 짧은 피드백 개선 표현 | CONV-08 | `conversation_feedback.reviews` JSON을 `{ good, improve, suggestion }` 구조로 정했습니다. |
| NPC 음성 보관 | CONV-06, 07 | `turns.npc_audio_key`를 추가했습니다. 보관·삭제·용량 기준은 [NPC 음성 저장 운영](../npc-audio-storage.md#보관삭제-정책)을 따릅니다. |
| 무례 판정 (v3.2) | CONV-03 | `turns.guard_abusive`(null이면 판정 실패), `turns.guard_error_code`, `conversations.rude_count`를 추가하고 `end_reason`에 `RUDE`를 더했습니다. |
| 학습자 말 번역 (v3.2) | CONV-03 | `turns.translation_en`을 추가했습니다. 번역 카드와 다음 요청의 대화 기록, 피드백에 씁니다. |
| 다른 표현 | CONV-03 | `turns.better_expressions`(JSON 문자열 배열)를 추가했습니다. 학습자가 한 말과 같은 뜻의 다른 표현(최대 3개)이며, 고친 문장·한국어가 섞인 말에도 줍니다. |
| AI 호출 기록 단위 (v3.2) | 1.4, CONV-02·03 | `ai_requests.kind`를 기능 단위(`STT`, `GUARD`, `JUDGE`, `NPC`, `TTS`, `HINT`, `MEANING`, `FEEDBACK`)로 나눴습니다. 한 턴의 여러 AI 호출이 같은 `request_id`를 써도 겹치지 않습니다. |
| STT 사용량 (v3.2) | 1.4, CONV-02 | `ai_requests.audio_ms`를 추가했습니다. 한도는 초당 토큰으로 환산해 차감합니다. |
| NPC 역할 이름 (v3.2) | WORLD-02, CONV | `npcs.role_label`을 추가했습니다. `persona`는 AI 프롬프트용이라 화면 표시용 값을 따로 둡니다. |
| 단어장 출처 대화 (v3.2) | VOCAB-01, 02 | `user_vocab.source_conversation_id`를 추가했습니다. 짧은 피드백(화면 16)은 턴 없이 대화 단위로 저장하므로 `source_turn_id`만으로는 출처 대화를 찾을 수 없습니다. |
| 단어장 문장 (v3.2) | VOCAB-01, 02 | `user_vocab.context_text`, `context_meaning_ko`를 추가했습니다. 단어를 저장한 문장과 문장 뜻입니다. |
| 닉네임 중복 불가 (v3.3) | AUTH-04, 06 | `users.nickname`에 unique 제약을 추가했습니다. 확인 API를 거쳐도 동시에 가입하면 같은 닉네임이 들어갈 수 있어 DB 제약으로 막습니다. |
| 미션 코드 (v3.3) | MIS-01 | `missions.code`를 추가했습니다. 시드를 반복 실행할 때 같은 미션을 찾는 고정 키이며, Unity 미션 콘텐츠의 키와 같습니다. |
| 호텔 개방 (v3.3) | WORLD-01·02, MIS-01 | 기능명세서 F-21·F-22에 맞춰 호텔을 열고(`scenes.is_open = true`) 호텔 직원 NPC(`HOTEL_STAFF`)와 호텔 미션을 시드에 추가했습니다. 컬럼 변경은 없습니다. |
| 게스트로 시작하기 | AUTH-07 | `users.provider`에 `GUEST` 값을 더했습니다. 컬럼 변경은 없습니다. |
| 피드백 실패 이유·결과 원본 (v3.4) | CONV-08, FB-01~03 | `conversation_feedback.error_code`(실패·건너뜀 이유)와 `result`(AI 워커가 돌려준 검증된 결과 원본)를 추가했습니다. `turn_feedback.better_text`는 적절한 발화에 고칠 표현이 없어 NULL을 허용합니다. |
| NPC 기억 카드 (v3.4) | CONV-01, 03, 08 | `user_npc_relations.memory_card`(기억 카드 JSON)와 `memory_conversation_id`(마지막으로 반영한 대화)를 추가하고 `ai_requests.kind`에 `MEMORY`를 더했습니다. 사실별 `npc_memories`는 만들지 않습니다. |
| NPC 음성 보관 정책 (v3.5) | CONV-01, 03, 06, 07, 10 | `turns.npc_audio_deleted_at`(보관 정책으로 음성을 지운 시각)을 추가했습니다. 처음 합성에 실패해 키가 없는 턴과 구분해, 지운 음성은 `speech.status: EXPIRED`로 알리고 다시 만들지 않습니다. |
| 공용 단어 카드 (v3.4) | WORLD-02·03, VOCAB-02 | Unity가 사물 카드를 사물별 단어에서 공용 단어로 바꾼 데 맞춰 `world_objects`·`user_object_prefs`를 `scene_words`(장소별 단어)·`user_word_prefs`(단어별 숨김)로 바꾸고 `vocabularies.word_key`를 추가했습니다. API의 `objectKey`는 `wordKey`로 바꿨습니다. |
| 화면 테마 (v3.6) | BOOT-01, AUTH-04·07, USER-01 | `users.ui_theme`(`JELLY`·`STICKER`·`CLOUD`, 기본값 `JELLY`)을 추가했습니다. 이미 가입한 사용자는 마이그레이션(V3)에서 `JELLY`로 채웁니다. |
| AI 개선 동의·학습용 녹음 (v3.7) | AUTH-03·04, CONV-02, USER-02·03 | `user_consents.consent_type`에 `TRAINING_TEXT`·`TRAINING_VOICE`를 더하고 철회 시각 `withdrawn_at`을 추가했습니다. 녹음 음성 활용에 동의한 사용자의 녹음 기록 `training_recordings`를 추가했습니다(마이그레이션 V4). |

### 5.2 아직 정할 사항

| 항목 | 관련 API | 내용 |
| --- | --- | --- |
| 예문 표시 시점 | WORLD-02 | 사물 카드에 예문을 저장 전부터 보여줄지(와이어프레임), 저장 후에만 보여줄지 정합니다. |
| 사물 저장 출처 | VOCAB-01 | 사물에서 저장한 표현은 어느 사물에서 저장했는지 기록하지 않습니다. 화면에 사물 이름이 필요하면 `user_vocab`에 사물 ID를 추가합니다. |
| 레벨 테스트 문항 | ONB-03~05 | 문항 원본 테이블이 없어 서버 설정 데이터로 둡니다. en-US 듣기 4·말하기 2문항입니다. |
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
| 직원 첫 인사 생성 방식 | CONV-01 | 미리 작성 / AI 생성(현재 구현) |
| NPC 음성 응답 방식 | CONV-03, 06 | 파일 주소(초안) / 본문 포함 |
| 음성 보관 동의·기간 | CONV-02, AUTH-04 | 보관 안 함 / 동의 시 기간 한정 보관 |
| 피드백 건너뛰기 버튼 | CONV-08 | 배치 여부 |
| 대화 중 새로고침 | CONV-11 | 이어서 진행 / 종료 처리. Unity는 대화를 시작할 때 `CONVERSATION_ALREADY_ACTIVE`를 받으면 CONV-11로 남은 대화를 받습니다. 지금 말을 건 NPC와의 대화면 지난 대사를 다시 보이고 이어서 진행합니다(새 대화를 만들지 않아 게스트 대화 횟수를 다시 쓰지 않음). 다른 NPC와의 대화면 피드백 없이 끝내고(`skipFeedback: true`) 새로 시작합니다. |
| 미션 완료 행동 | MIS-01 | 대화만(초안) / 장소 방문·단어 저장 등 추가 |
| 일일 미션 보상 | MIS-01 | 보상 여부 (날짜 기준은 MVP에서 `Asia/Seoul`로 확정) |
| 맵 확장 시 사물 조회 | WORLD-02 | 주변 사물 `wordKey` 묶음 조회 `[제안]` / 서버 반경 조회 |
| 사용량 소진 HTTP 상태 코드 | 1.5 | 429 `[제안]` / 403 |
| 설정 화면 | USER-02·03 | AI 개선 동의 변경 화면의 위치. 휴대폰 [설정] 창에 시점 감도·NPC 말 빠르기·효과음·대화 난이도와 함께 둡니다. 마이페이지(4.12)의 내 정보 창에는 내 정보·닉네임 변경·로그아웃·회원 탈퇴를 둡니다. |

## 7. MVP에서 뺀 기능

| 기능 | 화면 | 비고 |
| --- | --- | --- |
| 미션 수락·포기 | 09, 13 | 미션은 자동 시작 |
| 미션 상세 화면(지도, 설명) | 13 | 미션을 누르거나 상세 보기를 선택했을 때 여는 화면으로 이후 설계 |
| 튜토리얼 대화의 추천 단어·힌트·대화 종료 | 11 | 대본 진행이라 필요 없음 |
| 상세 피드백의 내 음성 듣기 | 17 | 음성 보관 정책과 함께 이후 검토 |
| 상세 피드백의 다시 말하기 | 17 | 이후 검토 |
| 단어장 중요도 | 18 | 목록은 최근 저장순 |
| 문장 저장, 피드백 숙어 저장, 단어장 유형 필터 | 16~18 | 숙어는 NPC 대사 단어 풀이에서만 저장. 피드백 단어장 후보의 숙어와 문장 저장은 이후 검토 |
| 회상 복습 | 18 | 요구사항명세서 REQ-17·18(정답 기록, 5회 정답 시 카드 숨김)과 다른 점으로 기록 |
| NPC 호감도 설정, 프로필 수정 | - | 와이어프레임에 화면 없음. 호감도 조회는 대화 화면(CONV-01·03 응답)과 휴대폰 [이웃](WORLD-08)에서 함 |
