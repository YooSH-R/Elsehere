# EC2에서 Unity WebGL 빌드하기

Unity 자동화의 기본 실행 환경은 [Windows 노트북 전용 Runner](windows-runner.md)입니다. 이 문서는 EC2에서 빌드하는 대안의 적용 조건과 시험 절차입니다. 현재 서버에 Unity Runner가 설치되었거나 WebGL 자동 빌드가 연결되었다는 의미는 아닙니다.

## 지원 조건

- 프로젝트 Editor는 `6000.3.26f1`이며 Web Build Support 모듈이 필요합니다.
- 현재 서버의 Ubuntu 24.04·x86_64는 Unity Linux Editor의 지원 운영체제·아키텍처에 해당합니다. 화면을 사용하지 않는 빌드에는 `-batchmode -nographics`를 사용합니다. GPU가 없는 환경의 자동화가 지원되지만 GI 베이킹 등 GPU가 필요한 작업과 브라우저 화면 확인을 대신하지는 않습니다. [Editor 명령줄 옵션](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)
- Unity는 Editor 메모리로 최소 8GB를 권장하며, 실제 필요량은 프로젝트에 따라 달라집니다. 현재 Linux 검사 Runner의 3GiB 제한을 Unity 빌드에도 그대로 적용하지 않습니다. [Unity 6.3 시스템 요구사항](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)

## 라이선스와 설치 도구

기존 Editor의 시리얼 기반 활성화 명령과 새 Unity CLI를 구분합니다. 새 [Unity CLI](https://docs.unity.com/en-us/unity-cli)는 Hub 데스크톱 없이 설치할 수 있으며 현재 실험 단계입니다. 검토 기준 버전은 `1.0.0-beta.14`입니다. 버전을 고정하고 해당 버전의 도움말로 지원 옵션을 확인한 뒤 사용합니다.

공식 CLI에는 `unity auth login`, `unity license status`, `unity license activate --personal` 기능이 있습니다. Personal 활성화에는 `--accept-eula`가 필요하므로 사용자가 적용 가능한 라이선스 조건을 확인하고 직접 동의합니다. 서비스 계정 로그인은 Editor 라이선스 활성화를 대신하지 않습니다. [Unity CLI 사용법](https://docs.unity.com/en-us/unity-cli/use-unity-cli), [CLI 변경 기록](https://docs.unity.com/en-us/unity-cli/release-notes)

인증은 실제 빌드를 실행할 Linux 사용자로 수행합니다. 원격 브라우저 인증과 활성화가 성공하는지 먼저 시험합니다. 계정 비밀번호·인증 URL·토큰·라이선스 파일을 저장소나 작업 로그에 넣지 않습니다. CLI를 설치하거나 도움말을 확인한 것만으로 라이선스 활성화 성공으로 판단하지 않습니다.

## 같은 서버에서 실행할 때의 구성

| 항목 | 기준 |
| --- | --- |
| 일반 검사 | 기존 `a305-linux` Runner와 CPU·메모리 제한 유지 |
| Unity 실행 | 별도 Linux 사용자와 Unity 전용 Runner, 지속되는 Editor·라이선스 저장 경로 |
| 실행 대상 | 보호된 `dev` 배포 후보. 검토 전 MR 코드에 라이선스 사용자 환경을 제공하지 않음 |
| 자원 시험 시작값 | CPU 2개·메모리 8GiB 상한. 충분한 값으로 확정한 것이 아니며 실제 빌드로 측정 |
| 동시 실행 | 일반 검사와 Unity 빌드를 같은 프로젝트의 공통 `resource_group`으로 직렬화 |
| 데이터 | 빌드 전용 작업 폴더·캐시. 서비스 실행 폴더·DB 볼륨·환경 파일과 분리 |
| 권한 | 빌드 사용자는 서비스 환경 파일, 호스트 Docker 소켓과 배포용 자격 증명을 사용하지 않음 |

서버 전체 메모리는 약 15GiB이며 개발·운영 서비스와 OS도 함께 사용합니다. Unity의 8GiB 상한을 추가해도 서비스에 필요한 여유가 남는지 확인합니다. 다른 검사 작업과 겹치지 않는다고 해서 서비스 부하까지 없어지는 것은 아닙니다. 측정 결과 여유가 부족하거나 빌드가 반복해서 실패하면 별도 빌드 머신을 사용합니다.

## 적용 순서와 판정

1. 실제 빌드 사용자, Editor·캐시·작업 폴더와 현재 서버 자원 사용량을 확인합니다.
2. 같은 사용자로 버전을 고정한 공식 CLI를 설치하고 도움말·인증·라이선스 상태를 확인합니다. 계정 로그인과 약관 동의는 사용자가 직접 수행합니다.
3. Editor `6000.3.26f1`과 `webgl` 모듈을 설치합니다. 프로젝트에 Unity Pipeline 패키지를 추가하지 않고 Editor의 `-executeMethod`로 저장소의 빌드 스크립트를 실행합니다.
4. 자원 상한 안에서 EditMode 테스트와 실제 WebGL 빌드를 한 번 수행하고 종료 코드, 최대 메모리, 실행 시간, 서비스 health를 확인합니다. 캐시가 없는 첫 빌드와 캐시를 사용하는 후속 빌드를 구분합니다.
5. 성공하면 Unity 전용 Runner와 공통 실행 잠금을 연결합니다. 실패하면 라이선스·의존성·메모리·빌드 로그의 원인을 해결한 뒤 다시 시험하며 필수 검사를 생략하지 않습니다.
6. 만들어진 정적 파일을 웹 이미지로 포장하고 실제 `/dev/` 경로에서 화면·압축 응답을 확인합니다. 자동 배포 성공·실패·재실행을 검증한 뒤 완료 처리합니다.

빌드 명령은 실행 환경에서 다음 변수를 지정해 사용합니다. `UNITY_PROJECT_PATH`는 빌드 전용 체크아웃의 `frontend/UnityClient`, `UNITY_EDITOR_PATH`는 설치된 Linux Editor 실행 파일, `WEBGL_BUILD_DIRECTORY`는 `Assets` 밖의 출력 폴더입니다. `CI_COMMIT_SHA`는 해당 체크아웃의 전체 커밋 SHA입니다.

```bash
export WEBGL_BUILD_DIRECTORY="${WEBGL_BUILD_DIRECTORY:?Set the output directory}"
export CI_COMMIT_SHA="${CI_COMMIT_SHA:?Set the exact candidate SHA}"
mkdir -p "$(dirname "$WEBGL_BUILD_DIRECTORY")"
/usr/bin/time -v "${UNITY_EDITOR_PATH:?Set the Editor executable}" \
  -batchmode -nographics -quit -buildTarget WebGL -job-worker-count 2 \
  -projectPath "${UNITY_PROJECT_PATH:?Set the build project path}" \
  -executeMethod A305.EditorTools.Deployment.WebGlBuild.Build \
  -logFile "$WEBGL_BUILD_DIRECTORY.log"
```

스크립트는 활성화된 Scene List를 순서대로 빌드하고 gzip 압축과 버전 파일을 생성합니다. 빈 Scene 목록과 `Assets` 내부 출력은 거부하고, 빌드 실패를 성공으로 처리하지 않습니다. 실행 환경의 자원 상한은 위 명령과 별도로 설정해야 합니다.
