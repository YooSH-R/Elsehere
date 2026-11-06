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
