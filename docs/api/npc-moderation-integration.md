# 피드백·NPC 기억 연동

## 범위와 담당

| 담당 | 범위 |
|---|---|
| 임다빈 | `POST /v1/feedback`, `POST /v1/memory` |
| 김은지 | `/v1/npc/reply`, `/v1/audio/{id}`, NPC 대화 지시문 |
| 이정훈 | `/v1/stt`, `/v1/guard`, stt·guard 모델 역할 |
| 팀 합의 필요 | judge·hint·suggest·meaning·tts 담당 |
| Spring | 인증, 원문·판정 보관, 무례 누적·종료, 번역 표시, DB 저장·중복 방지, 사용량 승인·원장 |

담당 구분 기준은 [AI API 분담 초안](https://lab.ssafy.com/s15-final/S15P31A305/-/merge_requests/48)의 0절·3절이다. guard 분리는 [MR !50](https://lab.ssafy.com/s15-final/S15P31A305/-/merge_requests/50)의 계약을 따른다. NPC 대사·경고·종료 지시값을 이 문서에서 새로 확정하지 않는다. `prompts.strip_actions`는 공유 처리이므로 관련 담당과 함께 검토한다.

워커 요청은 Spring에서만 보내며 `X-Internal-Token`을 사용한다. 아래는 피드백·기억 경계이며 Spring 저장 기능까지 구현됐다는 뜻은 아니다.

## guard·judge 분리 후 입력

- `/v1/judge`는 의도만 반환한다. 응답의 `guard`·`translation`·`better_expressions`를 읽지 않는다. 영어 번역·교정문과 다른 표현은 판정과 동시에 부르는 `/v1/expressions`에서 받는다.
- 무례 판정은 `/v1/guard`의 `guard.abusive`를 사용한다. Spring이 원래 null과 오류 이유를 보관한다.
- 번역 표시 여부는 Spring이 `유효 영어 번역 있음 && abusive === false`로 계산한다.
- 행동 묘사만 남아 dropped인 입력은 실제 사용자 발화로 평가하지 않는다.
- 한 대화의 1·2회 지적, 3회 종료 계산은 Spring, 대사 생성은 NPC 담당이다. MVP의 정당방위 장면·재대화 제한은 분담 초안에서 제외했다.
- 무례로 센 말마다 Spring이 그 NPC의 호감도를 깎는다(2026-10-29 팀 결정, [무례 처리 합의안](rudeness-handling.md) 3절).

| guard.abusive | 피드백 text_status | 기억 history[].rude |
|---|---|---|
| `true` | `blocked` | `true` |
| `false` | `final` | `false` |
| `null` | `final` | `false` |

기억의 rude는 bool만 받는다. 이 축약값으로 원래 판정이 false였는지 null이었는지 복원할 수 없다. 판정 실패를 정상 판정으로 확정해서 저장하지 않는다.

## 피드백 요청·응답

`POST /v1/feedback`의 입력·출력 계약은 **0.3.0 그대로**다. 전체 요청은 [입력 예시](../../ai/feedback/samples/inputs/free_grammar.json)를 참고한다.

1. 원문·판정을 확정한 뒤 scope의 사용자·NPC·대화 ID, conversation_revision, 실제 도움 사용 이력을 전달한다.
2. `final_text`에는 확정 사용자 원문을 넣는다. `/v1/expressions`가 만든 영어 번역으로 바꾸지 않는다.
3. true 판정 사용자 턴은 blocked로 넣는다. 요청 계약에 원문은 남지만 모듈이 모델 입력·평가·인용·교정·회화 설명·단어장 후보에서 제외한다.
4. null은 final을 유지한다. final이 영어 평가 가능 또는 무례 아님 확정을 의미하지는 않는다. 한글이 든 입력도 보존하며, STT 제품 지원 범위를 이 계약에서 새로 정하지 않는다.
5. 모든 사용자 턴이 blocked면 `insufficient_evidence / NO_USABLE_USER_TURNS`, 워커 `usage: []`이며 피드백 모델을 호출하지 않는다.

| HTTP 200의 feedback.status | 호출 측 처리 |
|---|---|
| `completed` | 유효 결과를 저장하고 조회에 연결 |
| `insufficient_evidence` | 자료 부족 안내. 교정 결과를 만들어 표시하지 않음 |
| `failed` | problem 기록. 완료된 학습 결과로 표시하지 않음 |

HTTP 성공만 보고 생성 성공으로 처리하지 않는다. 워커 응답과 DB 저장 완료는 별도이며, 요청 중복·사용량 합산·저장 후 재조회는 Spring에서 처리한다. 계약의 다른 상태는 [피드백 문서](../../ai/feedback/README.md)를 따른다.

프로토타입 변환도 `history.rude is True`일 때만 blocked다. false·null·누락은 final이며 NPC 줄은 이 플래그로 차단하지 않는다. 모델 호출을 생략한 경우 이전 대화의 last_call 사용량을 가져오지 않는다. 수집 로그·zip을 자동 재평가하지 않는다.

## 기억 요청·응답

`POST /v1/memory`에는 확정 이력 전체, 이전 카드, 서버에서 확인한 outcome을 전달한다. 현재 턴을 중복 넣지 않고, NPC 응답을 넣는 경우 실제 확정된 대사를 사용한다.

```json
{
  "npc": {"id": "mia", "name": "Mia", "role": "the barista.", "scene": "Cafe."},
  "outcome": "Spring ended the conversation after three confirmed rude turns.",
  "history": [
    {"who": "me", "text": "I'd like a tea, please.", "rude": false},
    {"who": "me", "text": "SYNTHETIC_BLOCKED_1", "rude": true},
    {"who": "npc", "text": "Please speak respectfully."},
    {"who": "me", "text": "SYNTHETIC_BLOCKED_2", "rude": true},
    {"who": "npc", "text": "This is your final warning."},
    {"who": "me", "text": "SYNTHETIC_BLOCKED_3", "rude": true},
    {"who": "npc", "text": "Goodbye."}
  ],
  "memory_card": {"name": "Alex", "claims": ["Prefers tea."], "events": [], "attitude": "neutral", "struggled": []}
}
```

이 대사는 요청 구조를 보여 주는 합성 예시다. NPC 담당의 실제 생성·종료 계약을 대체하지 않는다.

- 기억 모델에는 rude:true 사용자 원문·생성 번역 대신 `[RUDE: content withheld]`를 전달한다. 확정 사건은 원문 인용 없이 요약할 수 있지만 위협·횟수·제재·종료를 만들어 내지 않는다.
- 이름·프로필·선호·어려워한 표현에 욕설 원문을 넣지 않는 프롬프트 규칙을 적용한다. 의미 품질을 결정론적으로 보장하는 필터는 아니다.
- `attitude`는 NPC 대사 톤 힌트이며 서버 친밀도·제재 점수가 아니다.
- HTTP 200의 memory_card는 갱신 후보다. 실패하면 기존 카드를 유지하고 오류를 기록하며 빈 카드로 덮어쓰지 않는다.
- 피드백과 기억의 성공·실패는 각각 처리한다. 오래된 작업이 새 카드를 덮지 않도록 버전 검사·재시도·저장 정책은 Spring에서 구현한다.
- 사실별 기억 DB·철회 API·과거 카드의 의미 오류 자동 정정은 이 변경의 구현 범위가 아니다.

## 담당 모델 설정

[dabin-roles.env.example](../../ai/worker/dabin-roles.env.example)에는 `MODEL_MEMORY`·`MODEL_FEEDBACK`만 둔다. GPT-5.4 기준 설정이며 자동 로드 파일이나 최적 모델 선정 결과는 아니다. 기존 실행 환경에 필요한 값만 추가하고 NPC·STT·guard·그 밖의 역할 설정과 키는 유지한다.

실제 키는 예제·Git에 넣지 않는다. 역할 모델 외에도 fallback이 적용될 수 있으므로 실제 호출 승인 후에는 응답 usage의 모델·시도 내역으로 확인한다.

## 배포 파일 확인

피드백은 Python 코드 외에 다음 런타임 자원이 필요하다.

- `ai/feedback/prompts/feedback_system.ko.md`: build_messages가 읽는 시스템 지시문.
- `ai/feedback/content/spoken_usage.ko.json`: 회화 용례 후보·설명.
- `ai/worker/llm_profiles.json`: 워커 모델 구성.

Docker 이미지의 실제 배치 경로가 코드의 상대 경로와 맞아야 한다. prompts 폴더가 빠진 경우 유효 발화 피드백에서 모델 호출 전에 FileNotFoundError로 실패할 수 있다. 운영 /health 성공만으로 피드백 자원 포함 여부를 확인할 수는 없다. 이미지에 자원을 포함하고 가짜 공급자를 사용한 피드백 성공 검사를 수행한다. Dockerfile·배포 이미지 수정은 인프라 담당과 진행한다.

`python -X utf8 ai/feedback/check_runtime.py`로 로컬 필수 자원을 검사할 수 있다. 이미지 내부에서도 해당 경로의 명령을 실행한다. [피드백 연동 명세](feedback-integration.md)에 배치 예시와 검사 한계가 있다. 이 도구는 현재 운영 health 응답을 변경하지 않는다.

## 외부 호출 없는 검사

테스트 의존성이 설치된 Python으로 `ai/worker`에서 실행한다.

```sh
python -m pytest tests/test_feedback.py tests/test_memory_moderation.py tests/test_profiles.py -q
```

피드백 전체 회귀 검사는 `ai/feedback`에서 `python -m pytest -q`로 별도 실행한다. 테스트는 가짜 공급자를 사용하며 키·실제 서버·DB·GMS 호출이 필요 없다.

검사 범위는 차단·혼합 이력 처리, 원문 보존, 피드백 오류·시간 상한, 기억 입력 제외, 담당 역할만 설정하는지다. 실제 모델 품질·크레딧, Spring 상태·DB 저장, Unity 표시는 별도 검증 대상이다. 실제 GMS 호출은 사용자에게 모델·사례·횟수 승인을 받은 뒤 진행한다.
