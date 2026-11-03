# HomeScene 실내 디자인

내 집은 이동과 사물 호버를 익히는 첫 튜토리얼 공간이다. **Cozy Cottage v1**은 메인스트리트의 따뜻한 색과 둥근 형태를 실내로 이어 간다. 크림색 벽, 밝은 원목, 세이지색 가구, 테라코타색 소품을 사용한다.

## 열어 보기

- 실제 이용 공간: `Assets/Scenes/Production/HomeScene.unity`
- 외형과 가구 배치를 확인할 복사본: `Assets/Scenes/Sandbox/HomeInteriorSandbox.unity`
- 가구·마감 프리팹: `Assets/Prefabs/Home/CozyCottage/`
- 재질: `Assets/Art/Home/CozyCottage/`
- 카메라·색 보정: `Assets/Data/Home/`

Sandbox의 씬 이름은 튜토리얼 시작 조건인 `HomeScene`과 다르다. 튜토리얼과 집↔마을 출입은 Production 씬에서 확인한다. Sandbox는 배포 Scene List에 추가하지 않는다.

## 배치와 크기

단위는 Unity 1 unit = 1m이다. 남쪽이 현관, 북쪽이 TV와 주방이다. 중앙 통로에서 거실·주방·침대 구역으로 접근한다. 별도 방문으로 나누지 않아 첫 화면에서 학습할 가구를 찾기 쉽다.

| 항목 | 기준 |
| --- | --- |
| 바닥 / 천장 높이 | 10 × 10m / 3.6m |
| 플레이어 | 리틀 멍 v7 비율의 1.6m 대체 외형 |
| 소파 | 폭 2.7m, 높이 약 1.35m, 깊이 1.1m |
| 침대 | 폭 2.04m, 길이 2.78m, 머리판 약 1.47m |
| 주방 조리대 | 폭 2.9m, 높이 1.04m, 깊이 0.85m |
| 냉장고 / 책장 | 높이 2m / 2.1m |
| 시작 위치 | (0, 0.05, -1.5) |
| 현관 | `TownDoor`, (0, 0, -5) → `TownScene/HomeDoor` |
| 3인칭 시작 거리 / 조절 범위 | 3.4m / 1.5~4.2m |
| 시야각 / 시작 기울기 | 65° / 아래로 16° |
| 3인칭 기준 높이 / 1인칭 눈높이 | 1.5m / 1.074m |

주방 옆 식탁과 의자, 식물, 러그는 장식이다. 수면·요리·앉기·가구 배치 편집 기능은 포함하지 않는다.

## 프리팹 편집

| 프리팹 | 용도 |
| --- | --- |
| `RoomShell` | 바닥, 네 면 벽, 천장. 벽마다 창과 마감을 묶는다. |
| `RoomDecoration` | 식탁·식물·러그·선반 등 중첩 프리팹 배치 |
| `SageSofa`, `SageBed`, `OakBookcase` | 거실·침실 가구 |
| `Television`, `RetroRefrigerator`, `AirConditioner` | 사물 호버용 가전 외형 |
| `KitchenCounter`, `MediaConsole`, `CoffeeTable`, `SideTable` | 재사용 수납·테이블 |
| `CurtainedWindow`, `LivingRug` | 창과 패브릭 마감 |
| `TableLamp`, `CeilingPendant` | 조명 외형. `TableLamp`에는 Point Light가 포함된다. |

가구를 옮길 때는 `Environment/HomeRoom` 아래의 기존 사물 루트를 옮긴다. 루트의 `HoverableObject`와 `UnityKey`를 유지하고 자식 외형 프리팹을 교체한다. 기존 도형 Renderer·Collider는 비활성 상태이며 새 외형의 Collider가 이동과 호버를 처리한다. 문은 `SpaceDoorway`와 `ArrivalPoint`를 유지한다.

