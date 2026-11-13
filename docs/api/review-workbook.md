# 상황 복습장 API

종료한 대화의 상황과 추천 표현을 사용자의 복습 카드로 보관합니다. 로그인 세션과 `X-Requested-With` 변경 요청 헤더는 [MVP API 공통 규칙](mvp-api.md)을 따릅니다. 사용자 ID는 로그인 정보에서 가져옵니다. 다른 사용자의 대화·카드는 없는 것과 같이 `404 NOT_FOUND`입니다.

## 저장과 조회

| 동작 | 경로 | 응답 |
| --- | --- | --- |
| 원본 피드백으로 카드 저장 | `POST /api/v1/review-cards` | `201` 카드 |
| 최근 저장 순 목록 | `GET /api/v1/review-cards?sceneCode=CAFE&cursor=42&size=20` | `200` 목록 |
| 카드 상세 | `GET /api/v1/review-cards/{cardId}` | `200` 카드 |
| 카드 삭제 | `DELETE /api/v1/review-cards/{cardId}` | `204` |

저장 요청은 `{ "conversationId": 55 }`만 받습니다. 요청에 표현·NPC·장소·요약을 실어 저장할 수 없습니다. 서버가 소유한 종료 대화와 `DONE` 상태 피드백에서 기본 복습 목표(`targets`)를 다음 순서로 최대 5개 복사합니다.

1. 다시 말해 볼 문장: 문장 교정(FB-03)이 고쳐 준 학습자 발화. 한국어로 한 말을 먼저, 그다음 고친 영어 문장을 대화 순서대로 최대 3개 담습니다. `expression`은 고친 문장, `originalText`는 학습자가 원래 한 말, `usageKo`는 고친 이유입니다. `meaningKo`는 한국어로 한 말이면 그 말, 영어 문장이면 고친 이유입니다. 고칠 것이 없는 문장(고친 문장이 없거나 대소문자·문장부호만 다름)과 무례로 막힌 발화는 제외합니다.
2. 이 상황에서 자주 쓰는 표현: 피드백의 `situationExpressions`에서 서로 다른 유효 표현을 순서대로 최대 3개 담습니다. 표현과 뜻이 비어 있는 항목은 제외하며 `originalText`는 null입니다.

여기에 원본 사용자 턴의 `better_expressions`에 저장된 실시간 추천을 빠짐없이 합칩니다. 기본 5개 제한 때문에 뒤쪽 발화의 추천을 버리지 않으며, 원문과 같은 문장·중복·무례로 차단한 턴·직원 전용 문장은 제외합니다. 이 대안은 오류 교정이 아니므로 `originalText`는 null입니다. 한글 뜻을 저장하지 않은 문장은 `meaningKo`도 null이며 번역을 새로 생성하지 않습니다. 새 카드뿐 아니라 기존 카드의 상세·목록·진행·종료 결과에도 같은 문장 목록을 사용합니다.

시연 햄버거 주문(`SHOWCASE ORDER`)은 저장된 포장 답변의 대체 표현들을 맨 앞에 둡니다. 나머지 목표와 이미 저장한 뜻·설명은 유지합니다. 일반 대화·다른 시연 단계의 순서는 바꾸지 않습니다.

목표가 모두 합쳐 2개 미만이거나 완료된 피드백이 없으면 `409 REVIEW_NOT_READY`이며, 준비를 위해 AI를 호출하지 않습니다.

Unity는 따로 담는 단계 없이, [대화 노트]의 대화 줄이나 대화 피드백 창 아래 줄의 [다시 연습]을 누를 때 이 저장 요청으로 카드를 찾거나 만든 뒤 바로 복습 대화를 시작합니다. `409 REVIEW_NOT_READY`면 연습할 문장이 없는 대화라고 알림으로 안내합니다. 연습 횟수는 지난 대화 목록(CONV-09)의 `practiceCount`로 보입니다.

같은 원본 대화를 다시 저장하면 기존 카드와 같은 ID를 반환합니다. 원본 대화 행 잠금과 사용자·원본 대화 유니크 제약으로 동시 저장도 한 장으로 제한합니다. 피드백을 다시 생성해도 이미 저장한 카드의 표현은 바뀌지 않습니다. 조회 시 원본 턴에 저장된 실시간 추천을 결합하지만 카드나 완료 피드백을 다시 생성하지 않습니다. 삭제한 뒤 다시 저장하면 새 카드이며, 삭제한 카드는 `404 NOT_FOUND`입니다.

