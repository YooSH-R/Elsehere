# ObjectHover 모듈

3D 사물에 마우스를 올리면 사물에 연결된 영어 표현과 한국어 뜻, 예문을 툴팁으로 보여 줍니다. 내 방의 가구처럼 학습할 사물에 사물 키를 붙이면 공간 씬 어디서나 같은 방식으로 동작합니다.

이 서비스의 학습 흐름을 다루는 게임 전용 기능입니다. 플레이어의 이동 상태와 위치는 `A305.Player`의 공개 API로 읽습니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `HoverTooltipState` | 가리킨 사물과 표시 가능 여부로 툴팁에 보여 줄 사물을 정합니다. Unity 객체 없이 테스트합니다. |
| `ObjectWordTable` | 사물 키로 영어 표현을 찾습니다. |
| `TooltipPlacement` | 툴팁을 사물 위에 두되 화면 밖으로 나가지 않게 자리를 정합니다. |
| `HoverableObject` | 학습할 사물에 붙여 사물 키를 정합니다. 자식을 포함해 콜라이더가 있어야 가리킬 수 있습니다. |
| `ObjectHoverController` | 포인터 방향의 사물을 찾아 툴팁을 보이거나 숨깁니다. 플레이어 자신은 가리지 않습니다. |
| `WordTooltipView` | 영어 표현·뜻·예문을 보여 주는 화면 오버레이 툴팁입니다. 클릭을 막지 않습니다. |
| `ObjectHoverSettings` | 표시 지연, 최대 거리, 가리킬 레이어 같은 수치를 담는 데이터 에셋입니다. |
| `ObjectWordSource`, `MockObjectWordSource` | 사물 키로 영어 표현을 찾는 곳과 서버 연동 전 확인용 단어 목록입니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Player` | `PlayerMover.IsMoving`, `IsMovementLocked`, 플레이어 위치 |
| Unity Input System | `UI/Point` 포인터 위치 |
| uGUI, TextMeshPro | 툴팁 화면과 한국어 글자. 글꼴은 [Unity 개발 규약의 UI 글자와 글꼴](../../../../../docs/unity/conventions.md#ui-글자와-글꼴)을 따릅니다. |

## 표시 규칙

S15P31A305-166(호버 표시·자동 숨김 정책)이 정해지기 전의 기본값입니다. 수치는 `Assets/Data/ObjectHover/ObjectHoverSettings.asset`에서 조정합니다.

| 상황 | 동작 |
| --- | --- |
| 같은 사물을 `Show Delay Seconds`(기본 0.15초) 동안 가리킴 | 툴팁을 보입니다. 마우스가 스쳐 지나갈 때는 깜박이지 않습니다. |
| 포인터가 사물을 벗어남 | 바로 숨깁니다. |
| 다른 사물로 옮김 | 바로 숨기고, 새 사물을 같은 시간 가리키면 보입니다. 툴팁은 하나만 보입니다. |
| 플레이어가 움직임 | 바로 숨기고, 멈춘 뒤 다시 지연 시간이 지나면 보입니다. |
| 가리킨 지점이 플레이어에서 `Max Distance Meters`(기본 8m)보다 멂 | 보이지 않습니다. |
| 대화처럼 이동이 잠긴 상호작용 중 | 보이지 않습니다. |
| 사물 키에 연결된 단어가 없음 | 보이지 않고, 그 키를 Console에 한 번 알립니다. |

## 단어 데이터

사물과 단어는 서버 마스터 데이터입니다. Unity는 ERD `world_objects.unity_key`에 해당하는 사물 키만 사물에 붙이고, 영어 표현·뜻·예문(ERD `vocabularies`의 `text`·`meaning_ko`·`example`)은 서버에서 받습니다.

서버 API가 준비되기 전에는 `Assets/Data/ObjectHover/MockObjectWords.asset`의 확인용 목록을 씁니다. 서버 연동 작업에서 `ObjectWordSource`를 서버에서 받은 단어로 바꾸고, 확인용 목록은 개발 확인에만 씁니다.

| 사물 키 | 사물 | 영어 표현 | 뜻 |
| --- | --- | --- | --- |
| `tv` | 방 TV | TV | 텔레비전 |
| `refrigerator` | 방 냉장고 | refrigerator | 냉장고 |
| `air_conditioner` | 방 에어컨 | air conditioner | 에어컨 |
| `sofa` | 방 소파 | sofa | 소파 |
| `bookshelf` | 방 책장 | bookshelf | 책장 |
| `bed` | 방 침대 | bed | 침대 |
| `counter` | 카페 카운터 | counter | 계산대 |
| `menu` | 카페 메뉴판 | menu | 메뉴 |

## 사물 추가

1. 사물 루트에 `HoverableObject`를 붙이고 `Unity Key`에 서버와 같은 사물 키를 입력합니다. 자식을 포함해 콜라이더가 있어야 합니다.
2. 서버 연동 전에는 `MockObjectWords.asset`에 같은 키의 단어를 추가합니다.
3. 디자인 모델로 바꿀 때도 사물 루트의 `HoverableObject`와 키는 유지합니다.

공간 씬의 호버 처리와 툴팁은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 붙이므로 씬마다 직접 배치하지 않습니다.

## Builder

**Tools → A305 → ObjectHover → Build**(`Scripts/Editor/ObjectHover/ObjectHoverBuilder.cs`)가 다음을 준비합니다.

- 설정 `Data/ObjectHover/ObjectHoverSettings.asset`과 확인용 단어 `Data/ObjectHover/MockObjectWords.asset`: 없을 때만 만들고, 있으면 값을 유지합니다.
- 툴팁 프리팹 `Prefabs/ObjectHover/WordTooltip.prefab`: 컴포넌트와 연결만 맞추고, 색·크기·글꼴 같은 디자인은 유지합니다.
- 대체 공간 프리팹(`Prefabs/Home/HomeRoom`, `Prefabs/Cafe/CafeInterior`)의 가구에 사물 키: 키가 없는 가구에만 붙이고, 콜라이더가 없는 간판에는 상자 콜라이더를 붙입니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.ObjectHover.Tests` | `Assets/Tests/EditMode/ObjectHover/` | 표시 지연·즉시 숨김·하나만 표시, 사물 키 조회, 화면 안 배치 |
| `A305.ObjectHover.PlayModeTests` | `Assets/Tests/PlayMode/ObjectHover/` | 내 방에서 TV를 가리키면 단어·뜻이 보이고, 다른 곳을 가리키거나 플레이어가 멀어지면 숨는지, 방 가구에 사물 키가 있는지 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
