# 메인스트리트 Sandbox 사용 방법

`Assets/Scenes/Sandbox/TownMainstreetSandbox.unity`는 리틀 멍 v7의 대두 비율, 건물 크기, 카메라 시야와 거리 확장을 확인하는 블록아웃이다. 브릭 메인스트리트 v3-B의 배치·치수를 단순 도형으로 표현한다. 캐릭터는 완성 모델이 아닌 **높이 1.4m, 머리 60%·몸 40%의 비율 대체물**이다.

[목표 분위기 참고 이미지](../../artifacts/scenes/elsehere/mainstreet-v1/street-view.png)는 디자인 방향을 위한 콘셉트 이미지다. 아래 Unity 렌더는 그 배치를 실제 크기로 검토하는 단계다.

## 열고 확인하기

1. 이 브랜치의 `frontend/UnityClient`를 Unity 6000.3.26f1로 연다.
2. `Assets/Scenes/Sandbox/TownMainstreetSandbox`를 열고 Play한다.
3. 집 앞에서 왼쪽 카페와 오른쪽 호텔의 크기를 비교한다. 앞마당의 대화 상대, 세 건물의 빈 출입구와 북쪽 회색 보행 구간을 확인한다.

Unity 렌더 확인: [시작 시야](../../artifacts/unity/mainstreet-blockout/01-start-view.png), [대화 시야](../../artifacts/unity/mainstreet-blockout/02-dialogue-view.png), [확장 포함 전체 배치](../../artifacts/unity/mainstreet-blockout/05-layout-overview.png), [캐릭터·건물 높이 비교](../../artifacts/unity/mainstreet-blockout/06-character-building-scale.png). 높이 비교만 원근에 따른 크기 차이를 없앤 정투영 검토 화면이며, 플레이 카메라는 원근 투영이다.

| 조작 | 동작 |
| --- | --- |
| WASD·방향키 | 이동 |
| Shift / Space | 달리기 / 점프 |
| 마우스 왼쪽 드래그 / 휠 | 시점 회전 / 거리 조절 |
| V | 1인칭·3인칭 전환 |
| T | 앞마당 상대를 바라보는 대화 시점과 이동 잠금 전환 |

건물 안은 문 통과 확인을 위한 빈 공간이다. HomeScene·CafeScene·HotelScene으로 전환하지 않는다. Sandbox는 배포 Scene List에 등록하지 않는다.

이 Sandbox의 1인칭·대화 시점에서는 플레이어 대체물만 숨겨 큰 머리의 눈·코가 카메라 앞을 가리지 않게 한다. 3인칭으로 돌아오면 다시 보인다. NPC는 계속 표시한다.

## 배치와 치수

![기준 평면도](../../artifacts/scenes/elsehere/mainstreet-v1/layout-plan.png)

좌표는 동쪽 +X, 북쪽 +Z다. 원래 검토 구간은 X=-20~20, Z=-24~30이며, 기본 Sandbox는 북쪽 40m를 추가해 Z=70까지 연결한다. 경계 벽은 임시 추락 방지용이고 다음 블록을 붙일 때 옮긴다.

| 대상 | 중심 또는 전면 기준 위치 | 시작 치수 |
| --- | --- | --- |
| 집 | 전면 X=9, Z=-16 | 6×6m, 높이 4.2m. 지붕은 계단형 대체 형상 |
| 앞마당 | X=10, Z=-10 | 6×6m |
| 카페 | 전면 X=-7, Z=-8 | 6×6m, 높이 5.8m |
| 일반 상점 | 전면 X=-7, Z=-2 | 6×6m, 높이 5.8m. 닫힌 배경 외관 |
| 호텔 | 전면 X=7, Z=-1 | 8×8m, 높이 5.8m |
| 차도 / 보도 | 차도 X=-3~3, 양쪽 보도 각각 4m | 6m / 4m |
| 교차로 | Z=7~13 | 폭 6m, 양쪽 보도 4m |
| 문 | 집 (9,-17), 카페 (-7,-9), 호텔 (7,-3) | 유효 폭 2m, 높이 2.2m |
| 문 앞 비움 영역 | 각 문 바깥 | 전면 폭 3m, 깊이 2.4m |
| 캐릭터 | 집 앞 (6.5,-17)에서 시작 | 높이 1.4m, 머리 0.84m, 턱 아래 0.56m |
| 카메라 | 전용 CameraSettings | 피벗 0.9m, 거리 4m, 아래쪽 25도, 세로 시야각 55도 |

도로는 2m 길이의 모듈로 늘어난다. 건물은 단위 형상 프리팹을 조립해 만들기 때문에 블록 전체를 늘려 크기를 바꾸지 않는다. 단위 프리팹과 머티리얼은 이미 있으면 유지한다.

