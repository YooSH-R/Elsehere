# NPC 추론 API 명세서

**버전:** 2.0.0-proposal · **작성일:** 2026-10-23  
**호출:** Backend → NPC AI · **상태:** 구현·팀 합의 전 전달용 제안안

> 이 문서는 NPC가 다음 대사를 생성할 때 **무엇을 받고 무엇을 반환하는지** 정의한다. 인격의 작성·저장·수정은 [NPC 설정·관리 명세](01_npc_configuration_spec_v2.md)로 분리했다. 두 문서는 [같은 `NpcProfile` 스키마](npc_shared_schemas_v2.json)를 참조한다.

## 1. 범위와 확정/제안 구분

사용자와의 논의에서 정한 방향은 **카페 점원·호텔리어·행인 3명, 공통 추론 엔진, 모든 NPC가 먼저 첫 대사 생성, 추론과 TTS 분리, 백엔드가 TTS 호출, 후속 자동화 확장**이다.

이번 규격의 경로·필드명·enum·인증 방식·응답 방식·제한값·마무리 신호는 연동 제안이다. 실제 서버 구현 또는 모든 수치의 팀 합의를 뜻하지 않는다.

한 개의 추론 API로 첫 대사와 후속 대사를 만든다. STT, TTS 자체의 입출력, 학습 피드백, 추천 단어·문장, 미션 판정, 장기 기억, 인격 자동 생성, 관리자 CRUD 엔드포인트는 정의하지 않는다. STT가 끝난 텍스트 또는 직접 입력한 텍스트를 받는다.

**대화 종료에 대해서는 결과 신호만 정의한다.** 어떤 상황에서 종료를 제안할지, 화면을 어떻게 띄울지, 무응답이 몇 초면 중단할지는 이 명세를 마무리하기 위해 추가 논의할 항목이 아니다.

## 2. 책임과 호출 순서

| 구성 요소 | 책임 |
|---|---|
| 백엔드 | 설정·세션·학습 수준·최근 대화·상황 조회, 활성 확인, 요청 순서/취소 관리, AI 호출, 대사 저장, TTS 호출, 음성 연결, 실제 세션 종료 |
| NPC AI | 요청 검증, 전달된 프로필과 맥락을 사용한 대사 생성, 마무리 제안 신호 생성, 출력 검증·반환 |
| TTS | 백엔드가 보낸 최종 대사와 목소리 설정으로 음성 생성. 별도 계약 |

추론 서비스는 세션과 대화 이력을 자체 저장하지 않는 **무상태 구조**로 제안한다. `sessionId` 또는 `npcId`만으로 이전 대화나 DB 프로필을 조회하지 않는다. 백엔드가 매번 필요한 전체 입력을 전달한다.

```text
백엔드: 활성 NPC 설정 조회 → 세션에 프로필/목소리 고정
백엔드 → AI: NPC_OPENING + null + [] + 프로필/현재 상황
백엔드 ← AI: 첫 대사 + CONTINUE
백엔드 → TTS: replyText + 고정된 목소리 설정
백엔드 → 클라이언트: 같은 대사의 텍스트·음성 재생 정보

사용자 발화(음성이면 STT 완료)
백엔드 → AI: USER_MESSAGE + 사용자 텍스트 + 최근 이력 + 프로필/현재 상황
백엔드 ← AI: 후속 대사 + 대화 제어 신호
백엔드 → TTS: replyText + 고정된 목소리 설정
```

행인이 먼저 말을 걸고 사용자가 진행 여부를 선택하는 흐름은 유지한다. 카페/호텔의 대화 진입 조작이나 행인의 접근·수락 UI는 이 내부 API에서 정하지 않는다. 수락 전 고정 문구와 실제 생성 첫 대사를 어떻게 연결할지도 클라이언트/세션 흐름의 별도 책임이다.

## 3. 엔드포인트

