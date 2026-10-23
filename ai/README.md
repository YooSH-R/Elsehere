# AI 검사와 테스트

워커·피드백 모듈·측정 도구는 Python 3.12와 Ruff 0.17.1을 사용합니다. 공통 설정은 `pyproject.toml`, 검사 의존성은 `requirements-dev.txt`에서 관리합니다. 워커 설정도 공통 설정을 상속합니다.

## 실행 환경

저장소 루트에서 Python 3.12 가상환경을 만듭니다.

```sh
python -m venv ai/.venv
```

Windows PowerShell에서는 `ai\.venv\Scripts\Activate.ps1`, Linux에서는 `source ai/.venv/bin/activate`로 활성화한 뒤 실행합니다.

```sh
python -m pip install -r ai/requirements-dev.txt
```

실제 음성 합성·로컬 모델용 선택 의존성과 모델 파일은 단위 테스트에 필요하지 않습니다. 피드백 패널 테스트는 Node.js 22 이상을 사용하며 별도 npm 설치가 필요하지 않습니다.

## 공통 검사

아래 명령은 저장소 루트에서 실행하며 CI도 같은 명령을 사용합니다.

```sh
python -m ruff check ai --config ai/pyproject.toml
python -m ruff format --check ai --config ai/pyproject.toml
```

포맷과 import 순서를 정리할 때는 다음 명령을 사용합니다.

```sh
python -m ruff check ai --config ai/pyproject.toml --select I --fix
python -m ruff format ai --config ai/pyproject.toml
```

직접 작성한 Python 운영 코드·테스트·도구를 검사합니다. 가상환경·캐시와 Python이 아닌 파일은 대상에서 제외합니다. 문법 오류·정의되지 않은 이름·미사용 import 등과 import 정렬을 검사하고, 줄바꿈은 LF로 맞춥니다. 형제 패키지를 연결하는 두 진입점은 경로 설정 후 import가 필요하여 `E402`만 파일별로 제외합니다.

## 테스트

| 실행 위치 | 명령 | 대상 |
| --- | --- | --- |
| `ai/feedback/` | `python -m pytest -q` | 피드백·계약·고정 응답·평가 도구 |
| `ai/worker/` | `python -m pytest -q` | FastAPI 요청·응답, 오류·시간 상한, 모의 모델 공급자 |
| 저장소 루트 | `node --test ai/feedback/tests/test_prototype_panel.cjs` | 피드백 패널 표시와 HTML 처리 |

두 Python 테스트 묶음은 같은 이름의 테스트 모듈이 있으므로 별도 프로세스로 실행합니다. 자동 테스트는 가짜 공급자와 고정 응답을 사용하며 실제 모델 API나 모델 다운로드를 실행하지 않습니다. 실제 공급자 평가·음성 합성·로컬 모델 검증은 각 모듈의 실행 문서를 따릅니다.

## CI 산출물

Python 검사 작업은 JUnit 결과와 커밋으로 식별하는 `ai-source.tar.gz`, `ai-commit.txt`를 14일간 보관합니다. 아카이브에는 워커 코드·모델 구성·의존성 목록과 피드백 코드·스키마·콘텐츠가 포함됩니다. 실제 비밀값과 모델 가중치는 포함하지 않습니다. 이미지 생성·서비스 기동·배포 후 상태 검증은 배포 구성에서 이어서 수행합니다.
