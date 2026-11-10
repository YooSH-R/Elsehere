# 대화 종료 피드백 모듈

확정된 대화 텍스트를 분석해 요약, 의도 전달 평가, 리뷰 태그, 사용자 발화별 교정, 단어장 후보를 반환합니다. 기본 계약은 `0.3.0`이며, 로컬 검증판 결과에는 선택적인 `spoken_usage_notes` 확장과, 장면에서 자주 쓰는 표현을 담는 선택적인 `situation_expressions` 확장을 추가했습니다. 백엔드·Unity의 확정 계약에 적용하기 전 해당 필드와 버전을 함께 합의해야 합니다.

GMS 연결은 `feedback/gms.py`, 처리와 근거 검증은 `feedback/service.py`에 있습니다. 기본 모델은 저장소의 `gms-high` 후보와 같은 `gpt-5.4`이며 `reasoning_effort=none`을 사용합니다. 자동 재시도와 다른 모델로의 자동 전환은 하지 않습니다.

## 실행

Python 3.11 이상과 `requirements.txt`의 Pydantic을 사용합니다. 이 폴더에서 실행합니다.

```powershell
python -m pip install -r requirements.txt

# 외부 호출 없는 고정 응답 예시
python -X utf8 demo.py --case mixed_five_turns

# 설정 확인. 외부 호출과 과금 없음
python -X utf8 run_gms.py

# 실제 GMS 과금 호출: 가상 샘플 한 건, 요청 한 번
python -X utf8 run_gms.py --execute-real --case free_grammar

# 전체 자동 검사. 실제 네트워크를 호출하지 않음
python -X utf8 -m unittest discover -s tests -v
node --test tests/test_prototype_panel.cjs

# 수작업 가상 응답의 새 화면 확인. 실제 모델 호출/과금 없음
python -X utf8 render_spoken_examples.py

# 강건성 사례 14개의 입력 검증과 계획 확인. 외부 호출 없음
python -X utf8 run_evaluation.py

# 로컬 검사 통과 후 필요한 사례 하나만 실제 호출
python -X utf8 run_evaluation.py --execute-real --case unclear_reference
```

저장소 루트의 `.env.local`에 다음 값을 설정합니다. 기존 파일이 있으면 키를 보존하고 필요한 항목만 수정합니다. 환경변수로 제공한 값이 파일보다 우선합니다.

```dotenv
GMS_API_KEY=발급받은_키를_로컬에만_입력
GMS_BASE_URL=https://gms.ssafy.io/gmsapi/api.openai.com/v1
GMS_MODEL=gpt-5.4
```

`.env.local`, Python 실행 환경, `local_results/`는 Git 추적에서 제외합니다. 키를 소스·로그·실행 결과·공유 파일에 넣지 않습니다. `run_gms.py --env-file`로 로컬 설정 파일을 명시할 수도 있습니다.

`demo.py`는 수작업 고정 응답 전용입니다. 실제 모델 검증은 `run_gms.py`를 사용합니다. 실제 호출 실패를 고정 응답 성공으로 바꾸지 않습니다.

## 결과와 사용량

| 항목 | 의미 |
| --- | --- |
| `conversation_summary` | 실제로 주고받은 요청·답변·결정의 한국어 요약과 확정 발화 근거 |
| `communication` | 의도 전달 평가. 문법 교정과 구분 |
| `review_tags` | 잘한 점과 실제 문장 교정의 이유. 새 결과는 최대 2개이며 기존 계약은 최대 3개까지 읽음 |
| `turn_reviews` | 모든 확정 사용자 발화를 원래 순서로 한 번씩 평가. 모델은 고친 문장(`suggested_expression`)을 먼저 쓰고 설명(`explanation_ko`)을 쓰며, 설명이 원문에도 고친 문장에도 없는 영어를 인용하면 서버가 맞는 쪽 설명이나 실제 차이(“think lost” 대신 “think I lost”)로 바꿉니다 |
| `summary_suggestion_id` | 짧은 카드에서 보여줄 실제 교정 제안 참조 |
| `vocab_candidates` | 표현·뜻·예문·출처. 저장 완료 결과가 아님 |
| `spoken_usage_notes` | 검토한 회화 용법 설명 0~1개. 교정 아래의 별도 보충 자료이며 단어장 저장 출처가 아님 |
| `situation_expressions` | 이번 장면(scene_description)에서 자주 쓰는 관용·추천 표현 0~3개. 표현·뜻·쓰는 때·학습 예문과 번역. 사용자 발화 평가와 별개이며, 생성할 때는 2~3개를 요구하고 없던 기존 결과는 빈 배열로 읽음 |

