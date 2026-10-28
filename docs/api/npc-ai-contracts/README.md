# NPC 설정·추론 계약 패키지 v2.0

2026-10-23 · 전달용 제안안. 실제 구현이나 팀 합의 완료를 뜻하지 않습니다.

## 먼저 읽을 문서

| 문서 | 내용 |
|---|---|
| [01. NPC 설정·관리 명세](01_npc_configuration_spec_v2.md) | 성격·가치관·동기·사회적 반응·말투·경험·습관, 등록·저장·버전 정책 |
| [02. NPC 추론 API 명세](02_npc_inference_api_spec_v2.md) | 첫 대사/후속 응답 입력·출력, 오류, TTS 연결 |

설정 문서는 **데이터·관리 계약**입니다. MVP는 JSON 초기 등록을 유지하며 설정용 HTTP API를 추가하지 않았습니다. 추론 HTTP API는 `POST /internal/ai/npc/respond` 한 개입니다.

## 개발 파일

| 파일 | 용도 |
|---|---|
| [npc_shared_schemas_v2.json](npc_shared_schemas_v2.json) | 인격과 설정 구조의 단일 정의 |
| [npc_inference_openapi_v2.yaml](npc_inference_openapi_v2.yaml) | 공통 스키마를 참조하는 OpenAPI 3.1.1 추론 계약 |
| [npc_life_rules_schemas.json](npc_life_rules_schemas.json) | NPC 생활·관계 규칙 데이터 스키마 제안(`WorldRules`). 설계는 [NPC 생활·관계 설계](../../npc-life-design.md) |
| [npc_configuration_examples_v2.json](npc_configuration_examples_v2.json) | 카페 점원·호텔리어·행인 설정 예시. 비활성·목소리 미연결 |
| [npc_inference_examples_v2.json](npc_inference_examples_v2.json) | 완전한 첫 대사·후속·마무리 제안 요청/응답 예시 |
| [validate_contracts.py](validate_contracts.py) | 로컬 계약 검사 및 일부 애플리케이션 검증의 참고 구현 |
| [validation_report.md](validation_report.md) | 검증 범위와 결과 요약 |
| [validation_results.json](validation_results.json) | 상세 검사 결과 |

파일은 같은 디렉터리에 보관하세요. OpenAPI만 복사하면 `NpcProfile`의 외부 참조가 끊깁니다. 공통 스키마는 `$defs` 라이브러리이므로 검증할 대상 타입의 포인터를 지정해야 합니다.

```text
npc_shared_schemas_v2.json#/$defs/NpcProfile
npc_shared_schemas_v2.json#/$defs/NpcConfiguration
npc_shared_schemas_v2.json#/$defs/NpcConfigurationList
```

## 중요한 경계

설정 저장소 → `npcProfile` → 추론 → `replyText` → 백엔드 → TTS 순서입니다. `voiceId`는 백엔드/TTS에서 사용하며 추론 입력이 아닙니다. AI가 인격을 자동 생성하거나 DB를 직접 수정하는 기능은 아직 포함하지 않습니다.

프로필의 선택적 세부 설정을 모두 채울 필요는 없습니다. Big Five 형태의 수치도 선택 사항이며 예시에서는 사용하지 않았습니다. 예시의 이름·배경·성격은 확정 캐릭터 설정이 아닙니다.

v2는 `personality.traits`와 `background` 등이 바뀐 **하위 호환되지 않는 프로필 계약**입니다. v1.2의 프로필 JSON을 그대로 보내지 마세요. 세부 마이그레이션은 설정 명세 §16을 참고하세요.

## 검증 재실행

Python 3.10 이상 기준의 로컬 검증 스크립트입니다.

```bash
python -m pip install -r requirements.txt
python validate_contracts.py
```

HTTP 서버, DB, 모델, TTS를 실행하지 않습니다. 실제 NPC 인격 품질·실제 음성 재생·지연 시간 테스트는 별도입니다.
