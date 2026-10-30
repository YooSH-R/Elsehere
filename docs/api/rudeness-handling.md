# 무례 처리 합의안 (Spring)

학습자가 NPC에게 무례한 말을 했을 때 Spring이 할 일을 정리한 **합의안**입니다. AI 워커는 판정값(`/v1/guard`의 `guard`)만 돌려주고, 횟수를 세고 대화를 끝내는 일은 Spring 규칙이 합니다([AI API 명세](ai-worker.md) 0.1절, 2.2.1절).

- 규칙과 지시문은 웹 프로토타입(`prototype/mvp_web`)에서 플레이로 다듬은 것을 그대로 옮겼습니다.
- Unity·백엔드가 쓰는 응답 칸과 DB 칸은 [MVP API 명세](mvp-api.md) CONV-03과 [ERD](../erd.dbml) v3.2에 있습니다(6절). 이 문서는 그 두 문서를 고치지 않습니다.
- 2·3·5절(판정값별 처리, 누적·퇴장, NPC 지시문)은 Spring 내부 규칙이라 MVP API 명세에 넣지 않아도 됩니다.
- `[제안]`은 작성자 제안, `[미정]`은 팀 결정이 필요한 항목입니다.

## 1. 한 턴의 흐름 (CONV-03)

```
학습자 말 ─┬▶ POST /v1/guard  (무례 판정)
           └▶ POST /v1/judge  (의도·번역)      ← 같은 text·history로 동시에
              │
              ├ dropped: true      → 행동 묘사만 있음. 턴으로 세지 않고 notice만 보여 줌
              ├ guard.abusive true → 2절 "무례". 의도는 적용하지 않음
              ├ guard.abusive null → 2절 "미판정". 의도는 평소처럼
              └ guard.abusive false→ 의도를 현재 상태에서 검증해 적용
              │
           Spring 규칙이 instruction을 정함 (5절)
              │
           POST /v1/npc/reply  (instruction, history의 rude 표시 포함)
              │
           CONV-03 응답 (6절)  ─▶  Unity: NPC 대사 + 경고 카드
```

## 2. 판정값별 처리

| `guard.abusive` | 무례 횟수 | 의도(`intent.ids`) | NPC 지시문 | 번역 카드 | 턴 기록 |
|---|---|---|---|---|---|
| `true` | +1 | **적용하지 않음** (상태·미션·금액 그대로) | 경고 또는 퇴장 (5절) | 보여 주지 않음 | `rude: true` |
| `false` | 그대로 | 평소처럼 검증해 적용 | 상태 전환 규칙의 지시문 | `english`가 있으면 보여 줌 | `rude: false` |
| `null` | **그대로** (무례로 세지 않음) | 평소처럼 검증해 적용 | 상태 전환 규칙의 지시문 | **보여 주지 않음** | `rude: false`, `guard_error_code` 기록 |

- 무례한 말에 주문이 섞여 있어도(예: "latte 내놔 이 멍청아") 그 턴은 주문으로 처리하지 않습니다. NPC는 먼저 말투를 짚습니다. 학습자는 같은 주문을 정중하게 다시 말해야 합니다.
- 무례한 말은 번역 카드("영어로는 이렇게 말해요")를 띄우지 않습니다. 욕을 영어로 가르치는 꼴이 되기 때문입니다.
- 무례로 센 턴(`rude: true`)은 그 턴을 저장하는 같은 트랜잭션에서 `/v1/guard`의 `affinity_delta`만큼 호감을 깎습니다(`max(-100, affinity + affinity_delta)`). 크기는 NPC 성격에 따라 워커가 정하고, 비어 있으면 공통 규칙의 `rudeAffinityFallback`을 씁니다. `false`·`null`은 깎지 않습니다([AI 워커 명세](ai-worker.md) 2.2.1절).
- `null`은 판정 모델의 빈 답·거절, 시간 초과, 공급자 장애입니다. 무례로 세면 정상 학습자가 억울하게 쫓겨나고, 정상으로 단정하면 번역 카드에 욕이 나올 수 있습니다. 그래서 횟수에는 넣지 않고 번역 카드도 띄우지 않습니다(`userTurn.translation: null`).
- Spring이 AI 워커에 닿지 못해 `/v1/guard` 응답을 받지 못한 경우도 `null`로 다루고 `guard_error_code`에 `worker_unavailable`을 남깁니다. 무례 판정 때문에 턴을 실패시키지 않습니다.