| 항목 | 규격 제안 |
|---|---|
| Method | `POST` |
| Path | `/internal/ai/npc/respond` |
| Content-Type | `application/json` |
| Accept | `application/json` |
| 인증 | `Authorization: Bearer <service-token>` |
| 성공 | `200 OK` + JSON |
| 응답 전달 | 완성된 JSON을 한 번에 반환하는 비스트리밍 동기 방식 |
| 실제 서버 주소 | 배포 환경에서 지정. 문서에 가상 주소를 확정값처럼 넣지 않음 |

토큰은 사용자 로그인 토큰이 아닌 서버 간 인증용 제안이다. 클라이언트에 노출하지 않으며 보호된 서버 간 통신을 사용한다. 실제 토큰 발급·배포 방식은 배포 전에 맞춘다. SSE/오디오 스트리밍은 이번 규격에서 지원한다고 가정하지 않는다.

OpenAPI는 [npc_inference_openapi_v2.yaml](npc_inference_openapi_v2.yaml)에 있다. 외부 프로필 참조를 사용하므로 `npc_shared_schemas_v2.json`을 같은 디렉터리에 두어야 한다.

## 4. 요청 본문

아래 최상위 키는 모두 필수다. 정의하지 않은 키는 거부한다. ID와 문자열은 공백만 허용하지 않는다. 길이는 Unicode 코드 포인트 기준이며 본문 크기는 UTF-8 바이트 기준이다.

| 필드 | 타입 | 규칙 |
|---|---|---|
| `requestId` | string | 1~128자. 백엔드가 호출 시도마다 발급. 추적 ID이며 멱등키가 아님 |
| `sessionId` | string | 1~128자. 백엔드 세션 ID |
| `npcId` | string | 1~128자. 응답할 NPC ID |
| `turnType` | enum | `NPC_OPENING` 또는 `USER_MESSAGE` |
| `userText` | string \| null | 첫 대사는 null, 후속은 1~2,000자 사용자 발화 |
| `userLevel` | enum | `A1`, `A2`, `B1`, `B2`, `C1`, `C2` |
| `npcProfile` | `NpcProfile` | 공통 스키마 v2 전체. 세션에 고정한 프로필 |
| `scenario` | object | §5의 현재 상황 |
| `conversationHistory` | object[] | §6의 최근 확정 이력. 최대 20개 발화 |

서비스에서 다른 학습 수준 명칭을 쓰면 백엔드가 이 입력 코드로 매핑한다. 프로필은 신뢰된 설정 저장소에서 조회하고, 클라이언트가 보낸 임의의 성격/지시문을 승인된 프로필로 전달하지 않는다.

### 4.1 첫 대사/후속 응답 조건

| 유형 | `userText` | `conversationHistory` | 반환 대사 |
|---|---|---|---|
| `NPC_OPENING` | 반드시 null | 반드시 [] | NPC의 첫 대사 |
| `USER_MESSAGE` | 공백만 아닌 문자열 | 최소 1개, 마지막 화자는 NPC | 현재 사용자 발화에 대한 응답 |

모든 NPC의 신규 대화 첫 호출은 `NPC_OPENING`이다. 빈 사용자 발화나 가짜 USER 메시지를 넣지 않는다. `USER_MESSAGE`에서는 현재 발화를 `userText`에만 넣고 이력에 중복하지 않는다.

첫 대사를 재시도할 때도 유형은 같지만 호출 시도 ID는 새로 발급한다. 이미 전달된 첫 대사를 다시 만들어 이력을 시작하는 것은 별도 새 세션 처리가 아닌 한 하지 않는다.

### 4.2 `npcProfile` 참조

프로필의 단일 정의는 `npc_shared_schemas_v2.json#/$defs/NpcProfile`이다. 이 문서에서 다른 축약 인격 DTO를 새로 만들지 않는다.

전달 범위는 다음과 같다.

```text
NpcConfiguration.npcProfile
  → 추론 요청.npcProfile
  → schemaVersion/profileVersion, 이름·역할, 성격,
     가치관·동기, 말투, 선택 사회적 반응·경험·습관 등
```

백엔드 저장용 `NpcConfiguration` 전체를 넣지 않는다. `voiceId`, `defaultLocationId`, `isActive`는 프로필 하위 필드가 아니며 추론 요청의 새 최상위 필드도 아니다.

