# GitLab 초기 설정과 확인 절차

## 확인한 환경

- 프로젝트: [s15-final / S15P31A305](https://lab.ssafy.com/s15-final/S15P31A305)
- 제공 방식: SSAFY 설치형 GitLab
- 초기 확인일: 2026-10-07
- 최초 확인 당시 원격 저장소에는 커밋과 브랜치가 없었습니다.
- 브라우저에서 현재 로그인 계정 `rexsung`의 프로젝트 역할은 Developer로 확인했습니다.
- Developer 계정에는 프로젝트 Settings 메뉴가 표시되지 않았습니다. 기본 브랜치 변경, 브랜치 보호, 멤버 권한, MR 병합 설정에는 Maintainer의 적용이 필요합니다.

아래 항목은 적용 목표와 확인 절차입니다. 실제 적용 여부는 GitLab 화면에서 확인해야 합니다.

## 현재 적용 현황

| 항목 | 상태 | 근거 또는 다음 조치 |
| --- | --- | --- |
| 협업 문서와 MR 템플릿 | 로컬 작성 완료 | README, 협업 규칙, 초기 설정 안내, 일반·릴리스 MR 템플릿 |
| 로컬 `main`, `dev` | 생성 완료 | 두 브랜치가 동일한 초기 커밋에서 시작 |
| 원격 업로드 | GitLab 권한으로 차단 | Developer는 비어 있는 프로젝트의 최초 기본 브랜치를 생성할 수 없음 |
| 원격 `main`, `dev` | 생성 대기 | Maintainer의 최초 업로드 필요 |
| 기본 브랜치 및 브랜치 보호 | 적용 대기 | Maintainer 설정 필요 |
| MR 병합 방식 및 논의 해결 조건 | 적용 대기 | Maintainer 설정 필요 |
| 웹에서 MR 템플릿 확인 및 동료 리뷰 | 검증 대기 | 원격 브랜치 생성과 문서 업로드 후 진행 |

최초 `main` Push에서 GitLab은 다음 이유로 업로드를 거절했습니다.

```text
A default branch (e.g. main) does not yet exist for s15-final/S15P31A305
Ask a project Owner or Maintainer to create a default branch
```

### 재개 절차

1. 프로젝트 Maintainer가 `rexsung` 계정에 Maintainer 역할을 부여하거나, 준비된 초기 커밋을 직접 업로드합니다.
2. 권한 변경 뒤 원격 브랜치를 다시 확인합니다. 여전히 비어 있다면 준비된 `main`, `dev`를 업로드합니다.
3. 기본 브랜치를 `main`으로 설정하고, 아래의 브랜치 보호와 MR 설정을 적용합니다.
4. 원격 초기 브랜치가 준비되면 첫 문서 작업 MR을 `dev` 대상으로 생성해 템플릿을 확인합니다.
5. 실제 팀원의 리뷰와 병합을 거쳐 협업 흐름을 검증합니다.

아래 명령은 원격 저장소가 여전히 비어 있고 현재 인증 계정이 Maintainer인 경우에만 사용합니다.

```sh
git push --set-upstream origin main
git push --set-upstream origin dev
```

다른 담당자가 웹에서 별도 초기 커밋을 만들었다면 원격 이력을 먼저 가져와 통합합니다. 기존 커밋을 덮어쓰는 force push는 사용하지 않습니다.

## 1. 초기 브랜치와 문서

- 초기 협업 문서와 MR 템플릿을 `main`에 반영합니다.
- 같은 초기 커밋에서 `dev`를 생성합니다.
- GitLab 기본 브랜치를 `main`으로 설정합니다.
- 초기 Push가 역할 또는 그룹 보호 정책으로 차단되면 Maintainer가 초기 브랜치를 생성하거나 준비된 커밋을 업로드해야 합니다.
- 템플릿은 기본 브랜치에도 존재해야 GitLab에서 인식됩니다.

## 2. 팀원 역할

- 기존 학교 운영진과 상속된 그룹 권한은 그대로 둡니다.
- 프로젝트 유지관리 담당자를 확인해 팀 내 Maintainer를 2명 이상 확보합니다.
- 일반 개발 팀원은 Developer 역할을 사용합니다.
- 팀원과 담당자의 확정 없이 권한을 임의로 변경하지 않습니다.

## 3. 브랜치 보호

설정 위치는 GitLab 버전에 따라 `Settings → Repository → Branch rules` 또는 `Protected branches`입니다.

| 항목 | `main` | `dev` |
| --- | --- | --- |
| Allowed to merge | Maintainers | Developers + Maintainers |
| Allowed to push and merge 또는 Allowed to push | No one | No one |
| Force push | 비활성화 | 비활성화 |

직접 Push 권한은 빈 값 대신 `No one`으로 명시합니다. 기존 그룹 보호 정책과 와일드카드 규칙이 있다면 더 넓은 권한이 적용되지 않는지도 확인합니다.

## 4. MR 설정

설정 위치는 GitLab 버전에 따라 `Settings → Merge requests` 또는 `Settings → General`의 Merge requests 항목입니다.

- Merge method: Merge commit.
- 모든 논의가 해결되어야 병합하도록 설정합니다.
- 필수 승인 기능이 제공되면 작성자 외 승인 1명을 요구합니다. 제공되지 않으면 병합 담당자가 팀 규칙으로 확인합니다.
- 모든 MR에 squash를 강제하지 않습니다. `dev ↔ main` MR은 squash하지 않습니다.
- 일반 작업 브랜치는 병합 후 삭제하되 `main`, `dev`는 유지합니다. 릴리스 MR의 Delete source branch를 반드시 해제합니다.
- CI가 정상 실행되기 전에는 Pipelines must succeed를 활성화하지 않습니다.

## 5. MR 흐름 검증

1. `dev`에서 문서 작업 브랜치를 생성하고 의미 있는 문서 변경을 커밋합니다.
2. `dev` 대상으로 MR을 생성합니다.
3. Default 템플릿을 선택하고 내용이 표시되는지 확인합니다.
4. 작성자 외 팀원에게 리뷰를 받고 의견을 반영합니다.
5. 미해결 논의가 있으면 병합이 차단되는지 확인합니다.
6. 조건을 만족한 MR을 병합하고 작업 브랜치를 정리합니다.
7. `dev → main` MR에서 Release 템플릿을 확인합니다.
8. 릴리스 MR에서는 squash와 소스 브랜치 삭제를 해제합니다.

직접 Push 제한은 우선 GitLab의 보호 설정 화면으로 확인합니다. 실제 Push로 검증하려면 문서 전용 커밋을 사용하고, 설정 오류로 Push가 성공할 수 있다는 점을 알고 진행해야 합니다.

## 완료 기준

- [ ] 원격 `main`, `dev` 브랜치 생성
- [ ] 기본 브랜치 `main` 설정
- [ ] 협업 문서와 두 MR 템플릿을 기본 브랜치에 반영
- [ ] 유지관리 담당자와 팀원 역할 확인
- [ ] `main`, `dev` 직접 Push 및 force push 차단
- [ ] Merge commit과 미해결 논의 차단 설정
- [ ] 실제 GitLab에서 일반 MR과 릴리스 MR 템플릿 확인
- [ ] 작성자 외 팀원의 리뷰를 거쳐 일반 MR 병합

## 공식 참고 자료

- [보호 브랜치](https://docs.gitlab.com/user/project/repository/branches/protected/)
- [MR 설명 템플릿](https://docs.gitlab.com/user/project/description_templates/)
- [MR 승인과 요금제별 차이](https://docs.gitlab.com/user/project/merge_requests/approvals/)
