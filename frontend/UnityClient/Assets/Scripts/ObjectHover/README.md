# ObjectHover 모듈

3D 사물에 마우스를 올리면 사물에 연결된 영어 표현과 한국어 뜻, 예문을 툴팁으로 보여 줍니다. 사물을 클릭하면 카드를 그 자리에 고정해 커서가 벗어나도 카드 안 버튼([단어장에 저장]·[발음 듣기]·[알아요 · 다시 안 띄우기]·[닫기])을 누를 수 있습니다. 학습할 사물에 공용 단어 에셋을 연결하면 공간 씬 어디서나 같은 방식으로 동작합니다.

이 서비스의 학습 흐름을 다루는 게임 전용 기능입니다. 플레이어의 이동 상태와 위치는 `A305.Player`의 공개 API로 읽습니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `HoverTooltipState` | 가리킨 사물과 표시 가능 여부로 툴팁에 보여 줄 사물을 정하고, 클릭으로 고정한 카드를 유지합니다. Unity 객체 없이 테스트합니다. |
| `PointerClick` | 누른 자리와 뗀 자리로 사물 클릭인지 정합니다. 8px보다 많이 끌면 시점 회전으로 봅니다. |
| `ObjectWordTable` | 사물 키로 영어 표현을 찾습니다. |
| `ObjectWordCatalog`, `ObjectWordDetails` | 공용 단어 키로 뜻·영문 예문·해석·발음 파일 키를 조회하고 누락·중복 키를 거부합니다. |
| `ObjectWordDefinition` | Inspector에서 편집하는 공용 단어 원본입니다. 여러 프리팹이 같은 에셋을 참조합니다. |
| `CatalogObjectWordSource` | 공용 에셋을 직접 읽는 씬 검수용 데이터 소스입니다. |
| `ObjectHoverRaycast`, `ObjectHoverHitArea` | 이동 충돌과 별개인 시각 영역으로 사물을 가리킵니다. 일반 Trigger는 무시합니다. |
| `TooltipPlacement` | 툴팁을 마우스 바로 위에 두고 화면 밖으로 나가면 가장자리 안으로 당깁니다. |
| `ObjectWordPartsDefinition` | 가구 로컬 좌표의 세부 영역과 공용 단어 연결을 관리합니다. |
| `HoverableObject` | 학습할 사물에 붙여 Word 에셋을 연결합니다. 자식을 포함해 콜라이더가 있어야 가리킬 수 있습니다. |
| `ObjectHoverController` | 포인터 방향의 사물을 찾아 툴팁을 보이거나 숨기고, 클릭한 사물의 카드를 고정하거나 풉니다. 장소에 들어오면 서버 단어 카드를 받아 카드 버튼과 연결합니다. 플레이어 자신은 가리지 않습니다. |
| `WordTooltipView` | 영어 표현·뜻·예문을 보여 주는 화면 오버레이 툴팁입니다. 호버로 보일 때는 클릭을 막지 않고 "클릭하면 카드가 고정돼요" 안내를 보이며, 고정한 카드만 클릭을 받고 안내 문구(`Status`)와 버튼 줄(`WordActions`의 [단어장에 저장]·[발음 듣기], `Actions`의 [알아요 · 다시 안 띄우기]·[닫기])을 보입니다. |
| `ObjectWordCards`, `WordCard` | 장소의 서버 단어 카드(WORLD-02)와 사용자별 저장·숨김 상태. 저장(VOCAB-02)·숨김(WORLD-03)을 서버가 받아들인 뒤에만 반영하고, 실패하면 이유를 카드에 남깁니다. Unity 객체 없이 테스트합니다. |
| `WordCardButtons` | 고정한 카드의 버튼 문구와 누를 수 있는지를 정합니다. 서버 단어 카드가 없으면 [닫기]만 둡니다. |
| `IObjectWordApi`, `ObjectWordApi` | WORLD-02·03, VOCAB-02(`OBJECT`)의 경로와 본문, WORLD-04 발음 경로 |
| `ObjectWordScenes` | Unity 씬 이름으로 서버 장소 코드를 찾습니다. 서버 시드와 `World/LastSceneRecorder`의 장소와 같아야 합니다. |
| `ObjectWordServerLink` | 카드 프리팹에 붙은 서버 연결. API 주소(`ApiSettings`)와 발음 재생(`AudioSource`)을 맡아 장소 씬마다 따로 배치하지 않습니다. `Use Server`를 끄면 서버 없이 Unity 단어로만 카드를 보입니다. |
| `ObjectHoverSettings` | 표시 지연, 최대·세부 거리, 포인터 간격, 화면 여백, 가리킬 레이어를 담는 데이터 에셋입니다. |
| `ObjectWordSource`, `MockObjectWordSource` | 사물 키로 영어 표현을 찾는 곳과 서버 단어 카드가 없을 때 쓰는 확인용 단어 목록입니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Player` | `PlayerMover.IsMoving`, `IsMovementLocked`, 플레이어 위치 |
| `A305.Api` | 서버 단어 카드·저장·숨김 요청(`ApiClient`)과 서버 주소(`ApiSettings`) |
| Unity Input System | `UI/Point` 포인터 위치, 클릭·탭(`Pointer.current.press`), 고정을 푸는 Esc |
| uGUI, TextMeshPro | 툴팁 화면과 한국어 글자. 글꼴은 [Unity 개발 규약의 UI 글자와 글꼴](../../../../../docs/unity/conventions.md#ui-글자와-글꼴)을 따릅니다. |

## 표시 규칙

현재 구현된 표시·자동 숨김 규칙입니다. 수치는 `Assets/Data/ObjectHover/ObjectHoverSettings.asset`에서 조정합니다. 이 규칙은 S15P31A305-166의 표시 정책 범위에 해당합니다.

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
| 단어장에 저장한 단어 | 예문과 그 아래에 예문 뜻을 보입니다(기능 명세 F-12). |
| [알아요 · 다시 안 띄우기]로 숨긴 단어 | 호버로는 보이지 않고, 사물을 클릭하면 카드를 고정해 보입니다. 같은 단어를 쓰는 모든 사물과 장소에 적용됩니다. |
| 세부 영역이 있는 가구를 3m 밖에서 가리킴 | 침대·식물·주방 조리대처럼 가구 전체의 단어를 보입니다. |
| 세부 영역을 3m 이내에서 가리킴 | 베개·이불·화분·싱크대·수전처럼 해당 부분의 단어를 보입니다. |
| 사물을 클릭(누른 자리에서 8px 안에서 뗌, 휴대폰은 탭) | 지연 없이 클릭한 자리에 카드를 고정합니다. 걷는 중에도 고정할 수 있습니다. 시점을 돌린 드래그와 UI 위 클릭은 사물 클릭으로 보지 않습니다. |
| 카드를 고정한 뒤 커서가 벗어나거나 다른 사물을 가리킴 | 카드를 그대로 둡니다. 다른 사물을 클릭하면 그 사물 카드로 바꿉니다. |
| 고정한 카드의 [닫기], 사물이 아닌 곳 클릭, Esc | 고정을 풀고 닫습니다. 그 뒤의 호버는 다시 지연 시간을 지나야 보입니다. |
| 카드를 고정한 뒤 플레이어가 그 사물에서 `Max Distance Meters`보다 멀어지거나 대화로 이동이 잠김 | 고정을 풀고 닫습니다. |

툴팁은 포인터 위에 표시하며, 화면 가장자리에서는 잘리지 않도록 위치를 조정합니다. 표시 거리·세부 거리·지연·포인터 간격·화면 여백은 모두 `Data/ObjectHover/ObjectHoverSettings.asset`에서 편집합니다. 가구별 `Data/ObjectHover/Parts/` 에셋의 각 항목에는 단어 참조와 가구 로컬 좌표의 Bounds 영역을 저장합니다. 겹치는 영역은 목록의 앞 항목을 우선합니다. 프리팹의 `HoverableObject.Parts`에 해당 에셋을 연결합니다.

Project 창에서 아래 에셋을 선택하고 Inspector에서 편집합니다. 세부 거리는 카메라가 아닌 플레이어와 가리킨 표면 사이의 거리입니다.

| 편집할 내용 | `Assets/Data/ObjectHover/` 아래 위치 |
| --- | --- |
| 가까이에서 세부 단어로 바뀌는 거리 | `ObjectHoverSettings.asset`의 `Detail Distance Meters` |
| 호버 지연·최대 거리·포인터 간격·화면 여백 | 같은 에셋의 `Show Delay Seconds`, `Max Distance Meters`, `Pointer Gap`, `Screen Margin` |
| 침대의 베개·이불 등 부분별 연결과 영역 | `Parts/SageBed.asset` 등 가구별 에셋의 `Parts` 목록 |
| 단어·뜻·예문·발음 파일 키 | `Words/`의 공용 단어 에셋 |

## 단어 데이터

프리팹의 `HoverableObject.Word`에는 공용 단어 에셋을 직접 연결합니다. 예를 들어 여러 벤치가 모두 `Words/bench.asset`을 참조하므로 뜻을 한 번 수정하면 모든 벤치에 반영됩니다. 배치마다 식별자를 등록하지 않습니다.

편집 원본은 `Assets/Data/ObjectHover/Words/`의 `ObjectWordDefinition` 에셋입니다. Project 창에서 에셋을 선택하고 Inspector의 언어 코드·단어·한국어 뜻·영문 예문·예문 해석·발음 파일 키를 편집합니다. 발음 파일 키는 선택 항목이며 비워 두면 DB에 NULL로 적재합니다. 내부 공용 키는 자동으로 관리하고, 에셋 이름이나 위치를 바꿔도 유지합니다. 에셋을 복제하면 다른 키가 부여됩니다.

Inspector의 **백엔드 데이터 내보내기** 버튼 또는 **Tools → A305 → ObjectHover → Export Backend Data**로 아래 파일을 생성합니다. 씬 배치를 바꿨다면 먼저 씬을 저장합니다.

| 생성 파일 (저장소 루트 기준) | 내용 |
| --- | --- |
| `backend/src/main/resources/content/object-words.json` | 공용 키와 모든 단어 내용 |
| `backend/src/main/resources/content/object-word-bindings.json` | 씬별 공용 키·사물 수·세부 항목 수, 프리팹 GUID·경로·전체 및 세부 공용 키 |
| `backend/src/main/resources/db/seed/world.sql`의 `GENERATED OBJECT WORDS` 구간 | 서버 시작 시 적용할 어휘 SQL |

**Check Backend Export**는 에셋과 생성 파일이 일치하는지 검사합니다. Editor가 프로젝트를 닫은 상태에서는 저장소 루트의 `./infra/scripts/sync-object-words.ps1`과 `-Check`로 같은 작업을 실행할 수 있습니다. 프로젝트 버전의 Unity Editor가 필요합니다. 다른 설치 위치는 `-UnityPath` 또는 `UNITY_EDITOR_PATH`로 지정합니다.

`SharedObjectWords.asset`은 공용 에셋 참조 목록이며 내보낼 때 갱신합니다. 서버 단어 카드가 없을 때 호버는 연결된 에셋을 직접 읽고, 기존 검수 조건에 맞춰 TV만 저장된 단어로 미리 표시합니다. 사용자별 저장 여부는 원본에 넣지 않습니다. 예문 표시 규칙은 `ObjectWord.ForHover`가 맡습니다.

내보내기는 로컬 백엔드 파일을 생성하는 작업입니다. 서버는 시작 시 SQL의 어휘와 연결 JSON의 장소별 단어를 적재하고, Unity는 장소에 들어올 때 그 장소의 단어 카드를 받습니다. 현재 DB의 어휘 고유 키는 언어 코드와 영어 표현이므로 이를 바꾸면 새 행이 생기고 기존 행은 남습니다.

전체 단어 내용은 공용 에셋에서 확인합니다. 문서·프리팹·생성 JSON에 내용을 중복 작성하지 않습니다. **Migrate Legacy JSON to Word Assets**는 이전 JSON에서 빠진 에셋을 처음 만들 때만 쓰며, 기존 에셋의 편집 내용은 덮어쓰지 않습니다.

## 카드 버튼과 서버 연결

장소에 들어오면 `ObjectHoverController`가 씬 이름으로 장소 코드를 찾아 WORLD-02로 그 장소의 단어 카드를 한 번 받습니다. 카드는 서버 응답의 단어·뜻·예문과 사용자별 저장(`saved`)·숨김(`hoverHidden`) 상태로 그립니다. 저장·숨김 상태는 단어 기준이라 같은 단어를 쓰는 모든 사물과 장소에 같게 적용됩니다.

| 버튼·상황 | 동작 |
| --- | --- |
| [단어장에 저장] | VOCAB-02(`sourceType: OBJECT`, `wordKey`)로 저장합니다. 저장하면 버튼이 "저장했어요"로 바뀌어 꺼지고 예문과 예문 뜻이 보입니다. 실패하면 이유를 보이고, 다시 누르면 같은 `requestId`로 보냅니다. |
| [알아요 · 다시 안 띄우기] | WORLD-03 `hoverHidden: true`. 이후 호버로는 카드가 뜨지 않고 클릭해야 보인다고 안내합니다. 버튼은 [다시 띄우기]로 바뀝니다. |
| [다시 띄우기] | WORLD-03 `hoverHidden: false`. 다시 호버로 카드가 뜹니다. |
| [발음 듣기] | WORLD-04 음성을 받아 재생합니다. 같은 장소에서 다시 누르면 받은 음성을 다시 재생합니다. `hasPronunciation: false`인 단어는 버튼을 숨기고, 받지 못하면 다시 누르라고 안내합니다. |
| 요청을 기다리는 중 | [단어장에 저장]과 [알아요 · 다시 안 띄우기]를 막아 같은 요청을 두 번 보내지 않습니다. |
| 단어 카드를 받지 못함(로그인 전, 서버 꺼짐, 서버 장소가 아닌 검수 씬, `Use Server` 꺼짐) 또는 서버에 없는 단어 | Unity 단어로 카드는 그대로 보이고 단어 버튼과 안내 없이 [닫기]만 둡니다. |

단어장 메모(`user_vocab.memo`)는 단어장에서만 다루며 카드에는 보이지 않습니다. 저장 취소도 단어장의 삭제(VOCAB-04)로 합니다.

## 사물 추가

1. 기존 공용 에셋을 재사용합니다. 새 단어라면 **Assets → Create → A305 → Object Hover → Word**로 만들고 뜻·영문 예문·해석까지 입력합니다.
2. 프리팹의 `HoverableObject.Word`에 에셋을 지정합니다. Builder 자동 연결이 필요하면 `Scripts/Editor/ObjectHover/ObjectWordBindings.json`에 프리팹 또는 의미 있는 사물 루트 이름과 내부 공용 키를 추가합니다.
3. 세부 영역이 있는 가구는 **Assets → Create → A305 → Object Hover → Parts**로 `Data/ObjectHover/Parts/`에 에셋을 만들고, 부분별 단어와 가구 로컬 좌표의 영역을 입력해 `HoverableObject.Parts`에 연결합니다. 작은 부분을 앞에 두면 겹치는 큰 영역보다 우선합니다.
4. `BuilderTools`로 프리팹과 씬을 저장하면 알려진 사물에 에셋과 호버 전용 Trigger를 연결합니다. 이미 지정한 Word와 Parts는 유지합니다. 세부 데이터가 있는 합쳐진 모델은 떨어진 모양마다 호버 Trigger를 나눕니다. 이동 충돌은 유지합니다.
5. **Tools → A305 → ObjectHover → Register All Scene Words**로 배포용 집·카페·호텔·마을의 누락 검사를 통과시킵니다. 보고서 경로는 `A305_WORD_REPORTS` 환경변수 또는 임시 폴더의 `A305ObjectWords/registration.txt`입니다.
6. 전체 등록은 백엔드 파일도 내보냅니다. **Check Backend Export** 또는 `sync-object-words.ps1 -Check`를 통과시킵니다. 에셋·연결표·생성 데이터·프리팹·씬·새 `.meta`를 함께 반영합니다.

공간 씬의 호버 처리와 툴팁은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 붙이므로 씬마다 직접 배치하지 않습니다.

## Builder

**Tools → A305 → ObjectHover → Set Up Detailed Words**는 현재 가구의 세부 단어와 Parts 에셋을 없을 때만 만들고 프리팹에 연결합니다. 침대·조리대·소파·책장·탁자·냉장고·커튼·카페 벤치·화분을 지원합니다. 이미 편집한 단어·영역·설정을 덮어쓰지 않으며, 마지막에 전체 씬 등록과 백엔드 내보내기를 실행합니다.

**Tools → A305 → ObjectHover → Build**(`Scripts/Editor/ObjectHover/ObjectHoverBuilder.cs`)가 다음을 준비합니다.

- 설정 `Data/ObjectHover/ObjectHoverSettings.asset`: 없을 때만 만듭니다. 공용 에셋을 찾아 `SharedObjectWords.asset`의 참조 목록을 갱신합니다.
- 툴팁 프리팹 `Prefabs/ObjectHover/WordTooltip.prefab`: 컴포넌트와 연결만 맞추고, 색·크기·글꼴 같은 디자인은 유지합니다. 없을 때만 안내 문구(`Status`)와 버튼 줄(`WordActions`, `Actions`의 `HideButton`)을 만들고, 루트에 `ObjectWordServerLink`·`AudioSource`를 붙여 `Data/Api/ApiSettings.asset`을 연결합니다.
- 대체 공간 프리팹(`Prefabs/Home/HomeRoom`, `Prefabs/Cafe/CafeInterior`)의 가구에 사물 키: 키가 없는 가구에만 붙이고, 콜라이더가 없는 간판에는 상자 콜라이더를 붙입니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.ObjectHover.Tests` | `Assets/Tests/EditMode/ObjectHover/` | 표시 지연·숨김·화면 안 배치, 클릭 고정(바로 보임·커서가 벗어나도 유지·다른 사물 호버로 안 바뀜·다른 사물 클릭으로 바뀜·풀면 다시 지연), 클릭과 시점 회전 드래그 구분, 공용 에셋 수정 반영, 내부 키 유지·복제, JSON·SQL 내보내기와 필수값·중복, 거리 경계와 실제 프리팹 표면의 세부 단어 선택. 서버 단어 카드(장소별 상태 받기·실패 시 버튼 없음, 저장·실패 이유·같은 요청 ID로 다시 저장·중복 요청 막기, 숨김·다시 띄우기·실패), 버튼 문구, API 경로·본문, 씬 이름의 장소 코드, 저장한 단어의 예문 뜻 |
| `A305.ObjectHover.PlayModeTests` | `Assets/Tests/PlayMode/ObjectHover/` | 저장 여부에 따른 예문 표시, 자동 숨김, 클릭한 TV 카드 고정과 [닫기]·빈 곳 클릭·멀어짐으로 닫기, 방 가구의 단어 연결, 마우스 위 배치와 설정 에셋 간격 반영, 접근·후퇴 시 침대와 베개 단어 전환 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
