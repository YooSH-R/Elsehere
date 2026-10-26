# ObjectHover 모듈

3D 사물에 마우스를 올리면 사물에 연결된 영어 표현과 한국어 뜻, 예문을 툴팁으로 보여 줍니다. 학습할 사물에 공용 단어 에셋을 연결하면 공간 씬 어디서나 같은 방식으로 동작합니다.

이 서비스의 학습 흐름을 다루는 게임 전용 기능입니다. 플레이어의 이동 상태와 위치는 `A305.Player`의 공개 API로 읽습니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `HoverTooltipState` | 가리킨 사물과 표시 가능 여부로 툴팁에 보여 줄 사물을 정합니다. Unity 객체 없이 테스트합니다. |
| `ObjectWordTable` | 사물 키로 영어 표현을 찾습니다. |
| `ObjectWordCatalog`, `ObjectWordDetails` | 공용 단어 키로 뜻·영문 예문·해석·발음 파일 키를 조회하고 누락·중복 키를 거부합니다. |
| `ObjectWordDefinition` | Inspector에서 편집하는 공용 단어 원본입니다. 여러 프리팹이 같은 에셋을 참조합니다. |
| `CatalogObjectWordSource` | 공용 에셋을 직접 읽는 씬 검수용 데이터 소스입니다. |
| `ObjectHoverRaycast`, `ObjectHoverHitArea` | 이동 충돌과 별개인 시각 영역으로 사물을 가리킵니다. 일반 Trigger는 무시합니다. |
| `TooltipPlacement` | 툴팁을 사물 위에 두되 화면 밖으로 나가지 않게 자리를 정합니다. |
| `HoverableObject` | 학습할 사물에 붙여 Word 에셋을 연결합니다. 자식을 포함해 콜라이더가 있어야 가리킬 수 있습니다. |
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
| 단어장에 저장하지 않은 단어 | 영어 표현과 뜻만 보이고 예문 줄은 숨깁니다(ERD `vocabularies.example` "저장 후 호버에 표시"). |

## 단어 데이터

프리팹의 `HoverableObject.Word`에는 공용 단어 에셋을 직접 연결합니다. 예를 들어 여러 벤치가 모두 `Words/bench.asset`을 참조하므로 뜻을 한 번 수정하면 모든 벤치에 반영됩니다. 배치마다 식별자를 등록하지 않습니다.

편집 원본은 `Assets/Data/ObjectHover/Words/`의 `ObjectWordDefinition` 에셋입니다. Project 창에서 에셋을 선택하고 Inspector의 언어 코드·단어·한국어 뜻·영문 예문·예문 해석·발음 파일 키를 편집합니다. 발음 파일 키는 선택 항목이며 비워 두면 DB에 NULL로 적재합니다. 내부 공용 키는 자동으로 관리하고, 에셋 이름이나 위치를 바꿔도 유지합니다. 에셋을 복제하면 다른 키가 부여됩니다.

Inspector의 **백엔드 데이터 내보내기** 버튼 또는 **Tools → A305 → ObjectHover → Export Backend Data**로 아래 파일을 생성합니다. 씬 배치를 바꿨다면 먼저 씬을 저장합니다.

| 생성 파일 (저장소 루트 기준) | 내용 |
| --- | --- |
| `backend/src/main/resources/content/object-words.json` | 공용 키와 모든 단어 내용 |
| `backend/src/main/resources/content/object-word-bindings.json` | 씬별 공용 키·사물 수, 프리팹 GUID·경로·공용 키 |
| `backend/src/main/resources/db/seed/world.sql`의 `GENERATED OBJECT WORDS` 구간 | 서버 시작 시 적용할 어휘 SQL |

**Check Backend Export**는 에셋과 생성 파일이 일치하는지 검사합니다. Editor가 프로젝트를 닫은 상태에서는 저장소 루트의 `./infra/scripts/sync-object-words.ps1`과 `-Check`로 같은 작업을 실행할 수 있습니다. 프로젝트 버전의 Unity Editor가 필요합니다. 다른 설치 위치는 `-UnityPath` 또는 `UNITY_EDITOR_PATH`로 지정합니다.

`SharedObjectWords.asset`은 공용 에셋 참조 목록이며 내보낼 때 갱신합니다. 호버는 연결된 에셋을 직접 읽습니다. 기존 검수 조건에 맞춰 TV만 저장된 단어로 미리 표시하며, 사용자별 저장 여부는 원본에 넣지 않습니다. 예문 표시 규칙은 `ObjectWord.ForHover`가 맡습니다.

