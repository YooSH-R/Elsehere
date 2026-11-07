# Windows 노트북의 Unity 전용 Runner

Unity 테스트와 WebGL 빌드는 Windows 노트북의 `a305-unity-windows` Runner에서 실행합니다. 일반 Linux 검사는 EC2와 노트북의 별도 Linux Runner가 `a305-linux-ci` 태그로 나눠 받으며, 배포 계획·전달과 개발·운영 서비스는 EC2에서 실행합니다. Linux Runner는 Unity와 별도로 최대 두 작업을 실행하며 [설치·제한 기준](../ci-runner.md#노트북-linux-runner)을 따릅니다.

노트북에는 프로젝트와 같은 Unity `6000.3.26f1`, Web Build Support와 사용 가능한 라이선스가 필요합니다. 라이선스를 활성화한 Windows 계정으로 Runner를 실행합니다. Runner 등록·Online 상태와 실제 CI 테스트·빌드 성공은 각각 확인합니다.

Tripo 통합 검사에 필요한 Blender와 FBX는 테스트 스크립트가 준비합니다. 개인 모델 경로나 `TRIPO_*` 변수를 Runner 설정에 수동 등록하지 않습니다. [테스트 입력 준비](#blender와-fbx-테스트-입력)의 고정 버전·캐시와 생성 모델을 사용합니다.

바리스타 마스터 검수 테스트는 `TRIPO_MASTER_RECORD`가 가리키는 작업 기록의 원본 모델을 임시 에셋 폴더에 복사하고, 현재 서비스 마스터 적용 절차로 다시 연결한 뒤 골격·모션을 검사합니다. 저장된 옛 후보에 재연결 정보만 덧붙여 통과시키지 않습니다. 원본 FBX·가져오기 설정·작업 기록이 보존되는지 함께 검사하고 임시 후보는 검사 후 제거합니다. `TRIPO_MASTER_SCENE`의 손 연결 검수 씬은 PlayMode 이동 검사에 사용합니다. API를 호출하지 않으며, 로컬에서도 `infra/scripts/run-unity-ci.ps1 -Action test`로 실행하면 CI와 같은 두 입력이 기본 지정됩니다.

## 등록과 실행

GitLab 프로젝트의 **Settings → CI/CD → Runners → Create project runner**에서 다음 값을 사용합니다.

| 항목 | 값 |
| --- | --- |
| 운영체제·설명 | Windows, `a305-unity-windows` |
| 태그 | `a305-unity-windows` |
| Run untagged jobs | 해제 |
| Protected | 배포·수동 검사의 보호된 ref 사용에 맞춰 설정 |
| Lock to current projects | 활성화, 이 프로젝트에만 연결 |
| 최대 작업 시간 | 3600초 |

현재 Unity를 사용 중인 계정의 일반 PowerShell에서 설치합니다. 서비스 계정의 라이선스·환경을 별도로 확인하기 전에는 아래의 사용자 세션에서 실행합니다.

Runner의 작업 셸은 PowerShell 7(`pwsh`)을 사용합니다. Windows PowerShell 5.1은 Runner가 만든 작업 스크립트를 시스템 코드 페이지(한국어 Windows는 CP949)로 읽어, MR 설명처럼 한글이 들어간 CI 변수가 있으면 작업 시작 단계에서 구문 오류(`ParserError`)로 실패합니다. 관리자 권한 없이 사용자 계정에 설치할 수 있습니다.

```powershell
winget install --id Microsoft.PowerShell --source winget --exact
pwsh --version
$unityRunnerDirectory = Join-Path $env:USERPROFILE 'a305-unity-runner'
New-Item -ItemType Directory -Path $unityRunnerDirectory -Force | Out-Null
Set-Location $unityRunnerDirectory
Invoke-WebRequest -UseBasicParsing -Uri 'https://gitlab-runner-downloads.s3.amazonaws.com/v18.11.4/binaries/gitlab-runner-windows-amd64.exe' -OutFile './gitlab-runner.exe'
./gitlab-runner.exe --version
./gitlab-runner.exe register --config ./config.toml
```

이미 설치·등록했다면 반복하지 않습니다. 등록 URL은 `https://lab.ssafy.com`, executor는 `shell`입니다. GitLab에서 받은 인증 토큰은 등록 프롬프트에만 입력하고 공유하지 않습니다. Runner 버전은 기존 GitLab의 18.11 계열에 맞춘 값이며, 업그레이드할 때 서버와의 호환성을 확인합니다.

설정 파일은 저장소 밖의 Runner 폴더에서 관리합니다. 최상위 `concurrent = 2`, 등록된 `[[runners]]`의 `limit = 2`, `executor = "shell"`, `shell = "pwsh"`를 맞추고, 기존 ID·URL·토큰은 보존합니다. 실행 중인 Runner는 설정을 자동으로 다시 읽으므로 동시 실행 수 변경을 위해 진행 중인 작업을 재시작하지 않습니다. `shell` 변경은 다시 읽은 설정에 반영되지 않으므로, 진행 중인 작업이 끝난 뒤 Runner를 재시작하고 첫 작업 로그의 `Using Shell (pwsh) executor`를 확인합니다. 확인 전에는 GitLab에서 Runner를 일시 중지해 다른 MR 작업을 받지 않게 합니다.

```powershell
notepad ./config.toml
./gitlab-runner.exe verify --config ./config.toml
./gitlab-runner.exe run --config ./config.toml --working-directory "$env:USERPROFILE\a305-unity-runner"
```

PowerShell 창과 사용자 세션을 유지하고, 빌드 중에는 절전에 들어가지 않도록 합니다. 노트북이 꺼져 있으면 Unity 작업이 대기하며 이미 EC2에 배포된 서비스는 계속 실행됩니다. Runner는 별도 `builds` 폴더에 후보 커밋을 내려받으므로 개발 중인 원본 체크아웃을 작업 폴더로 지정하지 않습니다. 멈춘 작업은 GitLab에서 취소하며, 작업 폴더의 Unity 프로세스를 강제 종료하지 않습니다. 강제 종료하면 남은 빌드 프로세스가 `Library/Bee`를 잡아 다음 작업의 스크립트 컴파일이 실패할 수 있습니다. [Windows Runner 설치](https://docs.gitlab.com/runner/install/windows/), [지원 셸](https://docs.gitlab.com/runner/shells/)

## CI 연결 시험

Unity 관련 MR의 전체 EditMode·PlayMode 검사는 최종 커밋의 로컬 작업 트리에서 실행하고 결과를 MR에 기록합니다. MR 파이프라인은 Windows Editor 작업을 만들지 않습니다. 다른 프로젝트의 Unity MR은 기존처럼 Linux의 `unity:external-mr`에서 차단하고 프로젝트 브랜치에서 검토·검증합니다.

`unity:editor-and-webgl`은 보호된 ref의 수동 연결 시험 또는 운영 배포 자식에서 병합 결과의 전체 테스트와 필요한 WebGL 빌드를 수행합니다. 개발 배포는 `unity:webgl-build`에서 테스트 없이 WebGL 빌드·업로드만 수행합니다. 두 작업의 제한 시간은 1시간이며 MR 파이프라인에는 추가하지 않습니다. 수동 전체 연결 시험에는 `RUN_UNITY_BUILD=true`를 사용합니다. 실제 앱 전달까지 시험하는 절차는 [dev 자동 배포](../dev-auto-deployment.md)를 따릅니다.

1. 실행할 커밋과 `.gitlab/ci/unity.yml`, `infra/scripts/run-unity-ci.ps1`을 검토합니다.
2. 수동 빌드 시험의 대상 ref가 보호되어 있는지 확인합니다.
3. **Build → Pipelines → New pipeline**에서 대상 ref를 고르고 `RUN_UNITY_BUILD` 값을 `true`로 지정합니다.
4. 노트북의 `unity:editor-and-webgl` 작업이 해당 커밋을 받아 실행하는지 확인합니다. 이 작업은 Linux 검사 대기열과 독립적으로 시작할 수 있습니다.
5. EditMode·PlayMode 테스트, WebGL 빌드와 산출물 업로드가 모두 성공하는지 확인합니다. 전체 MR의 필수 검사 통과 여부도 별도로 확인합니다.

Runner에서 실행되는 코드는 Windows 실행 계정 권한으로 동작합니다. 프로젝트의 신뢰하는 팀원에게만 브랜치 작성 권한을 주고, Runner는 프로젝트에 잠근 채 지정 태그만 받습니다. `dev`·`main`의 브랜치 보호와 배포 자격 증명의 보호·환경 범위는 유지하며, 배포용 SSH 키·서버 환경 파일을 노트북 작업에 추가하지 않습니다. MR 작업에 protected 변수 접근을 허용하는 설정도 켜지 않습니다. [보호된 Runner](https://docs.gitlab.com/ci/runners/configure_runners/#protecting-sensitive-information), [Shell executor](https://docs.gitlab.com/runner/executors/shell/)

배포와 수동 검사는 Runner의 동시 실행 제한으로 최대 두 작업을 실행하며, Unity 전체를 하나로 묶는 `resource_group`은 사용하지 않습니다. Shell executor의 기본 작업 경로는 동시 실행 번호별로 분리되므로 각 작업은 자신의 Unity 프로젝트·`Library`·산출물을 사용합니다. 작업 경로를 하나로 고정하거나 `Library`를 두 작업이 동시에 쓰도록 연결하지 않습니다. [Shell executor 작업 경로](https://docs.gitlab.com/runner/executors/shell/)

프로젝트 전용 Runner의 일반 대기열은 FIFO로 처리합니다. 선행 검사가 끝나 실행 가능한 `pending` 작업 중 오래된 작업부터 빈 두 실행 자리에 배정합니다. 아직 선행 검사가 끝나지 않은 오래된 파이프라인을 기다리기 위해 새 작업을 막지는 않습니다. 파이프라인 ID 순서를 강제하는 자원 그룹의 `oldest_first`는 한 그룹에서 한 작업만 실행하므로 두 작업 병렬 실행의 순서 설정으로 사용하지 않습니다. [프로젝트 Runner 대기열](https://docs.gitlab.com/ci/runners/runners_scope/#project-runners), [자원 그룹 실행 순서](https://docs.gitlab.com/ci/resource_groups/#process-modes)

MR의 로컬 테스트 실패·미실행·결과 누락은 병합 전에 해결합니다. 이 확인은 GitLab이 자동 강제하지 않습니다. 배포 검사 실패·미실행·결과 누락은 파이프라인에서 차단합니다. 동시 실행 시 노트북의 메모리·CPU·디스크 여유와 로컬 개발 작업의 응답을 확인하고 부족하면 Runner 제한을 다시 `1`로 낮춥니다. `waiting_for_resource` 상태는 일반 Runner 대기열과 별개입니다. 자원 그룹을 사용한 설정으로 이미 생성한 작업의 잠금은 새 YAML을 반영해도 소급 제거되지 않으므로 해당 자원 그룹의 실행 상태를 따로 확인합니다.

## 실행 스크립트와 결과

저장소 루트에서 같은 스크립트로 직접 확인할 수 있습니다.

```powershell
powershell.exe -NoProfile -NonInteractive -File ./infra/scripts/run-unity-ci.ps1 -Action test
powershell.exe -NoProfile -NonInteractive -File ./infra/scripts/run-unity-ci.ps1 -Action build
```

- Editor 경로는 프로젝트 버전에서 구하며, 다른 설치 경로는 `UNITY_EDITOR_PATH`로 지정합니다.
- 이미 병합·검증된 고정 씬의 배치·연결·Builder 회귀 검사는 정리했으며, 새 기능이나 변경한 씬의 테스트는 추가·수정할 수 있습니다. 씬 배치의 시각적 결과는 직접 Play 실행과 브라우저에서 확인합니다.
- 개발 중 `-ChangedSince <기준 커밋 SHA>`로 영향을 받는 테스트를 선택할 수 있습니다. 최종 커밋의 MR 전 검사는 이 옵션 없이 전체를 실행합니다. 선택 결과는 `dist/unity-ci/test-plan.json`에 남깁니다. 구체적인 기준은 [Unity 테스트 범위](../ci-runner.md#unity-테스트-범위)를 따릅니다.
- 전체 실행 또는 `A305.Tripo.*` 테스트를 선택한 경우 Blender와 테스트 FBX를 준비하고 같은 프로세스에서 테스트 입력 변수를 지정합니다. 다른 모듈만 검사할 때는 준비하지 않습니다.
- 공통 캐릭터의 목 경계·머리 크기 검사는 저장된 조합과 부품을 메모리에 복제하고 현재 가져오기 환경의 의존성 정보를 설정한 뒤, 원래의 불일치 메시가 각각의 오류로 검출되는지 확인합니다. 저장된 조합의 승인 상태나 검사 기준은 변경하지 않습니다.
- 테스트는 `-runTests`로 종료하고 `-quit`을 함께 사용하지 않습니다. WebGL 빌드에는 `-quit`과 저장소의 빌드 진입점을 사용합니다.
- Editor 프로세스의 실제 종료까지 기다립니다. 크래시 후 남은 컴파일 보조 프로세스 때문에 작업 종료가 지연되지 않도록 Editor의 종료 코드를 직접 확인하고 로그를 보존합니다. 테스트 XML 누락·실패·미실행·성공 테스트 0개 또는 빌드 실패를 성공으로 처리하지 않습니다.
- Editor가 실행 중이면 기본 30초마다 현재 단계(EditMode·PlayMode·WebGL)와 경과시간을 CI 로그에 남기고 상세 로그를 `dist/unity-ci/`로 복사합니다. 검사 도중 제한 시간으로 종료되어도 그때까지 복사된 로그를 산출물로 확인할 수 있습니다. 로컬 진단에서는 `-ProgressIntervalSeconds`로 주기를 조절할 수 있으며, 진행 메시지는 테스트 성공이나 정체 해소를 뜻하지 않습니다.
- NUnit 결과와 Unity 로그는 임시 폴더에 생성한 뒤 GitLab 업로드용 사본을 Git에서 제외한 `dist/unity-ci/`에 모읍니다. Unity의 기본 NUnit XML을 JUnit 보고서로 표시하지 않습니다.
- WebGL 산출물은 `dist/webgl/`이며 `version.json`의 전체 커밋 SHA가 후보와 일치해야 합니다. 빌드된 정적 파일의 실제 HTTPS 로딩과 화면 확인은 별도 검증입니다.
- CI는 WebGL 빌드 후 `publish-webgl-package.ps1`로 같은 프로젝트의 Generic Package Registry에 `a305-webgl/<파이프라인 ID>-<작업 ID>/webgl.tar`를 게시합니다. Windows 기본 `tar.exe`와 작업 중에만 유효한 `CI_JOB_TOKEN`을 사용하며 별도 토큰은 등록하지 않습니다. 프로젝트의 패키지 저장소가 활성화되어 있어야 합니다.
- 일반 GitLab 산출물에는 `dist/unity-ci/`의 테스트·로그와 `dist/webgl-package.json`의 커밋·파이프라인·SHA256·크기만 넣습니다. 큰 WebGL 파일을 일반 산출물 ZIP에 함께 넣지 않으므로 해당 ZIP의 용량 상한을 올릴 필요가 없습니다. 전달과 임시 패키지 정리는 [개발 자동 배포](../dev-auto-deployment.md#webgl-빌드-전달과-정리)를 따릅니다.
- 동시에 최대 두 작업을 실행하도록 Runner 제한을 사용합니다. 동시 실행 번호별 CI 작업 폴더의 `Library`를 유지해 다시 가져오는 비용을 줄이며, 빌드 결과·로그는 새 작업에서 정리합니다. 두 번째 작업 폴더의 최초 실행에는 별도의 에셋 가져오기·패키지 준비·Blender 설치 시간이 필요합니다.

Editor 종료 대기와 선택적 테스트 범위 판정의 회귀 검사는 Windows에서 `python -m unittest discover -s infra/scripts/tests -p "test_unity_*.py" -v`로 실행합니다. 가짜 Editor가 실패한 뒤 보조 프로세스가 남아도 실패와 로그가 즉시 전달되는지, 변경한 테스트·실행 코드의 전이 의존 관계·공유 입력·비교 실패에 따라 범위를 고르는지 검사하며 실제 Unity 테스트를 대신하지 않습니다. Linux의 인프라 검사에서는 이 Windows 전용 검사를 건너뜁니다.

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
