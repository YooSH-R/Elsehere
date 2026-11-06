# HotelScene 로비 디자인

**Maple House v1**은 메인스트리트 호텔의 로비다. 크림색 벽과 밝은 석재 바닥, 남색 패널, 황동 장식, 세이지색 소파를 사용한다. 입구에서 아치 장식의 프런트를 바라보며 접근하고, 양옆 대기 좌석과 수하물 공간은 중앙 통로 밖에 둔다.

## 열어 보기

- 배포용 공간: `Assets/Scenes/Production/HotelScene.unity`
- 외형 검수용 복사본: `Assets/Scenes/Sandbox/HotelInteriorSandbox.unity`
- 호텔 프리팹: `Assets/Prefabs/Hotel/MapleHouse/`
- 호텔 재질: `Assets/Art/Hotel/MapleHouse/`
- 카메라·직원 설정·색 보정: `Assets/Data/Hotel/`

Production에서 대화와 마을 왕복을 확인한다. Sandbox는 배포 Scene List에 추가하지 않는다.

## 크기와 배치

Unity 1 unit = 1m이다. 남쪽은 마을로 통하는 입구, 북쪽은 프런트다.

| 항목 | 기준 |
| --- | --- |
| 실내 바닥 / 천장 높이 | 16 × 14m / 4.6m |
| 플레이어·호텔리어 | 리틀 멍 v7 비율의 1.6m 대체 모델 |
| 프런트 | 폭 5.85m, 깊이 1.22m, 상판 높이 0.77m, 중심 z=4.6m |
| 직원 위치 | (0, 0.05, 5.7), 입구 방향 |
| 대화 접근 위치 | (0, 0.05, 3.45) 부근 |
| 대기 좌석 | 좌우 각 1조. 소파 폭 2.65m, 테이블·러그·탁상등 포함 |
| 소파 앞 통로 | 소파와 커피 테이블의 충돌체 사이 약 1m. 플레이어가 양옆으로 통과할 수 있는 폭 |
| 출입문 | (0, 0, -7), `TownDoor` → `TownScene/HotelDoor` |
| 3인칭 시작 거리 / 조절 범위 | 3.8m / 1.5~5.2m |
| 시야각 / 아래 기울기 | 65° / 15° |
| 카메라 기준 높이 / 대화 눈높이 | 1.5m / 1.074m |

프런트는 큰 머리와 낮은 얼굴 중심을 가진 캐릭터의 입을 가리지 않도록 낮췄다. 데스크 위 벨과 방명록은 양 끝에 둔다. 수하물 카트는 왼쪽 뒤편에 배치한다. 엘리베이터 두 개는 닫힌 장식 외형이며 객실 이동 기능은 없다.

## 프리팹 편집과 재사용

| 프리팹 | 구성 |
| --- | --- |
| `LobbyShell`, `LobbyWindow` | 바닥·벽·천장과 커튼 창 |
| `LobbyDecoration` | 프런트·대기 좌석·수하물·식물을 배치한 중첩 프리팹 |
| `ReceptionDesk`, `ReceptionBackdrop` | 낮은 데스크와 아치·호텔 이름 패널 |
| `KeyCabinet`, `LobbyClock` | 키 보관함, 장식 시계 |
| `LoungeSet` | 집의 소파·테이블·탁상등을 재사용한 좌석 조합 |
| `Suitcase`, `LuggageCart` | 복제해서 사용할 수 있는 수하물 소품 |
| `LiftFacade` | 닫힌 엘리베이터 문 장식과 Collider |
| `LobbyChandelier`, `GlobeSconce` | 조명 외형과 실제 Point Light |

가구 원본은 Prefab Mode에서 편집한다. 장소마다 다른 색·구성을 만들 때는 Variant를 사용하고, 집에서도 사용하는 원본 가구·공통 재질을 직접 바꾸지 않는다. 식물은 기존 `TinyTreats/HousePlants/Monstera` 프리팹을 재사용하며 출처·라이선스는 `Assets/ThirdParty/CREDITS.md`를 따른다.

**Tools → A305 → Hotel → Apply Maple House Design**은 최초 적용용이다. `Environment/MapleHouse`가 이미 있으면 배치를 덮어쓰지 않는다. 프리팹·설정도 없을 때만 생성한다. 확장할 때는 중첩 프리팹을 복제하고 중앙 접근로와 직원 뒤 공간을 확보한다.

**Tools → A305 → Hotel → Widen Lounge Aisles**은 기존 `LoungeSet`의 커피 테이블을 앞으로 옮겨 소파 앞 통로를 확보하고, 호텔 씬의 NPC 이동 경로를 다시 굽는다. Production과 Sandbox에 적용하며, 이미 더 넓게 둔 테이블은 당기지 않는다. 집에서 재사용하는 가구 원본의 크기는 유지한다.

