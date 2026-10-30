# Player 모듈

PC 웹의 키보드·마우스와 모바일 웹의 터치로 플레이어 이동과 카메라 시점을 조작합니다. 탐험 중에는 3인칭·1인칭을 오가고, NPC와 대화할 때는 상대를 바라보는 1인칭으로 고정한 뒤 대화가 끝나면 직전 시점으로 돌아갑니다.

다른 프로젝트에서도 쓸 수 있도록 게임 진행, 서버 연동, NPC·미션 같은 게임 코드를 참조하지 않습니다. 대화 시작·종료처럼 게임이 정하는 일은 아래 공개 API로 요청받습니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `PlayerMotor` | 이동 입력과 카메라 방향으로 이번 프레임의 속도를 계산합니다. 달리기, 점프, 중력, 이동 잠금을 포함하며 Unity 객체 없이 테스트합니다. |
| `CameraOrbit` | 3인칭·1인칭·대화 시점 상태와 회전·줌 범위를 관리하고 카메라 위치를 계산합니다. |
| `DialogueFraming` | 화면 비율에 맞춰 NPC 얼굴을 위쪽·오른쪽에 배치하고 수평선은 유지합니다. 대화창과 왼쪽 도움말이 얼굴을 가리지 않도록 합니다. |
| `PlayerMover` | `CharacterController`로 플레이어를 움직이고 이동 방향으로 몸을 돌립니다. |
| `PlayerCameraRig` | 카메라가 플레이어 위치를 따라가게 하고, 벽에 막히면 카메라를 앞으로 당깁니다. 시점은 마우스·터치로만 돌립니다. |
| `CameraCollisionDistance` | 가로등·사람 같은 가는 장애물에 줌이 흔들리지 않도록 구분하고, 벽에서 벗어나면 카메라 거리를 서서히 복구합니다. |
| `MoveReferenceYaw` | 순간이동·대화 종료로 카메라가 플레이어 뒤로 다시 맞춰져도 누른 방향대로 곧게 움직이도록 이동 시작 방향을 유지합니다. 사용자가 직접 시점을 돌리면 기준을 갱신합니다. |
| `TouchDragInput` | 손가락별 드래그 소유권, 조이스틱 방향과 한 번만 적용할 카메라 이동량을 관리합니다. |
| `PlayerTouchControls`, `TouchDragArea` | 왼쪽 조이스틱과 오른쪽 드래그 영역의 터치 입력을 전달합니다. |
| `TouchControlsAvailability` | 조이스틱과 드래그 영역을 보일 기기인지 정합니다. Unity 객체 없이 테스트합니다. |
| `BrowserPointer`, `Plugins/WebGL/BrowserPointer.jslib` | Web 빌드에서 브라우저의 주 입력 장치가 손가락인지(CSS `pointer: coarse`)와 화면 배율(`devicePixelRatio`)을 읽습니다. |
| `PointerDeltaScale` | 브라우저가 화면 배율을 곱해 넘기는 마우스 이동량을 배율로 나눠, 화면 배율과 관계없이 같은 회전 감도를 냅니다. 에디터에서는 배율을 1로 봅니다. |
| `PlayerTouchSettings` | 조이스틱 중앙 무시 영역과 터치 회전 감도를 설정합니다. |
| `PlayerMovementSettings`, `PlayerCameraSettings` | 속도·점프 높이·카메라 거리·회전 감도 등 수치를 담는 데이터 에셋입니다. |
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
| 마우스 왼쪽 버튼을 누른 채 이동 | 카메라 회전 |
| 마우스 휠 | 3인칭 거리 조절 |
| V | 1인칭·3인칭 전환 |
| T | `DialogueTarget`을 바라보는 대화 시점과 이동 잠금 전환 |

### 모바일 터치 조작

휴대폰·태블릿 브라우저에서는 화면 왼쪽 아래 조이스틱을 밀어 이동하고, 화면 오른쪽 빈 영역을 드래그해 카메라를 돌립니다. 두 손가락으로 이동과 회전을 동시에 조작할 수 있습니다. 손을 떼거나 앱을 전환하면 입력이 초기화됩니다. 대화 중에는 기존 이동·회전 잠금이 적용됩니다. 점프·달리기·시점 전환의 터치 버튼은 제공하지 않습니다.

조이스틱과 드래그 영역을 보이는 기준은 다음과 같습니다.

| 실행 환경 | 기준 | 예 |
| --- | --- | --- |
| Web 빌드 | 브라우저 UA가 휴대폰·태블릿이거나(`Application.isMobilePlatform`) 주 입력 장치가 손가락(CSS `pointer: coarse`)이면 표시 | 휴대폰·iPad는 표시. 터치 노트북처럼 마우스·터치패드가 주 입력이면 터치스크린이 있어도 숨김 |
| Editor | Input System에 Touchscreen 장치가 있으면 표시 | Device Simulator, 터치 PlayMode 테스트 |

Unity Web은 PC 브라우저에서도 Touchscreen 장치를 등록할 수 있어 Web 빌드에서는 장치 유무로 판단하지 않습니다. iPadOS Safari는 기본으로 Mac UA를 보내 `Application.isMobilePlatform`이 false이므로 주 입력 장치를 함께 확인합니다.

