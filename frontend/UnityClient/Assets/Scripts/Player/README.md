# Player 모듈

PC 웹의 키보드·마우스와 모바일 웹의 터치로 플레이어 이동과 카메라 시점을 조작합니다. 탐험 중에는 3인칭·1인칭을 오가고, NPC와 대화할 때는 1·3인칭과 따로 NPC 얼굴 앞 정해진 거리·높이에서 얼굴을 비추는 대화 시점으로 고정한 뒤 대화가 끝나면 직전 시점으로 돌아갑니다. 튜토리얼에서 다가오는 NPC를 기다리는 동안은 플레이어 어깨 너머로 NPC를 비추는 구도로 붙잡습니다.

다른 프로젝트에서도 쓸 수 있도록 게임 진행, 서버 연동, NPC·미션 같은 게임 코드를 참조하지 않습니다. 대화 시작·종료처럼 게임이 정하는 일은 아래 공개 API로 요청받습니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `PlayerMotor` | 이동 입력과 카메라 방향으로 이번 프레임의 속도를 계산합니다. 달리기, 점프, 중력, 이동 잠금을 포함하며 Unity 객체 없이 테스트합니다. |
| `CameraOrbit` | 3인칭·1인칭·대화 시점 상태와 회전·줌 범위를 관리하고 카메라 위치를 계산합니다. |
| `DialogueFraming` | 대화 시점 카메라를 플레이어 눈이 아니라 NPC 얼굴 앞 정해진 거리·높이(`PlayerCameraSettings`의 대화 시점 항목)에 둡니다. 플레이어가 얼마나 떨어져 섰든 NPC마다 얼굴이 같은 크기로 보이고, 플레이어 위치는 카메라를 얼굴의 어느 쪽에 둘지만 정합니다. 화면 비율에 맞춰 얼굴을 정한 화면 자리(기본 위쪽·오른쪽)에 놓고 수평선은 유지해, 대화창과 왼쪽 도움말이 얼굴을 가리지 않게 합니다. |
| `PlayerMover` | `CharacterController`로 플레이어를 움직이고 이동 방향으로 몸을 돌립니다. 캡슐 아래 둥근 면이 가구 모서리에 걸려 발이 떠 있고 받친 점이 턱(`stepOffset`)보다 높으면 땅이 아닌 것으로 보고 모서리 바깥으로 미끄러뜨려, 가구 옆 공중에 떠 있지 않게 합니다. 계단 모서리는 턱 높이 안이라 그대로 오릅니다. 캡슐은 머리 아래 몸통만 덮고, 큰 머리는 `HeadClearance`가 머리 구로 따로 막습니다. |
| `HeadClearance` | 몸통 캡슐보다 큰 머리를 벽·키 큰 가구에서 막습니다. 머리 구를 이동 방향으로 밀어 보고 막히면 닿기 직전까지만 가고 면을 따라 미끄러집니다. 구의 아래 끝이 머리 아래 끝이라 낮은 가구는 머리 아래로 지나갑니다. 반지름·높이는 캐릭터 정의(`PlayerCharacterDefinition`)에서 읽습니다. |
| `MeetingFraming` | 다가오는 상대를 기다리는 구도. 카메라를 플레이어 뒤 오른쪽 어깨 너머(`PlayerCameraSettings`의 기다리는 구도 항목)에 두고 상대 얼굴을 화면 가운데에 놓아, 플레이어 뒷모습은 왼쪽에 상대는 가운데에 보입니다. |
| `PlayerCameraRig` | 카메라가 플레이어 위치를 따라가게 하고, 벽에 막히면 카메라를 앞으로 당깁니다. 시점은 마우스·터치로만 돌립니다. |
| `CameraCollisionDistance` | 가로등·사람 같은 가는 장애물에 줌이 흔들리지 않도록 구분하고, 벽에서 벗어나면 카메라 거리를 서서히 복구합니다. |
| `MoveReferenceYaw` | 순간이동·대화 종료로 카메라가 플레이어 뒤로 다시 맞춰져도 누른 방향대로 곧게 움직이도록 이동 시작 방향을 유지합니다. 사용자가 직접 시점을 돌리면 기준을 갱신합니다. |
| `TouchDragInput` | 손가락별 드래그 소유권, 조이스틱 방향과 한 번만 적용할 카메라 이동량을 관리합니다. |
| `PlayerTouchControls`, `TouchDragArea` | 왼쪽 조이스틱과 오른쪽 드래그 영역의 터치 입력을 전달합니다. |
| `TouchControlsAvailability` | 조이스틱과 드래그 영역을 보일 기기인지 정합니다. Unity 객체 없이 테스트합니다. |
| `BrowserPointer`, `Plugins/WebGL/BrowserPointer.jslib` | Web 빌드에서 브라우저의 주 입력 장치가 손가락인지(CSS `pointer: coarse`)와 화면 배율(`devicePixelRatio`)을 읽습니다. |
| `LookSensitivity` | 사용자가 마이페이지에서 고른 시점 회전 감도 배율(0.5~2배). `PlayerCameraRig`가 마우스·터치 회전에 곱하며, 이 기기에 `PlayerPrefs`로 저장합니다. |
| `PointerDeltaScale` | 브라우저가 화면 배율을 곱해 넘기는 마우스 이동량을 배율로 나눠, 화면 배율과 관계없이 같은 회전 감도를 냅니다. 에디터에서는 배율을 1로 봅니다. |
| `PlayerTouchSettings` | 조이스틱 중앙 무시 영역과 터치 회전 감도를 설정합니다. |
| `PlayerMovementSettings`, `PlayerCameraSettings` | 속도·점프 높이·카메라 거리·회전 감도 등 수치를 담는 데이터 에셋입니다. 대화 시점의 얼굴까지 거리·얼굴보다 높은 정도·얼굴의 화면 자리와, 기다리는 구도의 뒤로 물러난 거리·오른쪽으로 비킨 거리·높이도 `PlayerCameraSettings`에서 조절합니다. |
| `PlayerSeatPose` | 의자·벤치·소파에 앉은 동안 이동을 잠그고 Humanoid 골격을 앉는 면 높이에 맞춰 구부립니다. 다리가 짧은 리틀멍은 자리 앞쪽에 앉혀 허벅지를 쿠션 위에 눕히고 무릎 아래를 앞 모서리 밖으로 내려뜨립니다. 일어서면 앉기 전에 서 있던 곳으로 돌아갑니다. 서 있던 곳은 캡슐이 들어가 있던 빈 자리라 탁자·팔걸이·벽에 끼지 않습니다. |
| `UmbrellaHold`, `HeldUmbrella` | 플레이어 루트에 붙어 우산을 펴 듭니다. 펼 때 지금 외형의 오른팔 뼈대와 머리 크기(머리뼈 아래 정점)를 읽고, 팔이 닿는 데까지 몸 옆 어깨 높이로 손을 뻗어 손잡이를 쥐게 한 뒤(`TwoBonePose`) 우산대를 머리 쪽으로 14° 기울여 세웁니다. 우산대 길이는 캐노피 아랫면이 머리 꼭대기보다 6cm 위에 오도록 맞춥니다. 펴고 접는 일은 [Mission 모듈](../Mission/README.md)이 정합니다. 우산 모델(`Prefabs/Player/HeldUmbrella.prefab`, 살 8개 돔 `Art/Player/HeldUmbrellaCanopy.asset`)과 마을 플레이어 연결은 **Tools → A305 → Player → Add Umbrella Hold**(`PlayerUmbrellaBuilder`)가 만들며, 우산 모델은 없을 때만 만듭니다. |
| `SeatedLegs` | Humanoid 다리 뼈를 찾아 앉는 면 높이에 맞춰 구부리는 계산입니다. `PlayerSeatPose`와 쉬는 NPC([Npc 모듈](../Npc/README.md#생활)의 `NpcRoutine`)가 함께 씁니다. |
| `StandingPoseCorrections` | 리틀 멍 외형의 서 있는 자세 보정(`LittleMungGroundContact`·`LittleMungTerrainContact`·`LittleMungStairLocomotion`·`LittleMungArmClearance`)을 앉아 있는 동안 끄고, 일어서면 켜져 있던 것만 다시 켭니다. `PlayerSeatPose`와 `NpcRoutine`이 함께 씁니다. |
| `SeatView` | 앉은 캐릭터를 플레이어가 다가온 쪽에서 보도록 카메라 수평 방향을 정합니다. 자리 바로 위에서 앉아 다가온 방향을 알 수 없으면 자리 앞쪽에서 봅니다. Unity 객체 없이 테스트합니다. |
| `DialogueViewProbe` | Sandbox 씬에서 T 키로 대화 시점과 이동 잠금을 확인하는 확인용 컴포넌트입니다. 배포용 프리팹에는 붙이지 않습니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| Unity Input System | 키보드·마우스·다중 터치 입력 |
| Unity UI | 모바일 조이스틱과 드래그 영역 |
| `A305.Characters` | 모델 Animator에 이동 상태 전달. 연결하지 않아도 동작합니다. |

## Sandbox 씬에서 확인

`Assets/Scenes/Sandbox/PlayerSandbox.unity`를 열고 Play합니다. 바닥·장애물·경사로와 대화 상대 대신 쓰는 `DialogueTarget`이 배치되어 있습니다.

| 조작 | 동작 |
| --- | --- |
| WASD·방향키, Shift, Space | 이동, 달리기, 점프 |
| 마우스 왼쪽 버튼을 누른 채 이동 | 카메라 회전. 거의 움직이지 않고 뗀 왼쪽 클릭은 사물 카드 고정에 씁니다. Web 빌드는 게임 화면에서 브라우저의 오른쪽 클릭 메뉴를 띄우지 않습니다(`WebGLTemplates/A305Responsive`). |
| 마우스 휠 | 3인칭 거리 조절 |
| V | 1인칭·3인칭 전환 |
| T | `DialogueTarget`을 바라보는 대화 시점과 이동 잠금 전환 |

### 모바일 터치 조작

휴대폰·태블릿 브라우저에서는 화면 왼쪽 아래 조이스틱을 밀어 이동하고, 화면 오른쪽 빈 영역을 드래그해 카메라를 돌립니다. 두 손가락으로 이동과 회전을 동시에 조작할 수 있습니다. 손을 떼거나 앱을 전환하면 입력이 초기화됩니다. 대화 중에는 기존 이동·회전 잠금이 적용됩니다. 점프·달리기·시점 전환의 터치 버튼은 제공하지 않습니다.

조이스틱과 드래그 영역을 보이는 기준은 다음과 같습니다.

| 실행 환경 | 기준 | 예 |
| --- | --- | --- |
| Web 빌드 | 브라우저 UA가 휴대폰·태블릿이거나(`Application.isMobilePlatform`) 주 입력 장치가 손가락(CSS `pointer: coarse`)이면 표시 | 휴대폰·iPad는 표시. 터치 노트북처럼 마우스·터치패드가 주 입력이면 터치스크린이 있어도 숨김 |
| Editor | Device Simulator·테스트가 붙인 가상 Touchscreen 장치가 있으면 표시. 컴퓨터의 실제 터치스크린(`InputDevice.native`)은 세지 않아 PC 웹처럼 숨김 | Device Simulator, 터치 PlayMode 테스트는 표시. 터치 화면 노트북의 일반 Game 뷰는 숨김 |

Unity Web은 PC 브라우저에서도 Touchscreen 장치를 등록할 수 있어 Web 빌드에서는 장치 유무로 판단하지 않습니다. iPadOS Safari는 기본으로 Mac UA를 보내 `Application.isMobilePlatform`이 false이므로 주 입력 장치를 함께 확인합니다.

**Tools → A305 → Player → Build Touch Controls**는 `Prefabs/Player/TouchControls.prefab`, `Data/Player/PlayerTouchSettings.asset`을 준비하고 Player 프리팹에 터치 UI를 연결하며 `Scenes/Sandbox/TouchControlsSandbox.unity`를 만듭니다. 기존 디자인과 설정값은 유지합니다. Production 씬을 직접 수정할 필요 없이 기존 Player 프리팹 인스턴스에도 적용됩니다.

`TouchControlsSandbox`는 터치 가능한 장치에서 이동·회전을 단독 확인하는 씬이며 Scene List에는 넣지 않습니다. Web 빌드와 휴대폰 확인 방법은 [Unity 실행 문서](../../../../../docs/unity/setup.md)를 따릅니다.

씬·프리팹·데이터 에셋·입력 액션은 Editor 메뉴 **Tools → A305 → Player → Build Sandbox**(`Scripts/Editor/Player/PlayerSandboxBuilder.cs`)로 다시 만들 수 있습니다. Sandbox 씬은 덮어씁니다. `Player`·`PlayerCamera` 프리팹은 컴포넌트와 연결만 다시 맞추고, `Player`의 `Visual` 아래 모델과 모델에 맞춰 바꾼 `CharacterController` 크기는 유지합니다. 이미 있는 머티리얼과 데이터 에셋의 값도 유지합니다.

## 계정 캐릭터 적용

공용 Player는 `PlayerCharacterAppearance`로 `PlayerCharacterCatalog`에서 선택 ID의 외형·캡슐 크기를 적용합니다. 이동 설정은 공용 Player의 `PlayerMover`가 하나만 가지므로 어떤 외형을 골라도 같은 속도로 움직입니다. `PlayerCharacterSelection`은 서버가 확인한 ID를 씬 사이에 전달하며, 계정의 영구 저장은 Onboarding API가 맡습니다. `PlayerNickname`은 같은 방식으로 접속한 계정의 닉네임을 전달합니다. 입구 화면이 접속·가입할 때, 내 정보가 닉네임을 불러오거나 바꿀 때 넣고, 폰 메뉴의 날짜 줄이 읽습니다. F는 보정 리틀 멍, M은 같은 마스터 골격에 연결한 기본 남자 리틀 멍입니다. `PlayerCharacterVisibility`는 선택된 Visual을 1인칭·대화 시점에서 숨깁니다. 3인칭에서도 벽에 밀린 카메라가 캐릭터 기준점에서 1.2m 안으로 들어오면 숨기고, 1.5m 밖으로 물러나면 다시 표시합니다(`CloseCameraCharacter`). 두 거리 사이에서는 직전 상태를 유지해 경계에서 깜빡이지 않습니다.

이동 루트, 입력 액션, 카메라 추적 대상과 상호작용 참조를 교체하지 않습니다. 새 모델은 Character Definition의 Visual Prefab에 등록하고, Animator가 있다면 `Speed`, `IsGrounded`, `VerticalSpeed` 파라미터를 갖춰야 합니다. 모델의 접지 보정은 해당 Visual 안에 함께 구성합니다. Character Definition의 카메라 눈높이·조준점이 0보다 크면 해당 캐릭터에 적용하고, 0이면 씬 카메라 데이터의 높이를 사용합니다. 거리·회전·줌 설정은 씬 데이터를 유지합니다. 등록 경로와 확인 순서는 [리틀 멍 플레이어](../../../../../docs/unity/little-mung-player.md)를 따릅니다.
## 사용 방법

1. Project 창에서 **Create → A305 → Player**로 `Movement Settings`, `Camera Settings` 데이터 에셋을 만듭니다.
2. 플레이어 루트에 `CharacterController`, `PlayerMover`를 붙이고 데이터 에셋과 입력 액션을 연결합니다. 모델은 자식 객체에 둡니다.
3. 카메라에 `PlayerCameraRig`를 붙이고 데이터 에셋, 따라갈 플레이어, 입력 액션을 연결합니다.
4. `PlayerMover`의 Camera Transform에 카메라를 연결합니다. 이동 방향의 기준이 됩니다.

| 입력 액션 | 형식 | 연결 컴포넌트 |
| --- | --- | --- |
| `Player/Move` | Value · Vector2 | `PlayerMover` |
| `Player/Sprint` | Button | `PlayerMover` |
| `Player/Jump` | Button | `PlayerMover` |
| `Player/Look` | Value · Vector2(마우스 이동량) | `PlayerCameraRig` |
| `Player/CameraRotate` | Button(마우스 왼쪽 버튼) | `PlayerCameraRig`. 누르고 있는 동안 회전 |
| `Player/CameraZoom` | Value · Axis(마우스 휠 Y) | `PlayerCameraRig` |
| `Player/ToggleView` | Button | `PlayerCameraRig`. 1인칭·3인칭 전환 |

## 게임 코드에서 쓰는 API

| API | 설명 |
| --- | --- |
| `PlayerMover.IsMoving` | 이번 프레임에 수평으로 움직였는지. 이동 중에 사물 툴팁을 숨길 때 씁니다. |
| `PlayerMover.IsRunning` | 달리기 키를 누른 채 움직였는지. 튜토리얼 걷기 단계가 씁니다. |
| `PlayerMover.JumpedThisFrame` | 이번 프레임에 땅을 박차고 뛰어올랐는지(공중에서 누른 점프는 아님). 튜토리얼 걷기 단계가 씁니다. |
| `PlayerCameraRig.ZoomedByUserThisFrame`, `ToggledViewThisFrame` | 이번 프레임에 휠로 확대·축소했는지, 1인칭·3인칭을 바꿨는지. 튜토리얼 둘러보기 단계가 씁니다. |
| `PlayerMover.SetMovementLocked(bool)` | 대화처럼 움직이면 안 되는 동안 이동과 점프를 막습니다. 중력은 계속 적용합니다. |
| `PlayerMover.FaceToward(Vector3)` | 제자리에서 주어진 지점을 수평으로 바라보게 몸을 돌립니다. 대화를 마친 뒤 대화하던 NPC 쪽을 보게 할 때 씁니다. |
| `PlayerCameraRig.EnterDialogueView(Transform)` | 대상(보통 NPC 머리 위치) 앞 정해진 거리·높이에서 대상을 비추는 대화 시점으로 바꿉니다. 회전·줌·시점 전환 입력을 받지 않습니다. |
| `PlayerCameraRig.ExitDialogueView()` | 대화 직전의 시점으로 돌아가며 카메라를 플레이어가 보는 방향 뒤에 바로 세웁니다. 플레이어를 먼저 돌려 두면 그 방향을 그대로 이어 봅니다. |
| `PlayerCameraRig.EnterMeetingView(Transform)`, `ExitMeetingView()`, `IsInMeetingView` | 다가오는 상대(보통 NPC 머리 위치)를 플레이어 어깨 너머로 비추는 구도로 붙잡고 풉니다. 그동안 회전·줌·시점 전환 입력을 받지 않고, 대화 시점이 우선합니다. 풀면 카메라를 플레이어 뒤에 세웁니다. |
| `PlayerCameraRig.ViewMode` | 현재 시점(`ThirdPerson`, `FirstPerson`, `Dialogue`) |
| `PlayerMover.Teleport(Vector3, Quaternion)` | 플레이어를 바로 옮깁니다. 공간을 옮긴 뒤 출입구 앞에 세울 때 씁니다. |
| `PlayerCameraRig.SnapBehindTarget()` | 카메라를 플레이어가 보는 방향 뒤로 돌려 바로 그 자리에 놓습니다. `Teleport` 다음에 호출합니다. 카메라는 시작할 때도 플레이어 뒤에서 시작합니다. |
| `PlayerCameraRig.SnapView(float)` | 카메라를 주어진 수평 방향(도)으로 돌려 바로 그 자리에 놓습니다. 문을 지나 공간을 옮긴 뒤 들어가기 전 카메라 각도를 이어 맞출 때 씁니다. |
| `PlayerCameraRig.ViewYawDegrees` | 카메라가 바라보는 수평 방향(도). 마우스·터치로 돌린 결과입니다. |
| `PlayerSeatPose.Sit(Transform, float, float)` | 자리(원점은 바닥, +z는 앉아서 보는 방향)에 앉힙니다. 자리 원점에서 잰 쿠션 윗면 높이와 앞 모서리까지의 거리를 함께 넘깁니다. 이동이 잠겨 있거나 Humanoid 다리 뼈를 찾지 못하면 앉지 않고 `false`를 돌려줍니다. 앉을 자리와 쉬는 시간은 [Mission 모듈](../Mission/README.md#의자벤치소파-앉기)의 `RestSeat`가 정합니다. |
| `PlayerSeatPose.Stand()` | 앉기 전에 서 있던 곳에 자리 방향으로 세우고 이동 잠금을 풉니다. |
| `UmbrellaHold.SetOpen(bool)` | 우산을 펴거나 접습니다. 외형의 팔 뼈대나 우산 프리팹이 없으면 펴지 않습니다. |

## 테스트

`Assets/Tests/EditMode/Player/`의 `A305.Player.Tests` 어셈블리가 `PlayerMotor`와 `CameraOrbit`의 이동·시점 규칙, `TouchDragInput`의 터치 드래그 처리, `TouchControlsAvailability`의 터치 조작 표시 기준, `SeatView`의 앉은 뒤 카메라 방향, `DialogueFraming`의 대화 시점 카메라 위치와 얼굴의 화면 자리, `MeetingFraming`의 어깨 너머 기다리는 구도, 프로젝트 입력 액션의 시점 회전 버튼(`PlayerInputActionsTests`)을 검증합니다. 실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다. 입력 연결과 카메라 연출은 Sandbox 씬의 Play로 확인합니다. `BrowserPointer.jslib`은 Editor에서 실행되지 않으므로 Web 빌드를 PC·모바일 브라우저에서 열어 조이스틱 표시 여부를 확인합니다.
