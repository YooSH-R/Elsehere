# Unity 개발 규약 초안

Unity 클라이언트의 폴더, C# 스크립트, Scene과 Prefab 작성 기준입니다. [공통 코드 규약](code-conventions.md)을 기본으로 적용하며, 팀 리뷰를 거쳐 공유 규약으로 확정합니다. 설치와 실행은 [Unity 개발 환경과 Web 실행](unity-setup.md)을 따릅니다.

## 폴더 구조

직접 작성한 에셋은 `Assets/_Project`에 둡니다. 패키지와 외부 에셋은 자체 에셋과 구분하고 내부 구조를 목적 없이 변경하지 않습니다.

```text
frontend/UnityClient/
├── Assets/
│   └── _Project/
│       ├── Scenes/              # 화면과 공간
│       ├── Prefabs/             # 재사용하는 객체 구성
│       └── Scripts/             # 직접 작성한 C# 스크립트
├── Packages/                    # 패키지 선언과 잠금 파일
├── ProjectSettings/             # 공유 프로젝트 설정
└── .gitignore
```

- 실제 파일이 필요할 때 폴더를 추가합니다. 기능 구현 전에 빈 계층을 만들지 않습니다.
- 스크립트가 늘어나면 `Dialogue`, `UI`, `Networking`처럼 실제 책임을 기준으로 묶습니다. 예시 폴더를 모두 생성할 필요는 없습니다.
- `SampleScene`, `DemoCube`, `DemoCubeRotator`는 개발 환경 확인용 샘플입니다. 서비스 기능에 맞는 책임과 이름을 정해 구현합니다.
- 파일 이동과 이름 변경은 Unity의 Project 창에서 수행합니다. `.meta` 파일은 에셋과 함께 관리하며 임의로 삭제하거나 다시 만들지 않습니다.

## 이름과 C# 작성

| 대상 | 기준 | 예시 |
| --- | --- | --- |
| 클래스·메서드·프로퍼티 | PascalCase | `DialoguePanel`, `ShowMessage`, `IsRecording` |
| 필드·매개변수·지역 변수 | camelCase | `missionId`, `canStartMission` |
| 스크립트 파일 | 주 클래스 이름과 일치 | `DemoCubeRotator.cs` |
| Scene·Prefab | 역할을 드러내는 PascalCase | `CafeScene`, `DialoguePanel` |

- 이름과 경로에는 공백을 넣지 않습니다. 역할이 불분명한 `Manager`, `Util`, `Data`만으로 이름을 짓지 않습니다.
- 들여쓰기는 공백 4칸, 중괄호는 다음 줄에 배치합니다. 인코딩과 줄바꿈은 공통 규약의 UTF-8·LF 기준을 따릅니다.
- 컴포넌트로 연결할 클래스는 `MonoBehaviour`를 상속합니다. 값 계산·데이터 표현처럼 Unity 객체 기능이 필요 없는 코드는 일반 C# 클래스로 작성할 수 있습니다.
- 화면 표시, 대화 상태, 서버 통신처럼 역할이 다른 책임을 구분하되 작은 기능을 불필요한 인터페이스나 클래스로 나누지 않습니다.
- 사용하는 생명주기 함수만 작성합니다. 비어 있는 `Start`, `Update`를 남기지 않습니다.
- 매 프레임 처리할 일이 있을 때만 `Update`를 사용합니다. 일정한 속도로 이동·회전하려면 경과 시간을 반영합니다.

## Inspector 설정과 참조

- Inspector에서 조절할 값은 기본적으로 `[SerializeField] private` 필드로 노출합니다. 다른 코드에서 사용할 필요가 있을 때 공개 메서드나 프로퍼티를 제공합니다.
- 속도·시간 등 수치의 단위가 이름이나 Inspector 안내에서 드러나게 합니다.
- 코드의 필드 기본값은 새 컴포넌트의 초기값입니다. 기존 Scene·Prefab에 저장된 값이 코드 기본값 변경만으로 갱신된다고 가정하지 않습니다.
- 지속적으로 사용할 설정은 Play를 종료한 상태에서 변경하고 저장합니다. Play 중 변경은 동작 확인에 사용하며, 일반적인 Inspector 값 변경은 실행 종료 후 복원됩니다.
- 필수 객체 참조는 명시적으로 연결하고 누락을 확인합니다. 오류를 무시해 화면이 조용히 동작하지 않는 상태를 만들지 않습니다.
- 반복적인 전역 객체 탐색 대신 Inspector 참조 등 책임이 명확한 연결 방식을 우선합니다.

## Scene과 Prefab 협업

- Scene은 객체 배치와 화면·공간 구성을 담당하고, 반복 사용하는 UI·NPC 등은 Prefab으로 관리합니다.
- 모든 사용처에 반영할 변경은 Prefab 원본을 수정합니다. 특정 Scene의 객체만 다르게 할 때는 인스턴스 변경으로 구분합니다.
- Overrides를 원본에 적용하기 전에 다른 사용처에 미치는 영향을 확인합니다. 변경을 무조건 Apply All로 반영하지 않습니다.
- 같은 Scene·Prefab을 동시에 수정할 때는 담당자와 수정 범위를 먼저 조율합니다.
- 충돌 해결 후 참조 연결, Missing Script 여부와 화면 배치를 Editor에서 확인합니다.
- 버전 관리 설정은 `Visible Meta Files`, 직렬화는 `Force Text`를 유지합니다.

## 검증과 도구 적용

- 컴파일 오류를 해결한 뒤 Play 실행으로 변경 동작을 확인합니다. Web 관련 변경은 실제 브라우저에서도 검증합니다.
- API 호출과 마이크 연결은 성공뿐 아니라 권한 거부, 취소, 시간 초과와 오류 전달도 검증합니다.
- 샘플 동작에 구현을 복제하는 테스트를 추가하지 않습니다. 미션 판정·상태 변경 등 서비스 동작은 [공통 코드 규약](code-conventions.md)의 테스트 우선 개발 기준에 따라 테스트를 먼저 작성합니다. 테스트 방식과 실행 방법은 [프론트엔드 개발 지침](../frontend/AGENTS.md)을 따릅니다.
- C# 포맷은 CSharpier, Unity 규칙 검사는 Microsoft.Unity.Analyzers를 사용합니다. [Unity 실행 문서](unity-setup.md#c-포맷과-규칙-검사)의 공통 명령과 저장소에 고정한 도구 버전을 따릅니다.
- 린터는 빈 생명주기 메서드, 잘못된 Unity 콜백 선언·대소문자, 잘못된 객체 생성·직렬화 특성, Unity 객체의 잘못된 null 처리 등 선택한 규칙을 검사합니다. 이름의 의미와 책임 분리는 리뷰로 확인합니다.
- GitLab CI 연결은 별도 작업입니다. 편집기 경고와 Unity 컴파일만으로 공통 검사 명령을 대신하지 않습니다.
- 실행한 검사, 확인하지 못한 범위와 리뷰 의견은 MR·Jira에 기록합니다.

## 참고 자료

- [Unity 프로젝트 구성 가이드](https://unity.com/how-to/organizing-your-project)
- [Prefab 생성](https://docs.unity3d.com/6000.3/Documentation/Manual/CreatingPrefabs.html)
- [Unity Editor 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/class-EditorManager.html)
- [Unity 버전 관리 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/class-VersionControlSettings.html)
