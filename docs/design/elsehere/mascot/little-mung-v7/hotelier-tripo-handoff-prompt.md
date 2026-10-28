# 호텔리어 Tripo 제작과 Unity 명찰 연동 프롬프트

리틀 멍 v7 호텔리어의 현재 4면 이미지를 사용해 모델을 제작하고, 빈 금색 명찰에 NPC 이름을 연결하는 후속 작업 지시문이다. 아래 작업 프롬프트를 실행 담당 에이전트에게 전달한다. 이미지 편집본의 `hotelier-turnaround-v3`는 호텔리어 명찰 수정 차수이며 캐릭터 디자인 버전은 v7을 유지한다.

## 작업 프롬프트

elsehere 미국 소도시의 남성 호텔리어를 Unity용 3D 에셋으로 제작해 줘. 현재 확정된 4면 참고 이미지의 외형을 유지하고, 명찰의 이름과 직책은 Unity에서 바꿀 수 있게 만들어 줘.

### 입력과 작업 범위

- 저장소 루트의 `AGENTS.md`, `frontend/AGENTS.md`, `docs/code-conventions.md`, `docs/development.md`, `docs/unity/conventions.md`부터 읽어.
- [리틀 멍 v7 기준](README.md), [B안 제작 규격](../../village/production-v2/art-spec.md), [Unity 적용 순서](../../village/production-v2/unity-application-plan.md)를 함께 따라.
- 연결 스토리는 `S15P31A305-114`야. 작업 시작 시 현재 상태와 관련 작업을 확인하고, 같은 스토리의 기존 브랜치·워크트리를 우선 사용해. 다른 작업의 변경을 섞지 마.
- 현재 모델 생성 입력은 아래 네 파일이야. 파일명과 원본은 보존해. 과거 `hotelier-turnaround-v2`의 ELI 명찰이나 정장 차림의 행인 이미지를 입력으로 쓰지 마.

| 방향 | 저장소 기준 파일 |
| --- | --- |
| 정면 | [캐릭터 호텔리어(남) 정면.png](../../../../../frontend/design/캐릭터%20호텔리어%28남%29%20정면.png) |
| 코가 화면 왼쪽을 향하는 측면 | [캐릭터 호텔리어(남) 좌측면.png](../../../../../frontend/design/캐릭터%20호텔리어%28남%29%20좌측면.png) |
| 코가 화면 오른쪽을 향하는 측면 | [캐릭터 호텔리어(남) 우측면.png](../../../../../frontend/design/캐릭터%20호텔리어%28남%29%20우측면.png) |
| 후면 | [캐릭터 호텔리어(남) 후면.png](../../../../../frontend/design/캐릭터%20호텔리어%28남%29%20후면.png) |

네 이미지는 각각 1254×1254 PNG야. 같은 메시를 회전해 렌더한 이미지가 아니므로 시점 간 차이는 정면의 얼굴·비율과 단일 명찰 위치를 기준으로 정리해. 원본을 다시 생성하거나 자동으로 덮어쓰지 마.

### 외형과 명찰 규칙

- 아주 큰 머리와 작은 몸, 짧은 팔다리, 둥근 사각형에 가까운 볼, 중앙에 모인 눈·코·입을 유지해. 눈동자를 흰자 중앙으로 옮기거나 미간을 넓히지 마.
- 샌드 블론드 헤어, 헤이즐 눈, 밝은 피부와 옅은 주근깨, 작은 코와 미소를 유지해. 미국 현지라는 배경을 특정 민족의 과장된 얼굴 특징으로 표현하지 마.
- 네이비 호텔 유니폼, 낮은 스탠드 칼라, 금색 테두리와 단추, 아이보리 셔츠, 노란 넥타이, 갈색 구두를 유지해.
- 명찰은 **캐릭터 자신의 왼쪽 가슴**에 하나만 있어야 해. 정면에서는 화면 오른쪽이야. 반대쪽 가슴·팔·등에 복제하지 마.
- 명찰 앞면은 글자와 아이콘이 없는 평평한 금색 판이야. 이름, ELI, FRONT DESK, 로고, 문양, 음각·양각을 메시나 텍스처에 넣지 마.
- 명찰의 표면 법선·너비·높이와 부착 위치를 확인하고 Unity 텍스트를 올릴 수 있게 해. 생성 결과에서 명찰이 몸에 뭉개지거나 글자가 생기면 DCC에서 단순한 별도 판으로 정리하고 기존 표면의 글자 흔적도 제거해. 명찰 하나를 고치기 위해 전체 캐릭터를 반복 유료 생성하지 마.
- 거친 피부·직물 미세 질감은 생략하고 부드러운 무광 3D 게임 에셋 느낌으로 맞춰.