리뷰는 `appropriate`, `improvement_suggested`, `needs_clarification`, `not_assessable` 중 하나입니다. 적절한 발화를 무조건 오류로 고치지 않습니다. 의미가 불명확한 발화는 뜻을 지어내 교정하지 않습니다.

의도가 분명한 한국어·한영 혼합 발화는 한국어만 썼어도 `improvement_suggested`로 두고, 원문을 그대로 보존한 채 같은 뜻의 영어 문장을 `context_fit` 제안 하나로 제공합니다. 영어 능력의 근거가 없다는 이유로 한국어 발화를 `not_assessable`로 두지 않으며, 의도가 불분명할 때만 `needs_clarification`입니다. 한국어를 영어 문법 오류로 분류하지 않습니다. 프롬프트에는 확정 사용자 발화 중 영어 표현이 필요한 ID만 함께 보내며, 완료 결과에서 영어 표현이 빠지거나 한국어가 그대로 반복되면 출력 오류로 처리합니다. 추가 번역 호출이나 자동 재호출은 하지 않습니다. 모호·평가 불가·차단 발화는 추측해서 번역하지 않습니다.

GMS에는 strict JSON Schema를 전달합니다. 반환 후에도 Pydantic 형식 검사와 발화 순서·원문 인용·교정 참조·단어장 출처 검사를 수행합니다. 형식 준수는 교정과 번역의 의미 정확성을 보장하지 않습니다.

프롬프트 버전은 `feedback_prompt_v0.11.0`입니다. 요약은 남은 퀘스트 목표와 대화 밖의 실행 여부를 추정하지 않습니다. 워커는 모델의 장점 태그를 유지하고, 실제 교정이 있으면 문장 교정에 표시할 첫 교정의 발화·이유를 아쉬운 점으로 연결합니다. 자연스러운 대안과 표기만 바꾼 제안은 아쉬운 점에 넣지 않습니다.

"이 상황에서 자주 써요"는 `content/situation_expressions.ko.json`의 장면별 관용 표현·숙어 후보(서버가 장면에 맞춰 최대 8개를 골라 넘김)에서 모델이 대화에 안 쓴 3개를 고르고, 사용자가 이미 말했거나 영어·한국어 칸이 뒤바뀐 항목은 결과에서 뺍니다. 출력 스키마는 API 옵션으로 한 번만 전송하고, 반복 난이도 설정은 참조로 묶습니다. 사용자 발화 원문과 평가 범위는 줄이지 않습니다. 생성 시 태그·단어장 후보는 각각 최대 2개, 개선 제안은 발화당 최대 1개입니다. 기존 결과에 회화 설명 필드가 없어도 표시할 수 있습니다.

### 모델 출력 분량 줄이기

GMS와 공통 워커는 모델 전용 `compact-v1` 스키마를 사용합니다. `original_text`, 발화·태그의 `assisted`, 평가·제안·태그·단어장 후보의 로컬 ID는 모델이 생성하지 않습니다. `feedback/model_output.py`가 확정 사용자 원문과 실제 도움 사용 기록에서 복원한 뒤 기존 계약·발화 범위·인용·참조 검증을 수행합니다. 평가·교정 설명과 추천 표현은 그대로 생성합니다.

발화당 제안이 최대 하나이므로 평가와 제안의 로컬 ID는 해당 사용자 `turn_id`를 사용합니다. 모델이 선택하는 `summary_suggestion_id`와 단어장 제안 출처의 `review_id`·`suggestion_id`도 그 `turn_id`를 가리킵니다. 태그·후보 ID는 각각 `tag1`, `vocab1`부터 결과 순서대로 부여합니다. DB 식별자가 아니며 원래 ID 문자열 자체에는 의미를 부여하지 않습니다.