```json
{
  "id": 42,
  "sourceConversationId": 55,
  "sceneCode": "CAFE",
  "npcId": 2,
  "npc": { "npcCode": "CAFE_STAFF", "name": "Mia", "roleLabel": "카페 직원" },
  "situationSummary": "카페에서 음료를 주문하고 포장을 부탁했어요.",
  "scenario": {
    "key": "first_day_CAFE_BREAK",
    "title": "음료 주문하기",
    "situation": "카페에서 음료를 마시며 기다려요.",
    "learnerRole": "여행자",
    "learnerNotes": ["마실 음료 이름", "크기나 온도", "매장에서 마시겠다고 말하기"]
  },
  "targets": [
    {
      "expression": "Can I get that to go?",
      "meaningKo": "포장해 주실 수 있나요?",
      "usageKo": "음료를 가지고 나갈 때 써요.",
      "exampleEn": "Can I get that to go? I'm in a hurry.",
      "exampleKo": "포장해 주실 수 있나요? 조금 급해서요.",
      "originalText": null
    },
    {
      "expression": "Could I get less ice?",
      "meaningKo": "얼음을 조금만 넣어 주세요.",
      "usageKo": "얼음 양을 바꿀 때 써요.",
      "exampleEn": "Could I get less ice, please?",
      "exampleKo": "얼음을 조금만 넣어 주실 수 있나요?",
      "originalText": null
    }
  ],
  "practiceCount": 0,
  "lastPracticedAt": null,
  "usedExpressions": []
}
```

목록 응답은 `{ "items": [카드], "nextCursor": "42" }`입니다. `size` 기본값은 20, 허용 범위는 1~50입니다. `cursor`는 바로 전 응답의 값을 사용하며 양수 ID만 허용합니다. `sceneCode`를 생략하면 전체 장소를 조회합니다. ID 내림차순으로 정렬하며 마지막 페이지의 `nextCursor`는 null입니다. 조회 중 새 카드를 저장해도 이전 페이지의 카드가 다음 페이지에 다시 나타나지 않습니다.

## 책임과 보관

- 목록은 건물별로 묶고 그 안에서 `scenario.title`로 상황을 구분합니다. 같은 카페의 주문과 분실물 문의는 원본 대화의 미션 단계로 구분합니다. 같은 단계에 다시 말을 걸었던 대화는 시작 당시 남아 있던 목표만 복습에 사용합니다. `scenario`는 당시 담당 NPC·단계·목표를 바탕으로 구성해 `review_cards.scenario`에 저장합니다. 원본 대화에 목표 기록이 없는 첫날·식당 대화는 저장 요약을 사용합니다. 이미 상황을 저장한 카드는 그 내용을 보여 줍니다.
- `learnerNotes`는 사용자 참고 카드와 NPC 역할극에 같은 내용으로 전달합니다. 정보가 없는 자유 대화는 저장 요약을 보여주고 첫 질문에서 학습자가 정한 정보를 복습 동안 유지합니다. 복습은 한 상황만 다시 시작하며 다음 행선지·다른 퀘스트로 이어가지 않습니다.
- 추천 표현 생성은 내부 화자 구분으로 학습자 또는 양쪽 모두 쓸 표현만 남깁니다. 내부 분류 필드는 기존 피드백 응답·저장 계약에 추가하지 않습니다. 이전 카드의 명백한 직원 전용 표현도 목록·진행 조회·종료 결과에서 제외합니다.

- 저장·목록·상세·삭제는 AI·TTS·STT를 호출하지 않고 보관한 자료만 사용합니다.
- `practiceCount`, `lastPracticedAt`, `usedExpressions`의 최초 값은 0·null·빈 배열입니다. 실제 REVIEW 대화와 사용한 표현 기록은 별도 대화 흐름에서 갱신합니다. 카드 저장 API 자체는 대화를 시작하거나 퀘스트를 진행하지 않습니다.
- 게스트 내용 정리와 회원 탈퇴 시 복습 카드의 상황·표현도 함께 삭제합니다.
- 데이터 모델은 [ERD의 review_cards](../erd.dbml)를 따릅니다.

## 저장한 상황으로 연습 시작

