# 상황 복습장 API

종료한 대화의 상황과 추천 표현을 사용자의 복습 카드로 보관합니다. 로그인 세션과 `X-Requested-With` 변경 요청 헤더는 [MVP API 공통 규칙](mvp-api.md)을 따릅니다. 사용자 ID는 로그인 정보에서 가져옵니다. 다른 사용자의 대화·카드는 없는 것과 같이 `404 NOT_FOUND`입니다.

## 저장과 조회

| 동작 | 경로 | 응답 |
| --- | --- | --- |
| 원본 피드백으로 카드 저장 | `POST /api/v1/review-cards` | `201` 카드 |
| 최근 저장 순 목록 | `GET /api/v1/review-cards?sceneCode=CAFE&cursor=42&size=20` | `200` 목록 |
| 카드 상세 | `GET /api/v1/review-cards/{cardId}` | `200` 카드 |
| 카드 삭제 | `DELETE /api/v1/review-cards/{cardId}` | `204` |

저장 요청은 `{ "conversationId": 55 }`만 받습니다. 요청에 표현·NPC·장소·요약을 실어 저장할 수 없습니다. 서버가 소유한 종료 대화와 `DONE` 상태 피드백의 `situationExpressions`를 읽어 서로 다른 유효 표현을 순서대로 최대 3개 복사합니다. 표현과 뜻이 비어 있는 항목은 제외합니다. 표현이 2개 미만이거나 완료된 피드백이 없으면 `409 REVIEW_NOT_READY`이며, 준비를 위해 AI를 호출하지 않습니다.

같은 원본 대화를 다시 저장하면 기존 카드와 같은 ID·내용을 반환합니다. 원본 대화 행 잠금과 사용자·원본 대화 유니크 제약으로 동시 저장도 한 장으로 제한합니다. 피드백을 다시 생성해도 이미 저장한 카드의 표현은 바뀌지 않습니다. 삭제한 뒤 다시 저장하면 새 카드이며, 삭제한 카드는 `404 NOT_FOUND`입니다.

```json
{
  "id": 42,
  "sourceConversationId": 55,
  "sceneCode": "CAFE",
  "npcId": 2,
  "npc": { "npcCode": "CAFE_STAFF", "name": "Mia", "roleLabel": "카페 직원" },
  "situationSummary": "카페에서 음료를 주문하고 포장을 부탁했어요.",
  "targets": [
    {
      "expression": "Can I get that to go?",
      "meaningKo": "포장해 주실 수 있나요?",
      "usageKo": "음료를 가지고 나갈 때 써요.",
      "exampleEn": "Can I get that to go? I'm in a hurry.",
      "exampleKo": "포장해 주실 수 있나요? 조금 급해서요."
    },
    {
      "expression": "Could I get less ice?",
      "meaningKo": "얼음을 조금만 넣어 주세요.",
      "usageKo": "얼음 양을 바꿀 때 써요.",
      "exampleEn": "Could I get less ice, please?",
      "exampleKo": "얼음을 조금만 넣어 주실 수 있나요?"
    }
  ],
  "practiceCount": 0,
  "lastPracticedAt": null,
  "usedExpressions": []
}
```

목록 응답은 `{ "items": [카드], "nextCursor": "42" }`입니다. `size` 기본값은 20, 허용 범위는 1~50입니다. `cursor`는 바로 전 응답의 값을 사용하며 양수 ID만 허용합니다. `sceneCode`를 생략하면 전체 장소를 조회합니다. ID 내림차순으로 정렬하며 마지막 페이지의 `nextCursor`는 null입니다. 조회 중 새 카드를 저장해도 이전 페이지의 카드가 다음 페이지에 다시 나타나지 않습니다.

## 책임과 보관

- 저장·목록·상세·삭제는 AI·TTS·STT를 호출하지 않고 보관한 자료만 사용합니다.
- `practiceCount`, `lastPracticedAt`, `usedExpressions`의 최초 값은 0·null·빈 배열입니다. 실제 REVIEW 대화와 사용한 표현 기록은 별도 대화 흐름에서 갱신합니다. 현재 저장 API가 대화를 시작하거나 퀘스트를 진행하지 않습니다.
- 게스트 내용 정리와 회원 탈퇴 시 복습 카드의 상황·표현도 함께 삭제합니다.
- 데이터 모델은 [ERD의 review_cards](../erd.dbml)를 따릅니다.

## 저장한 상황으로 연습 시작

`POST /api/v1/review-cards/{cardId}/conversations`에 `{ "requestId": "UUID" }`를 보냅니다. 응답은 대화 시작(CONV-01)과 같고 `reviewCardId`가 추가됩니다. 일반 대화에서는 null, 복습 대화에서는 카드 ID입니다. 같은 ID는 대화 상세(CONV-10), 진행 중 대화(CONV-11), 종료(CONV-08)에도 유지됩니다. 클라이언트는 이 값을 확인해 본편 미션·튜토리얼·NPC 상호작용 완료 이벤트를 발생시키지 않습니다.

- 사용자 본인의 카드만 시작할 수 있고 타인의 카드는 `404 NOT_FOUND`입니다.
- 진행 중인 본편 또는 복습 대화가 있으면 기존 `409 CONVERSATION_ALREADY_ACTIVE`입니다. 기존 대화를 몰래 종료하지 않습니다. 동일 요청 ID의 재전송은 같은 복습 대화를 돌려주며 다른 모드·카드에 재사용하면 `409 REQUEST_ID_CONFLICT`입니다.
- 원본 상황과 2~3개 표현을 역할극 데이터로 사용합니다. 현재 퀘스트, 이전 관계 기억, 건넬 물건을 섞지 않고 같은 NPC의 고유 역할·성격은 유지합니다. 이전 대화의 결과를 이미 완료된 일로 가정하지 않습니다. 힌트·추천 단어도 원본 상황을 사용합니다.
- 발화는 기존 인증·AI 한도·사용량 원장·무례 판정·30턴 제한을 적용합니다. 무례 3회면 복습만 끝내고 본편 호감도·출동·페널티는 바꾸지 않습니다.
- 본편 퀘스트·보상·코인·호감도·기억·후속 약속은 갱신하지 않습니다. 복습은 본편 지난 대화 목록에 섞이지 않습니다.
- 종료 응답의 `feedback`, `followUp`은 null, `completedMissions`는 빈 배열입니다. 별도의 LLM 피드백·기억·후속 행동 생성을 호출하지 않습니다. 실제 표현 사용 근거·연습 기록은 복습 결과 계약에서 제공합니다.
- 진행 중인 복습의 카드는 `409 REVIEW_IN_PROGRESS`로 삭제를 막습니다. 시작 저장과 삭제는 같은 카드 행을 잠가, AI 응답을 기다리는 사이 삭제된 카드로 대화가 저장되지 않게 합니다.
