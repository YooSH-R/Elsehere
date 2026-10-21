# Unity 개발 규약

Unity 클라이언트의 폴더, Scene, Prefab, 데이터 에셋, C# 스크립트 작성 기준입니다. Unity를 처음 사용하는 팀원도 같은 방식으로 작업해 Scene 충돌, 참조 누락, 흩어진 수치 때문에 작업이 엉키지 않도록 하는 것이 목적입니다.

가독성, 이름의 의미, 책임 분리, 변경 범위, 주석, 테스트와 검사 운영 원칙은 [공통 코드 규약](../code-conventions.md)을 따릅니다. 이 문서는 Unity에만 적용하는 C# 명명·포맷과 에셋·Editor 작업 기준을 정의합니다. 설치와 실행은 [Unity 개발 환경과 Web 실행](setup.md), 브랜치·커밋·MR은 [개발 및 협업 규칙](../development.md)을 따릅니다.

## 핵심 원칙

1. 폴더는 **기능(책임) 단위**로 나눕니다. 개발자 이름으로 폴더를 만들지 않습니다.
2. 기능 개발과 실험은 기능별 **Sandbox 씬**에서 합니다. **배포용 씬**은 완성된 프리팹을 배치하고 연결할 때만 수정합니다.
3. 조절할 수치와 콘텐츠 설정은 프리팹에 흩어 두지 않고 **데이터 에셋(ScriptableObject)** 으로 분리합니다. 프리팹은 데이터 에셋을 참조합니다.
4. 기능의 구성과 동작은 프리팹에 담고, 씬에는 배치와 연결만 남깁니다.
5. 배포용 씬, 프로젝트 설정, 패키지처럼 모두가 사용하는 파일은 한 번에 한 명만 수정합니다.
6. 에셋의 이동·이름 변경·삭제는 Unity Project 창에서 하고, `.meta` 파일을 에셋과 함께 커밋합니다.
7. 씬과 프리팹의 Git 충돌은 텍스트로 직접 합치지 않습니다.

## 기능 개발 흐름

```text
작업 브랜치 생성
→ 기능 Sandbox 씬에서 프리팹·데이터 에셋 개발과 Play 확인
→ 프리팹 완성 기준 확인
→ 배포용 씬 수정 선언 후 프리팹 배치·연결
→ 커밋 전 변경 파일 확인
→ MR 리뷰와 dev 병합, 배포용 씬 수정 해제 알림
```

