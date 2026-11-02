# NPC 기억 카드 — 백엔드 연동

공유된 `POST /v1/memory` 카드 계약의 연결 방법이다. 새 사실별 0.1.0 API를 요구하지 않는다. 요청·응답 필드는 [AI 워커 명세](ai-worker.md#271-post-v1memory--npc-기억-카드)를 따른다. 호출 예시는 [검증된 합성 JSON](examples/memory-card-contract.json)에 있다. 예시의 카드·모델·토큰·지연은 모의 공급자 결과이며 실제 모델 품질·비용 측정값이 아니다.

AI 워커는 상태를 저장하지 않는다. Spring이 현재 사용자의 해당 NPC 카드를 조회해 전달하고, 성공한 카드만 저장한다. 다음 방문에는 저장된 카드를 NPC 대답 요청에 담는다.

## 대화 종료 → 생성 → 저장 → 다음 방문

1. Spring이 대화를 정상적으로 종료하고 확정한 이력과 결과를 준비한다. 최종 텍스트를 생성 번역으로 바꾸지 않는다.
2. 현재 사용자·NPC의 기존 카드를 서버에서 조회한다. 없으면 `memory_card: null`이다. 클라이언트가 임의로 보낸 카드를 다른 사용자의 기억처럼 저장하지 않는다.
3. 내부 토큰으로 `/v1/memory`에 `npc`, `outcome`, `history`, `memory_card`를 전달한다. 내부 워커 호출은 모델 생성 비용이 발생하므로 Spring의 호출 승인·사용량 정책을 거쳐야 한다.
4. HTTP 200이면 `memory_card` 전체를 새 카드로 저장한다. 목록을 이전 카드와 단순 병합하지 않는다. 그렇게 하면 수정·철회된 내용이 복원될 수 있다.
5. 429·502·503이면 이전 카드를 유지한다. 오류 응답에는 `memory_card`가 없으며 빈 카드로 바꿔 저장하지 않는다.
6. 다음 `/v1/npc/reply` 요청에 `memory: {visits, last_order, card}`를 전달한다. `card`는 저장된 최신 카드다. `visits`는 유효 대화를 나눈 방문 수이며, 기존 NPC 포맷터는 `visits < 1`이면 기억 맥락을 넣지 않는다.

AI의 카드 생성과 실제 저장 성공은 별개다. 같은 대화 종료 요청의 중복 처리 방지, 현재 사용자·NPC 확인, 저장 순서·트랜잭션·동시 갱신 제어는 Spring이 맡는다. DB의 저장 표현은 기존 백엔드 설계에 맞춰 변환하며, 이 문서가 새 DB 컬럼을 확정하지 않는다.

## 요청·응답

```json
{
  "npc": {"id": "emma", "name": "Emma", "role": "the barista.", "scene": "Cafe."},
  "outcome": "Conversation ended normally.",
  "history": [{"who": "me", "text": "I now prefer tea.", "english": null, "rude": false}],
  "memory_card": {
    "name": "Alex",
    "claims": ["says coffee is their favorite drink"],
    "events": [],
    "attitude": "neutral",
    "struggled": []
  }
}
```

성공 응답은 `{"memory_card": {...}, "usage": [...]}`이다. `usage`는 실제 실행한 역할·모델과 공급자가 반환한 사용량이며 크레딧 확정 차감액이 아니다.

| 카드 필드 | 처리 |
| --- | --- |
| `name` | 문자열, 양끝 공백 정리 후 최대 40자 |
| `claims` | 문자열 배열, 연락처 항목·빈 항목 제외 후 최대 5개·항목당 100자 |
| `events` | 동일한 배열 제한. 사건은 대화·서버 결과에 근거해야 함 |
| `attitude` | 문자열. `friendly/neutral/wary` 외 문자열은 기존 규칙대로 `neutral` |
| `struggled` | 동일한 배열 제한. 생성 번역을 학습자의 실제 영어로 평가하지 않음 |

다섯 필드가 모두 있어야 하며, 필드 누락·추가, 잘못된 자료형, 문자열이 아닌 배열 원소, 중복 JSON 키·NaN 등은 502 `invalid_output`이다. 일부가 잘못된 출력을 성공 카드로 바꾸지 않는다.

유효한 빈 카드(`name: ""`, 빈 목록, `attitude: "neutral"`)는 정상적인 전체 교체 결과다. 잘못된 출력의 기본값과 구별한다. 모델이 명시적 철회를 올바르게 이해했는지는 별도 의미 검토 대상이다.

## 무례·연락처·호출 처리

- Spring의 `guard.abusive === true`인 줄만 `history[].rude: true`다. null·false는 false로 보낸다. `rude` 필드에 null 자체를 보내지 않는다.
- 기존 기억 프롬프트 준비 단계는 확정 무례 원문·생성 번역을 표식으로 바꿔 전달한다. 사건 결과는 Spring의 `outcome`을 사용하며, 위협·횟수·처벌을 새로 추측하지 않도록 지시한다.
- 전화번호·이메일 검사는 길이를 자르기 전에 전체 원문에 적용한다. 필터는 정규식 범위이며 모든 개인정보 탐지를 보증하지 않는다.
- 기억 생성은 응답 지연만을 이유로 같은 요청을 병렬 추가 전송하는 hedge를 사용하지 않는다. 기존 공급자 실패 재시도·대체 모델 정책은 유지된다. 오류 응답에는 사용량이 없을 수 있어 실제 청구 내역과 별도로 대조한다.
- `attitude`는 말투 참고값이다. 호감도·보상·결제·미션 상태를 AI 카드로 확정하지 않는다.

## 관계(알아봄·호감) — 백엔드 연결

워커 계약은 [AI 워커 명세](ai-worker.md) 2.2·2.3·2.7.2절이다. 백엔드(`MemoryService`, `TurnService`)는 다음과 같이 연결한다.

- **사건 저장:** 발화 전송(CONV-03)에서 `/v1/judge`의 `events`를 그 학습자 턴(`turns.events`)에 저장하고, 이후 판정·대답·기억·관계 요청의 `history[].events`로 보낸다. NPC 줄과 사건이 없는 줄은 `[]`이다.
- **대화 중 호감:** 같은 트랜잭션에서 무례로 센 턴이면 `/v1/guard`의 `affinity_delta`를, 아니면 `/v1/judge`의 `affinity_delta`를 호감에 SQL 한 번으로 더한다(-100~100으로 자르고, 관계 행이 없으면 만든다). 그래서 그 턴의 응답 `conversation.affinity`에 바로 보인다.
- **대답·첫 인사:** 끝낸 대화가 있는 NPC면 `/v1/npc/reply`에 `relationship: {recognition, affinity, days_since_last_talk}`를 싣는다. 처음 만나면(끝낸 대화 없음) `memory`와 함께 뺀다. `days_since_last_talk`는 사용자별 게임 시계(WORLD-06·07)의 지금 날짜에서 마지막 대화를 끝낸 게임 날짜(`user_npc_relations.last_talk_game_day`)를 뺀 값이다. 게임 시계 도입 전 대화만 있으면 0이다.
- **대화 종료:** 기억 카드 갱신과 같은 비동기 작업에서 `/v1/npc/relationship`을 부른다. 보내는 `relationship`은 이번 대화를 반영하기 전에 저장돼 있던 값(지난 대화 뒤 지난 게임 날 수 포함)이고, 이번 대화를 끝낸 게임 날짜를 마지막 대화 날로 저장한다. 응답의 `recognition_delta`를 SQL 한 번으로 더한다(0~100으로 자름). 그래서 종료 처리가 겹쳐도 덮어쓰지 않는다. 호감은 대화 중에 이미 바뀌었으므로 여기서 바꾸지 않는다.
- 호출 조건은 기억 카드와 같다. [대화 종료]로 끝났거나 NPC가 끝낸 대화만, 학습자가 말한 대화만, 같은 대화는 한 번만 반영한다. 카드와 관계 중 하나가 실패해도 다른 하나는 반영하고, 실패한 쪽은 이전 값을 그대로 둔다.
- 관계 갱신은 모델을 부르지 않으므로 AI 호출 기록(`ai_requests`)을 남기지 않는다.

## 외부 호출 없는 연결 확인

`ai/worker/`에서 실행한다.

```powershell
python -m pytest tests/test_memory_card_flow.py tests/test_memory_moderation.py -q
```

고정 모델 카드와 테스트 저장 대역으로 최초 생성→선호 변경→철회→다음 NPC 프롬프트 전달, 실패 후 기존 카드 재사용, 요청 사이 카드 혼입 방지, 늦은 호출 중복 전송 방지를 검사한다. 실제 Spring·DB·GMS의 통합 검사나 모델의 의미 정확성 검사가 아니다.

현재 공유된 무례 처리 문서에서는 NPC 장기 기억을 MVP 제외로 분류한다. 이 작업은 요청받은 AI 카드 API와 연결 준비 범위이며, 본선의 장기 기억 기능을 자동 활성화하거나 Spring 저장 기능이 이미 완성됐다고 표시하지 않는다. 사실별 `add/replace/retract` 모듈·평가 자료는 별도 확장 자료로 유지한다.