### Tripo 실행 순서

1. `tripo-3d`와 `tripo-game-asset` 스킬을 읽고 설치된 공식 CLI의 버전·인증·지원 기능을 확인해. 실제 비밀 키를 출력하지 마.
2. 다중 시점 입력을 우선 검토해. 한글 원본 이름만으로 CLI가 방향을 알아본다고 가정하지 마. 스테이징 복사본에 `front.png`, `left.png`, `back.png`, `right.png`를 사용하되, 이 프로젝트의 측면 이름은 코가 향하는 화면 방향이라는 점을 고려해 실제 도구의 좌우 슬롯과 대조한 후 매핑해. 파일명의 방향 단서를 쓰지 않는 입력의 기본 순서는 front → left → back → right이므로 한글 파일의 정렬 순서대로 넘기지 마.
3. 선택한 모델과 다중 시점 지원 여부, 입력 슬롯 매핑, 전체 처리 단계, 예상 크레딧, 저장 경로를 dry-run/plan으로 먼저 보여줘. 프리셋이 FBX 변환이나 리깅·리타기팅을 추가하는지도 펼쳐서 확인해. 승인된 비용과 단계 범위 안에서만 실행하고 불필요한 재생성은 하지 마.
4. 이미지 기반 생성에서 텍스트 지시나 negative prompt가 전달된다고 가정하지 마. 아래 영문 설명은 외형 검수 기준이며 실제 선택한 모드가 해당 입력을 지원할 때만 전달해.
5. 먼저 한 명의 모델에서 외형·명찰·메시를 검사해. 현재 참고 포즈는 팔을 내린 자세이므로 자동 리깅용 T포즈로 간주하지 마. 필요하면 원본과 분리된 리깅 준비본에서 자세를 정리해.
6. 공통 베이스 리그가 있으면 호환성을 먼저 확인해. 없으면 이 모델을 공통화 후보로 검토하고 본 계층·관절 위치·bind pose·스킨을 확인해. NPC마다 따로 자동 리깅한 결과를 공통 리그라고 부르지 마.
7. 생성 원본 GLB를 보관하고 Unity용 FBX·텍스처를 준비해. 변환·리깅 등 후속 유료 단계는 실행 전에 계획에 포함해. 재질·스킨·축·텍스처 연결이 변환 후에도 유지되는지 확인해.

### 모델 정리와 Unity 연결

