# S15P31A305

영어 초보자가 여행 중 마주칠 상황을 AI와 대화하며 연습하는 언어 학습 프로젝트입니다. 카페와 음식점 등 장소별 역할과 미션을 중심으로 서비스를 구상하고 있습니다.

현재는 초기 폴더 구조, 협업 규칙, GitLab MR 템플릿을 준비한 단계입니다. 기술 스택과 실행 방법은 결정되는 대로 추가합니다.

## 담당 영역

| 영역 | 책임 |
| --- | --- |
| `frontend/` | 화면, 사용자 입력, 대화 인터페이스 |
| `backend/` | 인증, 사용자, 학습 기록, 서비스 API |
| `ai/` | 대화 생성, 학습 피드백, 프롬프트, 품질 평가 |
| `infra/` | 실행 환경, CI/CD, 배포, 운영 |

AI의 독립 서버 여부는 실제 실행 환경과 기술 스택을 정한 뒤 결정합니다. 각 서비스의 내부 구조는 선택한 프레임워크의 관례를 따릅니다.

## 폴더 구조

```text
.
├── frontend/                    # 화면과 사용자 상호작용
├── backend/                     # 서비스 API와 데이터 처리
├── ai/                          # 대화 생성, 프롬프트, 학습 피드백
├── infra/
│   ├── compose/                 # 로컬·개발·운영 실행 구성
│   ├── nginx/                   # 프록시 설정
│   └── scripts/                 # 배포, 상태 확인, 복구 스크립트
├── docs/
│   ├── api/                     # API·스트리밍 메시지 규격
│   ├── development.md           # 개발 및 협업 규칙
│   └── gitlab-setup.md          # GitLab 초기 설정과 확인 절차
├── .gitlab/
│   ├── ci/                      # 영역별 CI 설정
│   └── merge_request_templates/
│       ├── Default.md
│       └── Release.md
└── README.md
```

빈 폴더에는 Git에서 구조를 유지하기 위한 `.gitkeep`을 둡니다. 실제 파일이 추가되면 해당 폴더의 `.gitkeep`은 제거해도 됩니다. 실행 설정과 CI 파이프라인은 기술 스택을 정한 뒤 작성합니다.

## 협업 문서

- [개발 및 협업 규칙](docs/development.md)
- [GitLab 초기 설정과 확인 절차](docs/gitlab-setup.md)
- [Jira와 GitLab 연동](docs/jira-integration.md)
- [일반 MR 템플릿](.gitlab/merge_request_templates/Default.md)
- [릴리스 MR 템플릿](.gitlab/merge_request_templates/Release.md)

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
- `main`, `dev` 변경은 초기화 이후 MR로 반영합니다.
- 현재 CI/CD는 구성되지 않았습니다. 검증 방법과 결과는 MR에 직접 기록합니다.

## 프로젝트 도구

- [GitLab 저장소](https://lab.ssafy.com/s15-final/S15P31A305)
- [Jira 보드](https://ssafy.atlassian.net/jira/software/c/projects/S15P31A305/boards/16635)
- Jira 이슈 키의 접두사는 `S15P31A305`입니다. 자동 연동은 별도로 설정해야 합니다.
