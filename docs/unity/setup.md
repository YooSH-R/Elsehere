# Unity 개발 환경과 Web 실행

Unity 클라이언트는 `frontend/UnityClient`에서 관리하며 WebGL 기반 웹 브라우저 실행을 대상으로 합니다. 시작 Scene은 튜토리얼을 시작하는 내 방(`HomeScene`)이며, 도형으로 만든 대체 공간입니다. 서비스 화면과 음성·서버 연동은 별도 기능 작업에서 구현합니다.

## 개발 환경

| 항목 | 기준 |
| --- | --- |
| Unity Editor | Unity 6.3 LTS `6000.3.26f1` |
| 필수 설치 모듈 | Web Build Support |
| 프로젝트 템플릿 | Universal 3D |
| 그래픽 구성 | URP `17.3.0` |
| 스크립트 | C# |
| 시작 Scene | `Assets/Scenes/Production/HomeScene.unity` |

Editor 버전은 `ProjectSettings/ProjectVersion.txt`, 패키지 버전은 `Packages/manifest.json`과 `Packages/packages-lock.json`을 기준으로 맞춥니다. 버전 변경은 호환성을 검증하고 MR 리뷰를 거쳐 공유합니다. 대상 브라우저와 모바일 브라우저 지원 범위는 서비스 요구사항에 따라 결정합니다.

## 설치와 프로젝트 열기

