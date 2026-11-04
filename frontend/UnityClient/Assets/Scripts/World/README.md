# World 모듈

공간 사이 이동을 담당합니다. 튜토리얼을 시작하는 내 방(`HomeScene`)에서 마을(`TownScene`)로 나가고, 마을에서 카페(`CafeScene`)·숙소(`HotelScene`)·상점(`ShopScene`)에 드나듭니다. 공간마다 씬을 하나씩 두고, 출입구에 닿으면 연결된 씬을 불러와 그 씬의 출입구 앞에 플레이어를 세웁니다. 장소마다 영업시간이 있어 영업시간이 아닌 장소에는 들어가지 못하고, 머무는 장소의 영업이 끝나면 잠시 뒤 마을로 옮깁니다([영업시간](#영업시간)).

이 서비스의 공간 구성을 다루는 게임 전용 기능입니다. 플레이어 이동·카메라는 재사용 모듈인 `A305.Player`의 공개 API로 요청합니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `SpaceTravelState` | 이동 요청을 한 번에 하나만 받고, 다음 공간에서 나타날 출입구와 카메라 각도를 한 번만 넘겨줍니다. 잠그면 풀 때까지 이동 요청을 거절하고, 잠금은 도착·취소 뒤에도 남습니다. 영업이 끝난 장소에서 내보낼 때만 잠금과 상관없이 시작합니다(`evenIfLocked`). Unity 객체 없이 테스트합니다. |
| `SpaceTravel` | 씬을 불러와 공간을 옮깁니다. 씬이 바뀌어도 도착할 출입구와 카메라 각도가 남도록 정적으로 둡니다. 진행 흐름이 `SetLocked(true)`로 잠그면 문·[홈] 이동을 거절하고 `Refused`로 알립니다. 지금은 [Tutorial 모듈](../Tutorial/README.md)이 튜토리얼 단계마다 잠그고 풉니다. 잠기지 않았어도 가려는 장소(씬의 장소나 들어가는 마을 매장)가 영업시간이 아니면 거절하고 안내 문구와 함께 `ClosedPlaceRefused`로 알립니다. 나오는 이동은 막지 않습니다. 영업이 끝난 장소에서 내보내는 `TryLeaveClosedPlace`(공간 씬)와 매장 문 밖 이동은 튜토리얼 잠금과 상관없이 옮깁니다. |
| `SpaceTravelCurtain` | 공간을 옮기는 동안 화면을 검게 덮습니다(0.2초에 걸쳐 덮고 0.3초에 걸쳐 걷음). 덮는 동안 다음 씬을 미리 불러오고, 화면이 다 덮인 뒤에 씬을 바꿔 씬이 바뀌는 순간의 끊김을 가립니다. 덮여 있는 동안에는 `Application.backgroundLoadingPriority`를 `High`로 올려 불러오기에 프레임 시간을 더 쓰고, 도착하면 되돌립니다. 이전 공간을 끈 뒤 새 공간이 깨어나기까지는 공간의 카메라가 없으므로, 덮여 있는 동안 검은 화면만 그리는 카메라를 켭니다(Editor의 "No cameras rendering" 방지). 씬이 바뀌어도 남도록 처음 이동할 때 만들어 둡니다. 마을 매장도 같은 시간으로 덮은 동안 해당 실내를 로드하거나 해제합니다. |

씬은 통째로 바꾸지 않고 새 씬을 겹쳐 불러온 뒤(`LoadSceneMode.Additive`) 이전 씬만 내립니다. 통째로 바꾸면 Unity가 이전 씬 에셋 정리와 GC를 매번 함께 돌려 마을처럼 큰 씬은 1초 넘게 더 걸리기 때문입니다. 겹쳐 불러오면서도 통째로 바꿀 때와 같게 동작하도록 다음을 지킵니다.

- 화면이 다 덮이면 이전 씬의 최상위 오브젝트를 모두 끈 뒤 새 씬을 깨웁니다. 새 씬의 `Awake`·`Start`에서 이전 공간의 플레이어·카메라·출입구를 찾지 않습니다.
- 화면을 덮는 동안 이전 씬이 이미 내려갔다면 다시 끄거나 내리지 않고 새 씬 활성화를 계속합니다.
- 새 씬은 `sceneLoaded` 때(새 씬의 `Awake`·`OnEnable` 뒤, `Start` 전) 활성 씬으로 바꿉니다. 하늘·환경광·안개(`RenderSettings`)와 부모 없이 새로 만드는 오브젝트는 활성 씬을 따르므로, 이 값을 읽거나 오브젝트를 만드는 코드는 `Awake`가 아니라 `Start`에 둡니다(예: `WeatherPresenter`).
- 이전 씬의 에셋은 비우지 않습니다(`Resources.UnloadUnusedAssets`를 부르지 않음). 한 번 들른 공간에 다시 들어갈 때 에셋을 다시 읽지 않아 빠른 대신, 들른 공간의 에셋만큼 메모리를 더 씁니다.
| `SpaceDoorway` | 다른 공간으로 이어지는 출입구입니다. 플레이어가 문 앞 감지 영역에 들어오면 문으로 들어가는 방향 기준의 카메라 각도를 넘기며 이동하고, 다른 공간에서 들어오면 `ArrivalPoint`에 문을 등지고 섭니다. |
| `InteriorDoorway` | 같은 씬 안에서 건물 실내와 문 밖을 잇는 출입구입니다. 감지 영역에 들어오면 `SpaceTravelCurtain`으로 화면을 덮고, 그동안 짝 출입구의 `ArrivalPoint`로 플레이어를 옮긴 뒤 화면을 걷습니다. 카메라 각도는 `SpaceDoorway`와 같은 기준으로 이어 갑니다. 지금은 경찰서만 씁니다. |
| `SpaceArrival` | 공간 씬에 들어오면 플레이어를 도착 출입구 앞으로 옮기고, 문에 들어갈 때의 카메라 각도를 나오는 방향 기준으로 이어 맞춥니다. 앞으로 걸어 들어오면 카메라가 플레이어 뒤에, 뒤로 걸어 들어오면 문을 바라보는 쪽에 와서 누르던 키로 계속 문에서 멀어집니다. 장소 씬이면 재접속 위치로 저장합니다(WORLD-05). `apiSettings`가 비어 있으면 저장하지 않습니다. |
| `CarLoopRoute` | 마을 본길을 도는 차의 길. 동쪽 차로로 북쪽 끝까지 가서 유턴하고 서쪽 차로로 내려와 다시 유턴합니다. 길 위 거리로 위치·방향을 구하고, 정지 지점 앞에서 서며, 차 앞을 막은 사람이 있으면 설지 정합니다. Unity 객체 없이 테스트합니다. |
| `TrafficSignalCycle`, `TrafficLights`, `TrafficSignalHead` | 교차로 신호 주기(본길 초록 12초·노랑 3초, 가로길 초록 7초·노랑 3초)와 신호등 렌즈 켜기. 주기는 `TrafficLights` Inspector에서 조절합니다. |
| `LoopingCar` | 본길을 도는 차. 본길 신호가 초록이 아니면 교차로 정지선에서 서고, 앞을 사람이나 다른 차가 막으면 섭니다. 택시처럼 손님 옆에 설 때는 설 자리를 받습니다. 주차 트럭 옆과 남쪽 유턴(z<-12)은 두 차가 비켜 지나갈 수 없어 한 번에 한 차만 들어가고, 다른 차가 있으면 남행 z=-12에서 기다립니다. 비켜 가기 시작하는 차의 앞모서리가 맞은편 차로로 들어가므로 차 앞뒤 4m까지 구간에 걸치면 구간을 차지한 것으로 봅니다. |
| `TaxiFare` | 택시를 세우고 보내는 흐름. 인도에서 다가오는 택시만 세울 수 있고, 손님 옆에 서서 기다리다 대화를 마치거나 손님이 떠나면 다시 달립니다. Unity 객체 없이 테스트합니다. |
| `TaxiDestinationRequest`, `TaxiDestination` | 택시 목적지 판별(지원 장소 이름과 이동 요청 구분)과 장소별 정차·하차 지점. 장소 이름 찾기(`FindSinglePlace`)는 경찰 신고 판별도 함께 씁니다. Unity 객체 없이 테스트합니다. |
| `PoliceReportRequest` | 경찰관에게 한 말이 분명한 신고(도난·분실·사고·싸움·침입·긴급)인지 고르고, 마을 장소를 하나만 말했으면 그 장소 코드를 돌려줍니다. 부정·가정·이야기·길을 잃었다는 말은 신고가 아닙니다. 택시처럼 별도 LLM 호출 없이 보수적인 규칙으로만 찾습니다. Unity 객체 없이 테스트합니다. |
| `PoliceReportIntake` | 경찰관과 한 번 대화하는 동안 신고를 접수하고, 접수한 뒤 말한 장소로 출동 장소를 바꿉니다. 대화를 마치면 출동할 장소를 꺼내고 비웁니다. Unity 객체 없이 테스트합니다. |
| `PoliceDispatch` | 순찰차 출동 흐름. 주차 칸 → 진입로로 나가기 → 장소로 출동(장소가 없으면 본길 한 바퀴 순찰) → 장소 앞 20초 → 돌아오기 → 주차 칸. 나가 있는 동안 새 신고는 받지 않고, 돌아올 때 경광등을 끕니다. Unity 객체 없이 테스트합니다. |
| `DrivePath` | 주차 칸과 본길 사이처럼 `CarLoopRoute` 밖을 잇는 3차 베지어 곡선 길. 길 위 거리로 위치·방향을 구합니다. Unity 객체 없이 테스트합니다. |
| `PoliceCar` | 경찰서 옆 주차 칸의 순찰차. 진입로는 `DrivePath`로 직접 움직이고 본길은 `LoopingCar`로 신호를 지키며 달립니다. 주차 칸·진입로에서는 `LoopingCar.IsHeld`로 붙잡아 두어 본길 차들이 계속 피하게 하고, 진입로에서는 사람만 피합니다(차끼리 서로 기다리다 멈추지 않도록). 진입로는 트럭 옆 한 대씩 구간에 이어지므로, 다른 차가 그 구간에 있으면 주차 칸에서 기다렸다 나가고 진입로를 지나는 동안에는 구간을 차지합니다. 경광등은 출동해서 돌아오기 전까지 빨강·파랑을 번갈아 켭니다. |
| `PlaceScenes` | 서버 장소 코드와 Unity 씬 이름(`HOME`↔`HomeScene`, `TOWN`↔`TownScene`, `CAFE`↔`CafeScene`, `HOTEL`↔`HotelScene`, `SHOP`↔`ShopScene`), 영업시간 안내에 쓰는 한국어 이름(집·마을·카페·호텔·드럭스토어). 재접속 위치 저장과 [길 안내](../Wayfinding/README.md)가 함께 씁니다. 새 장소 씬을 추가하면 이 표와 서버 시드(`scenes`)에 함께 추가합니다. Unity 객체 없이 테스트합니다. |
| `PlaceHours`, `OpeningHours` | 장소별 영업시간 파일(`Data/World/PlaceHours.json`)을 읽고, 장소가 하루 중 그 시각에 열려 있는지 판정합니다. `OpeningHours`는 게임 전체가 같은 영업시간을 보도록 정적으로 두고 지금 게임 시각(`GameClock.Shared`)으로 묻습니다. `PlaceHours`는 Unity 객체 없이 테스트합니다. |
| `ClosingCountdown`, `ClosingPhase` | 머무는 장소의 영업이 끝난 뒤 마을로 옮기기까지 남은 게임 시간(30분, `GraceMinutes`)을 셉니다. 다시 열거나 다른 장소로 옮기면 멈춥니다. Unity 객체 없이 테스트합니다. |
| `OpeningHoursText` | 거절 안내("카페는 지금 영업시간이 아니에요 · 07:00–21:00")와 남은 시간 안내("영업이 끝났어요. 30분 후 마을로 이동합니다")를 씁니다. 장소 이름 받침에 맞춰 은/는을 붙입니다. Unity 객체 없이 테스트합니다. |
| `OpeningHoursController` | 장소 씬의 `Systems/OpeningHours`에 하나 있어 영업시간 파일을 `OpeningHours`에 넣고, 거절 안내를 띄우고, 머무는 장소의 남은 시간을 세어 보이다 마을로 내보냅니다. |
| `HudNoticeView` | 화면 위 가운데의 짧은 안내판(`UI/HudNotice`, 프리팹 `Prefabs/World/HudNotice.prefab`). 거절 안내는 3초 띄우고, 남은 시간 안내는 끌 때까지 띄웁니다. 택시 상태 판과 같은 자리·테마(Panel)를 씁니다. |
| `LastSceneRecorder`, `ILastSceneApi`, `LastSceneApi` | Unity 씬 이름을 `PlaceScenes`로 서버 장소 코드로 바꿔 `PUT /api/v1/me/last-scene`으로 저장합니다. 장소가 아닌 씬은 저장하지 않고, 실패해도 예외 없이 결과로 알립니다. Unity 객체 없이 테스트합니다. |
| `GameClock`, `IGameClockApi`, `GameClockApi`, `GameClockTime` | 사용자별 게임 시간(WORLD-06·07). 게임 시간은 서버가 사용자마다 플레이한 시간만큼 흐르게 합니다(실제 10분 = 게임 하루, 대화 중·튜토리얼을 마치기 전 멈춤). 처음 들어오면 바로, 그 뒤로 30초마다 `POST /api/v1/me/game-clock/beats`를 보내고 받은 게임 시각을 `GameClock.Shared.Current`에 두며 `Changed`로 알립니다. 응답을 기다리는 동안은 다시 보내지 않고, 실패하면 마지막 시각을 그대로 두고 다음 간격에 다시 보냅니다. NPC 일과가 끊기지 않게 `Minutes`(1일차 0시부터 센 분)는 신호 사이에도 실제 시간만큼 흐르다가 새 시각을 받으면 그 값으로 맞추고, 서버가 멈췄다고 알려 주거나 진행 흐름이 시계를 붙잡아 두면(`IsHeld`, 튜토리얼 동안) 흐르지 않습니다. 서버 시각을 받기 전(게스트, 서버 없이 씬만 볼 때)에는 새 사용자처럼 1일차 오전 8시에서 시작합니다. 장면을 옮겨도 주기가 이어지도록 정적으로 둡니다. Unity 객체 없이 테스트합니다. |
| `GameClockText` | HUD 시계 칩과 폰 화면의 시각 글자. 게임 시각(분)을 "n일차 · 오전/오후 h:mm"으로 쓰고, 폰은 큰 시각("h:mm"), 날짜("n일차 · 오전/오후"), 상태 표시줄 시각("오전/오후 h:mm")을 따로 씁니다. Unity 객체 없이 테스트합니다. |
| `JourneyStatus` | 미션 화면과 폰 메뉴가 함께 보는 오늘의 여정. 미션 화면이 단계 칩·할 일·진행 막대 상태(`JourneyMark`)·예산을 알리고, 폰 메뉴가 같은 내용을 보입니다. 폰의 [마을 지도] 앱은 `RequestMap`으로 미션 화면에 큰 지도를 열어 달라고 알립니다. 두 화면은 서로의 모듈을 참조하지 않고 이 상태로 만납니다. Unity 객체 없이 테스트합니다. |
| `CenterWindow` | 단어장·피드백·피드백 목록처럼 화면 가운데를 차지하는 창과 마을 HUD를 함께 보되 겹치지 않게 하는 약속입니다. 미션 화면이 보이는 퀘스트 카드·미니맵이 화면 왼쪽에서 차지하는 폭을 알리면, 가운데 창은 그 오른쪽 빈 곳(위아래 150·옆 24 여백) 가운데에 놓이고 들어가지 않으면 배율을 줄입니다(`Place`). 학습 화면(`LearningController`)이 창을 열었다고 알리면 미션 화면은 펼친 수첩·큰 지도를 닫습니다. 두 화면은 서로의 모듈을 모르므로 이 상태로 만납니다. |
| `GameClockDaylight` | 장소 씬의 `Systems/GameClockDaylight`에 있어, 매 프레임(LateUpdate) 게임 시각(`GameClock.Shared.Minutes`)을 그 장소의 낮·밤 표시(`A305.Weather`의 `DaylightView`: 마을은 `WeatherPresenter`, 실내는 `InteriorDaylight`)에 넘깁니다. 배포용 장소 씬 5개에는 **Tools → A305 → Weather → Build Day Night**으로 연결합니다(**World → Build Spaces**도 함께 붙입니다). |
| `GameClockDriver` | 장소 씬의 `SpaceArrival` 객체에 붙어, 그 씬에 있는 동안 `GameClock`의 신호를 보내고 매 프레임 게임 시각을 흘립니다. 입구 화면에는 없어 로그인 전·로그아웃 뒤에는 보내지 않습니다. 튜토리얼(`HomeScene`)도 플레이 시간으로 셉니다. `apiSettings`가 비어 있으면 보내지 않습니다. 배포용 장소 씬 5개에는 **Tools → A305 → World → Connect Game Clock**으로 연결합니다(새로 꾸미는 씬은 **Set Up Open Scene**이 함께 붙입니다). |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Api` | `ApiClient`, `ApiSettings`, `ApiException` |
| `A305.Player` | `PlayerMover.Teleport`, `PlayerMover.SetMovementLocked`, `PlayerCameraRig.ViewYawDegrees`, `PlayerCameraRig.SnapView` |
| `Unity.TextMeshPro` | `HudNoticeView`의 안내 글자 |

## 공간과 출입구

씬은 `Assets/Scenes/Production/`에 있고 Scene List에서 입구 화면(`EntryScene`) 다음에 이 순서로 등록됩니다. 튜토리얼은 `HomeScene`에서 시작합니다. 출입구 이름은 이어지는 공간 + `Door`로 짓습니다.

| 씬 | 출입구 | 이어지는 곳 |
| --- | --- | --- |
| `HomeScene` | `TownDoor` | `TownScene`의 `HomeDoor` |
| `TownScene` | `HomeDoor` | `HomeScene`의 `TownDoor` |
| `TownScene` | `CafeDoor` | `CafeScene`의 `TownDoor` |
| `TownScene` | `HotelDoor` | `HotelScene`의 `TownDoor` |
| `CafeScene` | `TownDoor` | `TownScene`의 `CafeDoor` |
| `HotelScene` | `TownDoor` | `TownScene`의 `HotelDoor` |
| `TownScene` | `ShopDoor` | `ShopScene`의 `TownDoor` |
| `ShopScene` | `TownDoor` | `TownScene`의 `ShopDoor` |

상점 연결과 연계 미션은 **Tools → A305 → Mission → Build First Day Journey**로 준비합니다. 카페 옆 빈 건물 앞의 DRUGSTORE 간판에서 들어갑니다. 다른 기존 건물과 씬 아트는 유지합니다.

상점 실내는 **Tools → A305 → World → Apply Drugstore Interior**(`DrugstoreInteriorBuilder`)가 미국 드럭스토어처럼 꾸밉니다. 음료 냉장고·선반·우산 통 같은 조각 프리팹과 `DrugstoreInterior` 프리팹은 `Assets/Prefabs/World/Drugstore/`에 없을 때만 만들고, 이미 있으면 고친 디자인을 그대로 둡니다. 이미 있는 `DrugstoreInterior` 프리팹에는 카메라를 막는 천장 충돌과 정문(`DrugstoreEntrance`: 문 구멍을 채우는 문틀·문 위 벽, 진열창, 앞벽 띠·걸레받이)이 빠졌을 때만 더합니다. 마을로 나가는 문판은 처음 한 번만 정문 틀 크기의 유리문으로 칠합니다. 외부 모델은 `Assets/ThirdParty/`의 원본을 그대로 쓰고 URP 재질은 `Assets/Art/World/Drugstore/Materials/`에 만듭니다.

상점 카메라는 **Tools → A305 → Mission → Apply Shop Interior Camera**가 맞춥니다. 카페와 같은 실내 거리 값을 `Assets/Data/Shop/ShopCameraSettings.asset`에 두고, 입구 도착 위치를 문 안쪽 3.6m로 옮겨 들어오자마자 카메라가 문 벽에 붙지 않게 합니다. **Build First Day Journey**도 같은 설정을 적용합니다.

마을 거리는 두 Builder가 미국 소도시처럼 꾸밉니다.

- **Tools → A305 → World → Apply American Street Details**(`AmericanStreetBuilder`): 카페 앞 횡단보도와 교차로에서 끊기던 중앙선을 정지선까지 잇고, 교차로 네 방향에 횡단보도·정지선을, 가로길에 중앙선을 그립니다. 교차로 모퉁이 두 곳에 MAPLE AVE 이름판을 단 신호등을 세우고, 길가에 속도 제한 25 표지판·버스 정류장·주차 미터기·신문 판매함·파란 우체통·보행자 횡단 표지판을 둡니다. 성조기는 호텔 외벽에 하나만 답니다.
- **Tools → A305 → World → Apply Town Street Life**(`TownStreetLifeBuilder`): 집 남쪽 잔디의 아이스크림 트럭 광장, 카페 앞 파라솔 테라스, 본길을 도는 택시·승용차와 교차로 신호, 자전거 거치대, 가로수 밑 꽃과 건물 앞 꽃덤불, 공원의 여우·비둘기와 집 앞 고양이를 둡니다.

승용차는 카페 앞 횡단보도 북쪽(z=-6)과 본길 북쪽 끝(z=66) 사이를 돕니다. 택시는 집까지 이동하도록 남쪽 끝을 z=-25까지 넓히고, 남행 중 기존 주차 트럭을 천천히 비켜갑니다. 두 차는 교차로 신호를 지키고, 택시는 인도에서 [택시 세우기]를 누르면 손님 옆 길가에 서면서 바로 뒷좌석에 타 택시 기사(`TAXI_DRIVER`)와 대화합니다. 목적지 11곳과 승하차 흐름은 [택시 안내](../../../../../docs/unity/taxi-ride.md)에 있습니다.

승용차에도 앉은 운전자와 운전대가 들어 있습니다. **Tools → A305 → World → Add Vehicle Drivers**는 기존 택시 기사의 외형과 자세를 승용차 프리팹에 복제합니다. 승용차에는 택시 대화 기능을 붙이지 않으며, 운전자가 이미 있으면 디자인을 유지합니다. 운전자 크기·위치·자세는 `Prefabs/World/StreetLife/Sedan.prefab`의 `Driver`에서 조절합니다. 주차된 픽업트럭과 아이스크림 트럭은 비워 둡니다. **Adjust Driver Steering**은 택시와 승용차의 핸들을 얼굴 아래로 낮추고 앞유리 쪽으로 조금 옮긴 뒤, 팔 길이를 유지하면서 양손 자세를 맞춥니다. **Capture Vehicle Drivers** 메뉴로 마을에서 두 차량의 앞쪽·운전석 쪽 모습을 `artifacts/vehicle-drivers/`에 저장해 확인할 수 있습니다.

두 Builder는 `TownMainstreet` 아래 `AmericanStreetDetails`·`StreetLife` 묶음이 이미 있으면 배치를 바꾸지 않습니다. 조각 프리팹은 각각 `Assets/Prefabs/World/MainstreetArt/`, `Assets/Prefabs/World/StreetLife/`에 없을 때만 만들고, 새 사물의 공용 단어(신호등·택시·자전거 등)는 뜻·예문과 함께 없을 때만 만듭니다.

- 북쪽 기존 빈 건물 세 동은 아래의 층별 상가로 사용합니다. 나머지 배경 주거동은 닫힌 외관을 유지합니다. 식당·공항의 도형 외관과 임시 간판은 현재 배치에서 사용하지 않습니다.
- 씬 이름은 ERD `scenes.unity_key` 값으로 사용합니다. 공간 이름 같은 서버 데이터는 Unity에 두지 않습니다. 영업시간은 AI 도구가 NPC 생활 규칙에서 만든 `Data/World/PlaceHours.json`을 읽고 Unity에서 고치지 않습니다([영업시간](#영업시간)).
- TownScene 외관은 `Prefabs/World/TownProduction`과 `CafeBlenderKit`을 사용합니다. HomeScene은 `Prefabs/Home/CozyCottage`의 실내 외형을 사용하며 기존 가구 사물 키와 문 연결을 유지합니다. 카페·호텔의 실내는 도형 대체 공간을 유지합니다. 아래 Builder를 다시 실행해도 기존 디자인을 덮어쓰지 않습니다.

## 거리 매장

`TownStorefrontBuilder`는 기존 배경 건물 7곳(북쪽 거리 6곳과 남서쪽 끝 경찰서)을 간판과 실내가 있는 매장으로 구성합니다. 마을에서 원래 문을 밟으면 화면을 덮고 해당 건물의 실내 씬을 추가 로드합니다. 퇴장하면 실내 씬을 해제합니다. 결제 기능은 포함하지 않습니다. 매장 직원 NPC는 `BuildingStaffBuilder`가 따로 둡니다([Npc 모듈](../Npc/README.md#거리-매장-직원)). 경찰서의 경찰관 4명은 `PoliceStationBuilder`가 둡니다.

| 프리팹 | 간판 | 건물 앞 위치(x, z) |
| --- | --- | --- |
| `PostOffice` | POST OFFICE | (-7, 30) |
| `MapleDiner` | MAPLE DINER | (7, 39) |
| `TownBank` | TOWN BANK | (-7, 48) |
| `SudsLaundromat` | SUDS LAUNDROMAT | (7, 48) |
| `BloomFlowers` | BLOOM FLOWERS | (-7, 57) |
| `MainStreetBarber` | MAIN ST. BARBER | (7, 57) |
| `PoliceStation` | POLICE | (-7, -20) |

**Build Town Storefronts Sandbox**로 검토용 씬을 만들고 **Apply Town Storefronts To Town**으로 TownScene에 적용합니다(두 메뉴 모두 **Tools → A305 → World**). 프리팹은 없을 때만 생성하며 기존 디자인은 유지합니다. 원래 배경 건물은 비활성으로 보존합니다. 적용 후 **Apply Taxi Cabin**으로 목적지를 연결하고 **ObjectHover → Register All Scene Words**로 단어 등록과 백엔드 내보내기를 완료합니다. 새 사물 단어는 사서함·소포·스툴·ATM·세탁기·건조기·꽃·이발 의자·경찰서입니다.

### 경찰서와 순찰차

**Tools → A305 → World → Apply Police Station**(`PoliceStationBuilder`)이 경찰서를 한 번에 적용합니다. 경찰서 매장(창구·대기 의자·게시판·서류함·국기)을 놓고, 경찰서 남쪽 잔디에 주차장(`TownMainstreet/PoliceParking`)과 본길 끝으로 이어지는 진입 포장을 깔아 순찰차를 세웁니다. 창구 뒤에는 경찰관 Dan(`POLICE_OFFICER`)·Victor·Kevin·Tyler를 세우고(창구에 가장 가까운 Dan이 근무 중이면 먼저 응대) 경찰관마다 신고 창구(`PoliceReportDesk`)와 순찰차를 잇습니다. 새 경찰관은 창구 뒤 자리가 가구와 겹치면 가까운 빈자리로 옮기고, 외형 표의 배색을 입힙니다. 마지막으로 **Apply Taxi Cabin**과 같은 구성으로 택시 목적지에 경찰서를 더합니다.

- 순찰차 모델은 KayKit City Builder Bits의 `car_police`이고 프리팹은 `Prefabs/World/PoliceStation/PoliceCar.prefab`입니다. 지붕 경광등 위에 빨강·파랑 발광 조각과 불빛(`LightBar/RedLights`, `LightBar/BlueLights`)을 얹어 두었습니다.
- 경찰관 외형은 호텔리어 모델에 갈색 바지·구두만 남색으로 바꾼 텍스처 사본(`Art/World/PoliceStation/PoliceOfficerUniform.png`)과 경찰 모자를 씌운 것입니다. 모자는 모델의 원래 자세를 기준으로 머리 뼈에 붙여 서 있기 동작에서 머리 앞을 봅니다. 경찰관 전용 모델은 아직 없습니다.
- 순찰차는 택시와 같은 본길(남쪽 끝 z=-25, 북쪽 끝 z=66)을 달리고, 택시 목적지와 같은 정차 지점을 씁니다(경찰서 자신은 빼고 10곳). 주차 칸은 (-9, -28.8)이고, 진입로는 동쪽 끝 정원수(-3.7, -27.8)를 피해 그 북쪽으로 지납니다.
- 주차장·순찰차 프리팹·경찰관 외형은 없을 때만 만들고, 이미 있으면 배치를 두고 연결(본길 설정·진입로 조절점·목적지·경광등·창구)만 맞춥니다. **Capture Police Station**은 검토용 화면을 `artifacts/unity/police-station/`에 저장합니다.
- 새 사물 단어는 경찰차·경찰관·주차장입니다.
- 경찰서 출입은 `InteriorDoorway` 한 쌍이 맡습니다. 문 밖 보도 쪽 `Entrance/OutsideDoorway`에 닿으면 실내 `Entrance/InsideDoorway`의 도착 지점(문 안쪽 2m, 창구를 봄)에, 실내 쪽에 닿으면 문 밖 2m(본길을 봄)에 섭니다. 문짝(`PoliceStationDoor`)은 여닫지 않고 닫힌 채 통로를 막습니다. 마을 씬과 경찰관의 근무 자리는 로드된 채로 유지하므로 경찰관 대화를 마치고 나오면 순찰차 출동을 그대로 봅니다. 매장 Builder가 기존 경찰서 프리팹의 여닫이 문 기능을 빼고 출입구 한 쌍을 더합니다.
- 거리 매장 천장에는 카메라를 막는 충돌이 있습니다. 위층이 없는 경찰서는 천장이 막지 않으면 내려다볼 때 카메라가 천장 위로 빠집니다(드럭스토어와 같은 문제). 매장 Builder는 충돌이 없는 기존 매장 프리팹에 충돌만 더합니다.

## 매장 실내 소품

`TownShopInteriorBuilder`는 거리 매장 6곳(경찰서 제외)과 [북쪽 상가](#북쪽-상가) 3동의 1·2층, 모두 12개 공간을 가구와 소품으로 채웁니다. 공간마다 묶음 프리팹 하나(`Prefabs/World/TownInteriors/<공간>Interior.prefab`)를 두고, 원점은 건물 앞면 바닥 가운데(문 z=0, 뒷벽 z=-7.8)입니다. 이 프리팹을 매장 건물 프리팹의 `Interior` 아래 그 층 높이(1층 0m, 2층 4m)에 끼웁니다. 매장 Builder 코드와 기존 가구(창구·이발 의자·세탁기 등)는 바꾸지 않습니다. 공간 안은 `Stations`·`Kitchen`·`Lobby`처럼 구역 묶음으로 나눕니다.

| 공간 프리팹 | 건물·층 | 더한 것 |
| --- | --- | --- |
| `BarberShopInterior` | 이발소 1층 | 이발대 서랍장·소독액 병·도구, 바퀴 도구함, 머리 건조기 의자, 샴푸 세면대와 의자, 수건·제품 선반, 가격판, 대기 의자·잡지 꽂이·코트걸이 |
| `BakeryInterior`, `BakeryLoungeInterior` | 빵집 1층, 2층 | 뒷벽 빵 선반, 진열대 조명·가격표, 창가 케이크 탁자, 계단참 아래 데크 오븐·쟁반 선반, 계단 아래 빵 상자·밀가루 포대 / 2층 깔개·의자·장식장·그림 |
| `MapleDinerInterior` | 다이너 1층 | 뒷벽 주방(냉장고·조리대·레인지와 후드), 소스·냅킨·버거, 부스 위 조명, 주크박스·껌 기계, 오늘의 파이 안내판 |
| `PostOfficeInterior` | 우체국 1층 | 우편 분류 칸, 우편 수레, 소포 더미, 줄서기 기둥, 양식 탁자, 파란 우체통, 게시판 |
| `TownBankInterior` | 은행 1층 | 창구 의자·화면, 금고 문, 서류함, 대기 깔개, 안내 책자 꽂이, 정수기, 금리 안내판 |
| `LaundromatInterior` | 빨래방 1층 | 빨래 바구니·세제, 벽 선반, 세제 자판기, 빨래 수레, 옷걸이, 게시판, 동전 교환기 |
| `FlowerShopInterior` | 꽃집 1층 | 벽 화분 선반, 천장에 건 화분, 꽃 냉장고, 포장지와 꽃다발, 꽃 통, 물뿌리개 |
| `ArcadeInterior`, `BookshopInterior` | 오락실 1층, Paper & Leaf 2층 | 오락기 3대와 의자, 경품 진열, 동전 교환기 / 책 선반·책장, 독서 의자와 스탠드, 계산 자리 |
| `HomeGoodsInterior`, `TeaRoomInterior` | 생활용품점 1층, Leaf Tea Room 2층 | 장식장·진열 탁자·스탠드·베개·깔개 / 찻주전자·찻잎 단지 선반·조명·깔개·메뉴판 |

- 소품은 `Prefabs/World/TownInteriors/Props/`에 있습니다. [KayKit 가구·주방 모델](../../ThirdParty/CREDITS.md#마을-매장-실내)을 감싼 래퍼는 원본의 0.7배(주방 가구는 벽에 붙도록 앞뒤 0.42배)로 줄였고, 받은 모델에 없는 것은 둥근 도형으로 만든 조각입니다. 조각은 재질별로 메시를 합쳐 `Art/World/TownInteriors/Meshes/`에 두고, 재질은 `Art/World/TownInteriors/`에 둡니다. Tiny Treats·북쪽 상가 소품·집 책장은 기존 프리팹을 그대로 씁니다.
- 바닥에 놓는 큰 가구에만 이동 충돌을 둡니다. 상자 윗면이 겉모양보다 뜨면 `PropColliderShape`로 메시 모양 충돌로 바꾸고, 선반·탁자 위에 올린 재사용 프리팹(빵·케이크 받침·인형·계산기)은 이동 충돌을 뺍니다. 문 앞 통로, 직원 자리, 계단과 계단 입구는 비웁니다.
- 호버 단어는 뜻이 맞는 기존 단어만 다시 씁니다(시계·그림·깔개·단지·레인지·냉장고·싱크대 등). 맞는 단어가 없는 소품(주크박스·오븐·빨래 바구니·옷걸이 등)은 단어를 비우므로, 기존 매장 소품처럼 가리키면 건물 단어(shop)가 뜹니다. 그 호버 영역은 마을 씬이 아니라 매장 건물 프리팹에 둡니다.
- **Tools → A305 → World → Build Town Shop Interiors**는 없는 프리팹만 만들고 아직 끼우지 않은 건물에 끼웁니다. **Rebuild Town Shop Interiors (Overwrite)**는 코드로 바꾼 배치를 반영하려고 공간·소품 프리팹을 같은 GUID로 덮어씁니다. 프리팹에서 손으로 고친 내용은 사라집니다. **Capture Town Shop Interiors**는 공간마다 문 안쪽 시점과 천장을 걷은 내려다보기, 비교용 카페·호텔 실내와 모음 두 장을 `artifacts/unity/town-interiors/`에 저장합니다.
- 큰 가구를 바꾼 뒤에는 **Npc → Build Building Staff**로 매장 직원이 다닐 바닥을 다시 굽고, **ObjectHover → Export Backend Data**로 장소 단어(`backend/src/main/resources/content/object-word-bindings.json`)를 다시 내보냅니다.
- 공간 묶음은 `Interior` 아래에 있으므로 Play·빌드 때 `ShopInteriorScenes`가 다른 실내 집기와 함께 건물별 실내 씬(`Scenes/ShopInteriors/<건물>Interior.unity`)으로 옮깁니다. 마을 씬에는 바닥·천장·계단만 남습니다. 편집은 지금처럼 공간 프리팹이나 매장 건물 프리팹에서 합니다.

## 확인 방법

### 북쪽 상가

북쪽 상가 3곳도 건물별 실내 씬을 사용합니다. 인도에서 Little Arcade의 열린 공용 계단실로 걸어 들어가 각 층의 기존 매장 문을 이용합니다. 계단과 바닥은 마을 씬에 남습니다.

| 기존 건물 앞 좌표 | 프리팹 | 층별 용도 | 출입 방식 |
| --- | --- | --- | --- |
| `(-7, 0.1, 21)` | `NorthBlock/MapleBakery` | 1층 베이커리, 2층 좌석 | 매장 안 복층 계단 |
| `(7, 0.1, 30)` | `NorthBlock/LittleArcade` | 1층 오락실, 2층 Paper & Leaf 서점 | 공용 계단실에서 각 층의 매장 문으로 진입 |
| `(-7, 0.1, 39)` | `NorthBlock/HomeLiving` | 1층 생활용품점, 2층 Leaf Tea Room | 1층 독립 출입구, 전면 외부 계단으로 2층 진입 |

`ShopInteriorScenes`는 Play 및 빌드 시 기존 건물 프리팹에서 실내 집기를 건물별 씬으로 분리합니다. `ShopDoorway`는 캐릭터 머리가 닫힌 문에 닿기 전에 몸통을 감지해 입·퇴장을 시작하고, 그 문이 여는 실내(`Room`)를 가집니다. 매장 직원은 방문 중에만 활동하며, 원본 프리팹에서 실내 배치를 편집할 수 있습니다. 새 매장은 **Tools → A305 → World → Register Shop Interior Scenes**로 빌드 목록에 등록합니다.

1층과 2층이 다른 가게인 건물(2층 높이에 따로 문이 있는 `LittleArcade`·`HomeLiving`)은 층마다 실내 씬을 나눕니다. 1층 문은 `<건물>Interior`, 2층 문은 `<건물>UpperInterior`를 열고, 각 씬에는 그 층 집기만 남깁니다(집기의 가장 낮은 곳이 건물 바닥에서 3.5m 이상이면 2층). 직원은 모두 1층에 있어 2층 가게를 방문할 때는 켜지지 않으므로, 바로 아래층 직원이 2층 손님에게 말을 걸지 않습니다. 매장 안 계단으로 오르는 복층(`MapleBakery`)은 한 씬입니다. 2층 문이 있는 건물을 새로 만들면 Register Shop Interior Scenes가 `UpperInterior` 씬도 만들어 등록합니다.

**Tools → A305 → World → Build North Block Interior Sandbox**로 `Scenes/Sandbox/NorthBlockInteriorSandbox.unity`를 준비합니다. 기존 프리팹 디자인을 유지하며 없는 프리팹만 만듭니다. **Set Up North Block Tenant Entrances**는 초안의 단일 매장 구조에 층별 출입 구조가 없을 때 한 번 적용합니다. 외부 계단이 이미 있는 건물에는 계단 앞 `TEA ROOM 2F >` 안내판(`TenantEntrances/StairSign`)을 기둥과 판에 붙여 다시 만듭니다. **Apply North Block Interiors**로 검토한 프리팹을 TownScene에 배치합니다. 기존 배경 인스턴스는 비활성 상태로 보존합니다. **Capture North Block Interiors**는 검토용 이미지를 `artifacts/unity/north-block/`에 저장합니다.

`SpaceTravelTests`는 이동 키로 원래 매장 문 12개를 양방향 통과하며 건물의 실내 씬 로드·해제를 확인합니다. `UpperFloorShopTests`는 2층 문(찻집·서점)이 2층 실내만 불러오고 1층 직원을 켜지 않는지, 층별 씬에 그 층 공간 묶음만 있는지 확인합니다. Unity에서 `TownScene`을 직접 Play할 때는 Ben 튜토리얼을 완료하거나 건너뛴 뒤 시험합니다. 서버 없이 Play하면 1일차 오전 8시에서 시작하므로, 9시 전에는 우체국·은행처럼 늦게 여는 매장 문이 열리지 않습니다([영업시간](#영업시간)).

`Assets/Scenes/Production/HomeScene.unity`를 열고 Play합니다. 방 남쪽 문으로 나가면 마을 남동쪽 집 앞에 서고, 큰길 서쪽의 `CAFE`와 동쪽의 `HOTEL` 건물 문으로 들어갈 수 있습니다. 실내 남쪽 문으로 나오면 그 건물 앞으로 돌아갑니다. 조작 방법은 [Player 모듈](../Player/README.md)을 따릅니다.

공간 씬을 바로 열고 Play하면 씬에 놓인 플레이어 위치에서 시작합니다.

씬과 Scene List는 Editor 메뉴 **Tools → A305 → World → Build Spaces**(`Scripts/Editor/World/SpaceSceneBuilder.cs`)로 준비합니다. 플레이어 프리팹은 **Tools → A305 → Player → Build Sandbox**로 먼저 만들어 둡니다.

- 씬이 없으면 도형으로 만든 대체 공간으로 새로 만들고, 이미 있으면 디자인을 건드리지 않고 기능 연결만 다시 맞춥니다.
- 대체 공간 프리팹과 머티리얼은 없을 때만 만듭니다. 디자인 에셋으로 바꾼 뒤에도 다시 실행할 수 있습니다.
- 출입구 프리팹(`Prefabs/World/SpaceDoorway.prefab`)은 감지 영역·`ArrivalPoint`·연결만 다시 맞추고, 문 모델을 두는 `Visual` 아래는 유지합니다.

이 기능은 공간 자체가 배포용 씬이라 Sandbox 씬 대신 배포용 씬의 Play와 PlayMode 테스트로 확인합니다.

## 새 공간 씬 만들기

디자인이 정해진 뒤 새 공간을 추가할 때도 기존 공간과 같은 방식으로 기능을 연결합니다.

1. `Assets/Scenes/Production/<장소>Scene.unity`로 씬을 만들고 디자인합니다. 지형·건물·소품은 `Environment` 그룹 아래에 둡니다. 그룹이 아직 없으면 3번 도구가 만들어 줍니다.
2. 들어오고 나가는 곳에 `Prefabs/World/SpaceDoorway.prefab`을 놓고 아래 [출입구 연결](#출입구-연결)대로 이어지는 공간과 짝을 맞춥니다.
3. 씬을 연 상태에서 **Tools → A305 → World → Set Up Open Scene**(`Scripts/Editor/World/SpaceSceneSetup.cs`)을 실행합니다. 다음을 적용하고 씬을 저장한 뒤 Scene List 끝에 추가합니다.
   - 규약 그룹(`Systems`·`Environment`·`Characters`·`UI`·`Lighting`)이 없으면 만듭니다.
   - 루트의 기본 카메라는 지우고 루트의 조명은 `Lighting`으로 옮깁니다.
   - 플레이어가 없으면 출입구 이름 순으로 첫 출입구 앞에 놓습니다. 이미 놓은 플레이어 위치는 유지합니다.
   - 플레이어 카메라와 도착 처리(`SpaceArrival`)를 붙이고 서로 연결합니다. 도착 처리에는 재접속 위치 저장용 `ApiSettings`도 연결합니다. 배포용 장소 씬 5개에 이 연결만 다시 맞출 때는 **Tools → A305 → World → Connect Last Scene Saving**을 씁니다.
   - 사물 호버 처리와 툴팁([ObjectHover 모듈](../ObjectHover/README.md))을 붙이고 플레이어·카메라와 연결합니다.
   - 버튼 입력을 받는 `EventSystem`, 대화 처리와 대화 화면([Dialogue 모듈](../Dialogue/README.md)), 행인 제안 화면([Npc 모듈](../Npc/README.md))을 붙이고, 씬에 놓인 행인을 플레이어·대화 처리와 연결합니다.
   - 피드백·단어장 화면과 학습 처리([Learning 모듈](../Learning/README.md))를 붙이고 플레이어·대화 처리·`ApiSettings`와 연결합니다.
   - 미션 창과 미션 처리([Mission 모듈](../Mission/README.md))를 붙이고 대화 처리와 연결합니다.
   - 튜토리얼 안내 카드와 진행 처리([Tutorial 모듈](../Tutorial/README.md))를 붙이고, 위 기능들과 씬에 놓인 행인을 연결합니다.
   - 영업시간 처리와 화면 위 안내판([영업시간](#영업시간))을 붙이고 플레이어·영업시간 파일과, 마을로 나가는 출입구가 있으면 그 출입구와 연결합니다.
4. 루트에 그룹 밖 객체가 있으면 경고가 나옵니다. 지형·건물·소품은 `Environment`, NPC는 `Characters`처럼 규약 그룹 아래로 옮깁니다.
5. PlayMode 테스트를 실행합니다. Scene List의 모든 씬에 플레이어·카메라·도착 처리가 하나씩 있는지, 출입구 짝이 맞는지 같은 기준으로 확인합니다.

도구는 여러 번 실행해도 기능 객체를 다시 만들지 않고, 디자인한 객체와 위치를 바꾸지 않습니다. 디자인을 고친 뒤 다시 실행해도 됩니다.

## 출입구 연결

1. 두 공간에 `Prefabs/World/SpaceDoorway.prefab`을 하나씩 배치합니다. 앞쪽(+z, `ArrivalPoint` 방향)이 플레이어가 서는 쪽입니다. 문 모델은 프리팹의 `Visual` 아래에서 바꿉니다.
2. 각 출입구의 `Doorway Id`, 이동할 씬(`Destination Scene`), 그 씬에서 나타날 출입구(`Destination Doorway Id`)를 서로 가리키게 입력합니다.
3. `ArrivalPoint`는 문 앞 감지 영역 밖에 둡니다. 감지 영역 안에 두면 도착하자마자 다시 이동합니다.

마을 출입문(집·카페·호텔·드럭스토어)에서는 문을 등지고 문 앞 인도 1m에 섭니다. 감지 영역은 문에서 0.6m 깊이까지 둡니다. 선 자리의 몸(반지름 0.33m)은 문 앞 0.67m까지만 와서 감지 영역에 닿지 않습니다. 문 쪽으로 걸으면 몸통보다 큰 머리(가장 큰 캐릭터 반지름 0.68m, `HeadClearance`)가 문짝에 먼저 닿아 몸이 0.475m에서 멈추므로, 감지 영역은 그보다 넉넉히 깊어야 들어갑니다. **Tools → A305 → World → Apply Town Door Arrival**(`TownProductionBuilder.ApplyDoorArrival`)이 TownScene의 출입문을 이 기준으로 맞추고, EditMode 테스트 `TownDoorArrivalTests`가 같은 기준을 검사합니다.

## 영업시간

장소마다 게임 시각 기준 영업시간이 있습니다. `Data/World/PlaceHours.json`은 `ai/tools/npc_life_export.py`가 NPC 생활 규칙에서 만들며, 장소 코드마다 늘 여는지(`alwaysOpen`)와 여는 시각·닫는 시각(`"HH:MM"`)을 둡니다. 여는 시각부터 열고 닫는 시각부터 닫습니다. 닫는 시각이 여는 시각보다 이르면 자정을 넘겨 다음 날 닫습니다. 파일에 없는 장소(플레이어의 집 `HOME`)는 늘 열려 있고, 마을(`TOWN`)·호텔·드럭스토어는 늘 엽니다.

- **들어가기 거절**: 영업시간이 아닌 장소의 문에 닿으면 들어가지 않고 화면 위 가운데에 "카페는 지금 영업시간이 아니에요 · 07:00–21:00"을 3초 띄웁니다. 카페·호텔·드럭스토어 문(`SpaceDoorway`)은 이동할 씬의 장소 코드(`PlaceScenes`)로, 마을 매장 문(`ShopDoorway`, 경찰서의 `InteriorDoorway`)은 들어갈 때 그 건물 `ShopInterior`의 장소 코드로 판정합니다. 나오는 문은 막지 않습니다. 튜토리얼이 이동을 잠근 동안에는 튜토리얼의 잠금 안내가 먼저 나옵니다.
- **영업 마감**: 머무는 장소가 닫히면 그때부터 게임 시간으로 30분(`ClosingCountdown.GraceMinutes`)을 세며 "영업이 끝났어요. N분 후 마을로 이동합니다"를 띄웁니다. 게임 시계를 따르므로 대화·튜토리얼로 시계가 멈춘 동안에는 줄지 않습니다. 그 사이 다시 열거나 장소를 나오면 안내를 끕니다. 남은 시간이 다 지나면 공간 씬은 그 씬의 마을 출입구(`exitToTown`)로 마을의 그 장소 문 앞에, 마을 매장은 플레이어와 가장 가까운 매장 문 밖에 세웁니다. 튜토리얼 잠금과 상관없이 옮기지만, 대화 중처럼 플레이어가 움직일 수 없는 동안에는 기다렸다가 옮깁니다.
- **마을 매장의 장소 코드**: Play·빌드 때 `ShopInteriorScenes`가 `ShopInterior`를 붙이면서 장소 코드와 안내 이름을 넣습니다. 거리 매장은 `TownStorefrontBuilder.Storefronts`의 `PlaceCode`·`KoreanName`(택시 목적지와 같은 코드)을, 북쪽 상가는 1층 매장(`BAKERY` 베이커리, `ARCADE` 오락실, `HOME_STORE` 생활용품점)을 씁니다. 같은 건물 2층의 서점·찻집은 그 건물의 영업시간을 따릅니다. 새 매장 건물을 더하면 이 코드도 함께 더합니다.
- **연결**: 장소 씬 5곳에는 **Tools → A305 → World → Connect Opening Hours**(`OpeningHoursBuilder`)가 `Systems/OpeningHours`와 `UI/HudNotice`를 하나씩 두고 플레이어·영업시간 파일·마을 출입구를 연결합니다. 이 메뉴는 영업시간 연결만 바꾸도록 사물 단어 등록 없이 씬을 저장합니다. 새로 꾸미는 씬은 **Set Up Open Scene**이 함께 붙입니다.

## 테스트

메인스트리트의 건물·캐릭터 크기와 확장 거리는 `Assets/Scenes/Sandbox/TownMainstreetSandbox.unity`에서 별도로 확인합니다. **Tools → A305 → World → Build Mainstreet Sandbox**로 만들며, 전용 데이터 에셋과 비율 대체물을 사용합니다. 배포 씬과 공용 플레이어 설정은 바꾸지 않습니다. [배치·수치 조정·검수 방법](../../../../../docs/unity/mainstreet-sandbox.md)을 따릅니다.

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | 택시 목적지와 경찰 신고 판별(장소 하나·여럿·없음, 부정·가정·길 잃음은 신고 아님), 순찰차 출동 단계(주차 → 출동·순찰 → 장소 앞 대기 → 복귀, 나가 있을 때 새 신고 거절, 경광등), 대화 중 신고 접수와 나중에 말한 장소, 마을 매장 천장이 카메라를 막는지, 진입로 곡선의 시작·끝 방향과 고른 걸음, 주차 트럭을 비켜 가는 곳이 모두 한 대씩 구간 안인지. 차가 도는 길의 위치·방향·유턴·한 바퀴, 정지 지점에서 서기와 기다리기, 앞을 막은 사람 앞에서만 서기, 신호 주기, 택시 세우기 조건(차로 쪽 인도·앞쪽·유턴 전)과 기다림·출발, 마을 씬의 달리는 차·신호등·택시 기사 연결. 이동 중 중복 요청 거절, 도착 출입구와 카메라 각도 전달, 이동 취소 후 재이동. 새 씬에 기능 연결 도구를 적용했을 때 그룹·플레이어·카메라·도착 처리가 하나씩 붙고 연결되는지, 다시 실행해도 늘지 않고 디자인과 플레이어 위치가 유지되는지. 장소 코드 변환과 재접속 위치 저장·실패 처리, 배포 장소 씬 도착 처리의 `ApiSettings` 연결. 배포 씬 사물 이동 Collider 윗면이 겉모양보다 뜨지 않는지, 뜬 상자를 찾고 메시 모양 Collider로 바꾸는 도구(`PropColliderShape`). 거리 매장 9곳 1층에 직원이 있고 구역이 그 건물 1층(길·2층 제외)인지, 직원마다 색을 바꾼 몸·눈 머티리얼을 쓰는지(`BuildingStaffTests`), 색 범위에 든 픽셀만 바꾸고 명도·투명도를 지키는지(`NpcColorSwapTests`). 영업시간 판정(여는·닫는 시각 경계, 늘 여는 장소, 파일에 없는 장소, 자정을 넘기는 영업, 잘못된 시각), 영업 마감 후 남은 시간(30분부터 게임 분 단위로 줄기, 남은 조각은 올림, 다시 열면 멈춤, 장소를 옮기면 처음부터), 안내 문구의 은/는, 잠금과 상관없이 내보내는 이동 시작. 장소 씬 5곳의 영업시간 처리·안내판 연결과 카페·호텔·드럭스토어의 마을 출입구, 마을 매장 10곳의 장소 코드가 영업시간 파일에 있는지(`OpeningHoursConnectionTests`) |
| `A305.World.PlayModeTests` | `Assets/Tests/PlayMode/World/` | Scene List의 모든 씬을 실제로 불러와 플레이어·카메라·도착 처리가 하나씩 있는지, 출입구 짝이 맞는지, 문에 닿으면 이동하는지, 도착 위치와 카메라 방향, 뒤로 걸어 문을 지나도 도착 후 같은 키로 문에서 멀어지는지, 씬이 바뀌는 순간 화면이 덮여 있고 도착 뒤 걷히는지, 옮기는 동안 그리는 카메라가 비지 않는지, 덮인 동안만 불러오기 우선순위를 올리는지, 이동 뒤 도착 씬만 남는지, 새 씬이 이전 공간의 플레이어·카메라 없이 깨어나는지, 마을에 걸어 들어와도 바로 열었을 때와 날씨 연출이 같은지. 닫힌 카페·우체국 문에 닿으면 들어가지 않고 안내가 뜨는지, 카페·우체국에 머무는 동안 영업이 끝나면 남은 시간을 보이다 마을의 카페 문 앞·우체국 문 밖으로 옮기는지(`OpeningHoursTests`). 시각에 따라 문이 달라지므로 이 어셈블리의 시험은 `TestGameClock`으로 멈춘 게임 시각을 맞춰 시작합니다(`SpaceTravelTests`는 모든 장소가 여는 오후 1시) |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.

## 거리·교통 구성

**Tools → A305 → World → Apply American Street Details**는 차선·횡단보도·신호등과 거리 소품을, **Apply Town Street Life**는 차량·생활 소품·택시 기사를 TownScene에 구성합니다. 기존 묶음이 있으면 배치를 유지합니다. 외부 모델과 출처는 `Assets/ThirdParty/CREDITS.md`에 기록합니다.

`CarLoopRoute`는 순환 길과 정차 위치를, `TrafficSignalCycle`은 신호 주기를 계산합니다. 경찰서 순찰차는 [경찰서와 순찰차](#경찰서와-순찰차)를 따릅니다. `LoopingCar`는 신호·장애물에 맞춰 정지하고, `TaxiFare`와 `TaxiRide`는 인도에서 택시를 세워 기사 Joe(`TAXI_DRIVER`)와 대화하는 흐름을 맡습니다. 뒷좌석 승객 시점과 목적지 이동·하차는 [택시 안내](../../../../../docs/unity/taxi-ride.md)를 따릅니다.
