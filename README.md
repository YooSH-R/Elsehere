# S15P31A305

영어 초보자가 여행 중 마주치는 상황을 AI NPC와 대화하며 연습하는 언어 학습 프로젝트입니다. 카페와 음식점 등 장소별 역할과 미션을 통해 필요한 표현을 익히는 것을 목표로 합니다.

## 학습 흐름

상황 선택 → NPC와 대화 → 표현 힌트 활용 → 미션 목표 달성 → 학습 피드백

## 기술 구성

| 영역 | 기술 및 선정 기준 |
| --- | --- |
| 백엔드 | Java 21 + Spring Boot 4.1.1, Gradle ([백엔드 README](backend/README.md)) |
| 프론트엔드 | Unity 6.3 LTS `6000.3.26f1` + C#, URP, WebGL 기반 브라우저 실행 |
| AI | Python + FastAPI의 AI 워커(`ai/worker`): Spring만 부르는 별도 프로세스. 모델은 GMS·Groq API, 음성 합성은 Kokoro ([AI 워커 README](ai/worker/README.md)) |
| 개발 도구 | 프로젝트 초기화 시 JDK·빌드 도구·의존성 버전 고정 |

Unity 클라이언트는 `frontend/UnityClient`에서 관리합니다. 시작 Scene은 개발 환경 확인용 샘플이며, 서비스 기능과 음성·API 연동은 별도 구현 범위입니다. 영역별 실행 방법은 각 프로젝트 문서에서 관리합니다.

## 담당 영역

| 영역 | 책임 |
| --- | --- |
| `frontend/` | 화면, 사용자 입력, 대화 인터페이스 |
| `backend/` | 인증, 사용자, 학습 기록, 서비스 API |
| `ai/` | 대화 생성, 학습 피드백, 프롬프트, 품질 평가 |
| `infra/` | 실행 환경, CI/CD, 배포, 운영 |

AI 워커는 Spring과 같은 서버에서 별도 프로세스(컨테이너)로 실행하고 외부에 포트를 열지 않습니다. 각 서비스의 내부 구조는 선택한 프레임워크의 관례를 따릅니다.

## 폴더 구조

```text
.
├── frontend/                    # 화면과 사용자 상호작용
├── backend/                     # 서비스 API와 데이터 처리
├── ai/
│   ├── worker/                  # AI 워커: 판정·NPC 대답·음성 인식·합성·힌트·대화 종료 API
│   ├── feedback/                # 대화 종료 학습 피드백 모듈
│   └── tools/                   # 측정 도구
├── infra/
│   ├── compose/                 # 로컬·개발·운영 실행 구성
│   ├── nginx/                   # 프록시 설정
│   └── scripts/                 # 배포, 상태 확인, 복구 스크립트
├── docs/
│   ├── api/                     # API·스트리밍 메시지 규격
│   ├── unity/                   # Unity 개발 환경과 규약
│   ├── code-conventions.md      # 공통 코드 규약과 검사 도구 적용 방침
│   ├── development.md           # 개발 및 협업 규칙
│   ├── gitlab-setup.md          # GitLab 운영 정책
│   └── jira-integration.md      # 작업과 코드 변경 이력 연동
├── .gitlab/
│   ├── ci/                      # 영역별 CI 설정
│   └── merge_request_templates/
│       ├── Default.md
│       └── Release.md
├── .agents/skills/              # Codex용 AI 작업 절차(스킬)
├── .claude/skills/              # Claude Code용 AI 작업 절차(스킬)
├── AGENTS.md                    # AI 코드·문서 작성 지침, 영역별 지침은 각 폴더의 AGENTS.md
├── CLAUDE.md                    # Claude Code가 AGENTS.md를 읽도록 연결
└── README.md
```

영역별 코드는 해당 폴더에서 관리합니다. 빈 폴더의 `.gitkeep`은 실제 파일을 추가할 때 제거합니다.

## 협업 문서

- [개발 및 협업 규칙](docs/development.md)
- [공통 코드 규약](docs/code-conventions.md)
- [변경 영역별 CI/CD와 운영 통합 검증 기준](docs/ci-cd.md)
- [Unity 개발 환경과 Web 실행](docs/unity/setup.md)
- [Unity 개발 규약](docs/unity/conventions.md)
- [AI 코드·문서 작성 지침](AGENTS.md)
- [AI 공통 검사와 테스트](ai/README.md)
- [GitLab 운영 정책](docs/gitlab-setup.md)
- [Jira와 GitLab 연동](docs/jira-integration.md)
- [단일 서버의 개발·운영 구성](docs/deployment.md)
- [개발·운영 서버 운영 방법](docs/server-operations.md)
- [일반 MR 템플릿](.gitlab/merge_request_templates/Default.md)
- [릴리스 MR 템플릿](.gitlab/merge_request_templates/Release.md)
- [검사 파이프라인과 Runner 연결](docs/ci-runner.md)

## 브랜치 흐름

```text
dev에서 작업 브랜치 생성
→ 작업 브랜치에서 구현과 검증
→ 작업 브랜치 → dev MR 및 리뷰
→ dev에서 통합 검증
→ dev → main 릴리스 MR
```

- `main`: 운영 배포를 위한 안정 버전이자 GitLab 기본 브랜치
- `dev`: 기능 통합과 개발 환경 검증을 위한 브랜치
- `main`, `dev` 변경은 MR로 반영합니다.
- 작성자 외 팀원 최소 1명의 리뷰와 변경 범위에 맞는 검증을 병합 기준으로 사용합니다. CI 도입 후에는 파이프라인 성공도 요구합니다.

## 프로젝트 도구

- [GitLab 저장소](https://lab.ssafy.com/s15-final/S15P31A305)
- [Jira 보드](https://ssafy.atlassian.net/jira/software/c/projects/S15P31A305/boards/16635)
- Jira 이슈 키의 접두사는 `S15P31A305`입니다. GitLab for Jira Cloud 앱으로 개발 탭에서 브랜치·커밋·MR을 확인하며, 작업 상태는 완료 조건에 따라 관리합니다.
