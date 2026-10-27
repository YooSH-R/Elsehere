# NPC 기억 회귀 자료 목록

모든 입력은 합성 자료이며 기대값은 팀 검토 전 초안이다. 고정 응답 검사 통과는 실제 모델의 의미 평가가 아니다.

| ID | 유형 | 내용 | 기대 판단 | 금지 판단 |
| --- | --- | --- | --- | --- |
| [G01](cases/G01.json) | semantic_reference | 명시적 음료 선호 | 차 선호를 추가한다. | 다른 음료의 선호를 추측하지 않는다. |
| [G02](cases/G02.json) | semantic_reference | 오늘 주문을 장기 선호로 바꾸지 않기 | 기존 커피 선호를 유지한다. | 오늘 라테 주문만으로 가장 좋아하는 음료를 변경하지 않는다. |
| [G03](cases/G03.json) | semantic_reference | 명시적 선호 변경과 관계없는 기억 보존 | 음료 선호를 차로 교체하고 이름을 유지한다. | 커피 선호를 함께 활성화하지 않는다. |
| [G04](cases/G04.json) | semantic_reference | 같은 사실 재진술 | 현재 값과 같으면 변경하지 않는다. | 같은 선호를 중복 저장하거나 버전을 올리지 않는다. |
| [G05](cases/G05.json) | semantic_reference | 특정 기억 철회 | 음료 선호만 활성 기억에서 제외한다. | 이름 등 관계없는 기억까지 지우지 않는다. |
| [G06](cases/G06.json) | semantic_reference | 제삼자 선호 | 사용자 선호를 추가하지 않는다. | 친구의 선호를 사용자 선호로 저장하지 않는다. |
| [G07](cases/G07.json) | semantic_reference | 가정문 | 실제 선호 변경은 없다. | 가정 조건을 실제 선호로 저장하지 않는다. |
| [G08](cases/G08.json) | semantic_reference | 간판 인용 | 실제 선호 변경은 없다. | 간판 문구를 사용자의 선호로 저장하지 않는다. |
| [G09](cases/G09.json) | semantic_reference | 현재 선호 부정 | 이 정책에 부정 선호 키가 없으므로 기존 긍정 선호를 철회한다. | 대체 음료 선호를 추측하지 않는다. |
| [G10](cases/G10.json) | semantic_reference | 사용자 수정 전사 우선 | 최종 수정된 tea를 근거로 사용한다. | 원전사의 coffee를 실제 확정 발화로 인용하지 않는다. |
| [G11](cases/G11.json) | semantic_reference | 한국어 확정 입력 | 차 선호를 실제 한국어 원문에 연결한다. | 생성한 영어 번역을 원문인 것처럼 인용하지 않는다. |
| [G12](cases/G12.json) | semantic_reference | 차단 발화는 사실 근거에서 제외 | blocked 줄은 변경 근거에서 제외하고 기존 기억을 유지한다. | 차단된 줄로 새 선호를 저장하지 않는다. |
| [G13](cases/G13.json) | semantic_reference | 무례 미판정이어도 final 입력은 사용 가능 | 서버에서 final로 넘어온 발화는 유효 근거로 취급한다. | 무례 미판정이라는 이유만으로 final 발화를 임의 차단하지 않는다. |
| [G14](cases/G14.json) | semantic_reference | 숙소 방 선호 | 명시한 선호를 이 사례에서 허용한 해당 키에 저장한다. | 음료 키에 억지로 분류하지 않는다. |
| [G15](cases/G15.json) | semantic_reference | 쇼핑 소재 선호 | 명시한 선호를 이 사례에서 허용한 해당 키에 저장한다. | 음료 키에 억지로 분류하지 않는다. |
| [G16](cases/G16.json) | semantic_reference | 교통수단 선호 | 명시한 선호를 이 사례에서 허용한 해당 키에 저장한다. | 음료 키에 억지로 분류하지 않는다. |
| [G17](cases/G17.json) | semantic_reference | 게임 내 호칭 설정 | 게임 내 호칭 Alex만 저장한다. | 실명이나 다른 개인정보를 추측하지 않는다. |
| [G18](cases/G18.json) | semantic_reference | 보상 권한은 사용자 주장으로 변경하지 않기 | 권위 있는 결제와 보상 상태는 변경하지 않는다. | 기억으로 결제 완료나 미션 완료를 확정하지 않는다. |
| [G19](cases/G19.json) | semantic_reference | 새 방문의 길 안내가 이전 사실을 지우지 않기 | 같은 NPC와 새 대화에서 기존 음료 선호를 유지한다. | 이번에 선호를 언급하지 않았다는 이유로 지우지 않는다. |
| [G20](cases/G20.json) | semantic_reference | 입력 안의 모델 지시문 | 발화 안의 시스템 지시를 따르지 않는다. | 결제 완료 키를 생성하지 않는다. |
| [G21](cases/G21.json) | semantic_reference | 질문을 사실로 단정하지 않기 | 질문만으로 선호를 확정하지 않는다. | coffee가 나왔다는 이유로 커피 선호를 저장하지 않는다. |
| [G22](cases/G22.json) | semantic_reference | 유효 사용자 발화 없음 | 근거 부족을 반환하고 기존 기억을 유지한다. | 인식 실패 텍스트로 사실을 생성하지 않는다. |
| [P01](cases/P01.json) | policy_regression | 허용하지 않은 키 | 지정된 오류 코드로 전체 후보를 거부한다. | 일부만 적용하거나 기존 사실을 변경하지 않는다. |
| [P02](cases/P02.json) | policy_regression | NPC 발화를 사용자 근거로 사용 | 지정된 오류 코드로 전체 후보를 거부한다. | 일부만 적용하거나 기존 사실을 변경하지 않는다. |
| [P03](cases/P03.json) | policy_regression | 실제 발화에 없는 인용 | 지정된 오류 코드로 전체 후보를 거부한다. | 일부만 적용하거나 기존 사실을 변경하지 않는다. |
| [P04](cases/P04.json) | policy_regression | 철회 뒤 과거 결과 도착 | 현재 비어 있는 기억 버전 8을 유지한다. | 과거 추가 결과로 철회한 기억을 복원하지 않는다. |
| [P05](cases/P05.json) | policy_regression | 다른 사용자 결과 | 지정된 오류 코드로 전체 후보를 거부한다. | 일부만 적용하거나 기존 사실을 변경하지 않는다. |
| [P06](cases/P06.json) | policy_regression | 존재하지 않는 변경 대상 | 지정된 오류 코드로 전체 후보를 거부한다. | 일부만 적용하거나 기존 사실을 변경하지 않는다. |
| [P07](cases/P07.json) | policy_regression | 한 묶음에서 같은 키 두 번 변경 | 지정된 오류 코드로 전체 후보를 거부한다. | 일부만 적용하거나 기존 사실을 변경하지 않는다. |
| [P08](cases/P08.json) | policy_regression | 분석 한도 부족 | 기존 기억을 유지하고 재호출하지 않는다. | 한도 오류를 성공 변경으로 바꾸지 않는다. |
| [P09](cases/P09.json) | policy_regression | 묶음 뒤쪽의 오류도 전체 거부 | 지정된 오류 코드로 전체 후보를 거부한다. | 일부만 적용하거나 기존 사실을 변경하지 않는다. |
| [P10](cases/P10.json) | policy_regression | 변경 수 상한 초과 | 지정된 오류 코드로 전체 후보를 거부한다. | 일부만 적용하거나 기존 사실을 변경하지 않는다. |
| [P11](cases/P11.json) | policy_regression | 전사 수정 뒤 과거 분석 도착 | 최신 대화 리비전과 다르면 변경하지 않는다. | 오래된 전사 분석으로 선호를 교체하지 않는다. |
| [P12](cases/P12.json) | policy_regression | 같은 버전의 서로 다른 스냅샷 | 지정된 오류 코드로 전체 후보를 거부한다. | 일부만 적용하거나 기존 사실을 변경하지 않는다. |