## 기능 연결

`HOTEL_STAFF`는 기존 `Passerby` 프리팹을 사용하는 고정 직원이다. `HotelStaffSettings`의 감지 거리는 3.5m, 대화 제안 거리는 2.5m, 걷는 속도는 0, 재제안 대기는 20초다. 튜토리얼의 대화 허용 조건과 기존 대화 시작·종료 흐름을 따른다.

직원 외형은 Tripo로 만들어 외관 승인 뒤 최종 등록한 호텔리어 `Assets/Prefabs/Characters/Tripo/LittleMung_HotelStaff_male.prefab`이다(AI 인격 이름 Jake). 마스터 골격과 공용 이동 애니메이션을 쓰며, 직원 루트의 `CharacterAnimationDriver`가 이 Animator를 움직인다. **Tools → A305 → Hotel → Connect Hotelier Character**는 `HotelScene`·`HotelInteriorSandbox`의 직원에 호텔리어를 한 번만 붙이고 이전 임시 외형을 지운다. 직원 위치·대화·시선 대상·캡슐은 바꾸지 않는다.

교대를 나눠 맡는 다른 직원(Owen·Clara·Walter·Diego)은 **Tools → A305 → Npc → Build Cast**가 데스크 양옆과 뒤쪽 양 끝에 둔다. Owen·Diego는 Jake와 같은 호텔리어 대화 외형에 배색만 바꾸고, Walter·Clara는 중년 외형을 쓴다. 근무 중인 직원 중 데스크에 가장 가까운 사람이 응대한다([Npc 모듈](../../frontend/UnityClient/Assets/Scripts/Npc/README.md#일터와-응대)).

`MockHotelLines`는 로컬 검수용 인사말이며 서버의 NPC 이름·성격이나 호텔 체크인 미션을 구현한 데이터가 아니다. 예약·결제·객실 이동·수하물 조작은 별도 기능이다.

양옆 대기 좌석의 소파에는 카페 의자와 같은 조작으로 앉는다. 소파마다 쿠션 두 개의 가운데에 자리를 두고, 가까운 자리 하나에 [잠깐 앉기]를 띄운다. 호텔 소파에서 쉰 것은 첫날 미션의 휴식으로 세지 않는다. 연결은 **Tools → A305 → Mission → Connect Bench And Sofa Sitting**이 하며 기준은 [Mission 모듈](../../frontend/UnityClient/Assets/Scripts/Mission/README.md#의자벤치소파-앉기)을 따른다. 호텔 가구에는 서버의 단어 키를 새로 부여하지 않았으므로 호버 툴팁이 나오지 않는다. 기존 출입문과 도착 지점은 유지한다.

## 조명과 화면 검수

| 대상 | 설정 |
| --- | --- |
| 방향광 | intensity 1.4, 부드러운 그림자 strength 0.6 |
| `Lighting/LobbyBounce` | intensity 2.1, range 15m |
| 샹들리에 2개 | 각 Point Light intensity 2.1, range 7m |
| 벽등 2개 | 각 intensity 1.2, range 4.2m |
| 좌석 탁상등 2개 | 각 intensity 1.8, range 3.6m |
| `HotelColor.asset` | 노출 +0.2, 대비 +6, 채도 +6, Bloom 0.12 |

천장은 방향광 그림자를 만들지 않는 스타일 조명이다. 창은 색면으로 표현하며 실제 마을 조망은 연결하지 않는다. 전역 조명 베이크는 사용하지 않는다. 위 값은 낮 기준이며, `Lighting/InteriorDaylight`가 게임 시각에 맞춰 밤에는 불을 켠 방처럼 방향광을 조금 약한 실내등 색으로 바꾸고 보조광(`LobbyBounce`)·환경광을 조금 줄이며 샹들리에·벽등·스탠드를 밝히고 창 유리(`WindowDaylight`)를 밤하늘 색으로 칠한다([Weather 모듈의 밤낮](../../frontend/UnityClient/Assets/Scripts/Weather/README.md#밤낮)).

**Verify Maple House Design**은 실제 Production Play에서 주요 이동 경로, 직원 대화와 시점 복원, 장식에 단어가 잘못 표시되지 않는지, 문 접촉을 통한 마을 왕복을 검수한다. 직원 대화는 기존 튜토리얼 건너뛰기로 대화 허용 조건을 충족한 상태에서 확인한다.

결과와 이미지는 `artifacts/unity/hotel-interior/`에 로컬로 보관하고 Git에 넣지 않는다. `04-cutaway-layout.png`만 검수 중 천장과 두 벽을 숨긴 단면이며 저장된 실내는 닫혀 있다. 이미지는 실제 카메라 렌더이고 HUD 합성은 포함하지 않는다. 최종 캐릭터 리깅과 Web 빌드 성능은 별도 검증 대상이다.
