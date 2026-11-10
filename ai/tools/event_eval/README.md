# 대화 사건 판정 테스트셋

의도 판정(`/v1/judge`)이 학습자 줄의 대화 사건도 함께 고르게 하자는 제안(「NPC 기분·관계 사건 판정 협의서」)을 평가하는 자료입니다. 사건 값은 NPC 호감·기분 숫자(`ai/npcs/rules/world-rules.json`의 `conversationEvents`)에 바로 들어가므로, 사건을 잡아내는지보다 **사건이 아닌 줄을 사건으로 잡지 않는지**를 먼저 봅니다.

- 측정 도구: `ai/tools/event_eval.py`
- 테스트셋: `cases.json` (109줄). 잠가 두고 지시문을 다듬을 때 보지 않습니다.
- 개발셋: `dev_cases.json` (98줄, 그중 미션 의도 줄 34). 지시문을 다듬거나 모델을 고를 때 보는 줄입니다. 테스트셋 문장을 넣지 않습니다(`test_event_eval.py`가 확인).
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

## 사건 정의 (가이드 v1.2)

학습자의 **이 줄**이 상대에게 하는 일만 봅니다. 무례는 여기서 판정하지 않습니다(`/v1/guard`의 몫). 무례한 줄은 테스트셋에 넣지 않으며, 무례 턴의 사건은 Spring이 버립니다. 영어 실수·한국어·짧은 대답 자체는 사건이 아니고, 한국어로 한 말도 뜻대로 셉니다.

| 사건 | 센다 | 세지 않는다 |
| --- | --- | --- |
| `GREETING` | 인사 ("hi", "hey", "good morning", "nice to meet you", "good to see you", "안녕하세요"). 인사로 하는 "how are you?", "how's it going?"도 인사 | 인사 없이 바로 용건 |
| `SMALL_TALK` | 용건 밖의 이야기: 날씨·경기·여행 이야기, 자기 소개나 자기 이야기를 덧붙여 답함 | 용건에 관한 질문·이유 설명, "yes" 같은 짧은 맞장구 |
| `PERSONAL_QUESTION` | 상대 자신의 삶을 물음 (일한 기간, 사는 곳, 주말, 반려동물) | 인사로 하는 "how are you?" (→ `GREETING`) |
| `APOLOGY` | 자기 잘못·상대에게 끼친 불편을 사과 ("sorry I'm late", "sorry for the trouble") | 못 알아들어 되묻는 "sorry?", 부르는 "excuse me", 위로하는 "sorry to hear that", 자기 영어 실력에 대한 사과 |
| `COMPLIMENT` | 상대·상대가 한 일·상대의 가게나 물건을 칭찬함. 상대·가게·물건이 좋다고 말함 ("I like you", "I like it here"). 감사에 상대를 높이는 말이 붙은 것 ("thanks, you're so kind") | 의례적 감사 ("thanks", "thank you so much", "감사합니다"), "great", "good" 같은 짧은 반응, 주문하며 말하는 자기 취향 ("I like iced americano"), 바람을 말하는 "I'd like ~" |
| `TASK_DONE` | 용건이 끝났다고 말로 드러냄 ("that's all", "that's everything I need", "anything else?"에 "no thanks") | 주문·결제 등 단계 하나를 함. 마지막 단계인지는 판정이 알 수 없으므로 보지 않음 |
| `HELP` | 학습자가 상대에게 구체적인 도움을 제안하거나 상대의 도움 요청을 수락함. 상대의 현재 문제에 맞는 유용한 정보를 알려줌. 명확한 제안만으로도 셈 | 학습자의 도움 요청, NPC가 도움을 주는 경우, 평범한 주문·결제, 막연한 친절, 거절, 인용·가정, 대가 요구 |
| `LEAVE` | 작별 인사 ("bye", "see you", "have a nice day", "안녕히 계세요"), 가 봐야 한다고 말함 | |

