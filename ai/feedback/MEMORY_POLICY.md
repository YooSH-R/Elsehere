# 사실별 NPC 기억 변경 계획

`feedback/memory_policy.py`는 서버가 받은 기억 분석 후보를 검사하고, 추가·교체·철회 후의 활성 값과 변경 목록을 반환한다. LLM, HTTP, DB, 환경변수, 키 파일을 사용하지 않는다. 원본 요청과 현재 기억도 수정하지 않는다.

이 모듈은 S15P31A305-135의 갱신·중복·충돌 처리 부분이다. 저장·조회 API와 기억 생성기는 별도로 연결해야 한다. 품질 기준은 [NPC 기억 품질 점검표](../../docs/ai-memory-quality.md)에 있다.

## 적용 경계

| 구분 | 현재 의미 |
| --- | --- |
| 사실별 참조 계약 `0.1.0` | `feedback/contract_v01.py`의 요청·분석 응답. 이 모듈의 입력 |
| 기존 `/v1/memory` | 전체 기억 카드 방식. 이 모듈과 연결하지 않았으며 계약도 변경하지 않음 |
| 학습 피드백 `0.3.0` | 별도 피드백 계약. 기억 변경 계획의 버전과 무관 |
| `ready` | 검사를 통과한 변경 계획. DB 저장 완료가 아님 |

## 호출 방법

`ai/feedback/`가 Python 실행 경로에 있을 때 다음과 같이 가져온다.

```python
from feedback.memory_policy import MemoryPolicy, MemoryPolicyError, plan_memory_update

policy = MemoryPolicy(
    allowed_keys={
        "profile.display_name": "in_game_profile",
        "preference.favorite_drink": "in_game_preference",
    },
    max_changes=5,
    max_active_facts=20,
    max_content_chars=180,
)

try:
    plan = plan_memory_update(
        analysis_request,
        analysis_response,
        current_scope=server_scope,
        current_snapshot=server_memory_snapshot,
        current_revision=server_conversation_revision,
        policy=policy,
    )
except MemoryPolicyError as error:
    error_code = error.code
    # 저장하지 않고 오류를 호출 측에서 처리한다.
```

변수는 호출 측에서 준비해야 한다. `analysis_request`와 `analysis_response`는 `contract_v01.validate_pair("memory", ...)`로 검증할 수 있는 JSON 객체다. `current_scope`는 `{user_id, npc_id, conversation_id}`, `current_snapshot`은 `{version, facts}`이며 각 사실은 `{memory_id, fact_key, kind, content}`다. `current_revision`은 현재 확정 대화의 정수 버전이다.

예시 키 두 개와 상한 5/20/180은 개발용 정책 시작값이다. 운영 허용 키·보존 범위·상한을 확정한 것으로 취급하지 않는다. 호출 측이 합의된 정책을 주입한다. 새 장소를 지원할 때 엔진에 카페·숙소 분기를 추가하지 않고 허용 키와 분석 규칙을 정한다. 동일 사실을 장소마다 다른 키로 중복 생성하지 않도록 키 의미를 함께 관리한다.

## 갱신 규칙

1. 요청과 분석 응답의 형식·범위·버전·근거를 기존 참조 계약으로 검증한다. 변경 근거는 현재 대화의 `final` 사용자 발화와 실제 포함된 인용이어야 한다. NPC, 차단 발화, 미확정 전사, 수정 전 STT 원문은 사실 변경 근거로 받지 않는다.
2. 서버의 현재 사용자·NPC·대화 범위와 비교한다. 현재 대화 리비전이나 기억 버전이 다르면 `stale`로 반환하고 변경하지 않는다. 버전이 같아도 사실 ID·키·내용이 다르면 오류다. 사실 목록의 순서 차이는 허용한다.
3. 완료된 분석만 처리한다. 실패·한도 부족·근거 부족·처리 중인 응답은 기존 기억을 유지한다.
4. 허용 키·종류와 상한을 검사한다. `add`는 없는 키만, `replace`와 `retract`는 스냅샷에 있는 동일 ID·키만 대상으로 삼는다. 한 묶음에서 같은 키를 두 번 변경할 수 없다.
5. `replace`가 기존 값과 동일하면 변경에서 제외한다. 동일성은 NFC 유니코드 정규화와 연속 공백 정리까지만 비교한다. 대소문자·동의어·의미를 임의로 합치지 않는다.
6. 교체하면 이전 활성 값을 새 값으로 대체하고, 철회하면 활성 값에서 제외한다. 다른 키의 기억은 유지한다. 한 변경이라도 유효하지 않으면 계획 전체를 거부한다.
7. 실제 변경이 있으면 다음 기억 버전을 한 번 증가시킨다. 반복·빈 변경·실패·오래된 결과에서는 버전을 증가시키지 않는다.

인용이 실제 발화에 포함된다는 검사는 그 인용이 사실 내용을 뒷받침한다는 의미 검증이 아니다. 예를 들어 “커피를 주문했다”에서 “커피를 가장 좋아한다”를 만든 오류는 이 모듈만으로 막을 수 없다. 같은 키의 활성 값 중복은 막지만, 다른 키·의역 간 의미 중복과 모순은 분석기와 별도 평가 대상이다.