`Environment/CozyInterior`에는 마감과 장식을 둔다. 재질이나 가구 원본을 변경하면 이 프리팹을 쓰는 장소에도 반영되므로, 특정 장소만 달라야 하면 Variant를 만든다. 곡면 메시 생성에는 마을의 공통 `MainstreetArtKit`을 사용하며 생성 메시는 `Art/World/MainstreetArt/Meshes`에서 공유한다.

## 조명과 색

낮의 방향광, 실내 보조 Point Light, 두 개의 스탠드 Point Light를 사용한다. 창 위치에 벽 개구부를 두고, 천장은 방향광 그림자를 만들지 않도록 설정한 스타일 조명이다. 물리 기반 실내 채광이나 베이크한 전역 조명은 아니다.

이 값은 낮 기준이다. `Lighting/InteriorDaylight`가 게임 시각에 맞춰 밤에는 불을 켠 방처럼 방향광을 조금 약한 실내등 색으로 바꾸고 보조광(`RoomBounce`)·환경광을 조금 낮추며 스탠드는 밝히고, 창 유리(`WindowDaylight`)를 밤하늘 색으로 칠한다. 값과 방식은 [Weather 모듈의 밤낮](../../frontend/UnityClient/Assets/Scripts/Weather/README.md#밤낮)을 따른다.

| 대상 | 설정 위치 |
| --- | --- |
| 햇빛 | `Lighting/Directional Light` |
| 실내 보조광 | `Lighting/RoomBounce` — intensity 2.2, range 10m |
| 스탠드 | `TableLamp/WarmLight` — intensity 1.8, range 3.6m |
| 노출·대비·채도·Bloom | `Data/Home/HomeColor.asset` |
| 카메라 거리와 높이 | `Data/Home/HomeCameraSettings.asset` |

## 제작과 확인 메뉴

- **Tools → A305 → Home → Apply Cozy Cottage Design**: 기존 HomeScene에 최초 외형을 적용하고 Sandbox 복사본을 만든다. 이미 `CozyInterior`가 있으면 배치를 덮어쓰지 않는다. 프리팹과 카메라 데이터도 없을 때만 만든다.
- **Apply Soft Daylight**: 집의 조명과 색 보정을 위 기본값으로 다시 맞추고 Sandbox 복사본을 갱신한다. 조명을 직접 편집한 뒤에는 이 메뉴가 수치를 재설정한다는 점을 유의한다.
- **Verify Cozy Cottage Design**: Production 씬을 Play하여 사물 호버, 주요 이동 동선, 문 접촉을 통한 마을 왕복을 확인하고 `artifacts/unity/home-interior/`에 결과를 저장한다. Play 검수용 이동과 카메라 변경은 저장 씬에 반영하지 않는다.

## 실제 Unity 화면의 로컬 보관

검수 메뉴가 생성하는 카메라 렌더와 실행 기록은 `artifacts/unity/home-interior/`에 로컬로 보관하며 Git에 추가하지 않는다. `01-player-start.png`는 기본 플레이어 시점이고 HUD 합성은 포함하지 않는다. `04-cutaway-layout.png`는 배치를 보기 위해 검수 중에만 천장·남쪽 벽·동쪽 벽을 숨긴 단면 시점이다. 저장된 씬에는 네 벽과 천장이 있다.

## 재사용과 적용 범위

식탁·의자·머그잔과 식물은 이미 프로젝트에 포함된 Tiny Treats CC0 프리팹을 재사용한다. 원본 출처와 라이선스는 `Assets/ThirdParty/CREDITS.md`를 따른다. 신규 유료 에셋이나 Tripo 생성물은 사용하지 않는다.

캐릭터는 비율·시야 확인용 대체 모델이다. 최종 캐릭터 모델·리깅과 Web 빌드 FPS·메모리는 별도 검증 대상이다. 이 작업은 가구 배치와 시각 요소에 해당하므로 테스트 우선 적용 예외이며, 실제 Play와 기존 공간·플레이어·호버·튜토리얼 테스트로 연결 상태를 확인한다.
