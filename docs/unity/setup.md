# Unity 개발 환경과 Web 실행

Unity 클라이언트는 `frontend/UnityClient`에서 관리하며 WebGL 기반 웹 브라우저 실행을 대상으로 합니다. 시작 Scene은 튜토리얼을 시작하는 내 방(`HomeScene`)이며, [집 실내 디자인](home-interior.md)을 적용합니다. 마을 외관은 [메인스트리트 디자인](mainstreet-art.md)을 따릅니다. 서비스 화면과 음성·서버 연동은 별도 기능 작업에서 구현합니다.

## 개발 환경

| 항목 | 기준 |
| --- | --- |
| Unity Editor | Unity 6.3 LTS `6000.3.26f1` |
| 필수 설치 모듈 | Web Build Support |
| 프로젝트 템플릿 | Universal 3D |
| 그래픽 구성 | URP `17.3.0` |
| 스크립트 | C# |
| 시작 Scene | `Assets/Scenes/Production/HomeScene.unity` |

Editor 버전은 `ProjectSettings/ProjectVersion.txt`, 패키지 버전은 `Packages/manifest.json`과 `Packages/packages-lock.json`을 기준으로 맞춥니다. 버전 변경은 호환성을 검증하고 MR 리뷰를 거쳐 공유합니다. 모바일 화면은 아래 반응형 Web 템플릿으로 대응하며, 모바일 터치 조작과 대상 브라우저의 기능 지원 범위는 별도로 검증합니다.

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

### 모바일 화면과 Web 템플릿

`Assets/WebGLTemplates/A305Responsive`는 PC와 모바일에서 게임 화면이 브라우저 창 전체를 채우게 합니다. 페이지에는 게임 화면만 두고 제품 이름·[전체 화면] 같은 줄을 두지 않으며, 브라우저 자체의 전체 화면(PC는 F11)을 씁니다. 캔버스 크기가 창에 따라 바뀌어도 Unity가 그 크기로 다시 그리므로 게임 화면을 자르거나 늘리지 않고, 화면 비율에 따라 보이는 범위가 달라집니다. UI는 1920×1080 기준으로 화면 크기에 맞춰 늘고 줄어듭니다(`UiBuilderTools.EnsureOverlayCanvas`). 노치·홈 표시줄의 안전 영역은 비우고, 브라우저 주소창에 따른 높이 변화도 반영합니다. 빌드 설정의 960×600은 처음 캔버스 크기일 뿐 화면 비율을 고정하지 않습니다.

게임 파일을 받는 동안 템플릿의 로딩 화면은 소개 그림(`intro/feature-*.jpg`)과 한 줄 문구를 3초마다 바꿔 보여 주고, 아래에 진행 막대를 둡니다. 로딩이 끝나면 로딩 화면이 서서히 사라지며 로그인 화면이 나옵니다. 이 화면이 Unity 시작 로고를 대신하므로 **Player Settings → Splash Image → Show Splash Screen**은 끕니다.

- 소개 그림은 기능마다 한 장씩 만든 1536×1024(3:2) 그림을 그대로 씁니다. 새 그림도 같은 크기로 만들어 자르거나 늘리지 않고 넣습니다.
- 왼쪽 위 로고(`logo.png`)는 `frontend/design/로고.webp`의 흰 바탕을 투명하게 바꾸고 글자 영역만 남긴 것입니다. 어두운 배경에서도 보이도록 흰 그림자를 두릅니다.
- 그림은 화면 비율과 상관없이 잘리지 않도록 흐린 배경 위 가운데 카드로 띄웁니다.
- 순서는 접속마다 섞습니다. 로딩이 짧으면 첫 장면만 보일 수 있으므로, 지난 접속의 첫 장면은 첫 자리에 두지 않습니다(브라우저 저장소 `elsehere.intro.lastFirstSlide`).
- 그림과 문구 목록은 `index.html`의 `introSlides`에서 관리합니다.

Web 실행은 `Application.targetFrameRate`의 기본값 `-1`을 유지해 브라우저의 화면 갱신 주기를 따릅니다. 실제 프레임은 기기 성능·화면 주사율·브라우저 정책에 따라 달라집니다. 144·240fps를 고정값으로 강제하지 않으며, 모바일 발열·배터리를 위한 별도 상한은 성능 검증 후 결정합니다. 동작 기준은 [Unity의 Web 프레임 제어](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-targetFrameRate.html)를 따릅니다.

