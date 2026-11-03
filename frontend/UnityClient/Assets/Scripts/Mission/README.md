# Mission 모듈

튜토리얼 뒤에는 **첫날의 작은 소동** 연계 미션을 진행합니다. 왼쪽 위에 퀘스트 카드를 두고, 카드를 누르면 마을 지도와 전체 7단계·목표·소지품을 담은 수첩을 카드 자리에 펼칩니다. 오른쪽 위 메뉴 버튼 왼쪽에는 게임 시계 칩(`GameClockText`: "1일차 · 오후 1:40")과 코인 칩(미션 잔액)을 둡니다. 지도의 길과 건물 칸은 `TownMapBuilder`가 TownScene 배치에서 그리고, `TownMapView`는 내 위치와 목적지 표시만 바꿉니다. 서버가 `/v1/judge`의 의미 판정 제안을 현재 NPC·단계에 맞춰 적용하고 사용자별 진행을 저장합니다. 새로고침과 씬 이동 후에도 서버 상태를 읽어 복원합니다. 퀘스트가 여러 개가 되므로 화면 문구에는 '첫날'·'여정'을 쓰지 않습니다(수첩 제목 '퀘스트', 시작 버튼 '퀘스트 시작'). 이미 만든 프리팹과 튜토리얼 안내의 옛 문구는 **Tools → A305 → Mission → Apply Quest Wording**(`QuestWordingBuilder`)이 바꿉니다.

`FirstDayMissionApi`는 공용 API 클라이언트를 사용하고, `FirstDayMissionController`는 씬 진입·NPC 응답·대화 종료 후 상태를 읽습니다. `FirstDayMissionView`는 화면과 수첩만 담당합니다. 왼쪽 위 퀘스트 카드는 확정 시안(g1)과 같은 크기·여백·글꼴(시안 px × 1.2)로, 위에서부터 단계 칩(`StepChip`: 지금 단계 / 전체, 시작 전에는 숨김)과 장소 이름(시안 문구: "호텔에 도착", "비 오는 거리" 등), 서버의 짧은 요청 안내(예: "호텔 직원에게 요청하세요."), 상황 한 줄, 단계마다 한 칸인 진행 칸, 이번 단계 목표 줄을 보입니다. 목표는 한 줄에 하나씩 동그라미 체크와 이름이고, 끝낸 목표는 초록 체크·옅은 글자·취소선입니다. 카페에서 쉴 차례에는 의자에 앉으라는 안내만 보이고 목표 줄은 숨깁니다. 단계 칩·진행 칸·체크·코인처럼 그림인 부분은 시안 HTML을 그대로 렌더링한 그림(`Art/UI/Hud`)입니다. 오른쪽 위 시계·코인 칩은 시안처럼 같은 크기(174×64)의 둥근 유리 알약이고, 대화 중에는 시안(g4)처럼 시계·코인 칩과 미니맵을 숨깁니다. 카드와 시계·코인 칩은 **Tools → A305 → Mission → Apply Quest Card**(`QuestCardBuilder`)가 만듭니다. 마을에서는 왼쪽 아래에 시안(g1)과 같은 유리 미니맵(`Content/MiniMap`, `TownMiniMapView`)을 둡니다. 둥근 창 안에 시안 스타일로 그린 마을 지도 그림(`Art/UI/Map/TownMap.png`)을 깔고, 내 위치가 창 가운데 오도록 그림을 움직이며 위에 내 위치 화살표와 목적지 장소 테두리를 얹습니다. 지도 그림은 실제 마을 배치(미션 장소 4곳, 경찰서·빵집·우체국·은행 등 가게, 공원, 가로수, 횡단보도)를 1m = 20단위로 그렸고 북쪽이 오른쪽입니다. 마을 좌표와 그림 좌표 변환, 장소 칸과 출입문 자리는 `TownMapArt`가 정하므로, 마을 배치를 바꾸면 그림과 `TownMapArt`를 함께 고칩니다. 미니맵은 범례 글자 없이 오른쪽 위 펼치기 버튼만 두고, 판 어디를 눌러도 큰 마을 지도(`Content/MapOverlay`, 수첩 지도 복사본)를 화면 가운데에 펼칩니다. 큰 지도는 닫기 버튼·Esc·바깥 누르기로 닫고 `ThemedTownMap`의 유리 모드로 바탕을 비치게 칠합니다(0.43). 큰 지도 오른쪽 아래의 [길 안내 켬/끔] 버튼(`GuideRouteToggle`)으로 도로 위 길 안내를 켜고 끕니다. 퀘스트 카드를 갱신할 때마다 지금 단계의 장소(`FirstDayMissionState.Destination`)를 [길 안내](../Wayfinding/README.md)에 알려(`GuideDestination`), 마을에서 그 건물 출입문 앞이 빛나고 도로 위에 길이 깔립니다. 미션 화면을 끄면(튜토리얼 중) 안내도 멈춥니다. 수첩이나 큰 지도가 열렸거나 마을 밖이면 미니맵을 숨깁니다. 보이는 퀘스트 카드·미니맵이 화면 왼쪽에서 차지하는 폭은 World 모듈의 `CenterWindow`로 알려, 단어장·피드백·피드백 목록 같은 가운데 창이 그 오른쪽 빈 곳에 놓이게 합니다. 창을 보면서 할 일과 지도를 함께 볼 수 있고, 퀘스트 카드 높이가 할 일 줄 수에 따라 바뀌어도 겹치지 않습니다. 가운데 창이 열리면 펼친 수첩·큰 지도는 닫습니다. 미니맵은 **Tools → A305 → Mission → Apply Mini Map**(`MiniMapBuilder`), 큰 지도까지 함께는 **Apply Live Mini Map**(`FirstDayMissionBuilder.ApplyLiveMiniMap`)이 지우고 다시 만듭니다. 수첩을 열면 캔버스 그리기 순서를 다른 화면(대화창 120, 피드백 130, 단어장 135)보다 높은 1000으로 올려 대화 중에도 가장 위에 보이고, 닫으면 원래 순서(108)로 돌립니다. 마을에서는 상태를 읽을 때마다 서버 `weather`(`CLEAR`/`CLOUDY`/`RAIN` 등)와 `FirstDayMissionState.FallbackWeather`를 [Weather 모듈](../Weather/README.md)의 `WeatherPresenter`에 넘겨 하늘·조명과 날씨 효과를 바꿉니다. `weather`가 없거나 Unity 날씨 목록에 없는 값이면 `FallbackWeather`(`raining`이면 `RAIN`, 아니면 `CLEAR`)를 보여 줍니다. 같은 때 `FirstDayMissionState.OpensUmbrellaIn(sceneCode)`로 플레이어 우산을 펴거나 접습니다. 우산은 드럭스토어 단계(5단계)를 지나면 생기고, 비 오는 마을(`TOWN`)에서만 플레이어가 손에 쥐고 펴 씁니다([Player 모듈](../Player/README.md)의 `UmbrellaHold`). 계약·7단계 내용·배포·QA 기준은 [연계 미션 API](../../../../../docs/api/first-day-mission.md)에 있습니다.