`POST /api/v1/review-cards/{cardId}/conversations`에 `{ "requestId": "UUID", "speechSpeed": 1.1 }`를 보냅니다. `speechSpeed`는 사용자가 고른 NPC 말 빠르기로 CONV-01과 같은 규칙입니다(선택, 0.8~1.5, 없으면 `1.1`). 응답은 대화 시작(CONV-01)과 같고 `reviewCardId`가 추가됩니다. 일반 대화에서는 null, 복습 대화에서는 카드 ID입니다. 같은 ID는 대화 상세(CONV-10), 진행 중 대화(CONV-11), 종료(CONV-08)에도 유지됩니다. 클라이언트는 이 값을 확인해 본편 미션·튜토리얼·NPC 상호작용 완료 이벤트를 발생시키지 않습니다.

- 사용자 본인의 카드만 시작할 수 있고 타인의 카드는 `404 NOT_FOUND`입니다.
- 진행 중인 본편 또는 복습 대화가 있으면 기존 `409 CONVERSATION_ALREADY_ACTIVE`입니다. 기존 대화를 몰래 종료하지 않습니다. 동일 요청 ID의 재전송은 같은 복습 대화를 돌려주며 다른 모드·카드에 재사용하면 `409 REQUEST_ID_CONFLICT`입니다.
- 원본 상황과 2~3개 표현을 역할극 데이터로 사용합니다. 현재 퀘스트, 이전 관계 기억, 건넬 물건을 섞지 않고 같은 NPC의 고유 역할·성격은 유지합니다. 이전 대화의 결과를 이미 완료된 일로 가정하지 않습니다. 힌트·추천 단어도 원본 상황을 사용합니다.
- 발화는 기존 인증·AI 한도·사용량 원장·무례 판정·30턴 제한을 적용합니다. 무례 3회면 복습만 끝내고 본편 호감도·출동·페널티는 바꾸지 않습니다.
- 본편 퀘스트·보상·코인·호감도·기억·후속 약속은 갱신하지 않습니다. 복습은 본편 지난 대화 목록에 섞이지 않습니다.
- 종료 응답의 `feedback`, `followUp`은 null, `completedMissions`는 빈 배열입니다. 별도의 LLM 피드백·기억·후속 행동 생성을 호출하지 않습니다. 실제 표현 사용 근거·연습 기록은 복습 결과 계약에서 제공합니다.
- 진행 중인 복습의 카드는 `409 REVIEW_IN_PROGRESS`로 삭제를 막습니다. 시작 저장과 삭제는 같은 카드 행을 잠가, AI 응답을 기다리는 사이 삭제된 카드로 대화가 저장되지 않게 합니다.


## 복습 중 표현 사용 표시

`GET /api/v1/review-cards/{cardId}/conversations/{conversationId}/progress`는 해당 복습 대화에 저장된 사용자 발화를 읽고 현재 사용 근거를 반환합니다. 카드와 연결된 대화 모두 로그인 사용자의 것이어야 하며, 다른 카드의 시도·일반 대화·없는 카드는 `404 NOT_FOUND`입니다.

```json
{
  "conversationId": 77,
  "reviewCardId": 42,
  "verifiedExpressionCount": 1,
  "expressions": [
    {"expression": "Can I get that to go?", "evidence": [{"turnId": 123, "text": "Could I get that to go, please?"}]},
    {"expression": "Could I get less ice?", "evidence": []}
  ]
}
```

종료 결과와 같은 보수적인 근거 규칙을 사용하며, 조회는 대화를 끝내거나 연습 횟수·완료 결과·본편 진행을 변경하지 않습니다. 추가 모델 호출도 없습니다. Unity는 복습 대사의 응답을 받은 뒤 조회해 참고 카드의 원형 배지를 갱신합니다. 이전 요청의 늦은 응답과 닫힌 대화의 응답은 화면에 반영하지 않습니다. 확인되지 않은 표현은 빈 배지로 남으며 오답을 뜻하지 않습니다.

표현과 발화가 같은 여러 문장인 경우도 확인하며, `No, I'm good.`처럼 앞선 제안에 답하는 `No`를 목표 표현 전체의 부정으로 오인하지 않습니다. 인용한 예문·NPC 발화·무례로 차단된 발화는 기존과 같이 근거에서 제외합니다.

복습 결과 화면은 검증된 표현마다 실제 사용자 문장, 표현의 뜻, 저장된 상황별 활용 설명을 연결합니다. 사용하지 않은 표현은 다음 연습 제안으로 구분합니다. 별도 의미·발음 평가를 실행하지 않으므로 확인하지 않은 자연스러움 점수나 성공 판정을 만들지 않습니다.

