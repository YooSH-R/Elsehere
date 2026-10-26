# 독립 외부 HTTPS 감시

앱 서버가 멈춰도 공개 HTTPS 응답과 인증서 상태를 확인할 수 있도록 Grafana Cloud Free의 공개 감시 위치를 사용합니다. 아래 내용은 적용 절차와 기준입니다. Cloud의 실제 체크 실행·규칙 상태·수신 경로와 시험 발동·복구를 확인하기 전에는 외부 감시 구성이 완료된 것으로 보지 않습니다.

## 계정과 검사 범위

무료 계정의 가입·약관 동의·로그인은 계정 소유자가 진행합니다. 서버에 설치한 Grafana와 Cloud의 Grafana는 별도 인스턴스이며, 공개 감시에 앱 로그·DB·관리 포트의 인증정보를 제공할 필요가 없습니다.

| 설정 | 값 |
| --- | --- |
| 방식 | API Endpoint의 HTTP 검사 |
| 이름·job | `elsehere-dev-external-https` |
| 대상 | `https://k15a305.p.ssafy.io/dev/api/health` |
| 메서드·정상 상태 | GET·HTTP 200 |
| 응답 검증 | 상태 본문의 `status`가 `UP`인지 확인 |
| 실행 위치 | 앱 서버와 독립된 공개 감시 위치 2곳 |
| 주기·시간 제한 | 60초·5초 |
| TLS | 인증서 검증 활성, HTTPS 필수, TLS 1.2 이상 |
| 리다이렉트 | 따라가지 않음 |
| 레이블 | 고정된 서비스·환경만 사용. 사용자·요청 ID 제외 |

2곳에서 1분마다 한 번 실행하면 31일 기준 `2 × 60 × 24 × 31 = 89,280`회입니다. 무료 API 검사 한도 100,000회 안에서 하나의 체크를 유지하고, 중복·데모·시험 체크의 사용량도 함께 확인합니다. 유료 업그레이드 없이 현재 계정의 Free 한도를 먼저 확인합니다. [공식 요금·무료 범위](https://grafana.com/products/cloud/synthetic-monitoring/)

## 외부 경보와 수신 경로

서버 내부의 API·수집기 경보는 기존 Prometheus·Alertmanager가 담당합니다. 외부 경보는 Cloud 안에서 평가하고 수신 채널로 직접 보냅니다. 장애가 난 앱 서버를 외부 알림의 중계점으로 사용하지 않습니다.

| 외부 경보 | 판단 기준 | 대기 |
| --- | --- | --- |
| 공개 HTTPS 장애 | 두 공개 위치 모두에서 정상 응답 실패 | 2분 |
| 인증서 만료 임박 | 수집한 가장 이른 만료일이 14일 미만 | 1시간 |
| 감시 데이터 없음·실행 오류 | 감시 자체의 중단을 정상 응답으로 처리하지 않음 | 체크 주기·Cloud 평가 설정과 함께 검증 |

두 위치의 정상 수집과 실제 레이블을 먼저 확인한 뒤 아래와 같은 PromQL로 사용자 정의 규칙을 구성합니다. 첫 번째 식은 두 위치의 지표가 모두 존재하고 모두 실패했을 때 참입니다. 위치 수가 줄거나 지표 자체가 사라지면 별도 무데이터·실행 오류 처리로 확인해야 합니다.

```promql
(max by (instance) (probe_success{job="elsehere-dev-external-https"}) == 0)
and on (instance)
(count by (instance) (probe_success{job="elsehere-dev-external-https"}) == 2)
```

```promql
min by (instance) (probe_ssl_earliest_cert_expiry{job="elsehere-dev-external-https"}) - time() < 14 * 24 * 60 * 60
```

Mattermost를 수신처로 선택할 때는 채널과 실제 문구를 보고하고 승인받은 뒤 전용 Contact point를 연결합니다. 기존 채널은 `ELSEHERE 운영 알림`(`elsehere-alerts`)이며 다른 채널로 임의 변경하지 않습니다. Mattermost의 채널 제한 웹훅을 사용하고 URL을 저장소·Jira·로그·채팅에 기록하지 않습니다. Cloud에 비밀 URL을 제공하는 단계는 계정 소유자가 직접 진행합니다.

Mattermost 공식 문서의 Grafana 연동은 Slack 형식의 Contact point를 사용합니다. 일반 Webhook의 기본 JSON을 그대로 보내면 Mattermost 메시지 형식과 다르므로, Slack 호환 전송과 맞춤 제목·본문을 실제 채널에서 검증합니다. 시험 전에는 전송 성공으로 기록하지 않습니다. [Mattermost 연동 안내](https://docs.mattermost.com/administration-guide/scale/performance-alerting), [Grafana Slack Contact point](https://grafana.com/docs/grafana/latest/alerting/configure-notifications/manage-contact-points/integrations/configure-slack/)

## 적용·검증·조정

1. Cloud의 **Testing & synthetics → Synthetics**를 초기화하고 기존 대상 체크가 있는지 확인합니다. 같은 대상 체크를 중복 생성하지 않습니다.
2. 위 설정으로 체크 하나를 만들고 두 공개 위치에서 HTTP 200, TLS 검증, 인증서 만료 지표를 확인합니다. Cloud의 실행 위치와 실제 규칙 레이블을 Jira에 기록합니다.
3. Cloud 안에서 위 경보를 구성하고 승인된 수신 경로에 해당 외부 규칙만 연결합니다. 다른 서비스나 데모 알림을 팀 채널에 일괄 전송하지 않습니다.
4. 실제 앱을 중단하지 않고 별도 시험 체크의 기대 상태를 바꾸는 방식으로 실패를 만듭니다. 시험임을 표시한 발동 알림이 도착한 뒤 정상 기대 상태로 돌려 복구 알림도 확인합니다. 시험 체크는 종료하고 실제 체크의 설정을 보존합니다.
5. Cloud 규칙·체크 상태와 사용자의 실제 채널 수신 확인을 검증 근거로 남깁니다. Mattermost 확인에 computer-use를 사용하지 않습니다.
6. 향후 prod 앱이 배포되면 실제 앱 health를 대상으로 추가하고 검사 수·위치·주기에 따른 무료 한도를 다시 계산합니다. 현재 bootstrap의 정상 응답을 운영 앱 정상으로 판단하지 않습니다.

Cloud의 모든 관련 체크를 중지하면 외부 감시도 중단됩니다. 계정·체크 정지나 무데이터를 장애 복구로 오인하지 않습니다. 경보 조정 시에는 기존 조건과 새 조건을 기록하고 정상·실패·복구·무데이터를 각각 확인합니다.

설정 근거: [HTTP 검사 옵션](https://grafana.com/docs/grafana-cloud/observe-and-act/testing/synthetic-monitoring/create-checks/checks/http/), [사용자 정의 경보](https://grafana.com/docs/grafana-cloud/observe-and-act/testing/synthetic-monitoring/configure-alerts/configure-custom-alerts/).
