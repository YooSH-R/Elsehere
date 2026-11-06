# 프론트엔드 개발 지침

루트 [AGENTS.md](../AGENTS.md), [공통 코드 규약](../docs/code-conventions.md), [Unity 개발 규약](../docs/unity/conventions.md)을 기본으로 적용합니다. 이 문서는 Unity 클라이언트(`UnityClient/`)의 테스트 우선 개발 기준과 AI 에이전트의 작업 범위를 정합니다. 진행 절차는 `tdd` 스킬을 따릅니다.

## 에이전트 작업 범위

- C# 스크립트와 테스트 코드는 직접 작성하고 수정합니다.
- 씬(`.unity`)·프리팹(`.prefab`)·데이터 에셋(`.asset`)은 직렬화 파일을 텍스트로 직접 쓰지 않고, [Unity 개발 규약의 에셋 Builder](../docs/unity/conventions.md#에셋-builder)로 만듭니다. Builder를 작성해 batchmode `-executeMethod`로 실행하고, 생성된 파일을 커밋해 사용자가 Editor에서 바로 열어 확인할 수 있게 합니다. 코드만 작성하고 씬·프리팹 구성을 사용자에게 넘기지 않습니다.
- Builder는 규약의 [Builder 동작 기준](../docs/unity/conventions.md#에셋-builder)에 따라 디자인 작업이 들어가는 에셋을 덮어쓰지 않습니다. 대체 에셋은 없을 때만 만들고, 기능 프리팹은 컴포넌트와 연결만 맞춥니다.
- Editor가 프로젝트를 열고 있어 batchmode를 실행할 수 없으면, Editor의 `Tools/A305/...` 메뉴로 Builder를 실행하도록 안내하고 결과를 받아 진행합니다.
- `.meta` 파일은 직접 만들거나 수정하지 않습니다. batchmode 실행이나 Editor 가져오기로 Unity가 생성한 `.meta`를 에셋과 함께 커밋합니다.
- 씬 디자인에서 사물을 추가할 때는 공용 단어 연결도 함께 완료합니다. 편집 원본은 `UnityClient/Assets/Data/ObjectHover/Words/`의 `ObjectWordDefinition` 에셋이며, 프리팹의 `HoverableObject.Word`가 직접 참조합니다. 기존 단어를 재사용하고, 새 단어가 필요하면 뜻·영문 예문·예문 해석을 함께 등록합니다. 내부 공용 키는 도구가 관리하므로 배치마다 식별자를 입력하지 않습니다.
- 침대의 베개·이불, 식물의 화분처럼 의미 있는 세부 부분도 등록합니다. 가구별 영역과 공용 단어는 `Data/ObjectHover/Parts/`의 `ObjectWordPartsDefinition` 에셋에서 관리하고 프리팹은 참조만 가집니다. 멀리서는 가구 전체, 설정 거리 안에서는 실제 가리킨 부분을 표시합니다. 거리·지연·포인터 간격·화면 여백은 `Data/ObjectHover/ObjectHoverSettings.asset`에서 관리합니다.
- `BuilderTools`로 프리팹·씬을 저장하면 `UnityClient/Assets/Scripts/Editor/ObjectHover/ObjectWordBindings.json`에 등록된 이름의 사물에 공용 에셋과 호버 영역을 연결합니다. 이미 지정한 에셋 참조는 유지합니다. 새 이름은 연결표에 추가하거나 Builder에서 에셋을 직접 지정합니다.
- 디자인 완료 전에 `Tools/A305/ObjectHover/Register All Scene Words`를 실행해 배포용 4개 씬의 누락 검사를 통과시킵니다. 이 도구는 백엔드 파일도 내보냅니다. 내용만 수정했다면 `Export Backend Data`, 검증은 `Check Backend Export` 메뉴를 사용합니다. Editor가 닫혀 있으면 저장소 루트의 `./infra/scripts/sync-object-words.ps1`과 `-Check`로 같은 작업을 실행합니다. 백엔드의 생성 JSON·SQL 어휘 구간은 따로 편집하지 않습니다.
- `Packages/manifest.json`의 패키지 추가와 변경은 필요성을 설명하고 확인을 받은 뒤 진행합니다.

## 테스트 대상과 방식

프로젝트에 Unity Test Framework(`com.unity.test-framework`)가 포함되어 있으며, 테스트는 NUnit 기반으로 작성합니다.

| 대상 | 방식 | 예 |
| --- | --- | --- |
| Unity 객체 기능이 필요 없는 C# 로직 | EditMode 테스트 | 대화 상태 전환, 미션 판정, API 응답 변환, 입력값 검증 |
| 컴포넌트 생명주기, 코루틴, 프레임 진행이 필요한 동작 | PlayMode 테스트 | 시간에 따른 상태 변화, 컴포넌트 사이의 상호작용 |
| 배치, 카메라 연출, 애니메이션, 셰이더, 화면 디자인 | 자동 테스트 대상에서 제외 | Play 실행과 브라우저에서 확인 |

- 테스트할 로직은 `MonoBehaviour`와 분리한 일반 C# 클래스로 작성합니다. `MonoBehaviour`는 입력 전달, 화면 표시, Unity 객체 연결을 맡고 판단은 분리한 클래스에 맡깁니다.
- EditMode 테스트를 우선합니다. PlayMode 테스트는 실제 프레임 진행이나 Unity 객체가 필요할 때만 작성합니다.
- 이미 병합·검증된 고정 씬의 배치·직렬화 연결·Builder 회귀 검사는 정리했습니다. 새 기능이나 변경한 씬의 동작 검증이 필요하면 관련 테스트를 작성할 수 있습니다. 씬 배치의 시각적 결과는 직접 Play 실행과 브라우저에서 확인합니다.
- 서버 통신과 브라우저 마이크 연결은 호출 경계를 분리하고, 테스트에서는 대체 구현으로 성공·실패·취소·시간 초과를 검증합니다.
- 브라우저 전용 연동(`.jslib` 등)은 Editor 테스트에서 실행되지 않습니다. 연동 코드는 얇게 유지하고 실제 브라우저에서 확인합니다.

## 테스트 어셈블리 구성

테스트 어셈블리는 기능별로 둡니다. 기능 어셈블리 구성은 [Unity 개발 규약의 기능 모듈](../docs/unity/conventions.md#기능-모듈)을 따릅니다.

- Assembly Definition을 사용하는 테스트 어셈블리는 기본 어셈블리(`Assembly-CSharp`)를 참조할 수 없습니다. 테스트할 코드에 Assembly Definition(`A305.<기능>`)을 두고 테스트 어셈블리에서 참조합니다.
- 테스트는 `Assets/Tests/EditMode/<기능>/`, `Assets/Tests/PlayMode/<기능>/`에 두고 테스트 어셈블리 이름은 `A305.<기능>.Tests`로 합니다. 같은 기능에 PlayMode 테스트도 있으면 `A305.<기능>.PlayModeTests`로 구분합니다. 예: `Tests/EditMode/Player/A305.Player.Tests.asmdef`, `Tests/PlayMode/World/A305.World.PlayModeTests.asmdef`
- 테스트용 Assembly Definition은 Test Runner 창의 생성 기능이 만드는 설정(`nunit.framework.dll`, `UNITY_INCLUDE_TESTS`, EditMode는 Editor 전용·PlayMode는 모든 플랫폼)과 같게 두고, 테스트할 기능 어셈블리를 References에 추가합니다.

## 실행 방법

- Editor: **Window → General → Test Runner**에서 EditMode·PlayMode 테스트를 실행합니다.
- 명령줄: Editor가 이 프로젝트를 열고 있지 않을 때만 사용할 수 있습니다. 저장소 최상위에서 실행합니다.

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -batchmode -projectPath frontend/UnityClient -runTests -testPlatform EditMode -testResults <저장소 밖 경로>\EditModeResults.xml
```

- `-runTests`는 테스트가 끝나면 Editor를 종료하므로 `-quit`을 함께 쓰지 않습니다. PlayMode는 `-testPlatform PlayMode`로 실행합니다.
- Red-Green 반복 중에는 `-testFilter`로 작성 중인 테스트와 바꾼 코드를 쓰는 묶음만 실행합니다. 여러 묶음은 `;`로 잇습니다. 예: `-testFilter "A305.World.PlayModeTests.SpaceTravelTests;A305.Npc.PlayModeTests.NpcRoutineTests"`
- 푸시하기 전에는 저장소 최상위에서 `./infra/scripts/run-unity-ci.ps1 -Action test`로 CI와 같은 조건(Tripo 입력 포함)의 EditMode·PlayMode 전체를 한 번 실행하고, `dotnet csharpier check Assets/Scripts Assets/Tests`로 형식을 확인합니다.
- 전체 검사는 MR의 최종 커밋과 일치하는 작업 트리에서 실행하고, 검사한 커밋 SHA·작업 트리 변경 여부·각 테스트 결과를 MR에 기록합니다. 검사 후 코드나 에셋을 바꾸면 최종 상태에서 다시 실행합니다. 실패·미실행 결과는 통과로 기록하지 않고 해결 전에는 병합하지 않습니다. GitLab MR 파이프라인은 이 로컬 결과를 자동으로 확인하지 않습니다.
- 결과 파일은 저장소 밖에 저장합니다.
- Editor가 프로젝트를 열고 있거나 Unity를 실행할 수 없으면, 테스트를 작성한 뒤 사용자에게 Test Runner 실행을 요청하고 결과를 받아 다음 단계로 진행합니다. 요청 횟수를 줄이려고 관련 시나리오 몇 개를 묶어 확인할 수 있지만, 구현 전에 묶은 테스트가 모두 실패하는지 먼저 확인합니다.
- 결과를 확인하기 전에는 Red·Green을 확인했다고 보고하지 않습니다.