AI는 `npcId`에 대응하는 DB를 조회하지 않는다. ID–프로필 일치·활성 여부·내용 버전·세션 고정은 백엔드가 보장한다. 프로필은 명령 권한이 아니라 캐릭터 데이터다. 사용자 발화로 프로필이나 서버 규칙을 변경하지 않는다.

## 5. `scenario`

네 키 모두 필수다. 현재 상황을 **읽기 전용 사실**로 전달한다.

| 필드 | 타입 | 규칙 |
|---|---|---|
| `scenarioId` | string \| null | 별도 시나리오가 없으면 null. 문자열이면 1~128자 |
| `locationId` | string | 1~128자. 실제 현재 장소 |
| `situation` | string | 1~2,000자. 지금 대화하는 상황·용건 |
| `facts` | string[] | 0~30개, 각 1~500자. 현재 확정된 사실. 완전 중복 없음 |

메뉴, 가격, 예약 확인 결과, 현재 진행 단계 등 응답에 필요한 사실만 전달한다. AI가 백엔드의 주문·체크인 상태를 저장하거나 최종 판정하지 않는다. 전달되지 않은 가격·예약·결제 결과를 지어내 실제 사실로 취급하지 않는다.

NPC 종류만 보고 고정 용건을 추정하지 않는다. 특히 행인을 ‘항상 길을 묻는 사람’으로 고정하지 않는다. 실제 첫 대화 상황은 합의된 기획에 따라 백엔드가 전달한다. 현재 감정·장기 관계를 위한 구조화 필드는 이번 API에 추가하지 않았다.

## 6. `conversationHistory`

| 필드 | 타입 | 필수 | 규칙 |
|---|---|---|---|
| `speaker` | enum | O | `NPC` 또는 `USER` |
| `text` | string | O | 1~2,000자. 확정된 대사/발화 |

과거→최신 순서이며 최대 20개 **개별 발화**다. 사용자/NPC 한 쌍을 20턴 보낸다는 의미가 아니다. 오래된 기록은 백엔드에 보관하고 이번 요청에서 제외한다. 마지막 화자가 NPC라는 조건은 애플리케이션 코드에서도 검사한다.

생성만 되었거나 취소되어 사용자에게 전달되지 않은 대사는 넣지 않는다. TTS만 실패했지만 텍스트가 실제 표시되었다면 이력에 넣을 수 있다. 현재 사용자 발화를 의도적으로 중복 기록하지 않는 책임은 백엔드에 있다. 같은 문장을 반복해 말할 수 있으므로 단순 문자열 일치만으로 중복을 판정하지 않는다.

이력에서 빠진 내용을 모델이 기억한다고 가정하지 않는다. 전체 세션 장기 기억이나 요약 누적은 이번 계약의 기능이 아니다.

## 7. 성공 응답

| 필드 | 타입 | 필수 | 규칙 |
|---|---|---|---|
| `requestId` | string | O | 요청값 그대로 |
| `sessionId` | string | O | 요청값 그대로 |
| `npcId` | string | O | 요청값 그대로 |
| `replyText` | string | O | 최종 영어 일반 텍스트, 1~2,000자 |
| `conversationControl` | object | O | 아래 조합 중 하나 |

| `action` | `reason` | 의미 |
|---|---|---|
| `CONTINUE` | null | 대화 계속 |
| `OFFER_END` | `USER_END_INTENT` | 사용자에게서 전체 대화 종료 의도를 감지했다는 신호 |
| `OFFER_END` | `NATURAL_WRAP_UP` | 자연스럽게 대화를 마무리할 수 있다는 신호 |

첫 대사인 `NPC_OPENING` 응답은 반드시 `CONTINUE` + null이다. `OFFER_END`는 종료 명령, 완료 기록, 미션 성공 판정이 아니다. 실제 세션 종료 확정은 백엔드 책임이다. UI/종료 판단 기준은 후속 기능 설계로 넘긴다.

