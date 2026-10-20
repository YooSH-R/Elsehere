# Unity 개발 환경과 Web 실행

Unity 클라이언트는 `frontend/UnityClient`에서 관리하며 WebGL 기반 웹 브라우저 실행을 대상으로 합니다. 시작 Scene은 개발 환경 확인용 샘플입니다. 서비스 화면과 음성·서버 연동은 별도 기능 작업에서 구현합니다.

## 개발 환경

| 항목 | 기준 |
| --- | --- |
| Unity Editor | Unity 6.3 LTS `6000.3.26f1` |
| 필수 설치 모듈 | Web Build Support |
| 프로젝트 템플릿 | Universal 3D |
| 그래픽 구성 | URP `17.3.0` |
| 스크립트 | C# |
| 시작 Scene | `Assets/_Project/Scenes/SampleScene.unity` |

Editor 버전은 `ProjectSettings/ProjectVersion.txt`, 패키지 버전은 `Packages/manifest.json`과 `Packages/packages-lock.json`을 기준으로 맞춥니다. 버전 변경은 호환성을 검증하고 MR 리뷰를 거쳐 공유합니다. 대상 브라우저와 모바일 브라우저 지원 범위는 서비스 요구사항에 따라 결정합니다.

## 설치와 프로젝트 열기

1. [Unity Hub](https://unity.com/download)를 설치하고 사용할 수 있는 라이선스를 활성화합니다.
2. **Installs → Install Editor**에서 `6000.3.26f1`을 설치합니다. 목록에 없으면 [해당 버전 페이지](https://unity.com/releases/editor/whats-new/6000.3.26f1)의 Install을 사용합니다.
3. 설치 모듈에서 **Web Build Support**를 선택합니다. 기존 Editor에는 **Manage → Add modules**로 추가하고 Editor를 재시작합니다.
4. GitLab 저장소를 받은 뒤 **Projects → Add → Add project from disk**에서 `frontend/UnityClient`를 선택합니다. 팀 프로젝트를 사용할 때 새 프로젝트를 별도로 생성하지 않습니다.
5. 같은 Editor 버전으로 열고 패키지 복원과 에셋 가져오기가 끝날 때까지 기다립니다.
6. Project 창에서 `Assets/_Project/Scenes/SampleScene`을 엽니다.

협업 브랜치와 Git 훅 설정은 [개발 및 협업 규칙](development.md)을 따릅니다.

## 실행과 Web 빌드

Play를 실행하면 Scene에 배치된 `DemoCube` 프리팹이 회전합니다. 회전 기능은 `Assets/_Project/Scripts/DemoCubeRotator.cs`가 담당합니다. 공유할 회전 속도는 Play를 종료하고 프리팹 원본의 **Demo Cube Rotator → Rotation Speed Degrees Per Second**를 수정한 뒤 저장합니다.

1. Scene과 프리팹 변경을 저장하고 Console에 컴파일 오류가 없는지 확인합니다.
2. **File → Build Profiles → Platforms → Web**을 선택하고 **Switch Platform**으로 전환합니다. 사용자 정의 Web 프로파일에서는 **Switch Profile**을 사용합니다.
3. **Scene List**에 `Assets/_Project/Scenes/SampleScene`이 포함되고 체크되어 있는지 확인합니다. 없으면 해당 Scene을 연 뒤 **Add Open Scenes**로 추가합니다.
4. **Build And Run**을 선택하고 프로젝트 내부의 `Builds/Web` 또는 저장소 밖의 별도 빌드 폴더에 저장합니다. `Assets` 안에는 빌드 결과를 저장하지 않습니다.
5. 브라우저에서 Scene 로딩과 큐브 회전을 확인하고 브라우저 Console에서도 오류를 확인합니다.

Build And Run은 로컬 웹 서버를 사용합니다. 생성된 `index.html`을 직접 더블클릭하는 방식 대신 Build And Run이나 웹 서버를 통해 실행합니다. 로컬 빌드 성공과 외부 서버 배포 성공은 각각 검증합니다.

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
- [Unity 개발 규약](unity-conventions.md)