- **Player Settings → Web → Resolution and Presentation → Web Template**에서 `A305Responsive`를 선택합니다. **Tools → A305 → Web → Configure Responsive Template**으로도 적용할 수 있습니다.
- **Tools → A305 → Web → Build**는 이 템플릿을 적용하고 현재 Scene List의 활성 씬을 `Builds/Web`으로 빌드합니다. 먼저 Build Profiles에서 Web을 활성 플랫폼으로 전환합니다. 같은 프로젝트가 Editor에 열려 있지 않으면 아래 명령을 사용하며, 새 환경에서도 Web용 렌더링 설정이 포함되도록 `-buildTarget WebGL`을 명시합니다.
- 템플릿을 수정한 뒤에는 다시 빌드합니다. `Builds/Web`의 HTML·CSS는 생성 결과이므로 직접 수정하지 않습니다.
- 모바일의 세로·가로 전환, 주소창 표시·숨김, 노치 영역, 로딩·오류 메시지와 게임 UI 가장자리의 잘림을 실제 기기에서 확인합니다. 모바일에서는 왼쪽 아래 조이스틱으로 이동하고 오른쪽 빈 영역을 드래그해 시점을 돌립니다. 두 손가락 동시 조작과 앱 전환·화면 회전 후 입력이 멈추는지도 확인합니다. PC의 키보드·마우스 조작은 그대로 사용할 수 있습니다.

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -batchmode -buildTarget WebGL -projectPath frontend/UnityClient -executeMethod A305.EditorTools.Common.WebBuildBuilder.Build -quit -logFile <저장소 밖 경로>\WebBuild.log
```

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

## 자동 빌드 진입점

`A305.EditorTools.Deployment.WebGlBuild.Build`는 활성화된 Scene List를 WebGL로 빌드하고 gzip 응답에 맞는 산출물과 `version.json`을 만듭니다. `WEBGL_BUILD_DIRECTORY`에 `Assets` 밖의 출력 폴더를 지정합니다. `CI_COMMIT_SHA`가 있으면 전체 커밋 SHA를 기록하고, 로컬 검증에서는 `local`을 기록합니다.

배포 빌드는 내용 해시를 파일명으로 사용해 이전 릴리스의 JavaScript·WebAssembly·데이터가 섞이지 않게 합니다. 웹 컨테이너는 시작 페이지와 일반 정적 파일에 `Cache-Control: no-cache`를 적용해 브라우저가 현재 파일을 재검증하도록 합니다. 압축 산출물은 내용이 바뀌면 URL도 바뀝니다.

자동화의 기본 실행 환경은 [Windows 노트북의 Unity 전용 Runner](windows-runner.md)입니다. EC2의 Editor·라이선스·자원 조건은 [EC2 빌드 검토](server-build.md)에 남깁니다. 빌드 스크립트, Runner 연결, 실제 CI 빌드와 서버 배포 성공은 각각 확인합니다.

`infra/scripts/run-unity-ci.ps1 -Action test`는 회귀 검사 입력을 준비하고 EditMode·PlayMode를 실행합니다. `TRIPO_MASTER_RECORD`와 `TRIPO_MASTER_SCENE`이 없으면 저장소의 카페 캐릭터 작업 기록(`Assets/Data/Tripo/100e191da12a84145a71d737bcfd57c2.asset`)과 손 바인딩 검수 씬(`Assets/Scenes/Sandbox/Tripo/LittleMung_Cafe_Female_112b39c5_HandBinding.unity`)을 사용해 마스터 골격·모션·이동 검사가 빠지지 않게 합니다. EditMode는 작업 기록의 원본 모델을 복사해 현재 마스터 적용 절차를 실행한 임시 후보를 검사하며, 저장된 옛 후보를 승인하거나 원본 기록을 갱신하지 않습니다. PlayMode는 지정된 검수 씬에서 이동을 검사합니다. 다른 캐릭터를 검사할 때는 두 환경 변수에 해당 Unity 에셋 경로를 지정합니다. 자동 검사 통과와 실제 외관 승인은 별도로 확인합니다.

## 후속 검증 범위

- 마이크: Unity Web의 기본 `Microphone` API는 지원되지 않으므로 브라우저 API 연결 방식을 검증합니다. 권한 허용·거부, 녹음과 재생을 실제 브라우저에서 확인합니다.
- 서버 통신: `UnityWebRequest`를 사용하는 API 샘플부터 검증합니다. 다른 출처의 서버에 접근할 때는 CORS 설정이 필요합니다.
- 배포: HTTPS, 압축 파일 응답 헤더, WebAssembly 파일 형식과 로딩을 외부 서버에서 검증합니다.
- 자동화: 수동 빌드·배포 절차를 검증한 뒤 CI 검사와 Web 빌드·배포 자동화를 구성합니다.

## Tripo 에셋 제작 작업대

**Tools → A305 → Assets → Tripo Workbench**에서 API 생성, Studio·파일 가져오기, Blender 수정본 반영,
표정·립싱크 미리보기, 부품·소켓 연결과 검수 프리팹 등록을 진행합니다.
첫 화면에서 **API 생성 / 웹·파일 가져오기**를 선택합니다. API 생성은 키와 API 크레딧을 사용하며,
웹·파일 가져오기와 로컬 검사에는 API 키가 필요하지 않습니다. 기존 작업은 **기존 작업 불러오기**에서 선택합니다.
저장소 루트에서 `./infra/scripts/install-tripo-bridge.ps1`을 실행해 공식 Bridge를 설치하고,
Blender 실행 파일은 각 개발 PC에서 설정합니다. Bridge 원본은 로컬 설치하며 Git에 넣지 않습니다.
서비스 여성 캐릭터는 수정된 리틀 멍 골격과 Female 부모 프리팹을 재사용합니다. API 자동 리깅 결과를 로컬에서 마스터에 다시 연결하고 검사합니다. 얼굴 Shape Keys 제작과 실제 외관 승인은 별도 작업입니다.

화면별 사용법, 파일 보존 방식과 자동 검증 범위는
[Tripo Asset Workbench 안내](../../frontend/UnityClient/Assets/Scripts/Editor/Tripo/README.md)를 따릅니다.
## 참고 자료

- [Unity Web 빌드 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/web-build-settings.html)
- [Unity Web 오디오 지원](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-audio.html)
- [Unity Web 네트워킹](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-networking.html)
- [브라우저 마이크 접근 조건](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getUserMedia)
- [Unity 개발 규약](conventions.md)