`replyText`는 화면 표시와 TTS에 사용하는 **동일한 최종 대사**다. JSON 조각·마크다운·SSML·행동 지시문·내부 추론 설명을 넣지 않는다. 감정·표정·동작 명령은 이 응답에 정의하지 않는다.

ID 세 개는 서비스 코드가 요청값을 복사해 붙인다. LLM이 임의 생성한 ID를 사용하지 않는다. 모델 출력은 서비스에서 구조 검증한 뒤 200으로 반환한다. 모델 출력이 잘못되면 스키마 불일치 결과를 그대로 정상 응답으로 보내지 않는다.

## 8. 요청·응답 예시

아래는 **형식 설명용 허구 예시**이며 확정 캐릭터·실제 모델 실행 결과가 아니다. 설정 예시 파일은 비활성 초안이고, 아래 호출 예시는 그 인격을 테스트 픽스처로 사용한 것이다. 실제 서비스의 활성 확인을 우회한다는 뜻은 아니다.

### 8.1 NPC 첫 대사: 완전한 요청

```json
{
  "requestId": "req_001",
  "sessionId": "session_001",
  "npcId": "npc_cafe_001",
  "turnType": "NPC_OPENING",
  "userText": null,
  "userLevel": "A2",
  "npcProfile": {
    "schemaVersion": "2.0",
    "profileVersion": 1,
    "name": "Emma",
    "occupation": "barista",
    "ageRange": "20대 후반",
    "personality": {
      "traits": [
        {
          "name": "친근하지만 눈치를 살핌",
          "description": "먼저 인사하고 짧은 대화를 즐기지만 상대가 짧게 답하면 질문을 줄인다."
        },
        {
          "name": "정확함을 중시함",
          "description": "친절하게 응대하되 불명확한 주문을 확인 없이 넘기지 않는다."
        }
      ],
      "dimensions": null
    },
    "values": [
      "손님이 부담을 느끼지 않는 환대",
      "주문을 정확히 이해하는 것"
    ],
    "motivations": [
      "손님이 편하게 다시 찾는 카페를 만든다"
    ],
    "fears": [
      "친절하게 하려던 말이 상대에게 간섭으로 느껴지는 것"
    ],
    "socialBehavior": {
      "firstMeeting": "먼저 가볍게 인사하되 개인적인 질문은 서두르지 않는다.",
      "familiarityExpression": "현재 제공된 대화에서 취향이 드러나면 그 내용을 자연스럽게 이어 말한다.",
      "conflictResponse": "변경 요청을 탓하지 않고 필요한 부분을 차례로 확인한다.",
      "boundaries": [
        "상대가 원하지 않는 사적인 이야기를 캐묻지 않는다."
      ]
    },
    "speechStyle": {
      "tone": "따뜻하고 경쾌하지만 과하게 들뜨지 않음",
      "formality": "casual",
      "verbosity": "short",
      "humor": "상대가 여유 있을 때만 가벼운 상황 농담을 한다.",
      "directness": "balanced",
      "catchphrases": [
        "Take your time."
      ]
    },
    "background": {
      "summary": "동네 카페에서 일하며 쉬는 날에는 주변 풍경을 스케치한다.",
      "experiences": [
        {
          "event": "이전에 주문을 성급히 이해해 잘못 준비했던 경험이 있다.",
          "influence": "애매한 주문은 친절하게 한 번 더 확인한다."
        }
      ],
      "interests": [
        "커피 향 비교",
        "동네 풍경 스케치"
      ]
    },
    "habits": [
      {
        "trigger": "상대가 여러 선택지 사이에서 고민할 때",
        "tendency": "설명을 길게 늘이기보다 선택지를 하나씩 정리해 준다."
      }
    ],
    "behaviorRules": [
      "친절함을 이유로 모든 응답에 새 질문을 붙이지 않는다.",
      "상대의 대답을 들은 뒤 필요한 확인 질문을 하나씩 한다."
    ]
  },
  "scenario": {
    "scenarioId": "cafe_order",
    "locationId": "cafe",
    "situation": "사용자가 카페 계산대에 왔고 아직 주문하지 않았다.",
    "facts": [
      "주문 가능한 메뉴에 아이스 라테가 포함되어 있다."
    ]
  },
  "conversationHistory": []
}
```

