# 독립 외부 HTTPS 감시

앱 서버가 멈춰도 공개 HTTPS 응답과 인증서 상태를 확인할 수 있도록 Grafana Cloud Free의 공개 감시 위치를 사용합니다. 체크는 Cloud에서 실행하고 경보도 Cloud에서 평가·전송합니다. 서버 내부의 Prometheus·Alertmanager와 별도로 동작합니다.

## 계정과 검사 범위

무료 계정의 가입·약관 동의·로그인은 계정 소유자가 진행합니다. 서버에 설치한 Grafana와 Cloud의 Grafana는 별도 인스턴스이며, 공개 감시에 앱 로그·DB·관리 포트의 인증정보를 제공하지 않습니다.

| 설정 | 값 |
| --- | --- |
| 방식 | API Endpoint의 HTTP 검사 |
| 이름·job | `elsehere-dev-external-https` |
| 대상 | `https://dev.elsehere.kr/api/health` |
| 메서드·정상 상태 | GET·HTTP 200 |
| 응답 검증 | Body가 `"status"\s*:\s*"UP"`에 일치. Regex Invert 활성 |
| 실행 위치 | Seoul·Singapore 공개 감시 위치 2곳 |
| 주기·시간 제한 | 60초·5초 |
| TLS | 인증서 검증 활성·HTTPS 필수 |
| 리다이렉트 | 따라가지 않음 |
| 레이블 | 고정된 서비스·환경만 사용. 사용자·요청 ID 제외 |

Regex 검증은 기본적으로 패턴에 일치하면 실패하므로 Invert를 켭니다. 설정 화면에서는 **Fail if SSL is not present**를 선택하고 **Disable target certificate validation**은 끕니다.

