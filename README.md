# S15P31A305

영어 초보자가 여행 중 마주치는 상황을 AI NPC와 대화하며 연습하는 언어 학습 프로젝트입니다. 카페와 음식점 등 장소별 역할과 미션을 통해 필요한 표현을 익히는 것을 목표로 합니다.

## 학습 흐름

상황 선택 → NPC와 대화 → 표현 힌트 활용 → 미션 목표 달성 → 학습 피드백

## 기술 구성

| 영역 | 기술 및 선정 기준 |
| --- | --- |
| 백엔드 | Java + Spring Boot |
| 프론트엔드 | 화면·3D 상호작용 설계에 따라 언어와 프레임워크 선정 |
| AI | PoC와 처리 흐름 설계에 따라 모델·언어·실행 방식 선정 |
| 개발 도구 | 프로젝트 초기화 시 JDK·빌드 도구·의존성 버전 고정 |

프론트엔드와 AI의 미정 기술은 설계 결정 후 반영합니다. 영역별 실행 방법은 각 프로젝트에서 관리합니다.

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
│   ├── code-conventions.md      # 공통 코드 규약과 검사 도구 적용 방침
│   ├── development.md           # 개발 및 협업 규칙
│   ├── gitlab-setup.md          # GitLab 운영 정책
│   └── jira-integration.md      # 작업과 코드 변경 이력 연동
├── .gitlab/
│   ├── ci/                      # 영역별 CI 설정
│   └── merge_request_templates/
│       ├── Default.md
│       └── Release.md
├── AGENTS.md                    # AI 코드·문서 작성 지침
└── README.md
```

영역별 코드는 해당 폴더에서 관리합니다. 빈 폴더의 `.gitkeep`은 실제 파일을 추가할 때 제거합니다.

## 협업 문서

- [개발 및 협업 규칙](docs/development.md)
- [공통 코드 규약](docs/code-conventions.md)
- [AI 코드·문서 작성 지침](AGENTS.md)
- [GitLab 운영 정책](docs/gitlab-setup.md)
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
- `main`, `dev` 변경은 MR로 반영합니다.
- 작성자 외 팀원 최소 1명의 리뷰와 변경 범위에 맞는 검증을 병합 기준으로 사용합니다. CI 도입 후에는 파이프라인 성공도 요구합니다.

## 프로젝트 도구

- [GitLab 저장소](https://lab.ssafy.com/s15-final/S15P31A305)
- [Jira 보드](https://ssafy.atlassian.net/jira/software/c/projects/S15P31A305/boards/16635)
- Jira 이슈 키의 접두사는 `S15P31A305`입니다. GitLab의 Jira 기본 연동으로 커밋·MR 링크와 댓글을 기록하며, 작업 상태는 자동 완료되지 않습니다.