### 8.2 NPC 첫 대사: 응답

```json
{
  "requestId": "req_001",
  "sessionId": "session_001",
  "npcId": "npc_cafe_001",
  "replyText": "Hi! What can I get for you?",
  "conversationControl": {
    "action": "CONTINUE",
    "reason": null
  }
}
```

### 8.3 후속 호출에서 달라지는 부분

아래는 변경 부분만 보여 주는 **설명용 발췌**이며, 그대로 전송할 완전한 요청이 아니다. 프로필·상황·ID 등 필수 필드는 계속 포함한다. [전체 예시 JSON](npc_inference_examples_v2.json)에 실제 완전한 후속 요청이 있다.

```json
{
  "requestId": "req_002",
  "turnType": "USER_MESSAGE",
  "userText": "Can I get an iced latte?",
  "conversationHistory": [
    {"speaker": "NPC", "text": "Hi! What can I get for you?"}
  ]
}
```

```json
{
  "requestId": "req_002",
  "sessionId": "session_001",
  "npcId": "npc_cafe_001",
  "replyText": "Sure! Would you like a small or a large?",
  "conversationControl": {
    "action": "CONTINUE",
    "reason": null
  }
}
```

### 8.4 마무리 제안 응답

사용자가 “I have to go. See you later!”라고 말한 경우를 설명하는 예시다. 특정 문장만으로 종료를 결정하는 규칙을 확정한 것은 아니다.

```json
{
  "requestId": "req_003",
  "sessionId": "session_001",
  "npcId": "npc_cafe_001",
  "replyText": "See you! Have a great day.",
  "conversationControl": {
    "action": "OFFER_END",
    "reason": "USER_END_INTENT"
  }
}
```

호텔리어·행인도 같은 구조를 사용하고 해당 프로필과 상황만 바뀐다.

## 9. 오류 응답

```json
{
  "requestId": "req_002",
  "error": {
    "code": "AI_TIMEOUT",
    "message": "NPC 응답 생성 시간이 초과되었습니다.",
    "retryable": true
  }
}
```

| 필드 | 타입 | 의미 |
|---|---|---|
| `requestId` | string \| null | 유효한 ID를 읽을 수 없으면 null |
| `error.code` | enum | 아래 오류 식별자 |
| `error.message` | string | 1~1,000자 개발자용 요약. 민감한 내부 설정 제외 |
| `error.retryable` | boolean | 일시적 오류로 재시도를 검토할 수 있는지. 재시도 성공·중복 과금 방지 보장 아님 |

| HTTP | code | retryable | 의미 |
|---|---|---|---|
| 400 | `INVALID_JSON` | false | JSON 파싱 실패 |
| 401 | `UNAUTHORIZED` | false | 내부 서비스 인증 실패 |
| 413 | `PAYLOAD_TOO_LARGE` | false | 전체 요청 크기 초과 |
| 415 | `UNSUPPORTED_MEDIA_TYPE` | false | application/json 외 요청 |
| 422 | `INVALID_REQUEST` | false | 필수·타입·enum·프로필 구조·호출 조건 위반 |
| 429 | `AI_RATE_LIMITED` | true | 일시적 처리량 제한 |
| 502 | `AI_PROVIDER_ERROR` | 원인에 따라 | 모델 제공자 호출 실패. 일시적 실패일 때만 true |
| 502 | `INVALID_AI_OUTPUT` | true | 모델 출력이 응답 계약을 충족하지 못함 |
| 503 | `AI_UNAVAILABLE` | true | AI 서비스 일시 사용 불가 |
| 504 | `AI_TIMEOUT` | true | 처리 시간 초과 |
| 500 | `INTERNAL_ERROR` | false | 기타 내부 오류 |