내보내기는 로컬 백엔드 파일을 생성하는 작업입니다. 서버는 시작 시 SQL의 어휘를 적재하며, 연결 JSON의 DB 적재와 Unity의 서버 조회는 아직 적용하지 않았습니다. 서버 연동 작업에서 `ObjectWordSource`와 사용자별 상태를 연결합니다. 현재 DB의 어휘 고유 키는 언어 코드와 영어 표현이므로 이를 바꾸면 새 행이 생기고 기존 행은 남습니다.

- 저장 여부: ERD `user_vocab`에 단어가 있으면 저장한 단어로 보고 예문을 보입니다.
- 사용자가 고친 뜻: ERD `user_vocab.custom_meaning`을 툴팁에 보일지는 단어장 작업에서 정합니다.
- 호버 숨김: ERD `user_object_prefs.hover_hidden`("알아요 · 다음에 안 띄우기")인 사물은 단어가 없는 것으로 처리해 툴팁을 띄우지 않습니다.

전체 단어 내용은 공용 에셋에서 확인합니다. 문서·프리팹·생성 JSON에 내용을 중복 작성하지 않습니다. **Migrate Legacy JSON to Word Assets**는 이전 JSON에서 빠진 에셋을 처음 만들 때만 쓰며, 기존 에셋의 편집 내용은 덮어쓰지 않습니다.

## 사물 추가

1. 기존 공용 에셋을 재사용합니다. 새 단어라면 **Assets → Create → A305 → Object Hover → Word**로 만들고 뜻·영문 예문·해석까지 입력합니다.
2. 프리팹의 `HoverableObject.Word`에 에셋을 지정합니다. Builder 자동 연결이 필요하면 `Scripts/Editor/ObjectHover/ObjectWordBindings.json`에 프리팹 또는 의미 있는 사물 루트 이름과 내부 공용 키를 추가합니다.
3. `BuilderTools`로 프리팹과 씬을 저장하면 알려진 사물에 에셋과 호버 전용 Trigger를 연결합니다. 이미 지정한 Word는 유지합니다. 모델을 합쳐 만든 프리팹은 하나의 사물 단위로 연결하고, 별도 하위 프리팹은 각각 연결합니다.
4. **Tools → A305 → ObjectHover → Register All Scene Words**로 배포용 집·카페·호텔·마을의 누락 검사를 통과시킵니다. 보고서 경로는 `A305_WORD_REPORTS` 환경변수 또는 임시 폴더의 `A305ObjectWords/registration.txt`입니다.
5. 전체 등록은 백엔드 파일도 내보냅니다. **Check Backend Export** 또는 `sync-object-words.ps1 -Check`를 통과시킵니다. 에셋·연결표·생성 데이터·프리팹·씬·새 `.meta`를 함께 반영합니다.

공간 씬의 호버 처리와 툴팁은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 붙이므로 씬마다 직접 배치하지 않습니다.

## Builder

**Tools → A305 → ObjectHover → Build**(`Scripts/Editor/ObjectHover/ObjectHoverBuilder.cs`)가 다음을 준비합니다.

- 설정 `Data/ObjectHover/ObjectHoverSettings.asset`: 없을 때만 만듭니다. 공용 에셋을 찾아 `SharedObjectWords.asset`의 참조 목록을 갱신합니다.
- 툴팁 프리팹 `Prefabs/ObjectHover/WordTooltip.prefab`: 컴포넌트와 연결만 맞추고, 색·크기·글꼴 같은 디자인은 유지합니다.
- 대체 공간 프리팹(`Prefabs/Home/HomeRoom`, `Prefabs/Cafe/CafeInterior`)의 가구에 사물 키: 키가 없는 가구에만 붙이고, 콜라이더가 없는 간판에는 상자 콜라이더를 붙입니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.ObjectHover.Tests` | `Assets/Tests/EditMode/ObjectHover/` | 표시 지연·숨김·화면 안 배치, 공용 에셋 수정 반영, 내부 키 유지·복제, JSON·SQL 내보내기와 필수값·중복 검증 |
| `A305.ObjectHover.PlayModeTests` | `Assets/Tests/PlayMode/ObjectHover/` | 내 방에서 저장한 TV는 단어·뜻·예문이, 저장하지 않은 에어컨은 단어·뜻만 보이는지, 다른 곳을 가리키거나 플레이어가 멀어지면 숨는지, 방 가구에 사물 키가 있는지 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