- 전체 키는 헤어 최고점부터 신발 바닥까지 1.60m로 맞춰. 머리 0.96m·턱 아래 몸 0.64m는 초기 배분값이므로 참고 이미지와 비교하며 측정해. 프리팹 루트 Scale은 (1,1,1), +Y 위·+Z 정면, 원점은 양발 사이 바닥으로 맞춰.
- 헤어·옷을 포함해 8k~15k triangles, 재질 슬롯 2~3개, 텍스처 아틀라스 1024px를 시작 예산으로 삼아. 생성 설정값과 실제 측정 결과를 구분해.
- 헤어와 의상 교체는 공통 베이스·본·bind pose에 맞춰 검사해. 이미지나 Tripo 생성만으로 모듈화·립싱크·실행 성능을 검증했다고 보고하지 마.
- 원본 외부 에셋은 Unity의 `Assets/ThirdParty/<에셋 이름>/`, 정리한 아트는 `Assets/Art/<기능>/`, 기능 프리팹은 `Assets/Prefabs/<기능>/`의 기존 구조에 맞춰 넣어. 시안·중간 파일·검수 캡처·ZIP은 Git에 넣지 마. 문서는 `docs/`에서 관리해.
- 이름 연동은 **모델 정리와 리그 확인 후, Unity NPC 프리팹을 구성할 때** 구현해. 명찰 가까이에 텍스트 부착점을 두고 가슴 본을 따라 움직이게 해. TextMeshPro 텍스트를 판의 방향에 맞춰 얹고 작은 간격으로 표면 겹침을 막아. 카메라를 향해 독립 회전하는 머리 위 이름표와 혼동하지 마.
- 명찰 판과 텍스트 부착점이 걷기·회전·대화 동작에서 함께 움직이는지 확인해. 판이 의상 스킨과 함께 변형된다면 텍스트가 떨어져 보이지 않도록 가슴 부착 구조를 정리해.
- NPC 표시 이름과 직책의 기존 데이터·생성 흐름을 찾아 연결해. 이름을 상수로 넣지 마. 대화 시작 응답에만 이름이 있다면 NPC가 처음 보일 때 표시할 데이터를 어디서 받는지 먼저 해결해. 없는 API나 필드를 구현된 것으로 가정하지 말고 필요한 계약을 확인해.
- NPC 생성·데이터 갱신 때 이름과 직책을 반영하고, 풀링 재사용 때 이전 값을 지워. 빈 값은 해당 글자를 숨기고, 긴 이름은 판 안에 들어가도록 크기·줄 수를 제한해. NPC 문자열을 TMP 서식으로 해석하지 않도록 처리해.
- C# 동작 변경에는 프로젝트 TDD 스킬을 적용해. 프리팹·씬·데이터 에셋은 기존 Builder와 Unity Editor로 만들고 직렬화 파일이나 .meta를 손으로 만들지 마.

### 완료 조건

- 정면·양쪽 측면·후면에서 얼굴, 머리 크기, 유니폼, 명찰 한 개의 위치를 비교해. 입력 이미지와 모델 회전 캡처를 함께 보여줘.
- 명찰 메시와 텍스처에 고정 이름·직책·아이콘이 남아 있지 않아야 해.
- 서로 다른 NPC 두 명, 초기 표시, 이름 변경, 긴 이름, 빈 이름, 풀링 재사용을 검증해. 걷기·회전·대화 중 글자가 판을 따라가고 깜빡임·관통이 없는지 Unity에서 확인해.
- 실제 삼각형 수·재질 수·텍스처 크기·키, 리그·애니메이션 결과와 확인하지 못한 항목을 구분해 보고해. 화면 확인 없이 이미지 또는 파일 생성만으로 Unity 적용 완료라고 하지 마.
- 작업을 작은 변경 단위로 커밋하고 관련 사용 문서를 최신화해. 실행 기록과 검증 결과는 Jira/MR에 남겨. 현재 작업과 무관한 브랜치·파일은 변경하지 마.

## 영문 외형 설명

아래 문구는 모델 제작·검수용 설명이다. 이미지 다중 시점 생성에서는 실제 도구가 지원하는 입력만 전달한다.

```text
Match the supplied four-view references of the Elsehere Little Mung v7 male hotel receptionist. Huge rounded-square head, tiny body, short limbs, clustered facial features, round pupils slightly inward, sandy-blond sculpted swept hair, hazel eyes, fair skin and subtle freckles. Preserve the reference silhouette and proportions. Navy stand-collar reception uniform with fine gold piping, brass buttons, ivory shirt, small yellow tie, navy trousers and brown shoes. One small blank brushed-gold rectangular nameplate on the character's anatomical left chest only. Its front must be flat, clean and unobstructed for separate Unity text. No name, letters, logo, emblem or engraving on the plate or clothing. Smooth matte stylized game materials; no realistic pores or fine fabric noise. No props, pedestal, floor or background geometry. Preserve hair and garment handedness across views.
```