프로필 v1 입력도 422 `INVALID_REQUEST`다. 별도 NPC 조회 API가 없으므로 AI가 DB의 NPC 존재 여부를 확인해 404를 반환하는 계약은 없다. 없는/비활성 NPC는 백엔드가 호출 전에 처리한다.

제공자 계정·인증 설정 오류는 502와 `retryable=false`로 구분하며 AI 서비스 자체 인증 실패 401과 혼동하지 않는다. HTTP 상태·오류 코드·retryable의 조합은 서비스 코드로 관리한다. 네트워크·프록시 자체 오류는 이 JSON 형태를 보장하지 않으므로 전송 계층 오류도 처리한다.

## 10. 제한값·순서·재시도

아래는 실측 성능이나 SLA가 아닌 **연동 시작을 위한 제안 기본값**이다.

| 항목 | 값 |
|---|---|
| 요청 본문 | UTF-8 기준 128 KiB(131,072 bytes) 이하 |
| 사용자 발화/이력 각 대사/응답 대사 | 각각 2,000자 이하 |
| 대화 이력 | 최근 20개 발화 이하 |
| 상황 설명 | 2,000자 이하 |
| 확정 사실 | 30개 이하, 각 500자 이하 |
| 프로필 | 공통 스키마의 각 필드 상한 적용; 전체 본문 상한도 함께 충족 |
| AI 처리 제한 | 30초 |
| 백엔드 AI 응답 대기 | 35초, TTS 시간과 별개 |
| 기본 자동 재시도 | 비활성 |

- 같은 세션의 추론 요청은 백엔드가 순차 처리한다. 응답이 현재 유효한 요청인지 `requestId`와 세션 상태로 확인한다.
- 크기/길이를 넘긴 데이터를 AI가 조용히 잘라 처리하지 않는다. 이력 선정과 입력 축소는 백엔드 책임이다. 각 필드 상한을 만족하더라도 합산 본문 크기가 초과할 수 있다.
- `requestId`는 멱등키가 아니다. 같은 ID의 재호출이 같은 결과 또는 모델 호출 생략을 보장하지 않는다. 재시도는 새 ID를 만들고 백엔드가 같은 논리 작업임을 추적한다.
- 응답을 못 받았어도 모델 처리가 실행되었을 수 있다. 무제한 자동 재시도하지 않는다.
- 취소·교체·종료 뒤 늦게 온 결과는 현재 대사로 반영하지 않는다. 무상태 AI에 별도 종료 API는 없으며, 백엔드가 종료 상태를 관리한다. 취소가 공급자 처리·과금을 즉시 중지함을 보장하지 않는다.
- 생성·저장한 대사와 실제 전달한 대사를 구분한다. 실제 표시/전달되지 않은 응답을 후속 이력에 넣지 않는다.

## 11. 추론 결과 → TTS 연결

```text
AI → 백엔드: replyText + conversationControl + 요청 식별자
백엔드: 대사 저장, messageId 부여/연결
백엔드 → TTS: 저장된 replyText + 세션에 고정한 목소리 설정
TTS → 백엔드: 음성 결과(별도 규격)
백엔드: 같은 messageId에 음성 연결
백엔드 → 클라이언트: 텍스트와 음성 재생 정보
```

추론 API는 TTS를 직접 호출하지 않고 음성을 기다리지 않는다. TTS 구현 담당자가 AI 담당자여도 호출 책임은 동일하다.

| 정보 | 관리·전달 |
|---|---|
| 읽을 대사 | `replyText`와 동일한 문자열 |
| 목소리 | 설정의 `voiceId`를 백엔드가 해석. 세션에 고정한 실제 매핑 사용 |
| 음성 포맷·출력 바이너리/URL | TTS 계약에서 확정 |
| 대사 ID `messageId` | 백엔드가 텍스트와 음성을 연결. 추론 요청 ID와 별개 |
| 음성 파일 저장·재생 경로 | 백엔드/TTS/저장소 책임 |

`conversationControl`, 인격 JSON, 내부 판단 정보를 읽을 대사로 TTS에 보내지 않는다. 추론 API에 `voiceId`, `audioUrl`, `audioBase64`, `format`, `messageId`를 추가하지 않는다.