## 3. 누적과 퇴장

| 항목 | 규칙 |
|---|---|
| 세는 단위 | 대화 하나(`conversations`). 새 대화는 0부터 |
| 한도 | 3회 (서버 설정값 `rudeLimit`) |
| 1~2회째 | 대화 계속. NPC가 경고 지시문으로 대답, 상태는 그대로 |
| 3회째 | NPC가 퇴장 지시문으로 대답하고 대화가 끝남. `status: ENDED_BY_NPC`, `endReason: RUDE` |
| 퇴장 뒤 | 다른 종료와 같이 CONV-08로 피드백을 만듦 |
| 다시 말 걸기 | 막지 않음. 일정 시간 막는 것은 MVP에서 제외 |

- 판정은 틀릴 수 있습니다. 잠긴 테스트셋 기준으로 LLM 판정의 정밀도는 약 83~92%, 재현율은 약 65~72%라서, 정상 발화를 무례로 볼 때도 있고 무례한 말을 놓칠 때도 있습니다. 한도를 1회가 아니라 3회로 두면 오판 한 번으로 대화가 끝나지 않습니다. 한도를 낮추려면 이 오판율을 함께 봐 주세요.
- 퇴장 턴의 대사도 AI로 만듭니다(`/v1/npc/reply` + 퇴장 지시문). 토큰이 떨어졌을 때처럼 미리 쓴 작별 대사(`npcs.farewell_line`)를 쓰면 "급한 일이 생겼어요"가 되어 맞지 않습니다.
- 퇴장 턴에서는 추천 단어를 만들지 않습니다(`suggest_words: false`, `suggestWords: null`).

## 4. 정당방위 장면 (MVP 제외)

NPC가 먼저 잘못한 장면(예: NPC가 커피를 쏟고 돈으로 넘어가려 함)에서 화를 내는 것은 무례로 제재하지 않기로 했습니다. 판정 라벨은 말 자체로만 달기 때문에, 이 예외는 게임 규칙으로 처리합니다.

MVP 장소(`TOWN`·`CAFE`·`HOME`)에는 NPC가 잘못하는 사건 장면이 없어 이 규칙을 구현하지 않습니다. 그래서 MVP에서는 `userTurn.rude`가 `guard.abusive === true`와 같습니다. 사건 장면을 추가할 때 아래대로 넣습니다.

- 장면 데이터에 "NPC 잘못" 표시를 둡니다.
- 그 장면에서는 `abusive: true`여도 무례 횟수에 넣지 않습니다. NPC에게는 꾸짖지 말고 사과하며 수습하라는 지시문을 줍니다(5절). 번역 카드는 보여 줍니다.
- 원래 기준은 위협(폭력·체포)과 성희롱만 정당방위 장면에서도 제재하는 것입니다. 하지만 지금 판정은 이진값이라 유형을 구분하지 못합니다. **유형을 돌려주는 계약이 생기기 전까지는 그 장면 전체를 자동 제재에서 뺍니다.**
- 판정값과 제재 여부가 달라지므로 `turns.rude_counted`(무례 횟수에 넣었는지)를 이때 추가합니다(6절).

## 5. NPC 지시문 (`/v1/npc/reply`의 `instruction`)

무례 턴에서는 상태 전환 규칙의 지시문 대신 아래 지시문 **하나만** 보냅니다. 지시문은 영어 그대로 보냅니다(프롬프트가 영어). 한도는 1000자입니다.

**경고 (1~2회째)**

```text
They just said something rude or insulting to you. Do not ignore it. React like a real person: calmly but firmly say it is not okay to talk like that. 1-2 short sentences.
```

**퇴장 (3회째)**

```text
They keep insulting you. Firmly tell them you won't talk with them anymore and they need to leave now. Then stop. 1-2 short sentences.
```

**정당방위 장면** `[제안]` (MVP 제외) — 프로토타입에서 검증하지 않은 문장입니다. 쓰기 전에 회귀 테스트로 확인합니다.

