# S15P31A305-249 외관 아트 검수

- 캐릭터 기준 키 1.6m 확정. 머리 60%·몸 40% 유지, 실제 v7 모델은 미제작.
- 카페·상점·호텔·집에 공통 창, 차양, 간판, 화단, 지붕·포치 추가.
- 공통 프리팹 13개 생성. 그중 외부 모델은 CC0 Kenney truck-flat 하나.
- Tripo API 호출 및 유료 생성 없음. 사용 크레딧 0.
- Unity 6000.3.26f1 batchmode Builder와 실제 Play 검수 실행. verification.txt의 결과 모두 통과, 로그 오류 0개.
- 캐릭터 렌더 외곽 높이 1.600m, 문 3곳 CharacterController 통과 및 헤어 폭 1.60m 간섭 검사, 북쪽 39m 보행, 대화 시점·이동 잠금 확인.
- 시작·대화·카페·확장·배치·높이 비교·아트 화면 9장 캡처. 첫 화면의 가로수 가림과 교차로 타일 접합을 보정한 뒤 최종 재검수.
- unity-code-quality.ps1 format/check 통과. 컴파일 오류 0개, 기존 MockObjectWordSource/MockNpcLineSource CS0649 경고 7개.
- 배치·메시·재질·카메라 검토 및 기본 수치 변경으로 TDD 제외. 이번 작업에서 전체 EditMode/PlayMode 테스트 스위트는 다시 실행하지 않음.
- 실제 v7 메시·공통 리그·립싱크, 앉기·손 도달, Web 성능, Production 씬 연결은 미검증.
- 원본 작업 폴더의 docs/design/elsehere 내 제작 규격·에셋 목록·Unity 적용 계획·v7 README·씬 README도 1.6m/확대안으로 갱신. 이 다섯 문서는 기존 미추적 디자인 자료이므로 별도 작업 브랜치 커밋에는 포함하지 않음.
