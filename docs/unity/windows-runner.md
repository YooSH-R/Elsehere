# Windows 노트북의 Unity 전용 Runner

Unity 테스트와 WebGL 빌드는 Windows 노트북의 `a305-unity-windows` Runner에서 실행합니다. 백엔드·AI·인프라 검사는 기존 EC2의 `a305-linux` Runner를 사용하며, 개발·운영 서비스는 EC2에서 실행합니다.

노트북에는 프로젝트와 같은 Unity `6000.3.26f1`, Web Build Support와 사용 가능한 라이선스가 필요합니다. 라이선스를 활성화한 Windows 계정으로 Runner를 실행합니다. Runner 등록·Online 상태와 실제 CI 테스트·빌드 성공은 각각 확인합니다.

## 등록과 실행

GitLab 프로젝트의 **Settings → CI/CD → Runners → Create project runner**에서 다음 값을 사용합니다.

| 항목 | 값 |
| --- | --- |
| 운영체제·설명 | Windows, `a305-unity-windows` |
| 태그 | `a305-unity-windows` |
| Run untagged jobs | 해제 |
| Protected | 활성화 |
| 최대 작업 시간 | 3600초 |

현재 Unity를 사용 중인 계정의 일반 PowerShell에서 설치합니다. 서비스 계정의 라이선스·환경을 별도로 확인하기 전에는 아래의 사용자 세션에서 실행합니다.

```powershell
$unityRunnerDirectory = Join-Path $env:USERPROFILE 'a305-unity-runner'
New-Item -ItemType Directory -Path $unityRunnerDirectory -Force | Out-Null
Set-Location $unityRunnerDirectory
Invoke-WebRequest -UseBasicParsing -Uri 'https://gitlab-runner-downloads.s3.amazonaws.com/v18.11.4/binaries/gitlab-runner-windows-amd64.exe' -OutFile './gitlab-runner.exe'
./gitlab-runner.exe --version
./gitlab-runner.exe register --config ./config.toml
```

이미 설치·등록했다면 반복하지 않습니다. 등록 URL은 `https://lab.ssafy.com`, executor는 `shell`입니다. GitLab에서 받은 인증 토큰은 등록 프롬프트에만 입력하고 공유하지 않습니다. Runner 버전은 기존 GitLab의 18.11 계열에 맞춘 값이며, 업그레이드할 때 서버와의 호환성을 확인합니다.

설정 파일은 저장소 밖의 Runner 폴더에서 관리합니다. 최상위 `concurrent = 1`, 등록된 `[[runners]]`의 `limit = 1`, `executor = "shell"`, `shell = "powershell"`을 맞춥니다. 기존 ID·URL·토큰은 보존합니다.

```powershell
notepad ./config.toml
./gitlab-runner.exe verify --config ./config.toml
./gitlab-runner.exe run --config ./config.toml --working-directory "$env:USERPROFILE\a305-unity-runner"
```

PowerShell 창과 사용자 세션을 유지하고, 빌드 중에는 절전에 들어가지 않도록 합니다. 노트북이 꺼져 있으면 Unity 작업이 대기하며 이미 EC2에 배포된 서비스는 계속 실행됩니다. Runner는 별도 `builds` 폴더에 후보 커밋을 내려받으므로 개발 중인 원본 체크아웃을 작업 폴더로 지정하지 않습니다. [Windows Runner 설치](https://docs.gitlab.com/runner/install/windows/), [지원 셸](https://docs.gitlab.com/runner/shells/)

## CI 연결 시험

`unity:editor-and-webgl`은 보호된 ref에서 웹으로 실행하고 `RUN_UNITY_BUILD=true`를 지정한 경우에만 선택합니다. 일반 작업 브랜치나 일반 문서 변경으로 노트북 작업을 실행하지 않습니다. `dev` 자동 배포의 누적 변경 판정과 이미지 전달은 별도로 연결·검증해야 합니다.

1. 실행할 커밋과 `.gitlab/ci/unity.yml`, `infra/scripts/run-unity-ci.ps1`을 검토합니다.
2. 대상 ref가 보호되어 있는지 확인합니다. MR 병합 전 작업 브랜치를 시험해야 한다면 해당 브랜치만 임시 보호하는 절차를 사용자와 합의합니다. Runner의 Protected 설정을 해제해 시험하지 않습니다.
3. **Build → Pipelines → New pipeline**에서 대상 ref를 고르고 `RUN_UNITY_BUILD` 값을 `true`로 지정합니다.
4. 노트북의 `unity:editor-and-webgl` 작업이 해당 커밋을 받아 실행하는지 확인합니다. 이 작업은 Linux 검사 대기열과 독립적으로 시작할 수 있습니다.
5. EditMode·PlayMode 테스트, WebGL 빌드와 산출물 업로드가 모두 성공하는지 확인합니다. 전체 MR의 필수 검사 통과 여부도 별도로 확인합니다.

보호된 Runner를 실행할 수 있는 사람의 코드가 Windows 실행 계정 권한으로 동작합니다. 우리 프로젝트의 검토된 후보에만 사용하며, 배포용 SSH 키·서버 환경 파일을 노트북 작업에 추가하지 않습니다. [보호된 Runner](https://docs.gitlab.com/ci/runners/configure_runners/#protecting-sensitive-information), [Shell executor](https://docs.gitlab.com/runner/executors/shell/)

## 실행 스크립트와 결과

저장소 루트에서 같은 스크립트로 직접 확인할 수 있습니다.

```powershell
powershell.exe -NoProfile -NonInteractive -File ./infra/scripts/run-unity-ci.ps1 -Action test
powershell.exe -NoProfile -NonInteractive -File ./infra/scripts/run-unity-ci.ps1 -Action build
```

- Editor 경로는 프로젝트 버전에서 구하며, 다른 설치 경로는 `UNITY_EDITOR_PATH`로 지정합니다.
- 테스트는 `-runTests`로 종료하고 `-quit`을 함께 사용하지 않습니다. WebGL 빌드에는 `-quit`과 저장소의 빌드 진입점을 사용합니다.
- GUI 실행 파일의 실제 종료까지 기다립니다. 테스트 XML 누락·실패·성공 테스트 0개 또는 빌드 실패를 성공으로 처리하지 않습니다.
- NUnit 결과와 Unity 로그는 임시 폴더에 생성한 뒤 GitLab 업로드용 사본을 Git에서 제외한 `dist/unity-ci/`에 모읍니다. Unity의 기본 NUnit XML을 JUnit 보고서로 표시하지 않습니다.
- WebGL 산출물은 `dist/webgl/`이며 `version.json`의 전체 커밋 SHA가 후보와 일치해야 합니다. 빌드된 정적 파일의 실제 HTTPS 로딩과 화면 확인은 별도 검증입니다.
- 동시에 하나만 실행하도록 Runner 제한과 `resource_group`을 사용합니다. CI 작업 폴더의 `Library`를 유지해 다시 가져오는 비용을 줄이며, 빌드 결과·로그는 새 작업에서 정리합니다.

현재 연결 시험은 앱 배포를 수행하지 않습니다. 실제 앱으로 전환할 때는 [배포 기준](../deployment.md)과 [서버 운영 방법](../server-operations.md)을 따릅니다. EC2에서 Unity를 실행하는 대안의 조건은 [EC2 빌드 검토](server-build.md)에 남겨 둡니다.