**Tools → A305 → Player → Build Touch Controls**는 `Prefabs/Player/TouchControls.prefab`, `Data/Player/PlayerTouchSettings.asset`을 준비하고 Player 프리팹에 터치 UI를 연결하며 `Scenes/Sandbox/TouchControlsSandbox.unity`를 만듭니다. 기존 디자인과 설정값은 유지합니다. Production 씬을 직접 수정할 필요 없이 기존 Player 프리팹 인스턴스에도 적용됩니다.

`TouchControlsSandbox`는 터치 가능한 장치에서 이동·회전을 단독 확인하는 씬이며 Scene List에는 넣지 않습니다. Web 빌드와 휴대폰 확인 방법은 [Unity 실행 문서](../../../../../docs/unity/setup.md)를 따릅니다.

씬·프리팹·데이터 에셋·입력 액션은 Editor 메뉴 **Tools → A305 → Player → Build Sandbox**(`Scripts/Editor/Player/PlayerSandboxBuilder.cs`)로 다시 만들 수 있습니다. Sandbox 씬은 덮어씁니다. `Player`·`PlayerCamera` 프리팹은 컴포넌트와 연결만 다시 맞추고, `Player`의 `Visual` 아래 모델과 모델에 맞춰 바꾼 `CharacterController` 크기는 유지합니다. 이미 있는 머티리얼과 데이터 에셋의 값도 유지합니다.

## 계정 캐릭터 적용

공용 Player는 `PlayerCharacterAppearance`로 `PlayerCharacterCatalog`에서 선택 ID의 외형·캡슐 크기를 적용합니다. 이동 설정은 공용 Player의 `PlayerMover`가 하나만 가지므로 어떤 외형을 골라도 같은 속도로 움직입니다. `PlayerCharacterSelection`은 서버가 확인한 ID를 씬 사이에 전달하며, 계정의 영구 저장은 Onboarding API가 맡습니다. F는 보정 리틀 멍, M은 같은 마스터 골격에 연결한 기본 남자 리틀 멍입니다. `PlayerCharacterVisibility`는 선택된 Visual을 1인칭·대화 시점에서 숨깁니다.

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
| `Player/CameraRotate` | Button | `PlayerCameraRig`. 누르고 있는 동안 회전 |
| `Player/CameraZoom` | Value · Axis(마우스 휠 Y) | `PlayerCameraRig` |
| `Player/ToggleView` | Button | `PlayerCameraRig`. 1인칭·3인칭 전환 |

## 게임 코드에서 쓰는 API

| API | 설명 |
| --- | --- |
| `PlayerMover.IsMoving` | 이번 프레임에 수평으로 움직였는지. 이동 중에 사물 툴팁을 숨길 때 씁니다. |
| `PlayerMover.SetMovementLocked(bool)` | 대화처럼 움직이면 안 되는 동안 이동과 점프를 막습니다. 중력은 계속 적용합니다. |
| `PlayerCameraRig.EnterDialogueView(Transform)` | 대상(보통 NPC 머리 위치)을 바라보는 대화 시점으로 바꿉니다. 회전·줌·시점 전환 입력을 받지 않습니다. |
| `PlayerCameraRig.ExitDialogueView()` | 대화 직전의 시점으로 돌아갑니다. |
| `PlayerCameraRig.ViewMode` | 현재 시점(`ThirdPerson`, `FirstPerson`, `Dialogue`) |
| `PlayerMover.Teleport(Vector3, Quaternion)` | 플레이어를 바로 옮깁니다. 공간을 옮긴 뒤 출입구 앞에 세울 때 씁니다. |
| `PlayerCameraRig.SnapBehindTarget()` | 카메라를 플레이어가 보는 방향 뒤로 돌려 바로 그 자리에 놓습니다. `Teleport` 다음에 호출합니다. 카메라는 시작할 때도 플레이어 뒤에서 시작합니다. |
| `PlayerCameraRig.SnapView(float)` | 카메라를 주어진 수평 방향(도)으로 돌려 바로 그 자리에 놓습니다. 문을 지나 공간을 옮긴 뒤 들어가기 전 카메라 각도를 이어 맞출 때 씁니다. |
| `PlayerCameraRig.ViewYawDegrees` | 카메라가 바라보는 수평 방향(도). 마우스·터치로 돌린 결과입니다. |

## 테스트

`Assets/Tests/EditMode/Player/`의 `A305.Player.Tests` 어셈블리가 `PlayerMotor`와 `CameraOrbit`의 이동·시점 규칙, `TouchDragInput`의 터치 드래그 처리, `TouchControlsAvailability`의 터치 조작 표시 기준을 검증합니다. 실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다. 입력 연결과 카메라 연출은 Sandbox 씬의 Play로 확인합니다. `BrowserPointer.jslib`은 Editor에서 실행되지 않으므로 Web 빌드를 PC·모바일 브라우저에서 열어 조이스틱 표시 여부를 확인합니다.
