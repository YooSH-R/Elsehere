# Player 모듈

PC 웹에서 플레이어의 키보드 이동과 카메라 시점을 담당합니다. 탐험 중에는 3인칭·1인칭을 오가고, NPC와 대화할 때는 상대를 바라보는 1인칭으로 고정한 뒤 대화가 끝나면 직전 시점으로 돌아갑니다.

다른 프로젝트에서도 쓸 수 있도록 게임 진행, 서버 연동, NPC·미션 같은 게임 코드를 참조하지 않습니다. 대화 시작·종료처럼 게임이 정하는 일은 아래 공개 API로 요청받습니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `PlayerMotor` | 이동 입력과 카메라 방향으로 이번 프레임의 속도를 계산합니다. 달리기, 점프, 중력, 이동 잠금을 포함하며 Unity 객체 없이 테스트합니다. |
| `CameraOrbit` | 3인칭·1인칭·대화 시점 상태와 회전·줌 범위를 관리하고 카메라 위치를 계산합니다. |
| `PlayerMover` | `CharacterController`로 플레이어를 움직이고 이동 방향으로 몸을 돌립니다. |
| `PlayerCameraRig` | 카메라가 플레이어를 따라가게 하고, 벽에 막히면 카메라를 앞으로 당깁니다. |
| `PlayerMovementSettings`, `PlayerCameraSettings` | 속도·점프 높이·카메라 거리·회전 감도 등 수치를 담는 데이터 에셋입니다. |
| `DialogueViewProbe` | Sandbox 씬에서 T 키로 대화 시점과 이동 잠금을 확인하는 확인용 컴포넌트입니다. 배포용 프리팹에는 붙이지 않습니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| Unity Input System | 키보드·마우스 입력 |
| `A305.Characters` | 모델 Animator에 이동 상태 전달. 연결하지 않아도 동작합니다. |

## Sandbox 씬에서 확인

`Assets/Scenes/Sandbox/PlayerSandbox.unity`를 열고 Play합니다. 바닥·장애물·경사로와 대화 상대 대신 쓰는 `DialogueTarget`이 배치되어 있습니다.

| 조작 | 동작 |
| --- | --- |
| WASD·방향키, Shift, Space | 이동, 달리기, 점프 |
| 마우스 오른쪽 버튼을 누른 채 이동 | 카메라 회전 |
| 마우스 휠 | 3인칭 거리 조절 |
| V | 1인칭·3인칭 전환 |
| T | `DialogueTarget`을 바라보는 대화 시점과 이동 잠금 전환 |

씬·프리팹·데이터 에셋·입력 액션은 Editor 메뉴 **Tools → A305 → Player → Build Sandbox**(`Scripts/Editor/Player/PlayerSandboxBuilder.cs`)로 다시 만들 수 있습니다. 씬과 프리팹은 덮어쓰고, 이미 있는 데이터 에셋의 수치는 유지합니다.

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
| `PlayerMover.SetMovementLocked(bool)` | 대화처럼 움직이면 안 되는 동안 이동과 점프를 막습니다. 중력은 계속 적용합니다. |
| `PlayerCameraRig.EnterDialogueView(Transform)` | 대상(보통 NPC 머리 위치)을 바라보는 대화 시점으로 바꿉니다. 회전·줌·시점 전환 입력을 받지 않습니다. |
| `PlayerCameraRig.ExitDialogueView()` | 대화 직전의 시점으로 돌아갑니다. |
| `PlayerCameraRig.ViewMode` | 현재 시점(`ThirdPerson`, `FirstPerson`, `Dialogue`) |

## 테스트

`Assets/Tests/EditMode/Player/`의 `A305.Player.Tests` 어셈블리가 `PlayerMotor`와 `CameraOrbit`의 이동·시점 규칙을 검증합니다. 실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다. 입력 연결과 카메라 연출은 Sandbox 씬의 Play로 확인합니다.