## 반환과 오류

| `status` | 처리 |
| --- | --- |
| `ready` | 유효한 변경 있음. 저장 트랜잭션 대상 후보 |
| `no_change` | 완료된 분석이나 실질적인 변경 없음 |
| `stale` | 현재 대화 또는 기억 버전과 다름. 자동 재생성하지 않음 |
| `skipped` | 분석이 완료 상태가 아님. `source_status`에서 원인 확인 |

공통 반환 필드:

| 필드 | 의미 |
| --- | --- |
| `source_status` | 입력 분석의 상태 |
| `request_id`, `scope` | 분석 요청 식별자와 범위 |
| `conversation_revision`, `analysis_config_version` | 분석 당시 대화·설정 버전 |
| `base_memory_version` | 분석 요청이 읽었던 기억 버전 |
| `current_memory_version` | 호출 측이 넘긴 현재 기억 버전 |
| `next_memory_version` | 계획 적용 후 기대 버전 |
| `changes` | 근거와 대상 ID를 보존한 유효 `add/replace/retract` 목록 |
| `active_values` | 적용 후 예상 `{fact_key: {kind, content}}`. DB 스냅샷 형식이 아니며 ID를 새로 만들지 않음 |
| `summary` | 완료·최신 분석의 근거 있는 요약 또는 `null`. 사실 변경이 없어도 존재할 수 있음 |

`stale`·`skipped`의 `changes`는 빈 배열, `summary`는 `null`이며 활성 값과 버전은 현재 스냅샷을 유지한다.

| `MemoryPolicyError.code` | 원인 |
| --- | --- |
| `INVALID_POLICY` | 허용 키·종류 또는 양의 정수 상한이 잘못됨 |
| `INVALID_CONTRACT` | 요청·응답·현재 스냅샷 형식, 근거, 대상 참조 또는 응답 범위가 잘못됨 |
| `INVALID_REVISION` | 서버의 현재 대화 리비전이 음수이거나 정수가 아님 |
| `SCOPE_MISMATCH` | 요청 범위와 서버 범위가 다름 |
| `SNAPSHOT_MISMATCH` | 같은 버전인데 현재 사실과 요청 당시 사실이 다름 |
| `KEY_OR_KIND_NOT_ALLOWED` | 정책에 없는 키 또는 키에 맞지 않는 종류 |
| `LIMIT_EXCEEDED` | 변경 개수·내용 길이·변경 후 활성 사실 개수 상한 초과 |

예외 원인에는 검증 입력이 포함될 수 있으므로 운영 로그에 예외 전체나 대화 원문을 무조건 남기지 않는다.

## 백엔드에서 이어 붙일 부분

- 인증된 사용자와 대상 NPC·대화를 확인하고 현재 대화 리비전·기억 스냅샷을 서버에서 조회한다. 클라이언트나 모델이 주장한 값을 현재 상태로 신뢰하지 않는다.
- 분석 당시 전체 요청 스냅샷을 보존해 분석 응답과 함께 전달한다. 현재 설정과 달라진 분석을 수용할지 여부는 서버가 확인한다. 정책·프롬프트·모델 조합을 `analysis_config_version`과 함께 관리한다.
- `ready` 계획을 저장할 때 트랜잭션 안에서 사용자·NPC 기억 버전과 대화 리비전을 다시 비교한다. 다른 요청이 이미 반영됐으면 덮어쓰지 않는다. 이 함수 호출만으로 동시 저장 경쟁을 해결했다고 보지 않는다.
- 신규 사실 ID 발급, 과거 값·철회 이력 보관, 근거 저장, 버전 갱신을 같은 트랜잭션으로 처리한다. `retract`는 활성 기억에서 제외한다는 뜻이며 물리 삭제·보존 기한은 별도 정책이다.
- 요청 ID·범위·대화 리비전·분석 설정 버전으로 처리 이력을 관리한다. 변경 없는 응답이나 요약 저장도 요청 단위 중복 처리가 되지 않도록 한다. 버전 비교는 변경 요청의 재반영 방어이지 전체 멱등 저장소가 아니다.
- 저장 성공 뒤에만 캐시를 갱신한다. 저장·조회가 연결되면 별도로 API 왕복 테스트를 수행한다.

## 외부 호출 없는 실행

기존 개발 의존성이 준비된 환경에서 `ai/feedback/` 기준으로 실행한다.

```powershell
python -m pytest tests/test_memory_policy.py -q

# 여러 대화의 추가 → 교체 → 반복 → 철회 → 과거 결과 재도착
python -m pytest tests/test_memory_policy.py -k M13 -v
```

테스트 입력과 분석 응답은 실제 플레이에서 수집한 자료가 아닌 합성 값이다. 테스트 안의 저장소 대역은 다음 세션용 입력을 만들 뿐 실제 DB 저장 성공을 검증하지 않는다. 테스트는 네트워크 연결을 금지하며 GMS를 호출하지 않는다.

독립 입력·기대값 JSON과 고정 응답 검사기는 [NPC 기억 회귀 데이터셋](samples/memory_regression/README.md)을 참고한다. 코드 회귀 결과와 실제 모델의 의미 품질을 구분한다.
