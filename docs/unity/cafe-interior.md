# CafeScene 실내 디자인

**Corner Cafe v1**은 마을에서 실제로 이용하는 카페다. 크림색 벽과 타일, 밝은 원목, 짙은 초록색 주문대, 코럴색 벤치를 사용한다. 입구에서 직원과 메뉴가 보이고, 좌석은 양옆에 배치해 가운데 주문 동선을 비운다.

## 열어 보기

- 배포용 공간: `Assets/Scenes/Production/CafeScene.unity`
- 외형 검수용 복사본: `Assets/Scenes/Sandbox/CafeInteriorSandbox.unity`
- 가구·실내 마감: `Assets/Prefabs/Cafe/CornerCafe/`
- 카페 재질: `Assets/Art/Cafe/CornerCafe/`
- 카메라·색 보정: `Assets/Data/Cafe/`

대화·미션·마을 왕복은 Production 씬에서 확인한다. Sandbox는 외형 확인용이며 배포 Scene List에 추가하지 않는다.

## 크기와 배치

Unity 1 unit = 1m이다. 남쪽이 입구, 북쪽이 주문대와 커피 준비대다.

| 항목 | 기준 |
| --- | --- |
| 바닥 / 천장 높이 | 14 × 12m / 4.2m |
| 플레이어·직원 | 리틀 멍 공용 마스터 골격과 배율 1.6. 직원은 Tripo로 만든 바리스타 캐릭터 |
| 주문대 | 폭 7.3m, 깊이 1.2m, 상판 높이 약 0.74m. 대화 시점에서 직원의 입이 보이도록 낮춤 |
| 커피 준비대 | 폭 8.6m, 깊이 0.8m, 상판 높이 0.94m |
| 직원 위치 | (0, 0.05, 4.72), 입구 방향 |
| 주문 접근 위치 | (0, 0.05, 2.65) 부근 |
| 왼쪽 벤치 | 2조, 벤치 폭 2.6m·좌면 높이 0.57m |
| 오른쪽 원형 테이블 | 2조, 테이블 높이 0.65m |
| 출입문 | (0, 0, -6), `TownDoor` → `TownScene/CafeDoor` |
| 3인칭 시작 거리 / 조절 범위 | 3.8m / 1.5~5.2m |
| 시야각 / 아래 기울기 | 65° / 15° |
| 카메라 기준 높이 / 대화 눈높이 | 1.5m / 1.074m |

카운터 왼쪽은 빵 진열, 가운데는 직원과 대화하는 위치, 오른쪽은 계산대·픽업 소품이다. 커피 준비대와 주문대 사이에는 직원 공간을 둔다. 의자에 앉기, 빵 선택, 결제 기능을 새로 구현한 것은 아니다.

## 프리팹과 기존 기능

| 프리팹 | 용도 |
| --- | --- |
| `CafeShell` | 타일 바닥, 네 벽, 창, 천장과 보 |
| `CafeDecoration` | 가구와 소품을 배치한 중첩 프리팹 |
| `ServiceCounter`, `BackBar` | 주문대와 커피 준비대 |
| `MenuBoard` | 카운터 위 메뉴판 |
| `BoothSet`, `RoundTableSet` | 복제해서 늘릴 수 있는 좌석 조합 |
| `PastryDisplay` | 진열장과 빵 조합 |
| `CafePendant` | 조명 외형과 Point Light |
| `Npc/Barista` | 직원(`CAFE_STAFF`)에 붙이는 바리스타 캐릭터. 마스터 골격에 연결한 `Female` Variant |

가구 원본은 Prefab Mode에서 편집한다. 장소마다 색·구성이 달라야 하면 Variant를 만든다. 다른 공간에서도 사용하는 Tiny Treats 원본과 공통 재질을 직접 변경하지 않는다. 둥근 메시와 원목·크림 재질은 마을·집 제작 키트를 재사용한다.