## 복습 종료 결과와 다시 보기

복습 대화의 `POST /api/v1/conversations/{conversationId}/end` 응답에는 `reviewResult`가 추가됩니다. 일반 대화에서는 null입니다. 결과는 종료 트랜잭션에서 실제 사용자 발화로 만들고 `conversations.review_result`에 한 번 저장합니다. 같은 종료 요청의 동시 전송·재시도에도 카드의 `practiceCount`는 한 번만 증가하고 같은 결과를 돌려줍니다. NPC가 먼저 마친 대화도 종료 API를 받아 기록합니다. 종료 처리 전에 카드를 삭제했다면 결과는 null이고 삭제한 카드를 복원하지 않습니다.

| 조회 | 경로 | 응답 |
| --- | --- | --- |
| 최근 종료한 연습 결과 | `GET /api/v1/review-cards/{cardId}/result` | `200` 결과 |
| 특정 연습 결과 | `GET /api/v1/review-cards/{cardId}/conversations/{conversationId}/result` | `200` 결과 |

카드와 시도 모두 현재 사용자의 것이어야 하며 다른 카드에 연결된 시도, 없는 결과, 종료 전 결과는 `404 NOT_FOUND`입니다. 최근 결과는 종료 시각 내림차순, 같은 시각이면 대화 ID 내림차순입니다. 조회·재조회·단어장 복습 기록에는 AI를 호출하지 않습니다.

```json
{
  "conversationId": 77,
  "reviewCardId": 42,
  "practiceCompleted": true,
  "verifiedExpressionCount": 2,
  "practiceCount": 1,
  "completedAt": "2026-11-07T00:00:00Z",
  "expressions": [
    {
      "expression": "Can I get that to go?",
      "evidence": [{ "turnId": 161, "text": "Could I please get that to go?" }]
    },
    {
      "expression": "Could I get less ice?",
      "evidence": [{ "turnId": 163, "text": "Could I get less ice, please?" }]
    }
  ],
  "utterances": [
    { "turnId": 161, "text": "Could I please get that to go?" },
    { "turnId": 163, "text": "Could I get less ice, please?" }
  ]
}
```

### 근거를 찾는 범위

- `expressions[].evidence`는 저장된 표현과 같은 문구가 보이는 실제 사용자 원문을 연결합니다. 무례 판정 true인 줄·NPC 줄·자동 번역문은 근거에서 제외하고 판정 null인 사용자 줄은 포함합니다. 모든 유효 원문은 `utterances`에서 직접 확인할 수 있습니다.
- 대소문자·구두점·공백, `I'm`/`I am`, `I'd`/`I would` 같은 지원 축약형, `please` 삽입, `Can I/you`와 `Could I/you`의 요청 변형만 보수적으로 정규화합니다. 단어 경계를 지키고 같은 문장 앞부분의 부정어를 확인해 다른 뜻의 말을 사용 근거로 세지 않습니다.
- `practiceCompleted`는 서로 다른 목표 표현 2개 이상에 근거가 확인됐다는 복습 기록이며, 정규화하면 같은 표현인 Can/Could·please·축약형 변형은 한 개로 셉니다. 이 값은 언어 점수·정답 판정이 아닙니다. 찾지 못한 표현은 오답이 아닙니다. 동의어·다른 문장 구조·일부 STT 누락 등은 이 규칙으로 확인하지 못할 수 있어 화면에서 실제 원문과 함께 안내합니다. 별도의 LLM 의미 평가를 추가하지 않습니다.
- `practiceCount`는 완료 표시 여부와 관계없이 종료한 연습 시도 수입니다. 결과의 횟수는 그 결과를 저장한 시점의 값이며 목록 카드에는 현재 누적값이 나옵니다. 카드의 `lastPracticedAt`, `usedExpressions`는 가장 최근 종료한 연습의 시각·표현입니다. 늦게 도착한 과거 종료 요청으로 최근 기록을 되돌리지 않습니다.
- 결과 조회는 새 판정을 하지 않고 저장한 자료를 돌려줍니다. 게스트 내용 정리·회원 탈퇴 시 카드와 결과에 복사된 발화 원문도 제거하고, 사용량 집계를 위한 대화 메타데이터만 남깁니다.
