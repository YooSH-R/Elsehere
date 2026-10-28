# Windows 노트북의 Unity 전용 Runner

Unity 테스트와 WebGL 빌드는 Windows 노트북의 `a305-unity-windows` Runner에서 실행합니다. 백엔드·AI·인프라 검사는 기존 EC2의 `a305-linux` Runner를 사용하며, 개발·운영 서비스는 EC2에서 실행합니다.

노트북에는 프로젝트와 같은 Unity `6000.3.26f1`, Web Build Support와 사용 가능한 라이선스가 필요합니다. 라이선스를 활성화한 Windows 계정으로 Runner를 실행합니다. Runner 등록·Online 상태와 실제 CI 테스트·빌드 성공은 각각 확인합니다.

Tripo 통합 검사에 필요한 Blender와 FBX는 테스트 스크립트가 준비합니다. 개인 모델 경로나 `TRIPO_*` 변수를 Runner 설정에 수동 등록하지 않습니다. [테스트 입력 준비](#blender와-fbx-테스트-입력)의 고정 버전·캐시와 생성 모델을 사용합니다.

저장된 바리스타 마스터 검수 테스트는 CI 작업의 `TRIPO_MASTER_RECORD`에 지정된 저장소 레코드와 `TRIPO_MASTER_SCENE`의 손 연결 검수 씬을 사용합니다. 별도 모델을 생성하거나 API를 호출하지 않으며, 로컬에서 전체 검사를 실행할 때도 `.gitlab/ci/unity.yml`의 같은 두 입력을 지정합니다.

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

`unity:editor-and-webgl`은 보호된 ref의 수동 연결 시험 또는 `dev` 자동 배포의 누적 변경 계획에서 Unity 검사가 선택됐을 때 실행합니다. 수동 Unity 연결 시험에는 `RUN_UNITY_BUILD=true`를 사용합니다. 실제 앱 전달까지 시험하는 절차는 [dev 자동 배포](../dev-auto-deployment.md)를 따릅니다. 일반 작업 브랜치나 일반 문서 변경으로 노트북 작업을 실행하지 않습니다.

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
- EditMode 전에 Blender와 테스트 FBX를 준비하고 같은 프로세스에서 테스트 입력 변수를 지정합니다.
- 테스트는 `-runTests`로 종료하고 `-quit`을 함께 사용하지 않습니다. WebGL 빌드에는 `-quit`과 저장소의 빌드 진입점을 사용합니다.
- GUI 실행 파일의 실제 종료까지 기다립니다. 테스트 XML 누락·실패·미실행·성공 테스트 0개 또는 빌드 실패를 성공으로 처리하지 않습니다.
- NUnit 결과와 Unity 로그는 임시 폴더에 생성한 뒤 GitLab 업로드용 사본을 Git에서 제외한 `dist/unity-ci/`에 모읍니다. Unity의 기본 NUnit XML을 JUnit 보고서로 표시하지 않습니다.
- WebGL 산출물은 `dist/webgl/`이며 `version.json`의 전체 커밋 SHA가 후보와 일치해야 합니다. 빌드된 정적 파일의 실제 HTTPS 로딩과 화면 확인은 별도 검증입니다.
- 동시에 하나만 실행하도록 Runner 제한과 `resource_group`을 사용합니다. CI 작업 폴더의 `Library`를 유지해 다시 가져오는 비용을 줄이며, 빌드 결과·로그는 새 작업에서 정리합니다.

## Blender와 FBX 테스트 입력

`infra/scripts/prepare-unity-test-inputs.ps1`은 테스트용 Blender `4.5.14` Windows x64 portable을 [공식 배포 경로](https://download.blender.org/release/Blender4.5/)에서 받습니다. ZIP의 SHA256을 공식 값 `b9533d2397ac1984db4466fb23a7a4649391cca93f6e84209f9bcc60d071c8b9`와 비교한 뒤 프로젝트의 `Library/TripoTools/`에 풉니다. 처음에는 다운로드·압축 해제 시간이 필요하며 다음 검사에서는 유지된 캐시를 사용합니다. 프로젝트 패키지나 시스템 Blender 설치는 변경하지 않습니다.

`infra/tests/tripo/create_ci_fixtures.py`는 매 실행의 임시 폴더에 다음 입력을 생성합니다.

| 입력 | 검증할 조건 |
| --- | --- |
| `Humanoid.fbx` | Humanoid 본 계층, 스킨 가중치, UV, 실제 좌표 변형이 있는 표정 Shape Key, Blender 왕복 후 소켓 보존 |
| `MisnamedColor.fbx` | PNG 바이트를 JPEG 이름으로 내장하고 같은 이미지를 색상·노멀에 연결한 재질 회귀 조건 |

`run-unity-ci.ps1`이 `TRIPO_REVIEW_MODEL`, `TRIPO_REVIEW_BLENDER`, `TRIPO_REVIEW_OUTPUT`, `TRIPO_MATERIAL_TEST_FBX`를 이 입력으로 지정합니다. 제작 모델·사용자 파일·Tripo API·생성 크레딧은 사용하지 않습니다. 준비에 실패하면 Unity를 실행하기 전에 오류로 종료하며, 기존 테스트의 조건을 제외하거나 미실행을 성공으로 바꾸지 않습니다.

준비 로그·테스트 입력·왕복 진단 결과는 `dist/unity-ci/Tripo/`에 모읍니다. FBX 생성기의 본·가중치·표정과 잘못된 재질 조건 자체는 `infra/tests/tripo/test_ci_fixtures.py`로 별도 확인할 수 있습니다. 이 검사는 실제 제작 모델의 외형·품질 승인을 대신하지 않습니다.

`RUN_UNITY_BUILD=true`의 수동 Unity 연결 시험은 앱을 배포하지 않습니다. `dev` 자동 배포의 자식 작업은 성공한 WebGL을 이미지 전달에 사용하며, [dev 자동 배포](../dev-auto-deployment.md)와 [배포 기준](../deployment.md)을 따릅니다. EC2에서 Unity를 실행하는 대안의 조건은 [EC2 빌드 검토](server-build.md)에 남겨 둡니다.
