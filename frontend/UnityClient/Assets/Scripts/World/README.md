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

- 마을의 식당·공항은 건물과 `COMING SOON` 간판만 있고 들어갈 수 없습니다.
- 씬 이름은 ERD `scenes.unity_key` 값으로 사용합니다. 공간 이름·개방 여부 같은 서버 데이터는 Unity에 두지 않습니다.
- 공간은 도형으로 만든 대체 공간입니다. 디자인 에셋이 정해지면 `Prefabs/Home`·`Prefabs/Town`·`Prefabs/Cafe`·`Prefabs/Hotel`의 공간 프리팹 안을 교체합니다.

## 확인 방법

`Assets/Scenes/Production/HomeScene.unity`를 열고 Play합니다. 방 남쪽 문으로 나가면 마을의 집 앞에 서고, 큰길 동쪽의 `CAFE`와 북서쪽의 `HOTEL` 건물 문으로 들어갈 수 있습니다. 실내 남쪽 문으로 나오면 그 건물 앞으로 돌아갑니다. 조작 방법은 [Player 모듈](../Player/README.md)을 따릅니다.

공간 씬을 바로 열고 Play하면 씬에 놓인 플레이어 위치에서 시작합니다.

씬·공간 프리팹·출입구 프리팹·머티리얼과 Scene List는 Editor 메뉴 **Tools → A305 → World → Build Spaces**(`Scripts/Editor/World/SpaceSceneBuilder.cs`)로 다시 만듭니다. 플레이어 프리팹은 **Tools → A305 → Player → Build Sandbox**로 먼저 만들어 둡니다. 다시 만들면 씬·프리팹·머티리얼을 덮어쓰므로, Editor에서 고친 내용은 Builder 코드에도 반영합니다.

이 기능은 공간 자체가 배포용 씬이라 Sandbox 씬 대신 배포용 씬의 Play와 PlayMode 테스트로 확인합니다.

## 출입구 추가

1. 두 공간에 `Prefabs/World/SpaceDoorway.prefab`을 하나씩 배치합니다. 앞쪽(+z, `ArrivalPoint` 방향)이 플레이어가 서는 쪽입니다.
2. 각 출입구의 `Doorway Id`, 이동할 씬(`Destination Scene`), 그 씬에서 나타날 출입구(`Destination Doorway Id`)를 서로 가리키게 입력합니다.
3. 새 씬은 Scene List에 추가합니다.
4. 배치를 `SpaceSceneBuilder`에도 반영하고, PlayMode 테스트의 공간 목록에 새 씬을 추가합니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | 이동 중 중복 요청 거절, 도착 출입구 전달, 이동 취소 후 재이동 |
| `A305.World.PlayModeTests` | `Assets/Tests/PlayMode/World/` | 배포용 씬을 실제로 불러와 출입구 연결이 서로 맞는지, 문에 닿으면 이동하는지, 도착 위치와 카메라 방향 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
