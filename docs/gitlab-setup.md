# GitLab 운영 정책

[프로젝트 저장소](https://lab.ssafy.com/s15-final/S15P31A305)의 접근 권한, 브랜치 보호와 병합 검사 설정을 관리합니다. 작업 절차와 리뷰·릴리스 기준은 [개발 및 협업 규칙](development.md)을 따릅니다.

## 접근 권한

- 프로젝트 유지관리 담당자 2명 이상을 Maintainer로, 개발 팀원을 Developer로 두는 것을 목표로 합니다.
- Developer는 기능 개발과 코드 리뷰를 담당합니다.
- Maintainer는 저장소 설정, 접근 권한, 릴리스 병합을 관리합니다.
- 학교 운영진과 그룹에서 상속된 권한은 프로젝트 팀의 역할과 구분하며 기존 운영진의 권한을 임의로 변경하지 않습니다.

| 보호 설정 | `main` | `dev` |
| --- | --- | --- |
| 직접 Push | 금지 | 금지 |
| MR 병합 권한 | Maintainer | Developer·Maintainer |
| Force push | 금지 | 금지 |

Maintainer도 `main`·`dev`에 직접 Push하지 않습니다. 브랜치 생성·병합 대상·긴급 수정의 동기화는 [브랜치 규칙](development.md#브랜치)과 [릴리스와 긴급 수정](development.md#릴리스와-긴급-수정)을 따릅니다.

## 병합 검사 설정

프로젝트의 `Settings → Merge requests → Merge checks`에서 다음 값을 유지합니다. 검사 정의만 작성한 상태가 아니라 실제 Runner에서 필수 작업이 실행되는 것을 확인한 뒤 적용합니다.

| 설정 | 값 |
| --- | --- |
| Pipelines must succeed | 활성 |
| Skipped pipelines are considered successful | 비활성 |

최신 파이프라인이 실패하거나 실행 중이면 병합하지 않으며 건너뛴 파이프라인을 성공 근거로 사용하지 않습니다. 일반 문서 MR도 항상 실행되는 `ci:yaml` 검사를 통과해야 합니다. 작업별 실행 조건·산출물·검사 실패 확인은 [검사 파이프라인 문서](ci-runner.md)를 따릅니다.

Unity 관련 MR은 `unity:format`과 `unity:editor-tests`의 EditMode·PlayMode까지 필수입니다. 실행 작업의 실패를 허용하거나 수동 선택으로 바꾸지 않습니다. Runner가 작업을 받지 못해 대기 중인 경우에도 병합을 허용하지 않으며 [Windows Runner 연결 조건](unity/windows-runner.md)을 먼저 해결합니다. MR의 성공 결과로 dev 병합 후 통합 검사·빌드·배포를 생략하지 않습니다.

병합 방식·리뷰·논의 해결·squash·브랜치 삭제는 [협업 규칙의 병합 조건과 방식](development.md#병합-조건과-방식)에서 관리합니다. GitLab Free에서 필수 승인을 강제하지 못하더라도 팀의 리뷰 기준은 유지합니다.

## 작업 추적

GitLab과 Jira의 개발 정보 연동, 중복 웹링크 방지와 상태 관리는 [Jira와 GitLab 연동](jira-integration.md)을 따릅니다.

## 설정 변경 관리

Maintainer는 브랜치 보호, 병합 조건, 연동 설정을 관리합니다. 설정 변경은 팀과 공유하고 관련 운영 문서를 함께 갱신합니다. API 토큰과 인증값은 GitLab 설정 또는 CI/CD 변수에서 관리하며 저장소에 기록하지 않습니다.