## 수치 조정과 다시 만들기

| 에셋 | 조정할 것 | 적용 |
| --- | --- | --- |
| `Data/World/MainstreetBlockout/TownBlockoutSettings` | 키, 층고, 문 크기, 북쪽 확장 길이 | 수정 후 Build Mainstreet Sandbox |
| `Data/World/MainstreetBlockout/CameraSettings` | 거리·피벗·각도·눈높이 | Play 전에 수정. 카메라가 시작할 때 읽음 |
| `Data/World/MainstreetBlockout/MovementSettings` | 걷기·달리기 속도 | Play 전에 수정. 이동 컴포넌트가 시작할 때 읽음 |
| 씬의 `BlockoutCamera` Camera | 시야각 | Inspector에서 변경. Builder 재실행 시 55도로 시작 |
| `Prefabs/World/MainstreetBlockout/LittleMungV7Visual` | 대체물의 외형 | 다른 모델로 교체할 때 전신 높이와 발 원점부터 확인 |

**Tools → A305 → World → Build Mainstreet Sandbox**는 해당 Sandbox 씬을 다시 만든다. Inspector에서 직접 조정한 씬 배치는 사라지므로 유지할 위치는 Builder에 반영한다. 기존 데이터 에셋의 조정값과 프리팹 Visual은 유지한다. 키를 바꾸면 머리와 몸의 60:40 비율을 유지하며 전신을 같은 비율로 조정한다. 눈높이·피벗은 별도로 맞춘다.

캐릭터의 몸 충돌은 CharacterController, 넓은 머리의 여유 검사는 별도로 본다. 머리 외형 전체를 하나의 굵은 캡슐로 취급하지 않는다. 기본 검수는 1.4m 키와 최대 헤어 폭 1.4m를 대상으로 한다. 실제 v7 메시·리그·애니메이션 호환성은 이 대체물로 판정할 수 없다.

## 이동 거리와 확장 판단

집 밖 → 앞마당 → 횡단보도 → 카페 문까지 제안한 꺾인 경로는 약 25.3m, 같은 앞마당에서 호텔로 분기하는 경로는 약 20.3m다. 시작 속도인 걷기 2.5m/s에서는 각각 약 10.1초·8.1초, 달리기 5m/s에서는 약 5.1초·4.1초다. 대화, 정지, 회전, 사용자 우회 시간을 제외한 계산값이다.

북쪽 회색 보도에는 10m마다 표시를 두었다. 40m 확장은 걷기 약 16초, 달리기 약 8초를 추가한다. 기본 생성 상태에서 실제로 걸어보고 건물을 더 배치할지, 이동 속도를 바꿀지 판단한다. 지금 단계에서 빠른 이동이나 차량 기능은 추가하지 않는다.

북쪽의 6×6m 표시는 추가 건물 부지다. 교차로 서쪽·동쪽에도 차도 6m와 양쪽 보도 4m의 연결 폭을 유지한다. 확장 시에도 출입구 비움 영역과 카메라 시야를 유지하고, 임시 경계와 카메라 원거리 평면을 새 범위에 맞춰 확인한다.

## 검수 도구

**Tools → A305 → World → Verify Mainstreet Sandbox**는 Play 상태에서 시작 화면, 대화 화면, 카페 접근, 북쪽 확장, 전체 배치, 캐릭터와 건물의 높이 비교 화면을 캡처한다. 카메라 렌더를 저장하므로 화면 위 조작 안내 UI는 캡처에 포함하지 않는다. 문 통과 시 CharacterController 이동과 머리 외곽의 간섭, 북쪽 보도의 연속 통행, Missing Script·카메라 수·루트 그룹·Scene List를 검사한다. 결과는 저장소의 `artifacts/unity/mainstreet-blockout/`에 저장한다.

이 도구는 Play를 시작하고 끝낸다. 검수 동안 사용자 입력을 피한다. `BuildAndVerify` 명령은 새 씬 생성, 프로젝트 파일 갱신, 검수를 이어서 실행한다. 자동 종료를 직접 처리하므로 아래 명령에 `-quit`을 붙이지 않는다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe' `
  -batchmode -projectPath frontend/UnityClient `
  -executeMethod A305.EditorTools.World.TownMainstreetVerification.BuildAndVerify `
  -logFile "$env:TEMP/MainstreetSandbox.log"
```

배치와 카메라 연출은 프로젝트의 테스트 우선 제외 대상이다. 기존 이동·카메라 로직은 재사용하며, 생성 에셋의 실제 실행과 화면 검수로 확인한다. 추가한 대체물 숨김·복구 동작은 PlayMode 회귀 테스트로 확인한다. Web 성능과 실제 입력 장치에서의 조작감은 별도로 확인한다.