공개 `0.3.0` 입력·출력 계약과 기존 고정 응답은 유지합니다. `schemas/feedback.llm-output.schema.json`은 복원 후 정규화된 형식이며, 실제 공급자용 스키마는 `feedback_response_format(messages)`로 만듭니다. 축약 응답에 서버 전용 필드를 섞거나 발화·인용·참조가 잘못되면 `INVALID_MODEL_OUTPUT`입니다. 다만 선택 항목 하나 때문에 피드백 전체를 버리지 않도록 복원 단계에서 두 가지는 그 항목만 정리합니다. 단어장 후보의 표현이 출처 글(사용자 원문 또는 연결된 개선 표현)에 그대로 있으면 `quote`를 그 표현으로 맞추고, 없으면 그 후보만 뺍니다. 음성 발화에 대소문자·끝 문장부호만 바꾼 교정은 그 교정만 빼고, 남은 제안이 없으면 판정을 `appropriate`로 두며 대표 제안과 그 제안을 출처로 한 단어장 후보도 함께 정리합니다. 출력 분량 감소는 실제 지연 단축률을 보장하지 않으며 동일 대화의 실모델 비교로 별도 확인해야 합니다.

### 문법 교정과 실제 회화 용법

문법·어휘 교정을 먼저 제시하고, 문법은 맞지만 확인된 의도와 어긋나면 `context_fit`으로 구별합니다. 적절한 표현은 고치지 않습니다. `I'm good`은 안부 답변과 제안에 대한 사양을 구분하며, 질문만 보고 수락 의도를 만들어내지 않도록 지시합니다. 의미 판단의 정확성은 별도 실모델 평가가 필요합니다.

`content/spoken_usage.ko.json`에는 공개 영상·방송 대본에서 조사한 38개 표현의 뜻, 용법, 근거 유형, 출처와 별도로 작성한 학습 예문이 있습니다. 생활 촬영·역할극·해설의 근거를 구별하며 사용 빈도 통계라고 주장하지 않습니다. 조건부 표현에는 관계·말투 주의를 붙이고, 이해용 표현은 원문 의미를 설명하는 용도로 제한합니다.

`feedback/spoken_usage.py`는 확정 사용자 발화에서 표현과 일부 관련 단서를 검색합니다. 장면·건물 ID에 의존하지 않으므로 새 상황에서도 같은 경로를 사용합니다. 다만 의미 검색기가 아니며 모든 동의어·활용형·의역을 포괄하지 않습니다. NPC·미확정 전사는 후보 근거에서 제외합니다. 후보는 최대 3개, 중복 인용은 후보당 최대 160자입니다. 영상 대본·전체 목록·출처·예문을 매번 모델에 보내지 않습니다.

기존 한 번의 피드백 호출에서 모델은 유용한 후보 하나의 ID와 근거만 선택하거나 `null`을 반환합니다. 서버는 후보와 발화·인용·평가 상태를 대조하고 검토된 설명·출처·학습 예문을 연결합니다. 후보와 맞지 않는 선택은 보충 설명만 생략하며 재호출하지 않습니다. 회화 설명이 필요 없는 대화는 빈 배열입니다. 설명용 추가 AI 호출은 없지만 후보·지시문·선택 필드의 입력/출력 토큰은 추가될 수 있습니다.