1. Jira 작업을 확인하고 최신 `dev`에서 작업 브랜치를 만듭니다.
2. `Assets/_Sandbox/<기능>/`에 작업용 씬을 만듭니다.
3. 배포에 사용할 스크립트·프리팹·데이터 에셋은 처음부터 `Assets/_Project`의 기능 폴더에 만들고, 시험용 임시 에셋만 Sandbox 폴더에 둡니다.
4. Sandbox 씬에 프리팹을 배치하고 Play로 동작을 확인합니다. 수치는 데이터 에셋에서 조정합니다.
5. [프리팹 완성 기준](#프리팹-완성-기준)을 확인합니다.
6. 작업 브랜치에 최신 `dev`를 반영하고, 팀 채널에 배포용 씬 수정을 선언한 뒤 프리팹을 배치합니다.
7. [커밋 전 확인](#커밋-전-확인)을 거쳐 MR을 만듭니다. MR에는 동작을 확인할 Sandbox 씬 경로와 확인 방법을 적습니다.
8. MR이 병합되면 팀 채널에 배포용 씬 수정 해제를 알립니다.

## 폴더 구조

```text
frontend/UnityClient/Assets/
├── _Project/                    # 배포에 포함하는 팀 에셋
│   ├── Scenes/                  # 배포용 씬
│   ├── Prefabs/<기능>/          # 완성 단위 객체
│   ├── Data/<기능>/             # 데이터 에셋(.asset)
│   ├── Scripts/<기능>/          # 직접 작성한 C# 스크립트
│   ├── Tests/EditMode/<기능>/   # 기능별 테스트(PlayMode도 같은 구조)
│   ├── Art/                     # 모델·머티리얼·텍스처·애니메이션
│   └── Audio/                   # 효과음·배경음
├── _Sandbox/<기능>/             # 기능별 개발·확인 씬과 시험용 에셋
└── ThirdParty/<에셋 이름>/       # 외부에서 가져온 에셋 원본
```

- 폴더는 기능 단위로 나누고 개발자 이름으로 나누지 않습니다. 담당자가 바뀌거나 여러 사람이 같은 기능을 맡아도 그 기능의 코드·씬·에셋을 한곳에서 찾을 수 있게 하기 위함입니다.
- `<기능>` 폴더는 `Npc`, `Dialogue`, `Player`, `UI`처럼 실제 책임을 기준으로 정하고, `Scripts`·`Prefabs`·`Data`·`Tests`·`_Sandbox`에서 같은 이름을 사용합니다. 한 기능의 스크립트·프리팹·데이터·테스트·확인용 씬을 같은 이름으로 찾을 수 있게 하기 위함입니다.
- 테스트 폴더와 테스트용 Assembly Definition은 [프론트엔드 개발 지침](../../frontend/AGENTS.md)에 따라 기능의 첫 테스트를 추가하는 작업에서 만듭니다.
- 폴더 이름 앞의 `_`는 Project 창에서 팀 폴더를 위쪽에 모아 보여 주기 위한 표시입니다.
- `Assets/Settings/`와 `Assets/InputSystem_Actions.inputactions`는 Unity 템플릿이 만든 프로젝트 전체 설정입니다. 위치를 유지하고 [공유 설정](#공유-설정-파일) 규칙에 따라 수정합니다.
- `SampleScene`, `DemoCube`, `DemoCubeRotator`는 개발 환경 확인용 샘플입니다. 서비스 기능에 맞는 책임과 이름을 정해 구현합니다.

### 에셋별 위치와 이름

| 에셋 | 위치 | 이름 기준 | 예시 |
| --- | --- | --- | --- |
| 배포용 씬 | `_Project/Scenes/` | 장소·화면 + `Scene` | `CafeScene.unity` |
| 프리팹 | `_Project/Prefabs/<기능>/` | 대상을 드러내는 PascalCase | `Npc/Barista.prefab`, `UI/DialoguePanel.prefab` |
| 프리팹 Variant | 원본과 같은 폴더 | 원본과 구분되는 대상 이름 | `Npc/Pedestrian.prefab` → `Npc/JoggingPedestrian.prefab` |
| 데이터 에셋 | `_Project/Data/<기능>/` | 사용 대상 + 내용 | `Npc/BaristaMovement.asset` |
| 스크립트 | `_Project/Scripts/<기능>/` | 주 클래스 이름과 동일 | `Npc/NpcWalker.cs` |
| 기능 모듈의 Assembly Definition·설명 | `_Project/Scripts/<기능>/` | `A305.<기능>.asmdef`, `README.md` | `Player/A305.Player.asmdef` |
| 에디터 확장 스크립트 | `_Project/Scripts/Editor/` | 주 클래스 이름과 동일 | `Editor/SceneReferenceChecker.cs` |
| 테스트 | `_Project/Tests/EditMode/<기능>/`, `_Project/Tests/PlayMode/<기능>/` | 검증 대상 + `Tests` | `EditMode/Player/PlayerMotorTests.cs` |
| 모델·머티리얼·텍스처·애니메이션 | `_Project/Art/<종류>/` | 대상 + 용도 | `Art/Materials/CafeFloor.mat` |
| 소리 | `_Project/Audio/` | 대상 + 용도 | `Audio/CafeDoorBell.wav` |
| Sandbox 씬 | `_Sandbox/<기능>/` | 작업 내용 | `_Sandbox/Npc/NpcGreeting.unity` |
| 외부 에셋 | `ThirdParty/<에셋 이름>/` | 가져온 원본 이름 유지 | `ThirdParty/KenneyCityKit/` |

- 파일·폴더 이름에는 공백과 한글을 넣지 않습니다.
- 표의 예시 이름은 형식을 보여 주기 위한 것입니다. 실제 기능 이름은 Jira 작업과 구현에 맞춰 정합니다.

### Unity가 특별하게 취급하는 폴더 이름

| 폴더 이름 | Unity 동작 | 팀 규칙 |
| --- | --- | --- |
| `Editor` | Editor에서만 사용하고 빌드에서 제외 | 에디터 확장 스크립트만 둡니다. |
| `Resources` | 참조 여부와 관계없이 안의 에셋을 모두 빌드에 포함 | 사용하지 않고 Inspector 참조로 연결합니다. 필요하면 먼저 팀과 합의합니다. |
| `StreamingAssets` | 파일을 변환 없이 빌드에 복사 | 필요할 때 팀과 합의한 뒤 사용합니다. |
| `Plugins` | 안의 스크립트를 다른 스크립트보다 먼저 컴파일되는 별도 어셈블리로 분리 | 직접 작성한 C# 스크립트를 두지 않고, 브라우저 JavaScript 연동 파일 등 플러그인이 필요할 때 사용합니다. |

## 기능 모듈

여러 곳에서 재사용하거나 다른 프로젝트에서도 쓸 수 있는 기능은 모듈로 경계를 지킵니다. MVP 동안은 `Assets/_Project` 안에서 관리하고, 기능이 안정되면 UPM(Unity Package Manager) 패키지로 옮깁니다.

| 구성 | 위치 | 이름 |
| --- | --- | --- |
| 코드와 Assembly Definition | `_Project/Scripts/<기능>/` | `A305.<기능>.asmdef`, namespace `A305.<기능>` |
| 설명 | `_Project/Scripts/<기능>/README.md` | 역할, 의존성, 사용 방법, 게임 코드에서 쓰는 API, 테스트 위치 |
| 테스트 | `_Project/Tests/EditMode/<기능>/` | `A305.<기능>.Tests.asmdef` |

- 모듈은 게임 전용 코드와 서버 연동을 참조하지 않습니다. 의존 방향은 게임 코드 → 모듈입니다. 대화 시작 시 이동 잠금처럼 게임 진행이 정하는 동작은 모듈의 공개 API로 요청합니다.
- 모듈끼리 참조가 필요하면 Assembly Definition의 References에 명시하고 README의 의존성에 적습니다. 서로 참조하는 순환 구조를 만들지 않습니다.
- 게임에서만 쓰는 흐름, 서버 연동, 화면 코드는 `_Project/Scripts/`의 `A305.Runtime` 어셈블리에 둡니다. 별도 Assembly Definition이 없는 `Scripts/<기능>/` 폴더는 이 어셈블리에 포함됩니다.
- 프리팹·데이터 에셋·씬은 모듈 폴더에 넣지 않고 `_Project/Prefabs/<기능>/`, `_Project/Data/<기능>/`, `_Project/Scenes/`에 둡니다.
- 기능이 안정되면 모듈 폴더를 `.meta`와 함께 `frontend/UnityClient/Packages/com.a305.<기능>/`으로 옮기고 `package.json`·CHANGELOG를 추가해 UPM 패키지로 만듭니다. 옮길 때 [공통 검사 명령](setup.md#c-포맷과-규칙-검사)과 `.editorconfig`의 검사 범위도 함께 조정합니다.

## Sandbox 씬과 배포용 씬

| 구분 | 위치 | 수정하는 사람 | Build Profile Scene List | 용도 |
| --- | --- | --- | --- | --- |
| Sandbox 씬 | `_Sandbox/<기능>/` | 씬을 만든 사람 | 등록하지 않음 | 기능 개발, 실험, 프리팹 단독 확인 |
| 배포용 씬 | `_Project/Scenes/` | 수정을 선언한 한 명 | 등록 | 사용자에게 제공하는 실제 화면과 공간 |

### Sandbox 씬

- 기능 작업을 시작할 때 `Assets/_Sandbox/<기능>/` 폴더를 만듭니다. `<기능>`은 `Scripts`·`Prefabs`·`Data`와 같은 이름을 씁니다. 예: `_Sandbox/Npc/`
- 작업용 씬은 **File → New Scene**으로 만들고 기능 폴더에 작업 내용을 드러내는 이름으로 저장합니다. 필요한 프리팹만 배치해 기능을 확인합니다.
- 같은 기능을 여러 사람이 작업하면 작업 내용별로 씬을 나눕니다. 다른 사람이 만든 Sandbox 씬은 열어 볼 수 있지만 저장하거나 커밋하지 않습니다. 실수로 변경했다면 [되돌립니다](#커밋-전-확인).
- Sandbox 폴더는 Git에 커밋합니다. 리뷰어가 같은 씬에서 기능을 확인할 수 있고, 작업 기록을 잃지 않기 위함입니다.
- Sandbox의 시험용 C# 코드도 `_Project`와 같은 이름·포맷·린터 규칙을 따릅니다. 커밋 전에 [공통 포맷·검사 명령](setup.md#c-포맷과-규칙-검사)을 실행합니다.
- Sandbox 씬은 Build Profile의 Scene List에 추가하지 않습니다.
- Sandbox 씬과 에셋은 `_Project`의 에셋을 자유롭게 참조할 수 있습니다. 반대로 `_Project`의 씬·프리팹·데이터 에셋은 `_Sandbox`의 에셋을 참조하지 않습니다. 시험용 에셋을 배포에 쓰게 되면 `_Project`의 해당 폴더로 옮긴 뒤 연결합니다.
- 끝난 실험과 사용하지 않는 시험용 에셋은 만든 사람이 정리합니다.

### 배포용 씬

- 배포용 씬은 `_Project/Scenes/`에 두고 Build Profile의 Scene List에 등록합니다. 현재 `SampleScene`은 개발 환경 확인용 샘플이며, 서비스 씬을 만들면 Scene List를 함께 정리합니다.
- 씬에는 프리팹 인스턴스의 배치, 위치, 씬 단위 참조 연결, 조명과 환경 설정을 둡니다. 기능의 구성과 동작은 프리팹에서 만듭니다.
- 씬에서 GameObject를 직접 조합해 기능을 만들지 않습니다. 프리팹으로 만든 뒤 배치합니다.
- 최상위 객체는 다음 그룹으로 정리합니다. 그룹 객체는 위치·회전 0, 크기 1로 두고 컴포넌트를 붙이지 않습니다.

```text
CafeScene
├── Systems        # 진행 흐름·입력·서버 연결처럼 화면에 보이지 않는 객체
├── Environment    # 지형·건물·소품
├── Characters     # 플레이어·NPC
├── UI             # Canvas와 화면 UI
└── Lighting       # 조명·Volume
```

### 배포용 씬 수정 선언

Unity 씬 파일은 두 사람이 동시에 수정하면 충돌을 안전하게 합치기 어렵습니다. 배포용 씬은 한 번에 한 명만 수정합니다.

1. 작업 브랜치에 최신 `dev`를 반영합니다.
2. 팀 채널에 수정할 씬, 작업자, Jira 키를 알립니다. 예: `[씬 수정] CafeScene / 김민수 / S15P31A305-000`
3. 다른 사람이 같은 씬을 수정 중이면 MR 병합을 기다리거나 그 작업자에게 배치를 요청합니다.
4. 프리팹 완성 후 배치·연결만 짧게 진행합니다. 씬을 수정한 상태로 다른 개발을 오래 이어 가지 않습니다.
5. MR이 `dev`에 병합되면 `[씬 해제] CafeScene`을 알립니다.

## 프리팹

- 기능 단위로 프리팹을 만듭니다. 프리팹 루트의 위치·회전은 0, 크기는 1로 둡니다. 모델의 크기와 방향은 자식 객체에서 맞춥니다.
- 프리팹은 Project 창에서 더블클릭해 여는 **Prefab Mode**에서 수정합니다. 씬의 인스턴스를 고친 뒤 원본에 적용하면 의도하지 않은 변경까지 함께 반영되기 쉽습니다.
- 큰 프리팹은 작은 프리팹을 포함하는 **Nested Prefab**으로 나눕니다. 예를 들어 `CafeInterior` 안에 `CafeCounter`, `Barista`를 넣으면 각 프리팹을 다른 사람이 따로 수정할 수 있어 충돌이 줄어듭니다.
- 배포용 씬에서 프리팹 인스턴스를 **Unpack**하지 않습니다. Unpack하면 이후 원본 프리팹의 수정이 반영되지 않습니다.
- 모든 사용처에 반영할 변경은 프리팹 원본을 수정합니다. 씬 인스턴스의 Override는 위치·회전과 사용할 데이터 에셋 연결 정도로 제한합니다.
- Overrides를 원본에 적용하기 전에 다른 사용처에 미치는 영향을 확인합니다. 변경을 무조건 **Apply All**로 반영하지 않습니다.
- 프리팹 이름을 바꾸거나 삭제하기 전에 사용처를 확인합니다. Project 창에서 우클릭한 뒤 **Find References In Scene**으로 현재 씬의 사용처를 찾을 수 있습니다.

### 변형이 필요할 때

| 차이 | 방법 |
| --- | --- |
| 수치·대사·설정만 다름 | 같은 프리팹에 다른 데이터 에셋을 연결합니다. |
| 기본 구성과 동작은 같고 외형이나 일부 구성 요소가 다름 | 원본을 우클릭해 **Create → Prefab Variant**로 만듭니다. |
| 동작 자체가 다름 | 별도 프리팹으로 만듭니다. |

### 프리팹 완성 기준

배포용 씬에 배치하기 전에 다음을 확인합니다.

- [ ] 필요한 프리팹만 둔 Sandbox 씬에서 Play했을 때 Console에 오류가 없습니다.
- [ ] 조절할 수치와 콘텐츠 설정이 데이터 에셋으로 분리되어 있습니다.
- [ ] 필수 참조가 모두 연결되어 있고 Missing Script·Missing Reference가 없습니다.
- [ ] `_Sandbox`의 에셋을 참조하지 않습니다. Project 창에서 프리팹을 우클릭한 뒤 **Select Dependencies**로 선택되는 에셋의 경로를 확인합니다.
- [ ] 플레이어·카메라처럼 프리팹 밖의 객체가 필요하면 Inspector 참조로 드러나 있습니다.
- [ ] 이름과 위치가 이 규약을 따릅니다.

## 데이터 에셋(ScriptableObject)

수치를 프리팹마다 Inspector에 흩어 두면 같은 값을 여러 곳에서 고쳐야 하고, 어떤 값이 실제로 쓰이는지 찾기 어렵습니다. 조절 대상 값은 ScriptableObject로 만든 데이터 에셋(`.asset`)에 모으고 프리팹은 그 에셋을 참조합니다. 수치 조정은 프리팹이나 씬을 열지 않고 데이터 에셋만 수정해 진행할 수 있습니다.

### 값을 둘 위치

| 값의 종류 | 위치 | 예시 |
| --- | --- | --- |
| 기획·난이도 조정 대상 수치, 여러 객체가 공유하는 설정 | 데이터 에셋 | NPC 이동 속도, 말을 거는 거리, 다시 말을 걸기까지의 대기 시간 |
| Unity 배치·연출에만 쓰는 콘텐츠 설정 | 데이터 에셋 | NPC 순찰 경로, 장소의 출입·도착 지점 |
| 해당 프리팹의 구성에만 의미 있는 값 | 프리팹의 Inspector 필드 | 자식 객체 참조, 콜라이더 크기, Animator 연결 |
| 실행 중 바뀌는 상태 | 컴포넌트·일반 C# 클래스의 필드 | 현재 대화 차례, 남은 대기 시간 |
| 서버가 관리하는 마스터 데이터 | API 응답. Unity에는 연결 키만 둠 | NPC 이름·성격, 장소 이름, 사물의 단어·뜻, 미션 내용 |
| 서버·AI가 결정하는 값 | API 응답 | NPC 응답 문장, 학습 피드백, 저장된 학습 기록 |
| 비밀값 | Unity 프로젝트에 두지 않음 | API 키, 토큰 |

데이터 에셋은 Unity에서 배치·개수·수치를 조정하는 값이고, [ERD](../erd.dbml)의 데이터는 서버가 저장하고 API로 주고받는 값입니다. 두 곳은 ERD의 연결 키로 같은 대상을 가리킵니다. 장면은 `scenes.unity_key`, NPC는 `npcs.code`, 사물은 `world_objects.unity_key`를 프리팹·데이터 에셋에 둡니다. 이름·뜻처럼 서버가 관리하는 값은 데이터 에셋에 복사하지 않습니다. 같은 값을 두 곳에 두면 서로 어긋나기 때문입니다.

WebGL 빌드에 포함된 코드와 데이터 에셋은 사용자가 내려받아 확인할 수 있습니다. 비밀값과 공개하면 안 되는 정보는 클라이언트에 넣지 않습니다.

### 작성 규칙

- 클래스 이름은 수치 묶음이면 `<대상>Settings`, 콘텐츠 하나를 정의하면 `<대상>Definition`으로 짓습니다. 예: `NpcMovementSettings`, `PlaceDefinition`
- 클래스는 `_Project/Scripts/<기능>/`, 데이터 에셋은 `_Project/Data/<기능>/`에 둡니다.
- `[CreateAssetMenu]`로 생성 메뉴를 등록하고 `menuName`은 `A305/<기능>/<이름>` 형식으로 맞춥니다. Project 창에서 우클릭한 뒤 **Create → A305**에서 팀 데이터 에셋을 만들 수 있습니다.
- 필드는 `[SerializeField] private`로 Inspector에 노출하고, 다른 코드에는 읽기 전용 프로퍼티만 공개합니다. 실행 중 다른 코드가 데이터 에셋 값을 실수로 바꾸지 못하게 하기 위함입니다.
- 단위는 필드 이름에 드러내고(`Seconds`, `Meters`, `PerSecond`), `[Min]`, `[Range]`, `[Tooltip]`으로 허용 범위와 의미를 표시합니다.
- 실행 중 바뀌는 값은 데이터 에셋에 쓰지 않고 컴포넌트 필드로 복사해 사용합니다. Editor에서는 실행 중 바꾼 값이 Play를 종료한 뒤에도 에셋에 남아 원래 수치를 덮어쓰고, 빌드에서는 데이터 에셋 값을 저장할 수 없습니다.
- 여러 프리팹이 같은 데이터 에셋을 참조하면 하나를 고칠 때 모두 바뀝니다. 일부만 다르게 하려면 에셋을 복제(Ctrl+D)하고 바로 이름을 바꿉니다.
- 같은 데이터 에셋을 다른 사람이 수정 중인지 확인한 뒤 수치를 조정합니다.

```csharp
using UnityEngine;

namespace A305.Npc
{
    [CreateAssetMenu(fileName = "NpcMovementSettings", menuName = "A305/Npc/Movement Settings")]
    public class NpcMovementSettings : ScriptableObject
    {
        [SerializeField]
        [Min(0f)]
        private float walkSpeedMetersPerSecond = 1.4f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("플레이어에게 말을 거는 최대 거리")]
        private float greetingDistanceMeters = 3f;

        public float WalkSpeedMetersPerSecond => walkSpeedMetersPerSecond;
        public float GreetingDistanceMeters => greetingDistanceMeters;
    }
}
```

프리팹의 컴포넌트는 데이터 에셋을 Inspector 참조로 받아 사용합니다.

```csharp
using UnityEngine;

namespace A305.Npc
{
    public class NpcWalker : MonoBehaviour
    {
        [SerializeField]
        private NpcMovementSettings movementSettings;

        private void Awake()
        {
            if (movementSettings == null)
            {
                Debug.LogError("NpcMovementSettings가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            float moveDistance = movementSettings.WalkSpeedMetersPerSecond * Time.deltaTime;

            transform.Translate(0f, 0f, moveDistance, Space.Self);
        }
    }
}
```

### 수치 조정과 찾기

- Play 중 Inspector에서 데이터 에셋 값을 바꾸면 Play를 종료해도 값이 남습니다. 씬·프리팹 값은 복원되는 것과 다르므로 수치 조정에 활용할 수 있습니다. 커밋 전에는 의도한 값만 바뀌었는지 diff로 확인합니다.
- 데이터 에셋 변경은 **File → Save Project**로 저장합니다.
- Project 창 검색창에 `t:<클래스 이름>`을 입력하면 해당 종류의 데이터 에셋을 모두 찾을 수 있습니다. 예: `t:NpcMovementSettings`

## C# 스크립트

| 대상 | 기준 | 예시 |
| --- | --- | --- |
| 클래스·메서드·프로퍼티 | PascalCase | `DialoguePanel`, `ShowMessage`, `IsRecording` |
| 필드·매개변수·지역 변수 | camelCase | `missionId`, `canStartMission` |
| 스크립트 파일 | 주 클래스 이름과 일치 | `NpcWalker.cs` |
| namespace | `A305.<기능 폴더>` | `A305.Npc`, `A305.Dialogue` |

- 서비스명이 정해지지 않았으므로 namespace의 최상위 이름은 팀 코드 `A305`를 사용합니다. Sandbox 폴더의 시험용 스크립트는 `A305.Sandbox.<기능>`을 사용합니다. 시험용 클래스가 `_Project`의 같은 이름 클래스와 부딪혀 병합 후 컴파일 오류가 나지 않게 하기 위함입니다.
- `MonoBehaviour`·`ScriptableObject` 클래스 이름은 파일 이름과 같아야 Unity가 컴포넌트와 에셋을 연결합니다. 클래스 이름을 바꿀 때는 파일 이름도 Project 창에서 함께 바꿉니다.
- 들여쓰기는 공백 4칸, 중괄호는 다음 줄에 배치합니다.
- 컴포넌트로 연결할 클래스는 `MonoBehaviour`를 상속합니다. 값 계산·데이터 표현처럼 Unity 객체 기능이 필요 없는 코드는 일반 C# 클래스로 작성할 수 있습니다.
- 사용하는 생명주기 함수만 작성합니다. 비어 있는 `Start`, `Update`를 남기지 않습니다.
- 매 프레임 처리할 일이 있을 때만 `Update`를 사용합니다. 일정한 속도로 이동·회전하려면 경과 시간을 반영합니다.

### Inspector 설정과 참조

- Inspector에서 조절할 값은 기본적으로 `[SerializeField] private` 필드로 노출합니다. 다른 코드에서 사용할 필요가 있을 때 공개 메서드나 프로퍼티를 제공합니다.
- 속도·시간 등 수치의 단위가 이름이나 Inspector 안내에서 드러나게 합니다.
- 코드의 필드 기본값은 새 컴포넌트의 초기값입니다. 기존 Scene·Prefab에 저장된 값이 코드 기본값 변경만으로 갱신된다고 가정하지 않습니다.
- 씬·프리팹에 지속적으로 사용할 설정은 Play를 종료한 상태에서 변경하고 저장합니다. Play 중 씬·프리팹 값 변경은 동작 확인에 사용하며, 실행을 종료하면 복원됩니다. **Edit → Preferences → Colors**의 **Playmode tint**를 눈에 띄는 색으로 바꾸면 Play 중인지 쉽게 알 수 있습니다.
- 필수 객체 참조는 명시적으로 연결하고 누락을 확인합니다. 오류를 무시해 화면이 조용히 동작하지 않는 상태를 만들지 않습니다.
- `GameObject.Find`처럼 반복적인 전역 객체 탐색 대신 Inspector 참조 등 책임이 명확한 연결 방식을 우선합니다.

## 공유 설정과 외부 에셋

### 공유 설정 파일

다음 파일은 프로젝트 전체에 영향을 주고 동시에 수정하면 충돌하기 쉽습니다. 배포용 씬과 같이 팀 채널에 알린 뒤 한 명만 수정합니다.

| 파일 | 바뀌는 경우 |
| --- | --- |
| `ProjectSettings/TagManager.asset` | Tag·Layer 추가와 변경 |
| `ProjectSettings/EditorBuildSettings.asset` | Build Profile Scene List 변경 |
| 그 밖의 `ProjectSettings/` 파일 | 물리, 품질, 플레이어·Web 빌드 설정 변경 |
| `Packages/manifest.json`, `Packages/packages-lock.json` | 패키지 추가·삭제·버전 변경 |
| `Assets/Settings/` | URP 렌더링 설정 변경 |
| `Assets/InputSystem_Actions.inputactions` | 입력 키와 액션 변경 |

- Unity가 Editor 버전 업그레이드를 제안해도 버전을 바꾸지 않습니다. 버전 변경은 [Unity 개발 환경](setup.md)의 기준에 따라 호환성을 검증하고 MR 리뷰를 거칩니다.
- 작업과 관계없이 Unity가 수정한 설정 파일은 커밋하지 않고 되돌립니다.

### 외부 에셋

- Asset Store, Kenney 등에서 가져온 에셋은 `Assets/ThirdParty/<에셋 이름>/`에 둡니다. 가져올 때 Import 창에서 필요한 파일만 선택하고 데모 씬과 샘플은 제외합니다.
- 출처와 라이선스를 `Assets/ThirdParty/CREDITS.md`에 기록합니다. 사용 조건을 확인하지 않은 에셋은 추가하지 않습니다.
- 가져온 원본은 수정하지 않습니다. 머티리얼이나 프리팹을 바꿔야 하면 `_Project`에 복사하거나 Prefab Variant를 만듭니다.
- 머티리얼이 분홍색으로 보이면 URP가 아닌 셰이더를 사용하는 에셋입니다. **Window → Rendering → Render Pipeline Converter**의 Material Upgrade로 변환하고, 가져오기와 변환을 같은 MR에 담습니다.
- 대용량 파일을 위한 Git LFS는 아직 구성하지 않았습니다. 수십 MB 이상인 파일은 추가하기 전에 팀과 저장 방식을 정합니다. Web 빌드 크기에 영향을 주므로 텍스처의 Max Size와 압축 설정도 확인합니다.

## Git 작업과 충돌 대응

### 커밋 전 확인

1. **File → Save**로 열린 씬을, **File → Save Project**로 프리팹·데이터 에셋 등 나머지 변경을 저장합니다.
2. Console에 컴파일 오류가 없는지 확인합니다.
3. `git status`로 변경된 파일을 확인합니다.
   - 새로 만들거나 옮긴 에셋의 `.meta` 파일이 함께 있는지 확인합니다. `.meta`가 빠지면 다른 사람의 환경에서 참조가 Missing으로 바뀝니다.
   - 열어 보기만 한 씬, 다른 사람이 만든 Sandbox 씬, 작업과 관계없는 설정 파일 변경이 없는지 확인합니다.
4. 의도하지 않은 변경은 Unity에서 해당 씬을 닫거나 Editor를 종료한 뒤 되돌립니다.

```sh
git restore <되돌릴 파일 경로>
```

### 씬·프리팹 충돌

- 씬·프리팹·데이터 에셋 파일의 충돌 표시를 텍스트 편집기에서 직접 합치지 않습니다. 겉보기에는 해결되어도 객체 참조가 깨질 수 있습니다.
- 작업 브랜치에 최신 `dev`를 병합하다 씬이나 프리팹이 충돌하면, 내가 바꾼 내용을 기록해 두고 `dev` 쪽 파일을 선택합니다. 병합 중 `--theirs`는 병합해 들어오는 `dev` 쪽 파일을 뜻합니다.

```sh
git checkout --theirs -- <충돌한 씬 또는 프리팹 경로>
git add <충돌한 씬 또는 프리팹 경로>
```

- Unity에서 해당 파일을 열어 내 변경을 다시 적용하고, 참조 연결과 Missing Script 여부, 화면 배치를 확인한 뒤 Play로 동작을 검증합니다.
- 충돌을 줄이려면 배포용 씬 수정 선언, Nested Prefab으로 작업 범위 나누기, 씬 수정 시간을 짧게 유지하기를 지킵니다.
- 버전 관리 설정은 `Visible Meta Files`, 직렬화는 `Force Text`를 유지합니다.

## 자주 생기는 문제

| 증상 | 주요 원인 | 조치 |
| --- | --- | --- |
| 다른 사람의 화면에서 프리팹·머티리얼 참조가 Missing | `.meta` 누락, `_Sandbox` 에셋 참조, Unity 밖에서 파일 이동 | `.meta`를 커밋하고, 에셋을 `_Project`로 옮긴 뒤 참조를 다시 연결합니다. |
| 컴포넌트에 Missing Script가 표시됨 | 클래스·파일 이름 불일치, 컴파일 오류, 스크립트 삭제 | 클래스 이름과 파일 이름을 맞추고 Console의 컴파일 오류를 해결합니다. |
| 최신 `dev` 반영 후 컴파일 오류 | 같은 namespace의 같은 클래스 이름, 삭제·변경된 클래스 사용 | namespace를 확인하고 오류가 난 파일의 작업자와 조율합니다. |
| Play를 종료하니 바꾼 값이 사라짐 | Play 중 씬·프리팹 값 변경은 종료 시 복원 | Play 종료 후 다시 입력하고 저장합니다. |
| 데이터 에셋 수치가 의도하지 않게 바뀜 | 실행 중 코드가 데이터 에셋 값을 변경, Play 중 Inspector 수정 | 실행 중 상태는 컴포넌트 필드로 옮기고, 바뀐 값은 diff를 보고 되돌립니다. |
| 머티리얼이 분홍색으로 보임 | URP가 아닌 셰이더 사용 | Render Pipeline Converter로 변환하거나 URP 셰이더로 교체합니다. |
| 씬을 열기만 했는데 Git 변경으로 표시됨 | Unity가 직렬화 정보나 조명 데이터를 갱신 | 작업과 관계없다면 `git restore`로 되돌립니다. |

## 검증과 도구 적용

- 컴파일 오류를 해결한 뒤 Play 실행으로 변경 동작을 확인합니다. Web 관련 변경은 실제 브라우저에서도 검증합니다.
- 배포용 씬에 프리팹을 통합한 뒤에는 Sandbox 씬뿐 아니라 배포용 씬에서도 Play로 확인합니다.
- API 호출과 마이크 연결은 성공뿐 아니라 권한 거부, 취소, 시간 초과와 오류 전달도 검증합니다.
- 테스트 작성 기준은 [공통 코드 규약의 주석과 검증](../code-conventions.md#6-주석과-검증)을, Unity 테스트 구성과 실행은 [프론트엔드 개발 지침](../../frontend/AGENTS.md)을 따릅니다.
- 검사 대상은 `Assets/_Project`와 `Assets/_Sandbox`의 C# 코드이며, Editor 코드와 테스트 코드도 포함합니다. 도구 선택은 [공통 코드 규약의 린터·포매터 적용 방침](../code-conventions.md#8-린터포매터-적용-방침)을, 고정 버전과 실행 명령은 [Unity 실행 문서](setup.md#c-포맷과-규칙-검사)를 따릅니다.
- 린터는 빈 생명주기 메서드, 잘못된 Unity 콜백 선언·대소문자, 잘못된 객체 생성·직렬화 특성, Unity 객체의 잘못된 null 처리 등 선택한 규칙을 검사합니다.
- GitLab CI 연결은 별도 작업입니다. 편집기 경고와 Unity 컴파일만으로 공통 검사 명령을 대신하지 않습니다.

## 참고 자료

- [Unity 프로젝트 구성 가이드](https://unity.com/how-to/organizing-your-project)
- [ScriptableObject](https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html)
- [CreateAssetMenu](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/CreateAssetMenuAttribute.html)
- [Prefab 생성](https://docs.unity3d.com/6000.3/Documentation/Manual/CreatingPrefabs.html)
- [Prefab Mode 편집](https://docs.unity3d.com/6000.3/Documentation/Manual/EditingInPrefabMode.html)
- [Nested Prefab](https://docs.unity3d.com/6000.3/Documentation/Manual/NestedPrefabs.html)
- [Prefab Variant](https://docs.unity3d.com/6000.3/Documentation/Manual/PrefabVariants.html)
- [특수 폴더 이름](https://docs.unity3d.com/6000.3/Documentation/Manual/SpecialFolders.html)
- [Render Pipeline Converter](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/features/rp-converter.html)
- [Unity Editor 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/class-EditorManager.html)
- [Unity 버전 관리 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/class-VersionControlSettings.html)