- `PERSONAL_QUESTION`과 `SMALL_TALK`는 겹치지 않게 셉니다. 상대의 삶을 물으면 `PERSONAL_QUESTION`만 셉니다.
- 한 줄에 여러 사건이 있으면 모두 셉니다 ("Hi! One latte, please. I love this cafe." → `GREETING`, `COMPLIMENT`).

도움 사건은 한 대화에서 한 번만 기본 호감 +6(인상 8 × 0.75)을 줍니다. 짧은 수락은 상대의 명시적인 도움 요청에 답할 때만 해당합니다. 도움 제안으로 실제 행동 수행이나 미션 성공을 확정하지 않습니다.

## 잠금 규칙

- 정답은 위 가이드로 정하고, 모델 결과를 본 뒤 케이스나 정답을 고치지 않습니다. 정의를 바꿔야 하면 가이드 버전을 올리고 케이스 전체를 다시 검토합니다.
- v1.1은 v1.0에서 정하지 않았던 "좋아한다"는 말을 칭찬으로 정했습니다. 테스트셋에서 해당하는 줄("I love this cafe" 등)은 원래 칭찬이었고 "I'd like to check in"은 칭찬이 아니어서, 정답은 바뀌지 않았습니다.
- v1.2는 도움 제공(`HELP`)을 추가합니다. 기존 문장과 정답은 유지하고 도움 제공·수락·정보 제공과 도움 요청·거절·결제의 경계 문장을 추가했습니다. 추가 문장의 실제 모델 판정 정확도는 별도 평가가 필요합니다.
- 지시문 문장에 테스트 문장을 예시로 넣지 않습니다 (과적합).
- 통과 기준 수치는 첫 측정 전에 정합니다. 지시문을 바꿀 때는 테스트셋에서 사건 없는 줄의 오탐 비율과 사건별 정밀도가 지금보다 나빠지지 않고, `GREETING`·`COMPLIMENT`·`TASK_DONE` 재현율이 올라야 합니다.

## 실행

워커 가상환경으로 저장소 루트에서 실행합니다. 공용 키라 한 줄씩 순서대로 보내고, 결과는 집계만 출력합니다.

```sh
ai/worker/.venv/Scripts/python ai/tools/event_eval.py --variant before   # 지금 의도 지시문: 미션 줄의 의도만 채점
ai/worker/.venv/Scripts/python ai/tools/event_eval.py --variant after    # 사건을 더한 지시문: 사건과 의도를 함께 채점
ai/worker/.venv/Scripts/python ai/tools/event_eval.py --variant service  # 지금 서비스 지시문(app.prompts) 그대로: 사건과 의도를 함께 채점
ai/worker/.venv/Scripts/python ai/tools/event_eval.py --variant service --cases dev   # 개발셋
```

`before`·`after`는 사건 판정을 넣을 때 비교한 기록용입니다(`after`는 협의서 초안 지시문을 덧붙임). 지금 서비스 판정은 `service`로 잽니다.

`--cases`(기본 `test`, 개발셋은 `dev`), `--profile`(기본 `gms-luna`), `--env`(키를 읽을 `.env`, 기본 저장소 루트), `--only clear,none`(묶음 고르기), `--out 결과.jsonl`(줄마다의 기록)을 쓸 수 있습니다.

GPU 서버 vLLM 모델과 비교할 때는 `--gpu-model Qwen/Qwen3.8-27B`를 줍니다. 구성의 intent 모델 대신 워커의 `gpu_llm` 공급자(`GUARD_GPU_BASE_URL`·`GUARD_GPU_TOKEN`)로 같은 지시문·출력 형식을 보냅니다. 개발 PC에서는 배포 서버를 거쳐 GPU 터널 입구로 포워딩한 주소를 넣습니다. `--concurrency 10`은 10줄씩 동시에 보내 동시 요청 때의 지연을 잽니다(기본 1). 지연은 응답이 온 호출만 중앙·p90·최대로 찍습니다. 공용 키·공용 GPU라 동시 측정은 작게, 짧게 합니다.