기존 `HoverableObject` 루트와 `counter`, `menu` 키를 유지한다. 메뉴판과 주문대의 새 Collider로 호버를 판정한다. 컵·빵·식물 등 추가 소품은 장식이며 새 학습 단어로 연결하지 않았다. 기존 `CAFE_STAFF`의 대화·미션 연결과 감지 거리는 유지한다. 문과 `ArrivalPoint`도 기존 연결을 유지한다.

**Tools → A305 → Cafe → Connect Barista Character**는 `CafeScene`과 `CafeInteriorSandbox`의 직원에 바리스타 캐릭터를 연결한다. 이전 도형 외형이 있으면 지우고, 직원 위치·대화·미션 연결·시선 대상·충돌 캡슐은 유지한다. 바리스타는 플레이어와 같은 이동 Animator와 접지·팔 관통 보정을 쓰며, 원본 FBX·메시·머티리얼·텍스처는 `Art/Npc/Barista/`에 둔다.

## 조명과 편집

방향광 1개, 실내 보조광 1개, 펜던트 Point Light 4개를 사용한다. 천장·창 유리는 방향광 그림자를 만들지 않는 스타일 조명이며, 베이크한 전역 조명은 아니다. 창은 불투명한 청록색 유리 표현이며 실제 마을 조망은 연결하지 않았다. 이 값은 낮 기준이며, `Lighting/InteriorDaylight`가 게임 시각에 맞춰 밤에는 불을 켠 방처럼 방향광을 조금 약한 실내등 색으로 바꾸고 보조광(`CafeBounce`)·환경광을 조금 줄이며 펜던트를 밝히고 창 유리(`DaylightGlass`)를 밤하늘 색으로 칠한다([Weather 모듈의 밤낮](../../frontend/UnityClient/Assets/Scripts/Weather/README.md#밤낮)).

| 대상 | 설정 |
| --- | --- |
| 햇빛 | intensity 1.5, 부드러운 그림자 strength 0.6 |
| 실내 보조광 | `Lighting/CafeBounce`, intensity 2.1, range 13m |
| 펜던트 | `CafePendant/WarmLight`, intensity 1.4, range 4.8m, 추가 그림자 없음 |
| 색 보정 | `Data/Cafe/CafeColor.asset`, 노출 +0.2, 대비 +6, 채도 +8, Bloom 0.1 |
| 카메라 | `Data/Cafe/CafeCameraSettings.asset` |

**Tools → A305 → Cafe → Apply Corner Cafe Design**은 최초 외형 적용용이다. 이미 `Environment/CornerCafe`가 있으면 저장된 배치를 덮어쓰지 않는다. 프리팹·재질·카메라 데이터도 없을 때만 만든다.

**Verify Corner Cafe Design**은 Production 씬을 Play하여 이동, 카운터·메뉴 호버, 직원 대화 시작·종료, 문 접촉을 통한 마을 왕복을 확인한다. 결과와 카메라 렌더는 `artifacts/unity/cafe-interior/`에 로컬로 저장하며 Git에 넣지 않는다. 검수용 이동·시점 변경은 씬에 저장하지 않는다. `04-cutaway-layout.png`는 검수 중에만 천장과 두 벽을 숨긴 단면이다. 다른 이미지도 카메라 렌더이며 HUD 합성은 포함하지 않는다.

## 에셋 출처와 검증 범위

프로젝트에 이미 포함된 Tiny Treats Bakery Interior·Baked Goods·House Plants의 무료 CC0 모델을 재사용한다. 출처와 동봉 라이선스는 `Assets/ThirdParty/CREDITS.md`를 따른다. 신규 유료 에셋이나 Tripo 크레딧은 사용하지 않는다.

씬·프리팹·카메라·조명 변경은 테스트 우선 적용 예외로, 실제 Play 검수와 기존 기능 회귀 테스트를 수행한다. 캐릭터는 비율·시야 확인용 대체 모델이며 최종 리깅, Web 빌드 FPS·메모리, 실제 주문 API·결제는 별도 검증 대상이다.
