# World 모듈

공간 사이 이동을 담당합니다. 튜토리얼을 시작하는 내 방(`HomeScene`)에서 마을(`TownScene`)로 나가고, 마을에서 카페(`CafeScene`)·숙소(`HotelScene`)·상점(`ShopScene`)에 드나듭니다. 공간마다 씬을 하나씩 두고, 출입구에 닿으면 연결된 씬을 불러와 그 씬의 출입구 앞에 플레이어를 세웁니다.

이 서비스의 공간 구성을 다루는 게임 전용 기능입니다. 플레이어 이동·카메라는 재사용 모듈인 `A305.Player`의 공개 API로 요청합니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `SpaceTravelState` | 이동 요청을 한 번에 하나만 받고, 다음 공간에서 나타날 출입구와 카메라 각도를 한 번만 넘겨줍니다. Unity 객체 없이 테스트합니다. |
| `SpaceTravel` | 씬을 불러와 공간을 옮깁니다. 씬이 바뀌어도 도착할 출입구와 카메라 각도가 남도록 정적으로 둡니다. |
| `SpaceTravelCurtain` | 공간을 옮기는 동안 화면을 검게 덮습니다(0.2초에 걸쳐 덮고 0.3초에 걸쳐 걷음). 덮는 동안 다음 씬을 미리 불러오고, 화면이 다 덮인 뒤에 씬을 바꿔 씬이 바뀌는 순간의 끊김을 가립니다. 덮여 있는 동안에는 `Application.backgroundLoadingPriority`를 `High`로 올려 불러오기에 프레임 시간을 더 쓰고, 도착하면 되돌립니다. 씬이 바뀌어도 남도록 처음 이동할 때 만들어 둡니다. |
| `SpaceDoorway` | 다른 공간으로 이어지는 출입구입니다. 플레이어가 문 앞 감지 영역에 들어오면 문으로 들어가는 방향 기준의 카메라 각도를 넘기며 이동하고, 다른 공간에서 들어오면 `ArrivalPoint`에 문을 등지고 섭니다. |
| `SpaceArrival` | 공간 씬에 들어오면 플레이어를 도착 출입구 앞으로 옮기고, 문에 들어갈 때의 카메라 각도를 나오는 방향 기준으로 이어 맞춥니다. 앞으로 걸어 들어오면 카메라가 플레이어 뒤에, 뒤로 걸어 들어오면 문을 바라보는 쪽에 와서 누르던 키로 계속 문에서 멀어집니다. 장소 씬이면 재접속 위치로 저장합니다(WORLD-05). `apiSettings`가 비어 있으면 저장하지 않습니다. |
| `CarLoopRoute` | 마을 본길을 도는 차의 길. 동쪽 차로로 북쪽 끝까지 가서 유턴하고 서쪽 차로로 내려와 다시 유턴합니다. 길 위 거리로 위치·방향을 구하고, 정지 지점 앞에서 서며, 차 앞을 막은 사람이 있으면 설지 정합니다. Unity 객체 없이 테스트합니다. |
| `TrafficSignalCycle`, `TrafficLights`, `TrafficSignalHead` | 교차로 신호 주기(본길 초록 12초·노랑 3초, 가로길 초록 7초·노랑 3초)와 신호등 렌즈 켜기. 주기는 `TrafficLights` Inspector에서 조절합니다. |
| `LoopingCar` | 본길을 도는 차. 본길 신호가 초록이 아니면 교차로 정지선에서 서고, 앞을 사람이나 다른 차가 막으면 섭니다. 택시처럼 손님 옆에 설 때는 설 자리를 받습니다. |
| `TaxiFare` | 택시를 세우고 보내는 흐름. 인도에서 다가오는 택시만 세울 수 있고, 손님 옆에 서서 기다리다 대화를 마치거나 손님이 떠나면 다시 달립니다. Unity 객체 없이 테스트합니다. |
| `LastSceneRecorder`, `ILastSceneApi`, `LastSceneApi` | Unity 씬 이름을 서버 장소 코드(`HomeScene`→`HOME`, `TownScene`→`TOWN`, `CafeScene`→`CAFE`, `HotelScene`→`HOTEL`, `ShopScene`→`SHOP`)로 바꿔 `PUT /api/v1/me/last-scene`으로 저장합니다. 장소가 아닌 씬은 저장하지 않고, 실패해도 예외 없이 결과로 알립니다. 새 장소 씬을 추가하면 이 표와 서버 시드(`scenes`)에 함께 추가합니다. Unity 객체 없이 테스트합니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Api` | `ApiClient`, `ApiSettings`, `ApiException` |
| `A305.Player` | `PlayerMover.Teleport`, `PlayerMover.SetMovementLocked`, `PlayerCameraRig.ViewYawDegrees`, `PlayerCameraRig.SnapView` |

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

상점 실내는 **Tools → A305 → World → Apply Drugstore Interior**(`DrugstoreInteriorBuilder`)가 미국 드럭스토어처럼 꾸밉니다. 음료 냉장고·선반·우산 통 같은 조각 프리팹과 `DrugstoreInterior` 프리팹은 `Assets/Prefabs/World/Drugstore/`에 없을 때만 만들고, 이미 있으면 고친 디자인을 그대로 둡니다. 외부 모델은 `Assets/ThirdParty/`의 원본을 그대로 쓰고 URP 재질은 `Assets/Art/World/Drugstore/Materials/`에 만듭니다.

마을 거리는 두 Builder가 미국 소도시처럼 꾸밉니다.

- **Tools → A305 → World → Apply American Street Details**(`AmericanStreetBuilder`): 카페 앞 횡단보도와 교차로에서 끊기던 중앙선을 정지선까지 잇고, 교차로 네 방향에 횡단보도·정지선을, 가로길에 중앙선을 그립니다. 교차로 모퉁이 두 곳에 MAPLE AVE 이름판을 단 신호등을 세우고, 길가에 속도 제한 25 표지판·버스 정류장·주차 미터기·신문 판매함·파란 우체통·보행자 횡단 표지판을 둡니다. 성조기는 호텔 외벽에 하나만 답니다.
- **Tools → A305 → World → Apply Town Street Life**(`TownStreetLifeBuilder`): 집 남쪽 잔디의 아이스크림 트럭 광장, 카페 앞 파라솔 테라스, 본길을 도는 택시·승용차와 교차로 신호, 자전거 거치대, 가로수 밑 꽃과 건물 앞 꽃덤불, 공원의 여우·비둘기와 집 앞 고양이를 둡니다.

승용차는 카페 앞 횡단보도 북쪽(z=-6)과 본길 북쪽 끝(z=66) 사이를 돕니다. 택시는 집까지 이동하도록 남쪽 끝을 z=-25까지 넓히고, 남행 중 기존 주차 트럭을 천천히 비켜갑니다. 두 차는 교차로 신호를 지키고, 택시는 인도에서 [택시 세우기]로 세우면 뒷좌석에 타서 택시 기사(`TAXI_DRIVER`)와 대화할 수 있습니다. 목적지 10곳과 승하차 흐름은 [택시 안내](../../../../../docs/unity/taxi-ride.md)에 있습니다.

두 Builder는 `TownMainstreet` 아래 `AmericanStreetDetails`·`StreetLife` 묶음이 이미 있으면 배치를 바꾸지 않습니다. 조각 프리팹은 각각 `Assets/Prefabs/World/MainstreetArt/`, `Assets/Prefabs/World/StreetLife/`에 없을 때만 만들고, 새 사물의 공용 단어(신호등·택시·자전거 등)는 뜻·예문과 함께 없을 때만 만듭니다.

- 북쪽 기존 빈 건물 세 동은 아래의 층별 상가로 사용합니다. 나머지 배경 주거동은 닫힌 외관을 유지합니다. 식당·공항의 도형 외관과 임시 간판은 현재 배치에서 사용하지 않습니다.
- 씬 이름은 ERD `scenes.unity_key` 값으로 사용합니다. 공간 이름·개방 여부 같은 서버 데이터는 Unity에 두지 않습니다.
- TownScene 외관은 `Prefabs/World/TownProduction`과 `CafeBlenderKit`을 사용합니다. HomeScene은 `Prefabs/Home/CozyCottage`의 실내 외형을 사용하며 기존 가구 사물 키와 문 연결을 유지합니다. 카페·호텔의 실내는 도형 대체 공간을 유지합니다. 아래 Builder를 다시 실행해도 기존 디자인을 덮어쓰지 않습니다.

## 북쪽 거리 매장

`TownStorefrontBuilder`는 기존 배경 건물 6곳을 간판과 실내가 있는 매장으로 구성합니다. 모두 TownScene 안에서 직접 걸어 들어가며, 문은 접근하면 열립니다. 매장 직원 NPC나 결제 기능은 포함하지 않습니다.

| 프리팹 | 간판 | 건물 앞 위치(x, z) |
| --- | --- | --- |
| `PostOffice` | POST OFFICE | (-7, 30) |
| `MapleDiner` | MAPLE DINER | (7, 39) |
| `TownBank` | TOWN BANK | (-7, 48) |
| `SudsLaundromat` | SUDS LAUNDROMAT | (7, 48) |
| `BloomFlowers` | BLOOM FLOWERS | (-7, 57) |
| `MainStreetBarber` | MAIN ST. BARBER | (7, 57) |

**Build Town Storefronts Sandbox**로 검토용 씬을 만들고 **Apply Town Storefronts To Town**으로 TownScene에 적용합니다(두 메뉴 모두 **Tools → A305 → World**). 프리팹은 없을 때만 생성하며 기존 디자인은 유지합니다. 원래 배경 건물은 비활성으로 보존합니다. 적용 후 **Apply Taxi Cabin**으로 목적지를 연결하고 **ObjectHover → Register All Scene Words**로 단어 등록과 백엔드 내보내기를 완료합니다. 새 사물 단어는 사서함·소포·스툴·ATM·세탁기·건조기·꽃·이발 의자입니다.

## 확인 방법

### 북쪽 상가

북쪽 상가는 `TownScene` 안에서 직접 걷는 실내입니다. 씬 이동이나 순간 이동 없이 계단을 오르내립니다. 기존 건물의 위치·높이·주요 외관을 유지하는 Prefab Variant를 사용하며, 호텔 앞 드럭스토어 구역은 이 Builder의 대상이 아닙니다.

| 기존 건물 앞 좌표 | 프리팹 | 층별 용도 | 출입 방식 |
| --- | --- | --- | --- |
| `(-7, 0.1, 21)` | `NorthBlock/MapleBakery` | 1층 베이커리, 2층 좌석 | 매장 안 복층 계단 |
| `(7, 0.1, 30)` | `NorthBlock/LittleArcade` | 1층 오락실, 2층 Paper & Leaf 서점 | 공용 계단실에서 각 층의 매장 문으로 진입 |
| `(-7, 0.1, 39)` | `NorthBlock/HomeLiving` | 1층 생활용품점, 2층 Leaf Tea Room | 1층 독립 출입구, 전면 외부 계단으로 2층 진입 |

`ShopSwingDoor`는 CharacterController가 문 앞에 접근하면 문짝을 열고, 감지 영역에서 나가면 닫습니다. 별도 키 입력은 필요하지 않습니다. 층별 문에는 씬 전환 Trigger를 사용하지 않습니다. 오락기·서점·찻집은 공간과 사물 학습을 위한 배치이며, 결제·오락기 플레이·상점 NPC 미션은 포함하지 않습니다.

**Tools → A305 → World → Build North Block Interior Sandbox**로 `Scenes/Sandbox/NorthBlockInteriorSandbox.unity`를 준비합니다. 기존 프리팹 디자인을 유지하며 없는 프리팹만 만듭니다. **Set Up North Block Tenant Entrances**는 초안의 단일 매장 구조에 층별 출입 구조가 없을 때 한 번 적용합니다. **Apply North Block Interiors**로 검토한 프리팹을 TownScene에 배치합니다. 기존 배경 인스턴스는 비활성 상태로 보존합니다. **Capture North Block Interiors**는 검토용 이미지를 `artifacts/unity/north-block/`에 저장합니다.

통행 검사는 실제 캐릭터의 최대 반지름과 계단 허용 높이를 사용하는 `NorthBlockTraversalTests`, 문 개폐는 `ShopSwingDoorTests`에서 확인합니다.

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
4. 루트에 그룹 밖 객체가 있으면 경고가 나옵니다. 지형·건물·소품은 `Environment`, NPC는 `Characters`처럼 규약 그룹 아래로 옮깁니다.
5. PlayMode 테스트를 실행합니다. Scene List의 모든 씬에 플레이어·카메라·도착 처리가 하나씩 있는지, 출입구 짝이 맞는지 같은 기준으로 확인합니다.

도구는 여러 번 실행해도 기능 객체를 다시 만들지 않고, 디자인한 객체와 위치를 바꾸지 않습니다. 디자인을 고친 뒤 다시 실행해도 됩니다.

## 출입구 연결

1. 두 공간에 `Prefabs/World/SpaceDoorway.prefab`을 하나씩 배치합니다. 앞쪽(+z, `ArrivalPoint` 방향)이 플레이어가 서는 쪽입니다. 문 모델은 프리팹의 `Visual` 아래에서 바꿉니다.
2. 각 출입구의 `Doorway Id`, 이동할 씬(`Destination Scene`), 그 씬에서 나타날 출입구(`Destination Doorway Id`)를 서로 가리키게 입력합니다.
3. `ArrivalPoint`는 문 앞 감지 영역 밖에 둡니다. 감지 영역 안에 두면 도착하자마자 다시 이동합니다.

## 테스트

메인스트리트의 건물·캐릭터 크기와 확장 거리는 `Assets/Scenes/Sandbox/TownMainstreetSandbox.unity`에서 별도로 확인합니다. **Tools → A305 → World → Build Mainstreet Sandbox**로 만들며, 전용 데이터 에셋과 비율 대체물을 사용합니다. 배포 씬과 공용 플레이어 설정은 바꾸지 않습니다. [배치·수치 조정·검수 방법](../../../../../docs/unity/mainstreet-sandbox.md)을 따릅니다.

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | 차가 도는 길의 위치·방향·유턴·한 바퀴, 정지 지점에서 서기와 기다리기, 앞을 막은 사람 앞에서만 서기, 신호 주기, 택시 세우기 조건(차로 쪽 인도·앞쪽·유턴 전)과 기다림·출발, 마을 씬의 달리는 차·신호등·택시 기사 연결. 이동 중 중복 요청 거절, 도착 출입구와 카메라 각도 전달, 이동 취소 후 재이동. 새 씬에 기능 연결 도구를 적용했을 때 그룹·플레이어·카메라·도착 처리가 하나씩 붙고 연결되는지, 다시 실행해도 늘지 않고 디자인과 플레이어 위치가 유지되는지. 장소 코드 변환과 재접속 위치 저장·실패 처리, 배포 장소 씬 도착 처리의 `ApiSettings` 연결 |
| `A305.World.PlayModeTests` | `Assets/Tests/PlayMode/World/` | Scene List의 모든 씬을 실제로 불러와 플레이어·카메라·도착 처리가 하나씩 있는지, 출입구 짝이 맞는지, 문에 닿으면 이동하는지, 도착 위치와 카메라 방향, 뒤로 걸어 문을 지나도 도착 후 같은 키로 문에서 멀어지는지, 씬이 바뀌는 순간 화면이 덮여 있고 도착 뒤 걷히는지, 덮인 동안만 불러오기 우선순위를 올리는지 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.

## 거리·교통 구성

**Tools → A305 → World → Apply American Street Details**는 차선·횡단보도·신호등과 거리 소품을, **Apply Town Street Life**는 차량·생활 소품·택시 기사를 TownScene에 구성합니다. 기존 묶음이 있으면 배치를 유지합니다. 외부 모델과 출처는 `Assets/ThirdParty/CREDITS.md`에 기록합니다.

`CarLoopRoute`는 순환 길과 정차 위치를, `TrafficSignalCycle`은 신호 주기를 계산합니다. `LoopingCar`는 신호·장애물에 맞춰 정지하고, `TaxiFare`와 `TaxiRide`는 인도에서 택시를 세워 기사 Joe(`TAXI_DRIVER`)와 대화하는 흐름을 맡습니다. 뒷좌석 승객 시점과 목적지 이동·하차는 [택시 안내](../../../../../docs/unity/taxi-ride.md)를 따릅니다.
