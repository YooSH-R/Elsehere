# 대화 종료 피드백 — 백엔드 연동

Spring에서 호출하는 `POST /v1/feedback`의 연결 기준이다. 사용자용 신규 API나 DB 테이블을 정의하지 않는다. 기본 계약은 0.3.0이며, [실행 대조용 요청·응답 6건](examples/feedback-contract.json)은 모두 가상 대화·고정 모델 응답이다. 예시의 모델 이름·토큰·지연을 실제 성능이나 비용으로 사용하지 않는다.

## 대화 종료 → 생성 → 저장 → 다시 열기

1. Spring이 대화 소유권과 종료 상태를 확인하고 확정된 대화 스냅샷을 만든다. 사용자·NPC·대화 ID는 서버에서 결정한다.
2. 같은 대화 버전의 저장 결과가 있으면 재사용한다. 새 호출의 사용량 승인·예산 예약·동시 중복 방지는 Spring 책임이다. `request_id`를 같게 보냈다고 워커가 중복 호출을 막지는 않는다.
3. 승인된 요청만 내부 토큰 헤더 `X-Internal-Token`과 함께 워커에 보낸다. 입력은 예시의 `request` 전체다. `scope`, `conversation_revision`, `analysis_config_version`을 함께 보관한다.
4. 응답은 `{ "feedback": { ... }, "usage": [ ... ] }`다. `feedback.status`를 확인하고 요청 식별자·scope·revision·설정 버전이 해당 작업과 맞는지 대조한다. 이전 작업의 늦은 응답으로 최신 결과를 덮지 않는다.
5. 생성 결과와 저장 결과를 구별한다. 저장 후 재조회하는 화면은 저장된 결과를 사용하고, 다시 열 때 AI를 재호출하지 않는다.

## 입력 변환

| 원래 판정·입력 | 피드백 입력 |
| --- | --- |
| 사용자 `abusive === true` | `text_status: blocked` |
| 사용자 `abusive === false` 또는 `null` | 확정 원문이면 `text_status: final` |
| STT 미확정 | `needs_review` 등 실제 상태 유지. 임의로 final로 확정하지 않음 |
| NPC 대사 | speaker는 npc, input_type은 generated. 실제 확정 대사 사용 |
| judge가 만든 영어 번역 | 사용자 `final_text`를 대체하지 않음 |
| 도움 사용 | 실제 `assistance_events`와 해당 턴의 `assistance_ids`를 연결 |
| 첫날 퀘스트 단계 대화 | `scene_description`에 장소·NPC·상황·안내와 그 단계의 할 일(목표 제목과 설명)을 넣는다. 모델이 단계에 맞는 표현만 권하게 한다 |
| 대화 난이도 | `settings_version`은 `level-N`, `vocabulary_profile`은 `level-N: 수준 설명`(워커 힌트와 같은 CEFR 설명). 이 입력을 바꾸면서 `analysis_config_version`을 `elsehere-feedback-v2`로 올렸다 |

null 판정은 무례가 아님을 뜻하지 않는다. 원래 null·실패 이유는 Spring 기록에 유지한다. 한글 입력도 원문 그대로 전달한다. 모듈은 blocked·미확정 사용자 줄을 평가·원문 인용·교정·회화 용법·단어장 후보에서 제외한다.

## 응답 처리

| HTTP·상태 | Spring 처리 | 모델 요청 |
| --- | --- | --- |
| 200 / completed | 유효 학습 결과 저장. 요약·발화별 평가·교정·단어장 후보 표시 | 발생 가능 |
| 200 / insufficient_evidence | 평가할 자료 부족 표시. 완료된 교정을 만들어 표시하지 않음 | 유효 사용자 발화가 전혀 없으면 0회. 모델이 자료 부족으로 판단한 경우에는 발생 가능 |
| 200 / failed | problem 저장. 학습 결과 completed로 처리하지 않음 | 발생했을 수 있음 |
| 400 / invalid_request | 요청 형식 수정. 그대로 자동 재전송하지 않음 | 입력 검증에서 거부되면 0회 |
| 401 등 HTTP 오류 | 내부 인증·전달 오류 정책으로 처리 | 오류 코드만으로 비용을 단정하지 않음 |

`failed`의 `AI_TIMEOUT`, `AI_PROVIDER_ERROR`, `INVALID_MODEL_OUTPUT`은 교정 결과가 아니다. 예시의 잘못된 모델 출력은 HTTP 200이지만 `feedback.status: failed`이며, 생성 토큰 사용량은 남는다. 실패를 무료로 계산하지 않는다. 자동 재호출은 새 예산 승인과 중복 방지 정책을 거쳐야 한다.

계약 자체에는 `blocked_by_quota`와 `processing`도 있지만, 현재 워커의 단일 동기 요청은 Spring의 사전 승인을 전제로 처리한다. Spring이 승인하지 않은 요청을 워커에 보내 quota 판정을 대신하게 하지 않는다.

