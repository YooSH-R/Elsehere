# Elsehere 디자인 문서

캐릭터와 마을의 디자인 기준, 비교 시안 설명, 제작 프롬프트를 이 폴더에서 관리한다. 생성 이미지와 이미지 제작 스크립트는 저장소의 `artifacts/mascot/`, `artifacts/village/`, `artifacts/scenes/`에 보관한다.

현재 마을의 색감·재질 기준은 **브릭 메인스트리트 v3-B**, 최신 제작 문서는 **production-v2**다. 캐릭터 높이 1.6m, 1층 층고 4.0m, 2층 상점 총높이 7.4m, 문 유효 폭 2.0m·높이 2.2m의 확대 규격을 사용한다. 현재 캐릭터는 **리틀 멍 v7 — 둥근 눈동자 표현·기존 B 시선 복원**이다. `tripo-turnaround-v1` 정면·양쪽 측면·후면을 작업 기준으로 사용한다. 눈동자는 각 흰자 안에서 코 쪽에 조금 붙은 위치를 유지한다.

| 문서 | 용도 |
| --- | --- |
| [메인스트리트 씬 디자인 v1](scenes/mainstreet-v1/README.md) | 집 앞 출발 화면, 주요 건물과 첫 대화 공간의 배치 제안, 치수 평면도 |
| [B 선택과 층고 비교](village/brick-mainstreet-v3/height-comparison.md) | 최신 선택안, 이미지, 비교 프롬프트와 검증 시작값 |
| [B안 제작 규격 v2](village/production-v2/art-spec.md) | 최신 치수·4m 출입구 접합·재질·캐릭터 모듈·제작 예산 |
| [B안 에셋 목록 v2](village/production-v2/asset-list.md) | 캐릭터·건축·가구·소품의 규격, 선행 조건과 제작 우선순위 |
| [Unity 적용 순서](village/production-v2/unity-application-plan.md) | 크기 시험 → 공통 베이스 → 카페 한 구간 → MVP 조립 → Web 검증 |
| [리틀 멍 v7 캐릭터 기준](mascot/little-mung-v7/README.md) | 현재 얼굴·눈동자 위치·비율, Tripo용 4방향 이미지·프롬프트, NPC 확장 기준 |
| [Unity 외관·조명·무료 소품](../../unity/mainstreet-art.md) | 실제 제작한 공통 프리팹, 조명 조절, Tiny Treats 소품 사용 기준 |
| [Blender 카페 키트](../../unity/cafe-blender-kit.md) | 수정 가능한 원본과 1·2·3층 중첩 프리팹의 제작·사용 방법 |
| [마을 스타일 비교](village/town-comparison-v1.md) | 초기 세 가지 미국 소도시 방향의 비교 기록 |

캐릭터·마을·씬의 생성 프롬프트는 각각 `mascot/`, `village/`, `scenes/` 아래 해당 버전 폴더에 보관한다.

`village/production-v1/`은 이전 마을 제작 기록이다. 현재 제작에는 production-v2와 리틀 멍 v7 기준을 사용한다. 이미지에서 실제 모델 치수·리그 호환성·Unity 성능이 검증된 것은 아니다.