2곳에서 1분마다 한 번 실행하면 31일 기준 `2 × 60 × 24 × 31 = 89,280`회입니다. 무료 API 검사 한도 100,000회 안에서 체크 하나를 유지하고, 다른 체크의 사용량도 함께 확인합니다. 위치 추가·중복 체크·prod 감시 추가 전에는 실행 횟수와 계정의 현재 한도를 다시 계산합니다. [공식 요금·무료 범위](https://grafana.com/products/cloud/synthetic-monitoring/)

## 외부 경보

Cloud Prometheus 데이터 소스를 선택하고 **Code·Instant**로 아래 수치 쿼리를 등록합니다. 서버 내부 Grafana의 `Prometheus` 데이터 소스와 혼동하지 않습니다. 폴더는 `Grafana Synthetic Monitoring`, 평가 그룹은 `elsehere-external`, 평가 간격은 1분입니다.

| 규칙 | Alert condition | Pending period |
| --- | --- | --- |
| `ExternalHTTPSUnavailable` | 쿼리 값이 1 미만 | 2분 |
| `ExternalTLSCertificateExpiringSoon` | 잔여 일수가 14 미만 | 1시간 |

두 규칙 모두 Keep firing은 `0s`이며 No data·Error 처리에는 **Alerting**을 선택합니다. 현재 Grafana에서는 이 처리에도 각 규칙의 Pending period를 적용합니다. 감시 데이터 누락·조회 오류를 포함하는 경보이므로 알림만으로 서비스 장애나 인증서 만료를 확정하지 않고 규칙의 데이터와 오류 사유를 확인합니다. [무데이터·오류 처리](https://grafana.com/docs/grafana/latest/alerting/fundamentals/alert-rule-evaluation/nodata-and-error-states/)

HTTPS 가용성 쿼리는 다음과 같습니다.

```promql
max by (instance) (
  probe_success{
    job="elsehere-dev-external-https",
    probe=~"Seoul|Singapore"
  }
)
and on (instance)
(
  count by (instance) (
    probe_success{
      job="elsehere-dev-external-https",
      probe=~"Seoul|Singapore"
    }
  ) == 2
)
```

두 위치가 모두 수집되고 하나 이상 성공하면 `1`, 모두 실패하면 `0`입니다. 선택한 두 위치의 결과가 없거나 개수가 맞지 않으면 빈 결과가 되어 No data 처리로 확인합니다. 정상값 `1`이 반환돼야 하므로 쿼리 끝에 `== 0`을 붙이지 않습니다. 실패 여부는 Grafana의 **IS BELOW 1** 조건으로 평가합니다.

인증서 잔여 일수 쿼리는 다음과 같습니다.

```promql
(
  min by (instance) (
    probe_ssl_earliest_cert_expiry{
      job="elsehere-dev-external-https",
      probe=~"Seoul|Singapore"
    }
  ) - time()
) / 86400
```

이 값은 두 위치에서 관측한 TLS 인증서 체인의 가장 빠른 만료까지 남은 일수입니다. 정상 상태에서도 값을 반환하고, Grafana의 **IS BELOW 14** 조건에서 임박 여부를 판단합니다. 인증서 갱신 자체는 HTTPS 운영 절차에서 관리합니다.

## Mattermost 수신 경로

Contact point `elsehere-dev-external-mm`는 Grafana Alertmanager의 **Slack** 호환 연동을 사용합니다. 두 규칙에서 이 Contact point를 직접 선택하며 앱 서버를 알림의 중계점으로 사용하지 않습니다. [Mattermost 연동 안내](https://docs.mattermost.com/administration-guide/scale/performance-alerting), [Grafana Slack Contact point](https://grafana.com/docs/grafana/latest/alerting/configure-notifications/manage-contact-points/integrations/configure-slack/)

승인된 수신 채널·문구의 변경과 웹훅 보관은 [공통 Mattermost 발송 승인 기준](observability.md#mattermost-발송-승인)을 따릅니다. 채널 제한 웹훅을 사용하며 Cloud에 비밀 URL을 입력하는 단계는 계정 소유자가 직접 진행합니다.

| 알림 설정 | 값 |
| --- | --- |
| Group wait·Group interval·Repeat interval | 공통 발송 승인 기준의 묶음 대기·변경·반복 주기 적용 |
| Disable resolved message | 체크 해제 |
| 사용자·그룹·채널 멘션 | 비움 |

`Disable resolved message`를 켜면 복구 알림이 차단됩니다. 복구 상태 전환과 메시지 도착 사이에는 Group interval에 따른 지연이 있을 수 있습니다. [알림 간격](https://grafana.com/docs/grafana/latest/alerting/fundamentals/notifications/group-alert-notifications/)

제목과 본문은 상태·규칙명, 규칙의 Summary·Description, 대상 주소, 검사 위치와 한국시간 기준 시작·복구 시각, Grafana 확인 링크를 표시합니다. 본문 대상 주소는 체크의 실제 대상과 일치시킵니다.

## 대상 변경과 검증

1. 기존 체크를 편집해 실제 개발 앱의 HTTPS health 주소를 대상으로 지정합니다. 체크 이름·두 검사 위치·주기를 유지하고 중복 체크를 만들지 않습니다.
2. Contact point 본문의 대상 주소를 같은 주소로 바꿉니다. 수신 채널·문구의 승인 범위를 확인하고 웹훅·멘션·복구 알림 설정을 보존합니다.
3. Cloud Explore에서 새 주소의 `instance` 레이블로 한정해 Seoul·Singapore의 `probe_http_status_code=200`, `probe_success=1`과 인증서 잔여 일수를 확인합니다. `probe_ssl_last_chain_info`의 인증서 주체·대체 이름에도 새 도메인이 포함되는지 확인합니다.
4. 두 실제 경보의 Normal 상태를 확인합니다. 주소 전환 직후에는 과거 instance의 지표가 잠시 남을 수 있으므로 새 instance의 정상 결과를 직접 확인합니다.
5. 승인된 문구로 시험 알림 1건을 보내 새 주소 표시와 전송을 확인합니다. 체크의 주소만 변경하고 수신 경로·경보 조건을 유지했다면 기존에 성공한 전체 발동·복구 시험을 반복할 필요는 없습니다.

## 발동·복구 시험과 조정

전체 경보 흐름을 처음 연결하거나 평가·수신 설정을 바꿨을 때는 실제 앱을 중단하지 않고 별도 시험 규칙을 사용합니다. 시험임을 표시한 이름·본문과 승인된 수신처를 지정하고 실제 규칙과 같은 평가 간격·Pending·복구·알림 간격을 적용합니다.

정상값 `vector(1)`로 시험 규칙을 준비한 뒤, `vector(time() >= bool <자동 복구 Unix 시각>)`처럼 정해진 시각 전에는 `0`, 이후에는 `1`이 되는 시험 쿼리를 사용합니다. 복구 시각은 저장·첫 평가·Pending·발동 알림에 충분한 시간을 두고 정합니다. 저장 뒤 쿼리를 수정하지 않고 Pending→Alerting→Normal과 실제 FIRING·RESOLVED 수신을 확인합니다. 규칙 편집에 따른 상태 초기화를 정상 평가에 의한 복구와 혼동하지 않습니다.

시험을 마치면 임시 규칙을 삭제하고 실제 경보 두 개와 체크를 유지합니다. 지표 누락·조회 오류의 설정 확인, 정상 상태의 인증서 평가, 시간 기반 발동·복구 시험과 실제 인증서 만료·서버 중단 재현을 구분해 기록합니다. 실행 결과는 Jira·MR에 남깁니다.

Cloud 체크를 중지하면 외부 감시도 중단됩니다. 계정·체크 정지나 무데이터를 장애 복구로 오인하지 않습니다. 임계값 조정 시에는 기존 조건·새 조건과 정상 이용 측정 근거를 기록합니다. 서버 내부 API 지표와 오류율·p95 기준의 근거는 [서비스 관측 운영](observability.md)을 따릅니다.

prod 앱을 배포하면 실제 앱 health를 대상으로 감시를 추가하고 무료 실행 한도를 다시 계산합니다. 현재 bootstrap의 정상 응답을 운영 앱 정상으로 판단하지 않습니다.

설정 근거: [HTTP 검사 옵션·지표](https://grafana.com/docs/grafana-cloud/observe-and-act/testing/synthetic-monitoring/create-checks/checks/http/), [Grafana 경보 규칙](https://grafana.com/docs/grafana/latest/alerting/alerting-rules/create-grafana-managed-rule/).