```text
They are upset because of what you did wrong in this scene. Do not scold them for how they said it. Apologize sincerely and try to make it right. 1-2 short sentences.
```

- `history`의 학습자 줄에는 무례 횟수에 넣은 줄만 `rude: true`를 붙입니다. `abusive`가 `null`이었던 줄은 `false`입니다. 판정과 NPC 대답은 앞 대화 원문(판정은 최근 4줄, NPC 대답은 12줄)을 보고 앞서 들은 말을 압니다. 그래도 요청마다 같은 `history`를 보내면 됩니다.

## 6. 응답 칸과 DB 칸

**MVP API 명세·ERD v3.2에 있는 것**

| 위치 | 칸 |
|---|---|
| CONV-03 응답 | `userTurn.rude`, `conversation.rudeCount`·`rudeLimit`, 종료 시 `status: ENDED_BY_NPC`·`endReason: RUDE` |
| `turns` | `guard_abusive`(null이면 판정 실패), `guard_error_code` |
| `conversations` | `rude_count`, `end_reason`의 `RUDE` |

**기능을 켤 때 추가할 것** `[제안]`

| 칸 | 형식 | 언제 | 뜻 |
|---|---|---|---|
| `turns.guard_source` | varchar(10) | 로컬 판정기를 켤 때 | `local`·`llm` |
| `turns.guard_score` | real | 로컬 판정기를 켤 때 | 로컬 판정기 점수 (0~1, 확률 아님) |
| `turns.rude_counted` | boolean | 정당방위 장면을 넣을 때 | 무례 횟수에 넣었는지 |

- `guard_source`·`guard_score`는 로컬 판정기의 LLM 호출 감소와 자동 판정 품질을 재는 데 씁니다(AI API 명세 2.2.1절). 로컬 판정기는 기본으로 꺼져 있습니다.

**Unity 표시** `[제안]` (프로토타입 문구)

| 때 | 표시 |
|---|---|
| `userTurn.rude: true` | 내 말 아래 경고 카드: "⚠️ 방금 말은 상대에게 무례하게 들릴 수 있어요." 상단 알림 "무례한 말 1/3" |
| `endReason: RUDE` | 상단 알림 "무례한 말이 계속돼 상대가 대화를 끝냄", 이어서 피드백 화면 |

## 7. 다른 AI 기능과의 관계

| 기능 | 무례 표시가 쓰이는 곳 | 상태 |
|---|---|---|
| NPC 대답 (`/v1/npc/reply`) | 무례 턴의 지시문(5절). 앞서 들은 말은 `history` 원문으로 앎 | 워커 지원 |
| 대화 종료 피드백 (`/v1/feedback`, `ai/feedback` 0.3.0) | 무례한 줄(`abusive: true`)은 `text_status: blocked`로 보내 평가·인용에서 뺍니다. 판정 못 한 줄(`null`)은 `final` | 계약 변경 없음. 기능 명세 F-30(한 문장마다 평가)에 예외 한 줄 추가 필요 `[미정]` |
| NPC 기억 카드 (`/v1/memory`) | `history[].rude`로 무례했던 일을 남기고 다음 대화 태도를 조심스럽게 함 | NPC 장기 기억은 MVP 제외(기능 명세 5.1) |
| 호감 감점 (`/v1/guard`의 `affinity_delta`) | 무례로 센 발화마다 바로 호감을 낮춤(공통 규칙, NPC 성격에 따라 크기가 다름) | 워커 지원([AI 워커 명세](ai-worker.md) 2.2.1절). 백엔드 적용 `[미정]` |
| 관계 갱신 (`/v1/npc/relationship`) | 무례는 호감 계산에 다시 넣지 않음(이중 감점 방지). 대답 중에는 무례 뒤 몇 턴 동안 짜증이 남음 | 워커 지원(S15P31A305-338, [AI 워커 명세](ai-worker.md) 2.7.2절). 백엔드 저장 `[미정]` |

## 8. 남은 확인

| 항목 | 대상 | 지금 문서 | 제안 |
|---|---|---|---|
| 무례한 줄의 피드백 제외 | 기획·`ai/feedback` 담당 | 기능 명세 F-30에 예외 없음 | F-30에 "무례로 센 발화는 평가하지 않음" 추가 |
