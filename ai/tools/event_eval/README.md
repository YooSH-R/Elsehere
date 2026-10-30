# 대화 사건 판정 테스트셋

의도 판정(`/v1/judge`)이 학습자 줄의 대화 사건도 함께 고르게 하자는 제안(「NPC 기분·관계 사건 판정 협의서」)을 평가하는 자료입니다. 사건 값은 NPC 호감·기분 숫자(`ai/npcs/rules/world-rules.json`의 `conversationEvents`)에 바로 들어가므로, 사건을 잡아내는지보다 **사건이 아닌 줄을 사건으로 잡지 않는지**를 먼저 봅니다.

- 측정 도구: `ai/tools/event_eval.py`
- 케이스: `cases.json` (가이드 v1.0 기준)
- 문장은 모두 직접 지어낸 것입니다. 팀원 기록·AI Hub 데이터는 넣지 않습니다.

## 무엇을 확인하나

| 묶음 (`group`) | 내용 | 보는 것 |
| --- | --- | --- |
| `clear` | 분명한 사건 (한국어 줄, 한 줄에 사건 여럿 포함) | 사건을 잡아내는지 (재현율) |
| `none` | 용건만 말함, 짧은 대답, 틀린 영어, 한국어가 섞인 용건 | 빈 배열을 내는지 (오탐) |
| `boundary` | 의례적 감사, 못 알아들은 "sorry?", 영어 실력 사과, 용건 관련 질문 등 | 아래 정의대로 가르는지 |
| `mission` | 첫날 미션 상태에서 의도 + 사건 | 사건 칸을 더해도 의도가 그대로인지 (전후 비교) |

지표:

- 사건별 정밀도·재현율. **정밀도를 우선**합니다 (잘못 잡은 사건은 호감을 부풀립니다).
- 사건 없는 줄(`events: []`)에서 하나라도 잡은 비율 (오탐 비율).
- 줄 단위 완전 일치율.
- 미션 줄의 의도 일치율: 지금 지시문(`before`)과 사건을 더한 지시문(`after`)을 같은 모델로 비교합니다. 의도 순서는 보지 않고 집합으로 비교합니다. 목표에 맞는 의도가 없으면 `unclear`입니다.

자유 대화(`free_talk`)는 Spring이 의도 `chat` 하나만 보내므로 의도는 채점하지 않습니다.

## 사건 정의 (가이드 v1.0)

학습자의 **이 줄**이 상대에게 하는 일만 봅니다. 무례는 여기서 판정하지 않습니다(`/v1/guard`의 몫). 무례한 줄은 테스트셋에 넣지 않으며, 무례 턴의 사건은 Spring이 버립니다. 영어 실수·한국어·짧은 대답 자체는 사건이 아니고, 한국어로 한 말도 뜻대로 셉니다.

| 사건 | 센다 | 세지 않는다 |
| --- | --- | --- |
| `GREETING` | 인사 ("hi", "hey", "good morning", "nice to meet you", "안녕하세요"). 인사로 하는 "how are you?", "how's it going?"도 인사 | 인사 없이 바로 용건 |
| `SMALL_TALK` | 용건 밖의 이야기: 날씨·경기·여행 이야기, 자기 소개나 자기 이야기를 덧붙여 답함 | 용건에 관한 질문·이유 설명, "yes" 같은 짧은 맞장구 |
| `PERSONAL_QUESTION` | 상대 자신의 삶을 물음 (일한 기간, 사는 곳, 주말, 반려동물) | 인사로 하는 "how are you?" (→ `GREETING`) |
| `APOLOGY` | 자기 잘못·상대에게 끼친 불편을 사과 ("sorry I'm late", "sorry for the trouble") | 못 알아들어 되묻는 "sorry?", 부르는 "excuse me", 위로하는 "sorry to hear that", 자기 영어 실력에 대한 사과 |
| `COMPLIMENT` | 상대·상대가 한 일·상대의 가게나 물건을 칭찬함. 감사에 상대를 높이는 말이 붙은 것 ("thanks, you're so kind") | 의례적 감사 ("thanks", "thank you so much", "감사합니다"), "great", "good" 같은 짧은 반응 |
| `TASK_DONE` | 용건이 끝났다고 말로 드러냄 ("that's all", "that's everything I need", "anything else?"에 "no thanks") | 주문·결제 등 단계 하나를 함. 마지막 단계인지는 판정이 알 수 없으므로 보지 않음 |
| `LEAVE` | 작별 인사 ("bye", "see you", "have a nice day", "안녕히 계세요"), 가 봐야 한다고 말함 | |

- `PERSONAL_QUESTION`과 `SMALL_TALK`는 겹치지 않게 셉니다. 상대의 삶을 물으면 `PERSONAL_QUESTION`만 셉니다.
- 한 줄에 여러 사건이 있으면 모두 셉니다 ("Hi! One latte, please. I love this cafe." → `GREETING`, `COMPLIMENT`).

## 잠금 규칙

- 정답은 위 가이드로 정하고, 모델 결과를 본 뒤 케이스나 정답을 고치지 않습니다. 정의를 바꿔야 하면 가이드 버전을 올리고 케이스 전체를 다시 검토합니다.
- 지시문 문장에 테스트 문장을 예시로 넣지 않습니다 (과적합).
- 통과 기준 수치는 첫 측정 전에 정합니다.

## 실행

워커 가상환경으로 저장소 루트에서 실행합니다. 공용 키라 한 줄씩 순서대로 보내고, 결과는 집계만 출력합니다.

```sh
ai/worker/.venv/Scripts/python ai/tools/event_eval.py --variant before   # 지금 의도 지시문: 미션 줄의 의도만 채점
ai/worker/.venv/Scripts/python ai/tools/event_eval.py --variant after    # 사건을 더한 지시문: 사건과 의도를 함께 채점
```

`--profile`(기본 `gms-luna`), `--env`(키를 읽을 `.env`, 기본 저장소 루트), `--only clear,none`(묶음 고르기), `--out 결과.jsonl`(줄마다의 기록)을 쓸 수 있습니다.
