# World 모듈

공간 사이 이동을 담당합니다. 튜토리얼을 시작하는 내 방(`HomeScene`)에서 마을(`TownScene`)로 나가고, 마을에서 카페(`CafeScene`)와 숙소(`HotelScene`)에 드나듭니다. 공간마다 씬을 하나씩 두고, 출입구에 닿으면 연결된 씬을 불러와 그 씬의 출입구 앞에 플레이어를 세웁니다.

이 서비스의 공간 구성을 다루는 게임 전용 기능입니다. 플레이어 이동·카메라는 재사용 모듈인 `A305.Player`의 공개 API로 요청합니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `SpaceTravelState` | 이동 요청을 한 번에 하나만 받고, 다음 공간에서 나타날 출입구를 한 번만 넘겨줍니다. Unity 객체 없이 테스트합니다. |
| `SpaceTravel` | 씬을 불러와 공간을 옮깁니다. 씬이 바뀌어도 도착할 출입구가 남도록 정적으로 둡니다. |
| `SpaceDoorway` | 다른 공간으로 이어지는 출입구입니다. 플레이어가 문 앞 감지 영역에 들어오면 이동하고, 다른 공간에서 들어오면 `ArrivalPoint`에 문을 등지고 섭니다. |
| `SpaceArrival` | 공간 씬에 들어오면 플레이어를 도착 출입구 앞으로 옮기고 카메라를 플레이어 뒤로 맞춥니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Player` | `PlayerMover.Teleport`, `PlayerMover.SetMovementLocked`, `PlayerCameraRig.SnapBehindTarget` |

## 공간과 출입구

씬은 `Assets/Scenes/Production/`에 있고 Scene List에 이 순서로 등록되어 `HomeScene`에서 시작합니다. 출입구 이름은 이어지는 공간 + `Door`로 짓습니다.

| 씬 | 출입구 | 이어지는 곳 |
| --- | --- | --- |
| `HomeScene` | `TownDoor` | `TownScene`의 `HomeDoor` |
| `TownScene` | `HomeDoor` | `HomeScene`의 `TownDoor` |
| `TownScene` | `CafeDoor` | `CafeScene`의 `TownDoor` |
| `TownScene` | `HotelDoor` | `HotelScene`의 `TownDoor` |
| `CafeScene` | `TownDoor` | `TownScene`의 `CafeDoor` |
| `HotelScene` | `TownDoor` | `TownScene`의 `HotelDoor` |

- 현재 TownScene의 이용 장소는 집·카페·호텔입니다. 빈 건물은 셔터가 닫힌 외관으로, 나머지는 배경 주거동으로 구분하며 출입 Trigger를 두지 않습니다. 식당·공항의 도형 외관과 임시 간판은 현재 배치에서 사용하지 않습니다.
- 씬 이름은 ERD `scenes.unity_key` 값으로 사용합니다. 공간 이름·개방 여부 같은 서버 데이터는 Unity에 두지 않습니다.
- TownScene 외관은 `Prefabs/World/TownProduction`과 `CafeBlenderKit`을 사용합니다. HomeScene은 `Prefabs/Home/CozyCottage`의 실내 외형을 사용하며 기존 가구 사물 키와 문 연결을 유지합니다. 카페·호텔의 실내는 도형 대체 공간을 유지합니다. 아래 Builder를 다시 실행해도 기존 디자인을 덮어쓰지 않습니다.

## 확인 방법

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
   - 플레이어 카메라와 도착 처리(`SpaceArrival`)를 붙이고 서로 연결합니다.
   - 사물 호버 처리와 툴팁([ObjectHover 모듈](../ObjectHover/README.md))을 붙이고 플레이어·카메라와 연결합니다.
   - 버튼 입력을 받는 `EventSystem`, 대화 처리와 대화 화면([Dialogue 모듈](../Dialogue/README.md)), 행인 제안 화면([Npc 모듈](../Npc/README.md))을 붙이고, 씬에 놓인 행인을 플레이어·대화 처리와 연결합니다.
   - 피드백·단어장 화면과 학습 처리([Learning 모듈](../Learning/README.md))를 붙이고 플레이어·대화 처리와 연결합니다.
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
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | 이동 중 중복 요청 거절, 도착 출입구 전달, 이동 취소 후 재이동. 새 씬에 기능 연결 도구를 적용했을 때 그룹·플레이어·카메라·도착 처리가 하나씩 붙고 연결되는지, 다시 실행해도 늘지 않고 디자인과 플레이어 위치가 유지되는지 |
| `A305.World.PlayModeTests` | `Assets/Tests/PlayMode/World/` | Scene List의 모든 씬을 실제로 불러와 플레이어·카메라·도착 처리가 하나씩 있는지, 출입구 짝이 맞는지, 문에 닿으면 이동하는지, 도착 위치와 카메라 방향 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