1. [Unity Hub](https://unity.com/download)를 설치하고 사용할 수 있는 라이선스를 활성화합니다.
2. **Installs → Install Editor**에서 `6000.3.26f1`을 설치합니다. 목록에 없으면 [해당 버전 페이지](https://unity.com/releases/editor/whats-new/6000.3.26f1)의 Install을 사용합니다.
3. 설치 모듈에서 **Web Build Support**를 선택합니다. 기존 Editor에는 **Manage → Add modules**로 추가하고 Editor를 재시작합니다.
4. GitLab 저장소를 받은 뒤 **Projects → Add → Add project from disk**에서 `frontend/UnityClient`를 선택합니다. 팀 프로젝트를 사용할 때 새 프로젝트를 별도로 생성하지 않습니다.
5. 같은 Editor 버전으로 열고 패키지 복원과 에셋 가져오기가 끝날 때까지 기다립니다.
6. Project 창에서 `Assets/Scenes/Production/HomeScene`을 엽니다.

협업 브랜치와 Git 훅 설정은 [개발 및 협업 규칙](../development.md)을 따릅니다.

## 실행과 Web 빌드

Play를 실행하면 방에서 시작하고, 문으로 마을과 카페·숙소를 오갈 수 있습니다. 조작은 [Player 모듈](../../frontend/UnityClient/Assets/Scripts/Player/README.md), 공간과 출입구는 [World 모듈](../../frontend/UnityClient/Assets/Scripts/World/README.md)을 따릅니다. 개발 환경 확인용 샘플 `Assets/Scenes/SampleScene`(`DemoCube` 회전)은 Scene List에 넣지 않습니다.

1. Scene과 프리팹 변경을 저장하고 Console에 컴파일 오류가 없는지 확인합니다.
2. **File → Build Profiles → Platforms → Web**을 선택하고 **Switch Platform**으로 전환합니다. 사용자 정의 Web 프로파일에서는 **Switch Profile**을 사용합니다.
3. **Scene List**에 `Assets/Scenes/Production`의 `HomeScene`·`TownScene`·`CafeScene`·`HotelScene`이 이 순서로 체크되어 있는지 확인합니다. 다르면 **Tools → A305 → World → Build Spaces**로 다시 만듭니다.
4. **Build And Run**을 선택하고 프로젝트 내부의 `Builds/Web` 또는 저장소 밖의 별도 빌드 폴더에 저장합니다. `Assets` 안에는 빌드 결과를 저장하지 않습니다.
5. 브라우저에서 방 로딩, 이동, 문을 통한 공간 전환을 확인하고 브라우저 Console에서도 오류를 확인합니다.

Build And Run은 로컬 웹 서버를 사용합니다. 생성된 `index.html`을 직접 더블클릭하는 방식 대신 Build And Run이나 웹 서버를 통해 실행합니다. 로컬 빌드 성공과 외부 서버 배포 성공은 각각 검증합니다.

## C# 포맷과 규칙 검사

직접 작성한 `Assets/Scripts`와 `Assets/Tests` 아래의 C# 코드에 공통 검사 명령을 사용합니다. `Scripts/Editor`의 Editor 코드도 대상입니다. 폴더가 아직 없으면 해당 경로는 건너뜁니다. 외부 템플릿·패키지와 생성 파일은 포맷·규칙 검사 대상에서 제외하며, 컴파일에 필요한 참조로는 사용합니다.

### 준비

- Unity 프로젝트를 같은 Editor 버전으로 열어 패키지 복원과 컴파일을 마칩니다.
- **Assets → Open C# Project**로 프로젝트·솔루션 파일을 생성하거나 갱신합니다. Visual Studio Editor 패키지가 생성한 SDK 형식의 `.csproj`와 `.slnx` 또는 `.sln`을 사용합니다. 스크립트나 Assembly Definition을 추가·이동·삭제한 뒤에도 갱신합니다.
- 별도로 .NET SDK `10.0.401` 또는 같은 기능 버전의 최신 패치를 설치합니다. Unity의 C# 런타임·언어 버전을 변경하는 설정이 아니라 로컬 검사 도구의 실행 환경입니다. SDK 선택은 Unity 폴더의 `global.json`을 따릅니다.

| 역할 | 고정 버전 | 설정 위치 |
| --- | --- | --- |
| 포맷 정리·확인 | CSharpier `1.3.0` | `.config/dotnet-tools.json` |
| Unity 규칙 검사 | Microsoft.Unity.Analyzers `1.28.0` | `Directory.Build.targets` |
| 형식·대상별 규칙 | UTF-8·LF·공백 4칸·다음 줄 중괄호 | `.editorconfig` |

### 실행

저장소 최상위에서 PowerShell로 실행합니다. 첫 실행에는 고정 도구와 패키지를 받기 위한 NuGet 연결이 필요합니다.

```powershell
# C# 포맷 적용
./infra/scripts/unity-code-quality.ps1 -Action format

# 파일을 자동 수정하지 않고 포맷·컴파일·Unity 규칙 검사
./infra/scripts/unity-code-quality.ps1 -Action check
```

`format`은 C# 포맷만 수정합니다. `check`는 포맷 위반, 컴파일 오류, 선택한 Unity 규칙 위반이 있으면 실패 코드로 종료합니다. 새 스크립트가 생성된 프로젝트에 포함되지 않았으면 누락된 파일을 알리고 실패하므로 Unity에서 프로젝트 파일을 갱신한 뒤 다시 실행합니다. 검사 실행 중에는 프로젝트 구성과 소스 파일을 변경하지 않습니다.

검사 범위는 `Assets/Scripts/**/*.cs`와 `Assets/Tests/**/*.cs`이며, `.g.cs`·`.generated.cs`·`.designer.cs`와 자동 생성 표시가 있는 파일은 도구의 생성 코드 제외 기준을 따릅니다. Scene·Prefab·`.meta`는 수정하지 않습니다. `Assets/ThirdParty`, `Assets/TutorialInfo`, `Packages`, `Library`, `Temp`, 빌드 결과와 생성 `.csproj`·`.sln`·`.slnx`도 포맷 대상에서 제외합니다.

CLI 검사는 Unity가 만든 프로젝트·참조를 그대로 읽고 `UnityCodeQuality=true`일 때만 고정 분석기를 연결합니다. PC의 Visual Studio 분석기 참조는 그 실행에서만 고정 버전으로 대체하며 Unity 소스 생성기는 유지합니다. 생성 프로젝트 파일을 직접 수정하거나 저장소에 추가하지 않습니다. 검사 결과와 Unity Editor의 실제 컴파일·Play 동작은 각각 확인합니다.

### 편집기와 CI

- 편집기 내장 포맷과 CSharpier가 동시에 저장 시 포맷을 수행하지 않도록 설정합니다. 자동 포맷을 사용할 경우 저장소 버전의 CSharpier와 같은 결과인지 확인합니다.
- 이름 변경이나 규칙 자동 수정은 `format`에 포함하지 않습니다. 특히 직렬화된 필드 이름은 Scene·Prefab의 저장값에 영향을 줄 수 있으므로 별도 검토합니다.
- 최종 기준은 저장소의 `check` 명령입니다. IDE가 표시하는 분석기 경고는 설치 버전에 따라 다를 수 있습니다.
- GitLab CI에서는 Unity 프로젝트 생성·참조가 준비된 환경에서 같은 `check` 명령을 연결합니다. Web 빌드·실행 검증은 C# 정적 검사와 별도로 수행합니다.

## 팀 공유 파일

| 공유 | 제외 |
| --- | --- |
| `Assets`와 에셋·폴더의 `.meta` 파일 | `Library`, `Temp`, `Obj`, `Logs` |
| `Packages/manifest.json`, `Packages/packages-lock.json` | `UserSettings`, `.vs`, `.idea` |
| `ProjectSettings`, 프로젝트 `.gitignore` | `Build`, `Builds`, 생성된 `.csproj`·`.sln`·`.slnx` |

제외 기준은 프로젝트의 `.gitignore`를 따릅니다. Unity 프로젝트의 텍스트 파일은 저장소의 `.gitattributes`에 따라 LF로 공유합니다. 새로 저장소를 받은 팀원도 Editor 실행과 Web 빌드를 확인한 뒤 초기 환경 공유를 완료합니다.

## 후속 검증 범위

- 마이크: Unity Web의 기본 `Microphone` API는 지원되지 않으므로 브라우저 API 연결 방식을 검증합니다. 권한 허용·거부, 녹음과 재생을 실제 브라우저에서 확인합니다.
- 서버 통신: `UnityWebRequest`를 사용하는 API 샘플부터 검증합니다. 다른 출처의 서버에 접근할 때는 CORS 설정이 필요합니다.
- 배포: HTTPS, 압축 파일 응답 헤더, WebAssembly 파일 형식과 로딩을 외부 서버에서 검증합니다.
- 자동화: 수동 빌드·배포 절차를 검증한 뒤 CI 검사와 Web 빌드·배포 자동화를 구성합니다.

## 참고 자료

- [Unity Web 빌드 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/web-build-settings.html)
- [Unity Web 오디오 지원](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-audio.html)
- [Unity Web 네트워킹](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-networking.html)
- [브라우저 마이크 접근 조건](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getUserMedia)
- [Unity 개발 규약](conventions.md)