## 화면에 넘길 데이터

- 짧은 피드백: conversation_summary, communication, review_tags와 summary_suggestion_id가 가리키는 실제 교정.
- 상세 피드백: 원래 순서의 turn_reviews. 문법·어휘 오류와 context_fit을 구별하고, 적절한 표현을 오류로 표시하지 않는다.
- 단어장: vocab_candidates는 저장 후보다. 사용자 선택과 백엔드 저장 성공 전에는 저장 완료라고 표시하지 않는다. BE는 단어장이 단어 하나만 저장하므로 `kind: word`이고 한 단어인 후보만 `conversation_feedback.reviews`에 함께 저장하고 FB-01·CONV-08 응답의 `feedback.vocabCandidates`로 내려 준다. 저장은 VOCAB-02 `FEEDBACK`이 이 목록으로 단어를 확인한다.
- 회화 보충: `spoken_usage_notes`는 교정 아래의 선택적 설명 0~1개다. 현재 로컬 확장 필드이며 BE·Unity 적용 합의가 별도로 필요하다. 필드 누락은 빈 목록으로 처리할 수 있어야 하며, 설명이 없다고 실패로 처리하지 않는다.
- 장면 표현: `situation_expressions`는 이 장면에서 자주 쓰는 관용·추천 표현 0~3개(표현·뜻·쓰는 때·학습 예문과 번역)다. 워커가 생성할 때는 2~3개를 요구하고, 필드가 없던 결과는 빈 목록으로 처리한다. BE는 `conversation_feedback.reviews`에 함께 저장하고 FB-01·CONV-08 응답의 `feedback.situationExpressions`로 내려 준다. Unity는 피드백 화면의 회화 칸에 "이 상황에서 자주 써요"로 보여 준다.

`review_id`, `suggestion_id`, `candidate_id`는 결과 내부 참조값이며 DB 식별자가 아니다. usage의 토큰 수는 크레딧이 아니다. 현재 워커의 공급자 재시도·대체 모델 정책이 적용될 수 있으므로 실제 모델·시도·사용량을 응답 기준으로 기록한다.

## 배포 자원 검사

피드백 Python 패키지 옆에 다음 파일이 있어야 한다.

```text
ai/feedback/
  feedback/...
  prompts/feedback_system.ko.md
  content/spoken_usage.ko.json
  check_runtime.py
```

환경 의존성을 설치한 뒤 아래 검사를 실행한다. 키를 읽거나 모델을 호출하지 않으며 사용자 기록·샘플 파일도 필요하지 않다.

```sh
python -X utf8 ai/feedback/check_runtime.py
# 별도 배포 폴더의 정적 자원만 검사
python -X utf8 ai/feedback/check_runtime.py --root /app/ai/feedback
```

`{"ready":true,"issues":[]}`이면 필수 정적 자원 검사를 통과한 것이다. 누락·읽기 오류·UTF-8 오류·빈 파일·JSON 오류·필수 콘텐츠 필드 오류·중복 콘텐츠 ID·잘못된 정규식은 종료 코드 1이다. 파일 본문이나 상세 예외를 출력하지 않는다.

이는 서버 기동, 프롬프트 의미 품질, 실제 컨테이너 포함 여부, 전체 의존성, Spring 저장 연동을 보증하지 않는다. 대상 이미지 안에서도 직접 실행해야 한다. 자원이 없어도 `/health`만 성공하는 상황을 점검하는 별도 도구이며, 운영 API·health 응답은 변경하지 않는다.

인프라는 실제 이미지 경로에 맞춰 코드뿐 아니라 prompts와 content를 포함해야 한다. 예를 들어 코드가 `/app/ai/worker`에 있는 이미지에서는 다음처럼 형제 위치를 유지한다. 아래는 인프라 적용 예시이며 현재 Dockerfile을 수정·배포한 결과가 아니다.

```dockerfile
COPY ai/feedback/feedback/ /app/ai/feedback/feedback/
COPY ai/feedback/prompts/ /app/ai/feedback/prompts/
COPY ai/feedback/content/ /app/ai/feedback/content/
COPY ai/feedback/check_runtime.py /app/ai/feedback/check_runtime.py
RUN python -X utf8 /app/ai/feedback/check_runtime.py
```

## 외부 호출 없는 연동 검사

```sh
# ai/worker에서: 전달 예시와 실제 API 경로 대조
python -m pytest tests/test_feedback_handoff.py tests/test_feedback.py -q
# ai/feedback에서: 정적 배포 자원 검사와 전체 모듈 회귀
python -m pytest -q
```

무례 판정 분리와 담당 범위는 [피드백·기억 연동](npc-moderation-integration.md), 기억 카드 저장·다음 방문 전달은 [기억 연동](npc-memory-integration.md)을 따른다. Spring·DB·Unity의 실제 왕복과 실제 모델 의미 품질은 별도 검증 대상이다.
