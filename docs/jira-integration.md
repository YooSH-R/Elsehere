# Jira와 GitLab 연동

## 현재 상태

2026-10-07 기준 GitLab에는 활성 연동이 없고 웹훅도 0개입니다. Jira 프로젝트 설정 화면에는 현재 계정으로 구성을 편집할 수 없다고 표시됩니다. 자동 연동을 설정하거나 실제 동작을 검증한 상태는 아닙니다.

## 이슈 번호로 커밋과 MR 연결하기

이슈 키를 커밋과 MR에 넣어 관련 Jira 이슈에 연결할 수 있습니다. 작업 내용을 보고 이슈 번호를 자동으로 추측하는 기능은 아닙니다.

예시의 `S15P31A305-123`은 실제 작업의 키로 바꿉니다.

```text
브랜치: feat/S15P31A305-123-cafe-order
커밋: feat(frontend): 카페 주문 화면 추가 [S15P31A305-123]
MR 제목: [S15P31A305-123] 카페 주문 화면 추가
```

GitLab 프로젝트의 `Settings → Integrations → Jira issues`에서 설정할 수 있습니다.

- Jira Web URL: `https://ssafy.atlassian.net`
- Email or username: 연동에 사용하는 Jira 계정
- API token or password: 해당 Jira Cloud 계정의 API 토큰
- 이벤트: Commit, Merge request

연동 계정에는 대상 이슈를 보고 링크와 댓글을 추가할 수 있는 권한이 필요합니다. 토큰은 GitLab 설정 화면에 직접 입력하고 문서나 커밋에 기록하지 않습니다. 자동 상태 전환은 팀의 완료 기준을 정한 뒤 별도로 구성합니다.

Jira의 개발 패널에 브랜치·커밋·MR을 표시하려면 `GitLab for Jira Cloud` 연동을 사용합니다. 이 연동은 GitLab 그룹을 연결하므로 학교의 상위 그룹을 프로젝트 팀이 임의로 연결하지 않고, 관리자와 적용 범위를 확인합니다.

## 웹훅으로 댓글과 상태 자동화하기

웹훅도 사용할 수 있습니다. 방향은 `GitLab → Jira`입니다.

1. Jira 프로젝트 관리자가 Automation에서 Incoming webhook 규칙을 만듭니다.
2. GitLab 프로젝트의 `Settings → Webhooks`에 수신 URL을 등록합니다.
3. Jira의 인증값은 GitLab Custom headers의 `X-Automation-Webhook-Token`에 설정합니다. GitLab의 Secret token은 다른 헤더를 사용하므로 그대로 대체할 수 없습니다.
4. Push와 Merge request 이벤트를 선택합니다.
5. Jira 규칙에서 커밋 메시지 또는 MR 제목의 이슈 키를 추출하고, 프로젝트가 `S15P31A305`인지 확인합니다.
6. 해당 이슈에 커밋·MR 링크를 기록하거나, 합의한 조건에서 상태를 변경합니다.
7. 전용 검증 이슈로 이벤트를 보내고 Jira 자동화 감사 로그와 이슈 변경 결과를 확인합니다.

GitLab의 원래 웹훅 데이터는 Jira가 기대하는 이슈 목록과 다를 수 있으므로, 수신 규칙에 이슈 키 추출과 조회가 필요합니다. Push는 여러 커밋과 이슈를 포함할 수 있습니다. MR 생성·수정·병합도 구분하고 같은 이벤트로 댓글이 중복 생성되지 않게 구성합니다.

이 방식은 댓글·링크·상태 자동화입니다. Jira 개발 패널의 정식 커밋·MR 표시는 공식 개발 도구 연동으로 처리합니다. 처음에는 링크 기록부터 검증하고, 커밋 Push만으로 이슈를 완료 처리하지 않습니다.

## 필요한 준비

- 프로젝트 단위 웹훅 자동화: Jira 프로젝트 관리자 권한과 수신 규칙의 URL·인증값
- Jira issues 공식 연동: 대상 프로젝트에 필요한 권한이 있는 Jira 계정과 API 토큰
- 개발 패널 공식 연동: Jira 앱 관리와 GitLab 그룹 연결을 적용할 관리자의 확인

## 참고

- [GitLab Jira 연동 종류](https://docs.gitlab.com/integration/jira/)
- [Jira Incoming webhook 설정](https://support.atlassian.com/cloud-automation/docs/configure-the-incoming-webhook-trigger-in-atlassian-automation/)
- [GitLab 웹훅](https://docs.gitlab.com/user/project/integrations/webhooks/)