**Tools → A305 → Mission → Build First Day Journey**가 다섯 공간의 기능 연결, 수첩 프리팹, 상점과 출입구를 준비합니다. 기존 씬 디자인은 유지합니다. `SpaceSceneSetup`도 같은 기능을 연결하므로 공간을 다시 구성해도 연결이 남습니다.

## 의자·벤치·소파 앉기

카페 원형 테이블 의자 4개, 마을 벤치 4개, 호텔 대기 소파 2개에 같은 조작으로 앉습니다. 앉을 자리마다 `RestSeat`가 하나씩 있고, 벤치·소파는 앉을 위치를 왼쪽·오른쪽 중에서 고르게 가구 폭 방향으로 두 자리를 둡니다. 쉬는 NPC도 같은 자리에 앉으므로(`INpcSeat`, [Npc 모듈](../Npc/README.md#생활)) 한 자리에는 한 명만 앉습니다.

| 상황 | 동작 |
| --- | --- |
| 자리에서 1.55m 안에 섬 | 가장 가까운 자리 하나에만 [잠깐 앉기]를 띄웁니다(`RestSeatPicker`). 버튼이나 E 키로 앉습니다. 대화 중, 이동 잠금 중, 행인 제안이나 다른 [말 걸기]가 떠 있으면 띄우지 않습니다. |
| NPC가 앉은 자리 | 그 자리는 빼고 남은 자리 중 가장 가까운 자리에 띄웁니다. NPC는 플레이어가 앉은 자리에 앉지 않습니다. |
| 앉음 | 3초 쉰 뒤 앉기 전에 서 있던 곳으로 일어섭니다. 카메라는 플레이어가 다가온 쪽에서 앉은 캐릭터를 봅니다. 자세와 카메라는 [Player 모듈](../Player/README.md)의 `PlayerSeatPose`가 맡습니다. |
| 카페 | 첫날 미션이 상태를 읽기 전과 서버 요청 중에는 앉기를 막습니다. 쉬고 일어서면 `canRest`일 때만 서버에 휴식을 보냅니다. |
| 마을·호텔 | 미션 상태와 상관없이 앉고, 미션 휴식으로 세지 않습니다(`FirstDayMissionState.ManagesSeatsIn`). 서버도 마지막 장소가 카페일 때만 휴식을 받습니다. |

`RestSeat`의 `cushionTopMeters`·`frontEdgeMeters`는 자리 원점(바닥, +z가 앉아서 보는 방향)에서 잰 쿠션 윗면 높이와 앞 모서리까지의 거리입니다. 기본값은 카페 의자이고, 벤치·소파는 Builder가 모델에 맞춰 넣습니다.

| 가구 | 모델 | 쿠션 윗면 | 앞 모서리 | 자리 |
| --- | --- | --- | --- | --- |
| 카페 의자 | `CafeChair` | 0.33m | 0.30m | 의자마다 1개 |
| 메인스트리트 벤치 | `ParkBench_HoneyWood`(`MainstreetArtKit.Bench`) | 0.325m | 0.205m | 폭 1.6m를 반씩, x = ±0.4m |
| 공원 벤치 | TinyTreats `bench.fbx`를 0.8배 | 0.40m | 0.527m | 폭 1.6m를 반씩, x = ±0.4m |
| 세이지 소파 | `Home_SageSofa_Sage`(`HomeInteriorKit.Sofa`) | 0.705m | 0.485m | 쿠션 두 개의 가운데, x = ±0.57m |

**Tools → A305 → Cafe → Connect Chair Sitting**(`CafeSeatBuilder`)은 `CafeScene`·`CafeInteriorSandbox`의 원형 테이블 의자에 `CafeSeat1~4`를 둡니다. **Tools → A305 → Mission → Connect Bench And Sofa Sitting**(`BenchSofaSeatBuilder`)은 `TownScene`·`HotelScene`에서 위 벤치·소파 모델을 그리는 활성 가구마다 자식 `RestSeat1`, `RestSeat2`를 두고 플레이어·대화·상호작용 화면과 앉는 면 값을 연결합니다. 두 Builder 모두 가구 배치와 디자인은 바꾸지 않습니다. 다른 벤치·소파 모델을 앉을 수 있게 하려면 `BenchSofaSeatBuilder`의 모델 목록에 메시 경로, 잰 값, 자리 위치를 추가합니다.

## 튜토리얼용 기본 미션

기존 `MissionBoard`·`MissionPanelView`는 첫날 미션 화면이 없는 장면(서버 없는 데모)에 유지합니다. 튜토리얼은 이 창을 안내하지 않고 마지막에 첫날 미션의 지도 버튼을 안내합니다. 이 경로는 키워드를 사용하며 서버 연계 여정의 완료 판정에 사용하지 않습니다. 튜토리얼이 오늘의 목표 단계에 이르거나 끝나면 `MissionController.SetStoryAvailable`로 기본 창을 숨기고 새 수첩을 표시합니다. 지도 버튼으로 수첩을 열면 `FirstDayMissionView.JournalOpened`로 알립니다. 아래는 이 기본 미션 경로의 구성입니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `MissionBoard` | 미션 시작(진행 중인 다른 미션은 교체), 완료 판정, 완료한 미션 재시작 거절. Unity 객체 없이 테스트합니다. |
| `MissionContent` | 미션 하나의 내용. 미션 키, 제목, 설명, 장소, 대화 상대 NPC 키, 키워드 |
| `MissionSource`, `MockMissionSource` | 오늘의 미션을 찾는 곳과 서버 연동 전 확인용 미션입니다. |
| `MissionProgress` | 공간 씬이 바뀌어도 미션 진행이 남도록 씬 밖에 둡니다. Play를 시작할 때마다 비웁니다. |
| `MissionController` | 미션 시작과 대화가 끝난 뒤의 완료 판정을 미션 창과 연결합니다. |
| `MissionPanelView`, `MissionRowView` | 미션 창과 미션 한 줄. 미션 수만큼 줄 템플릿을 복제합니다. |
| `MissionText` | 미션 창 문구. 미션 시작·교체 알림과, 완료 전 미션 줄에 보이는 진행 상태와 말할 단어(키워드). |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Dialogue` | `DialogueController.ConversationEnded`, `DialogueRecord`, `KeywordMatcher`. 앉기 제안은 대화 중에 띄우지 않습니다. |
| `A305.Player` | 첫날 미션 지도의 플레이어 위치, 앉기의 `PlayerMover`·`PlayerSeatPose` |
| `A305.Npc` | [잠깐 앉기]를 띄우는 `PasserbyOfferView`의 상호작용 버튼 |
| `A305.Weather` | 첫날 미션의 예보 날씨를 마을에 보여 주는 `WeatherPresenter` |
| `A305.Wayfinding` | 지금 단계의 장소를 길 안내에 알리는 `GuideDestination`, 크게 본 지도의 길 안내 켜기·끄기 버튼(`GuideRouteToggle`) |
| uGUI, TextMeshPro | 미션 창 |

## 동작 규칙

| 상황 | 동작 |
| --- | --- |
| [미션 시작] | 그 미션을 진행 중으로 바꿉니다. 진행 중인 다른 미션이 있으면 바꾸고, 미션 창 안내에 "'A' 대신 'B' 미션을 시작했어요"로 멈춘 미션을 함께 알립니다. |
| 완료 전 미션 줄 | 진행 상태와 함께 대화에 넣어 말할 키워드를 "말할 단어: soy, latte"처럼 보여 줍니다. 키워드가 없는 미션은 진행 상태만 보입니다. |
| 대화가 끝남 | 진행 중인 미션의 대화 상대와 같고, 내가 말한 문장들에 키워드가 모두 있으면 완료합니다. 키워드는 여러 문장에 나뉘어 있어도 됩니다. |
| 미션을 시작하지 않고 키워드를 말함 | 완료하지 않습니다. |
| 완료한 미션 | [미션 시작]이 사라지고 다시 시작할 수 없습니다. |

미션 창을 언제 보일지는 이 모듈이 정하지 않고, 튜토리얼 같은 진행 흐름이 `MissionController.SetPanelVisible`로 정합니다. 화면 정렬 순서는 105입니다.

## 확인용 미션

`Assets/Data/Mission/MockMissions.asset`

| 미션 키 | 미션 | 대화 상대 | 키워드 |
| --- | --- | --- | --- |
| `CAFE_SOY_LATTE` | 나만의 카페라테 주문하기 | `CAFE_STAFF` | `soy`, `latte` |
| `CAFE_DESSERT` | 달콤한 디저트 찾기 | `CAFE_STAFF` | `dessert` |

필드 초기값은 에셋을 처음 만들 때 들어가는 예시이며, 이후 수정은 에셋에서 합니다. 미션 내용은 서버 데이터라 서버 연동 후에는 서버 값을 씁니다.

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `MissionController.MissionStarted` | 미션을 시작하면 미션 키를 알립니다. |
| `MissionController.SetPanelVisible(bool)` | 미션 창을 보이거나 숨깁니다. |
| `MissionProgress.Clear()` | 미션 진행을 비웁니다. 테스트에서 상태를 비울 때 씁니다. |

## Builder

**Tools → A305 → Mission → Build**(`Scripts/Editor/Mission/MissionBuilder.cs`)가 `Prefabs/Mission/MissionPanel.prefab`과 확인용 미션을 준비합니다. 화면은 없는 요소만 만들고 이미 있는 요소의 디자인은 유지합니다. 공간 씬에 붙이는 일은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 합니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Mission.Tests` | `Assets/Tests/EditMode/Mission/` | 미션 시작, 다른 미션으로 교체, 없는 미션 거절, 여러 문장에 나뉜 키워드로 완료, 다른 NPC·빠진 키워드·시작 전에는 미완료, 완료한 미션 재시작 거절, 시작·교체 알림 문구, 미션 줄의 말할 단어 표시, 닿는 거리 안의 가장 가까운 자리 고르기(`RestSeatPicker`), 앉아 쉰 시간(`RestSeatTimer`), 미션이 자리를 다루는 장소는 카페뿐인지, 퀘스트 카드가 지금 단계 장소를 길 안내에 알리고 끝내거나 미션 화면을 끄면 멈추는지, 크게 본 지도에 길 안내 켜기·끄기 버튼이 있는지 |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | 공간 기능 연결이 미션 창과 미션 처리를 한 번씩 붙이고 대화 처리와 연결하는지, 카페 의자 4개(`CafeSeatSceneTests`)와 마을 벤치 4개·호텔 소파 2개에 두 자리씩(`BenchSofaSeatSceneTests`) 앉기가 연결됐는지 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