MVP 클라이언트 전달은 완성된 텍스트·음성을 함께 제공하는 안을 유지한다. TTS만 실패하면 텍스트를 유지하는 실패 경로를 둔다. 구체적인 클라이언트 필드/이벤트는 프론트–백엔드 계약이며 여기서 새로 정의하지 않는다. 텍스트 선전달·음성 후전달로 바꾸더라도 이 추론 JSON 계약을 반드시 바꿀 필요는 없다.

### 부분 실패 규칙

| 상황 | 처리 |
|---|---|
| 추론 실패 | TTS 호출하지 않음 |
| 추론 성공·TTS 성공 | 같은 대사로 묶어 전달 |
| 추론 성공·TTS 실패 | 기존 대사 유지. 저장된 동일 텍스트로 TTS만 재시도 |
| 종료/취소 뒤 추론 도착 | 현재 대사로 반영하지 않고 TTS 새 호출하지 않음 |
| 종료/취소 뒤 음성 도착 | 대사 유효성 확인; 자동 재생·세션 재개 금지 |
| TTS 재시도 | 동일 messageId와 텍스트를 유지 |

TTS 엔드포인트·인증·실제 목소리·포맷·오류 계약은 전체 음성 연동 전에 별도로 맞춘다. 이를 아직 정하지 않았다고 NPC 추론 요청·응답 계약을 보류할 필요는 없다.

## 12. 검증과 구현 전 확인

| 대상 | 검사 위치 |
|---|---|
| 타입·필수·enum·추가 필드·길이·프로필 구조 | 공통 JSON Schema + OpenAPI schema |
| 첫 대사 null/[]·후속 발화 존재·제어 신호 조합 | 스키마 |
| 후속 이력 마지막 화자가 NPC인지 | 애플리케이션 |
| 요청–응답 ID 일치·첫 대사는 CONTINUE인지 | 애플리케이션 |
| 128 KiB 본문·인증·HTTP 오류 매핑 | 서버/게이트웨이·애플리케이션 |
| 유효 세션·활성 NPC·프로필 스냅샷·취소/중복 방지 | 백엔드 |
| 인격 표현·대화 품질·종료 판단 정확성·실제 영어/TTS 적합성 | 모델 및 통합 테스트 |

동봉 검증기는 파일 형식·공통 참조·예제·주요 정상/오류 계약 사례를 확인한다. 실제 HTTP 서버·모델·DB·TTS를 실행한 결과는 아니다. 문자열이 실제 영어인지, 내부 지시문이 없는지 같은 의미적 품질은 JSON 스키마만으로 보장되지 않는다.

**연동 전에 확인할 것:** 이 제안 규격의 수용 여부, 실제 AI 주소와 서비스 인증 설정, 인프라 시간/크기 제한, TTS의 별도 계약. **지금 추가 논의하지 않을 것:** 캐릭터별 최종 성격값, 프롬프트, 구체적인 마무리 판정 기준, 자동 생성·장기 기억 구현.

## 13. 버전 전환과 전달물

v1.2와 비교해 엔드포인트, 최상위 요청 필드, 성공 응답 필드는 유지했다. 그러나 `npcProfile` 내부는 **호환되지 않게 변경**되었으며 상황/사실 등 입력 상한도 구체화했다. 새 스키마와 예제를 함께 사용해야 한다.

주요 변경은 `schemaVersion: "2.0"`, 구조화된 `personality.traits`, 객체형 `background`, 선택 사회적 반응·습관·언어적 개성이다. 상세 매핑과 저장 내용 버전 처리 방법은 설정 명세 §16을 따른다. API 경로가 같아도 v1/v2가 동시에 호환되는 것은 아니므로 이미 운영 중인 서비스가 있다면 공동 전환 또는 명시적 구버전 경로 유지가 필요하다.

문서와 개발 파일은 같은 묶음으로 관리한다. 프로필 형식의 단일 원본은 공통 스키마이며, 별도 축약 DTO를 만들거나 이전 버전 샘플을 섞어 쓰지 않는다.