모델용 `spoken_usage_selection`과 서버용 `spoken_usage_notes`를 분리합니다. 설명문과 출처를 모델이 새로 생성하지 않습니다. 선택 필드는 [공식 Structured Outputs 규칙](https://developers.openai.com/api/docs/guides/structured-outputs)에 따라 필수 nullable로 전송하며 후보가 없으면 null만 허용합니다. 형식 검사와 후보 일치는 의미 관련성을 보증하지 않습니다.

회화에서 적절한 짧은 답변에 대소문자·마침표·please 부족만으로 교정을 붙이지 않도록 지시합니다. 음성 발화의 대소문자·종결 부호만 바꾼 제안은 계약 검증에서 거부하며, 축약 응답에서는 복원 단계가 그 제안만 뺍니다. 한영 혼합, 모호한 지시어, 메타 명령, 취소·부정의 의미 구분은 프롬프트와 별도 평가 사례로 점검하며 의미 정확성을 결정론적으로 보장하지는 않습니다.

`run_evaluation.py`는 실호출 전에 전체 로컬 자동 검사를 실행하고 실패하면 모델 호출을 차단합니다. 실호출에는 `--case` 하나가 필수이며 일괄·반복 호출은 차단합니다. 요청 시작 기록을 먼저 남기고 기존 결과를 덮어쓰지 않습니다. `execution_state=started`로 남은 호출은 응답을 확인하지 못한 것이며 과금 가능성을 별도 확인해야 합니다. 기본 실행은 입력 검증만 하며 실제 모델 평가를 통과했다는 뜻이 아닙니다.

실제 샘플 실행 결과는 기본적으로 `local_results/gms_<case>_<time>.json`에 저장합니다. `--output`으로 별도 경로를 지정할 수 있습니다. 요청/반환 모델, 응답 ID, 시작/종료 시각, 지연 시간, 입력·출력·캐시 토큰, 검증 결과를 포함합니다. 샘플용 도구는 의미 검토를 위해 가상 데이터의 모델 원문도 저장하며 운영 대화 로깅용으로 사용하지 않습니다.

토큰 수는 크레딧이 아닙니다. GMS 사용 내역의 같은 모델·호출 시각과 대조해 실제 크레딧을 기록합니다. 공동 계정의 전체 잔액 차이를 한 요청의 비용으로 계산하지 않습니다. `actual_gms_credits: null`은 확인 전을 뜻하며 무료 호출을 뜻하지 않습니다. 형식 검증 실패나 출력 길이 초과도 모델 생성 비용이 발생할 수 있습니다.

검증판의 `credit_estimate`는 이 프로젝트에서 GPT-5.4 호출 13건과 일치했던 관측식 `입력 × 0.05 + 출력 × 0.225`를 적용한 예상값입니다. 공식 요금표나 확정 차감액이 아니며 캐시 할인을 가정하지 않습니다. 모델 또는 토큰 수가 불명확하면 예상값도 `null`입니다.

출력 상한은 6,000토큰, 호출 타임아웃은 60초입니다. 상한만큼 매번 사용하는 것은 아닙니다. `gpt-5.4`는 `max_completion_tokens`와 원래 프로필의 `reasoning_effort=none`, `gpt-4o-mini`는 `max_tokens`를 사용합니다. 다른 모델의 지원 여부와 요청 옵션은 별도 확인해야 합니다.

## 공통 워커에서 호출

Spring의 종료·저장·재조회 처리와 가상 요청·응답 6건은 [피드백 백엔드 연동](../../docs/api/feedback-integration.md)에 있습니다. 배포 전 `python -X utf8 check_runtime.py`로 필수 프롬프트·회화 콘텐츠의 누락·손상을 검사할 수 있습니다. 키와 외부 모델은 사용하지 않으며, 종료 코드 0은 정적 자원 검사 통과를 뜻합니다. 실제 이미지·서버·DB 연동 검증을 대신하지 않습니다.

### Maple Town 플레이 검증

수집판과 다른 폴더에 둔 `prototype/S15P31A305-55-leejunghoon` 복사본을 지정해 실행합니다.

```powershell
python -X utf8 prototype_preview.py --prototype-root <별도_프로토타입_루트> --port 8011
```

`http://localhost:8011/?at=cafe&play=normal`에서 카페 대화 → 대화 끝내기를 누르면 이 모듈의 GPT-5.4 피드백을 표시하고 저장합니다. 학습 기록에서 다시 열 때 모델을 재호출하지 않습니다. NPC·판정·기억은 프로토타입의 기본 설정을 사용합니다.

먼저 `render_spoken_examples.py`를 실행하면 `http://localhost:8011/feedback-demo`에서 수작업 가상 응답 3개의 화면을 볼 수 있습니다. 문법 교정, 적절한 안부 답변, 뒤에서 수락 의도를 직접 확인한 경우를 재생하며 GMS를 호출하거나 플레이 기록에 저장하지 않습니다. 실제 모델 평가 결과가 아닙니다. 코드·프롬프트·패널을 수정하면 검증용 서버를 재시작해야 합니다.

검증판은 힌트·NPC 번역을 차단해 도움 사용 여부를 추정하지 않습니다. Groq 키가 없으면 음성 버튼을 끄고 타자 대화를 제공합니다. 음성도 확인하려면 본인 `GROQ_API_KEY`를 로컬 환경변수 또는 `.env.local`에 추가하고 검증 서버를 다시 실행합니다.

검증판의 사용자 줄은 `history.rude is True`일 때만 `text_status: blocked`로 변환해 평가에서 제외합니다. null·false·누락은 final을 유지하며, 원문을 생성 영어 번역으로 교체하지 않습니다. 모든 사용자 줄이 제외되면 모델을 부르지 않고 근거 부족을 반환하며 이전 대화의 호출·토큰 정보를 재사용하지 않습니다. Spring에서 같은 입력을 만드는 규칙과 NPC 지적·종료 지시값은 [NPC 무례 대응 연동](../../docs/api/npc-moderation-integration.md)을 참고합니다.

데이터는 지정한 별도 복사본에 저장됩니다. 이 검증판의 가상 테스트와 피드백 개선용 기록을 무례·STT 수집용 zip에 합치지 않습니다. 수집 동의는 대신 선택하지 않으며 기존 수집 zip을 자동으로 읽거나 GMS에 재전송하지 않습니다.

### 서버 호출 경계

```python
from feedback.gms import GmsClient, GmsSettings
from feedback.service import generate_feedback

client = GmsClient(GmsSettings(api_key=server_secret, model="gpt-5.4"))
response = generate_feedback(conversation_snapshot, client, ai_allowed=server_approved)
usage = client.last_call
```

`server_secret`, `conversation_snapshot`, `server_approved`는 호출 측에서 제공해야 합니다. 클라이언트가 보낸 사용자 ID·사용량 승인 여부를 그대로 신뢰하지 않습니다. 새 호출의 예산 예약, 중복 요청 방지, 작업 상태 및 DB 저장은 서버 책임입니다. 동기식 호출이므로 FastAPI의 이벤트 루프에서 직접 호출하려면 작업 스레드 등으로 분리해야 합니다.

현재 모듈은 HTTP 서버·Spring 엔드포인트·Redis 작업·SSE·Unity 화면을 구현하지 않습니다. 프로토타입 브랜치의 `/v1/conversation/end` 응답은 이 모듈의 v0.3 계약과 다르므로 변환과 저장 계약을 합의한 뒤 연결해야 합니다. 모델 분석 완료를 DB 저장·미션 완료·호감도 변경으로 취급하지 않습니다.

## 코드와 검증 자료

- `feedback/models.py`: 계약과 발화/출처 검증
- `feedback/prompt.py`, `prompts/feedback_system.ko.md`: 모델 입력과 평가 지시문
- `feedback/service.py`: 호출 승인 확인, 생성, 실패 변환, 결과 검증
- `feedback/gms.py`: GMS 실제 호출과 사용량 수집
- `tests/`: 외부 호출 없는 처리·통신 경계 테스트
- `run_evaluation.py`, `samples/robustness/`: 사례를 명시하는 수동 평가 도구와 합성 강건성 사례
- `feedback/costs.py`: 이전 GMS 사용 내역의 관측식에 따른 비용 추정
- `run_memory_regression.py`, `samples/memory_regression/`: 외부 호출 없는 사실별 기억 합성 자료와 고정 응답 검사기
- `review_memory_results.py`, [MEMORY_REVIEW.md](MEMORY_REVIEW.md): 이미 저장된 사실별 기억 응답의 계약·누락 의심·중복 점검과 수동 검토 안내
- `samples/inputs`, `samples/fixtures`, `samples/rubrics`: 가상 샘플, 수작업 응답, 기대·금지 판단
- `schemas/`: 입력·서비스 출력·모델 출력 JSON Schema
- `샘플대화_평가기준.md`, `결과미리보기.md`, `examples/`: 고정 응답 단계의 참고 자료
- `검증결과.md`, `GMS_연결후_검토표.md`: 기존 고정 응답 단계 기록. 최신 실호출 결과를 대신하지 않음

샘플은 가상 데이터입니다. 한 번씩의 소수 사례 검토는 운영 성능이나 전체 학습 효과 검증을 뜻하지 않습니다. NPC 기억의 생성·저장·조회 서비스는 이 패키지에서 구현하지 않습니다. 사실별 참조 계약 0.1.0을 사용하는 저장 전 변경 계획은 [MEMORY_POLICY.md](MEMORY_POLICY.md), 평가 기준은 [NPC 기억 품질 점검표](../../docs/ai-memory-quality.md)에 있습니다. 이 모듈은 기존 카드형 `/v1/memory`나 피드백 0.3.0 계약에 연결하지 않았습니다.

GMS의 OpenAI 호환 요청 형식은 [OpenAI Docs의 구조화 출력](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=chat)과 [GPT-5.4 모델 문서](https://developers.openai.com/api/docs/models/gpt-5.4)를 참고합니다. OpenAI 정가를 GMS 크레딧 차감량으로 간주하지 않습니다.

추천 표현은 생성 내부의 speaker_role(learner/npc/both)로 화자를 구분하고 학습자가 말할 수 있는 것만 반환합니다. 이 메타데이터는 공개 출력·저장 스키마에서 제거하므로 기존 0.3.0 계약은 유지합니다. 복습 준비·결과 화면을 위해 별도 모델 호출을 추가하지 않습니다.
