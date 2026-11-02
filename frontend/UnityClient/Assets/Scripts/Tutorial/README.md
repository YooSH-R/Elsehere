# Tutorial 모듈

마을에 막 도착한 여행자가 숙소에서 걷기·둘러보기·사물 단어 저장·단어장을 익히고, 밖으로 나가 길을 헤매는 Ben에게 정해진 대사로 카페 가는 길을 알려 준 뒤, 피드백과 오늘의 목표(지도)를 확인하기까지 단계별로 안내합니다. 단계에 맞게 화면 위쪽 안내 카드와 코치 마크, 행인이 다가와도 되는지, 장소 이동·택시 잠금, 미션 표시를 바꿉니다.

이 서비스의 진행 흐름을 다루는 게임 전용 기능이며, 다른 기능 모듈의 공개 API만 써서 흐름을 잇습니다. 진행 단계는 Play 동안만 남고, 끝낸 기록(완료·건너뛰기)만 서버에 저장합니다.

## 단계

| 단계 | 넘어가는 조건 | 안내 카드 | 코치 마크 |
| --- | --- | --- | --- |
| `Arrival` | [시작하기] | 숙소 도착 | 카드 |
| `Walk` | 이 단계에서 걷기와 달리기(터치 기기는 걷기만)를 모두 하면 완료 표시 1초 뒤 저절로 | [W][A][S][D]·[Shift], 터치 기기는 조이스틱 | 없음 |
| `Look` | 이 단계에서 시점 돌리기·휠 확대·V 시점 바꾸기(터치 기기는 화면 끌기만)를 모두 하면 완료 표시 1초 뒤 저절로 | [마우스 오른쪽] 누른 채 끌기·[휠] 줌·[V], 터치 기기는 화면 오른쪽 끌기 | 없음 |
| `SaveWord` | 고정한 사물 카드의 [저장]. 카드의 [저장]은 서버 단어 카드를 받아야 보이므로, 빌드에서는 서버에 연결되지 않으면 이 단계를 마칠 수 없습니다(건너뛰기는 할 수 있음). Editor에서는 서버가 없어도 확인용 단어 카드로 [저장]을 눌러 볼 수 있습니다([ObjectHover 모듈](../ObjectHover/README.md)). | 호버 카드·클릭 고정·[발음 듣기]·[저장] | 카드를 고정하면 [저장] |
| `OpenVocabulary` | 메뉴(≡)의 [단어장] | 단어장·[피드백 목록]·[홈] | ≡ → [단어장] |
| `LeaveLodging` | 숙소 밖 공간에 들어감 | 밖으로 나가기(단어장이 떠 있으면 숨김) | 숙소 문 위 화살표. 단어장을 열어 이 단계로 넘어온 동안에는 단어장 창을 밝히고 단어장 설명(저장한 단어가 모이는 곳, [외웠어요]·메모)과 닫고 밖으로 나가라는 다음 할 일 |
| `MeetBen` | Ben과 대화 시작. Ben이 [대화하기]를 제안하는 동안에는 멀어지지 않게 이동을 잠급니다(다가오는 동안은 잠그지 않음). | Ben 만나기 | Ben 머리 위 화살표, 제안이 뜨면 [대화하기] |
| `BenDialogue` | 길 안내를 끝까지 마치고 대화 종료 → `BenFeedback`. 중간에 끝내면 `MeetBen`으로 되돌아가 Ben이 다시 다가옵니다. | 숨김 | 입력칸·[보내기]·[예시 문장]·[꾹 눌러 말하기](설명에 말하거나 입력할 대사), 마치면 [대화 종료] |
| `BenFeedback` | 길 안내 피드백의 [닫기] | 피드백 보기(피드백이 떠 있으면 숨김) | 피드백 창 |
| `TodayGoal` | 지도 버튼으로 수첩 열기 → `Completed` | 오늘의 목표·택시·벤치 | 지도 버튼 |
| `Completed` | — | 숨김 | 숨김 |

- 숙소가 아닌 장소에서 시작하면 숙소 안내를 건너뛰고 `MeetBen`부터 합니다.
- [건너뛰기]는 카드 안에서 "튜토리얼을 건너뛸까요?"를 한 번 더 묻고, [건너뛰기]를 다시 눌러야 `Completed`로 넘깁니다. [계속하기]를 누르면 하던 단계로 돌아갑니다.
- 대화 제안·대화·피드백·단어장 화면이 떠 있으면 안내 카드를 숨기고, 그동안 할 일은 코치 마크 설명으로 알립니다. 카드는 이동을 막지 않습니다.
- Ben 대화는 글자로는 정해진 대사와 같게 입력해야 넘어갑니다(대소문자·문장부호·작은 오타는 허용). [꾹 눌러 말하기]로 말하면 무엇을 말했든 정해진 대사로 이어 가고, 다르게 들렸으면 그렇게 했다고 알립니다. 빈 입력칸에는 말하거나 입력할 대사가 회색 안내로 보입니다([Dialogue 모듈](../Dialogue/README.md)).

| 잠금 | 풀리는 단계 |
| --- | --- |
| 문·[홈]으로 다른 장소에 가기(`SpaceTravel.SetLocked`) | 숙소에서는 `LeaveLodging`부터, 밖에서는 `Completed`. 잠긴 문에 닿으면 안내 카드의 할 일 아래 주황 띠로 "아직 다른 곳으로 갈 수 없어요. 할 일부터 해 주세요."를 3초 보이고 카드를 짧게 흔듭니다. 카드가 숨은 동안(대화·피드백 화면)에는 HUD 알림으로 알립니다. |
| 택시 세우기(`TaxiRide.IsHailAllowed`) | `Completed` |

| NPC | 다가와도 되는 단계 |
| --- | --- |
| 길을 묻는 행인 Ben(`TOWN_PASSERBY`) | 튜토리얼 중에는 `MeetBen`, `BenDialogue`. 튜토리얼을 마치면(`Completed`) 항상 |
| 그 밖의 NPC | `Completed`. 튜토리얼 동안 다른 NPC가 다가오면 누구와 말해야 하는지 헷갈리므로 막습니다. |

첫날 미션(지도 버튼·목표 카드·수첩)은 `TodayGoal`부터 보입니다(건너뛴 뒤에도 보임).

## 안내 카드

화면 위쪽 가운데에 짧은 카드 하나만 둡니다. 걷기·둘러보기의 할 일은 " · "로 나눠 적고, 해낸 것은 초록색으로 바뀌어 무엇이 남았는지 보입니다. 할 일 순서는 `TutorialProgress.RequiredActions`와 같아야 합니다(`TutorialGuideAssetTests`가 확인). 윗줄에 진행 점(●○)과 단계 이름·[건너뛰기], 가운데에 할 일 한 줄을 보이고, 주 버튼은 도착 인사의 [시작하기]에만 있습니다. 문구의 `{키}`는 강조색 `[키]`로 보입니다(테마 제목 글꼴에서는 TMP 강조 배경이 그려지지 않아 괄호와 글자색만 씁니다). 터치 조작판이 보이는 기기(휴대폰·터치 화면 노트북)에서는 단계별 터치 문구를 보입니다. 기호는 서비스 글꼴에 있는 ●·○·→·≡만 씁니다.

## 코치 마크

눌러야 할 버튼은 화면을 반투명 검은 판으로 덮고 그 버튼만 노란 테두리로 밝혀, 구멍 안만 누를 수 있게 합니다. 판은 밝힐 버튼이 있는 캔버스 바로 위에 그리므로, 그보다 위에 있는 안내 카드와 [튜토리얼 건너뛰기]는 가리지 않습니다. 걸어가야 할 숙소 문과 Ben은 화면을 덮지 않고 머리 위에서 오르내리는 노란 화살표로 가리키고, 화면 밖이면 가장자리에서 그쪽을 가리킵니다(카메라 뒤면 아래 가장자리). 안내 카드가 숨는 대화 제안·대화·피드백 화면에서는 할 일을 화면 위쪽 가운데 설명으로 알리고, 길 안내 대화에서는 지금 입력할 대사를 함께 크게 보입니다. 무엇을 밝힐지는 `TutorialCoachPlan`이 단계와 화면 상태로 고릅니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `TutorialProgress`, `TutorialStep` | 신호를 받아 단계를 넘기고, 행인이 다가와도 되는지·장소 이동 가능 여부·미션 표시 여부를 알려 줍니다. Unity 객체 없이 테스트합니다. |
| `TutorialState` | 공간 씬이 바뀌어도 진행이 이어지도록 씬 밖에 둡니다. Play를 시작할 때마다 비우고 장소 이동 잠금도 풉니다. 서버에서 이미 튜토리얼을 끝낸 사용자는 입구 화면이 `MarkFinishedOnServer()`로 알려 `Completed`로 시작합니다. |
| `TutorialFinishRecorder`, `ITutorialApi`, `TutorialApi` | 튜토리얼을 끝낸 방법에 맞춰 완료(TUT-01 `POST /api/v1/me/tutorial/complete`)나 건너뛰기(TUT-02 `/skip`)를 한 번 저장합니다. 네트워크 실패면 기록을 남겨 다음 장소에 들어갈 때 다시 보내고, 409(튜토리얼 이전 단계)면 다시 보내지 않습니다. Unity 객체 없이 테스트합니다. |
| `TutorialCoachPlan`, `TutorialCoachTarget`, `CoachMarkLayout` | 코치 마크가 밝힐 대상을 고르고, 구멍·둘레 판과 화살표 자리·방향을 계산합니다. Unity 객체 없이 테스트합니다. |
| `TutorialAction` | 걷기·둘러보기에서 해야 할 조작(걷기·달리기·시점 돌리기·확대·시점 바꾸기) |
| `TutorialGuideText`, `TutorialAutoAdvance` | 안내 문구의 `{키}`를 `[키]` 표시로 바꾸고 해낸 할 일을 초록색으로 바꾸며 진행 점을 그리고, 해내면 끝나는 단계를 완료 표시 뒤 저절로 넘깁니다. Unity 객체 없이 테스트합니다. |
| `TutorialGuideDefinition`, `TutorialStepGuide` | 단계별 짧은 이름과 할 일 한 줄(키보드·마우스, 터치 기기), 안내 카드가 숨는 동안의 코치 마크 설명([대화하기]·입력할 대사·다른 문장·[대화 종료]), 잠긴 문 안내, 숙소 씬 이름·길을 묻는 행인(Ben)·그동안 쓰는 대본 키(`TUTORIAL_PASSERBY`). 행인 코드와 대본 키는 `TutorialBuilder`가 코드 기본값으로 맞춥니다. 데이터 에셋 `Assets/Data/Tutorial/TutorialGuide.asset` |
| `TutorialDirector` | 공간 씬마다 하나. 걷기·시점·단어 저장·단어장·공간 이동·대화·피드백·지도 신호를 진행에 전하고, 안내 카드·코치 마크·행인 허용·장소 이동과 택시 잠금·미션 표시를 바꿉니다. `Completed`가 되면(또는 저장하지 못한 채 다음 장소에 들어오면) 끝낸 기록을 서버에 저장합니다. `apiSettings`가 비어 있으면 저장하지 않습니다. |
| `TutorialGuideView` | 화면 위쪽 가운데의 안내 카드(정렬 순서 115). 진행 점·단계 이름·할 일 한 줄과 건너뛰기 확인, 할 일 아래 잠깐 보이는 주의 띠 |
| `CoachMarkView` | 구멍 둘레를 덮는 판·테두리·설명과 장면 속 대상 위 화살표 |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Api` | `ApiClient`, `ApiSettings`, `ApiException` |
| `A305.Player` | `PlayerMover.IsMoving`, `PlayerCameraRig.TurnedByUserThisFrame` |
| `A305.ObjectHover` | `WordTooltipView.IsPinned`·`CanSaveWord`·`SaveRequested`·`SaveButtonRect` |
| `A305.Dialogue` | `DialogueController.Started`·`ConversationEnded`·`IsTalking`·`IsGuidedCompleted`, `DialoguePanelView.GuidedLineParts`·`EndButtonRect` |
| `A305.Npc` | `Passerby.IsApproachAllowed`·`SetTutorialScript`, `NpcIdentity.Code`, `PasserbyOfferView.IsShownBy`·`ShownButtonRect`, `TaxiRide.IsHailAllowed` |
| `A305.Learning` | `LearningController.FeedbackClosed`·`IsShowingScreen`, `LearningHudView.VocabularyRequested`·`ShowMessage`, `HudMenuView.IsOpen`, `FeedbackView.IsVisible` |
| `A305.Mission` | `MissionController.SetStoryAvailable`·`SetPanelVisible`, `FirstDayMissionView.JournalOpened` |
| `A305.World` | `SpaceTravel.SetLocked`·`Refused`, `SpaceDoorway` |
| uGUI, TextMeshPro | 안내 카드, 코치 마크 |

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `TutorialState.Clear()` | 튜토리얼을 처음부터 다시 시작하고 장소 이동 잠금을 풉니다. PlayMode 테스트에서 상태를 비울 때 씁니다. |
| `TutorialState.MarkFinishedOnServer()` | 서버 단계가 튜토리얼 이후인 사용자로 표시합니다. 입구 화면(`OnboardingScreen`)이 장소로 보낼 때 부릅니다. |

## Builder

**Tools → A305 → Tutorial → Build**(`Scripts/Editor/Tutorial/TutorialBuilder.cs`)가 `Prefabs/Tutorial/TutorialGuide.prefab`·`CoachMark.prefab`과 안내 문구 에셋을 준비합니다. 안내 문구는 없을 때만 만들고, 이후 수정은 에셋에서 합니다. 단계를 바꿔 문구를 코드 기본값으로 다시 맞춰야 할 때만 **Tools → A305 → Tutorial → Reset Guide Texts**를 씁니다. 공간 씬에 붙이고 씬에 놓인 행인 목록을 채우는 일은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 합니다. 행인을 새로 놓았다면 연결 도구를 다시 실행합니다. 배포용 장소 씬 5개에 코치 마크와 단계 신호 화면만 다시 연결할 때는 **Tools → A305 → Tutorial → Connect Coach Marks**, `ApiSettings`만 다시 연결할 때는 **Tools → A305 → Tutorial → Connect Tutorial Saving**을 씁니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Tutorial.Tests` | `Assets/Tests/EditMode/Tutorial/` | 단계 순서, 걷기·시점·단어 저장·단어장은 그 단계에서 한 것만 인정, 숙소 밖 진입, 단계별 장소 이동 잠금, 행인 허용 단계, 길 안내 완료·중단, 피드백 닫기, 지도로 완료, 건너뛰기, 끝낸 방법 기록과 서버 저장(완료·건너뛰기·재시도·409 거절·API 경로), 서버에서 이미 끝난 사용자, 코치 마크 대상 고르기와 구멍·판 계산, 화면 밖 대상의 가장자리 화살표, 안내 문구의 [키] 표시·진행 점·보이는 글자 수, 해낸 단계의 자동 넘어가기, 단계 안내가 기기별 짧은 한 줄인지 |
| `A305.Tutorial.PlayModeTests` | `Assets/Tests/PlayMode/Tutorial/` | 배포용 숙소·마을 씬에서 숙소 문 잠금과 문 위 화살표, Ben 머리 위 화살표와 [대화하기]만 밝히기, 정해진 대사와 다른 문장은 되돌리고 안내한 뒤 길 안내를 마치면 [대화 종료] 밝히기, [건너뛰기]는 한 번 더 묻고 확인해야 끝나기, 잠긴 문 알림이 안내 카드에 잠깐 보였다 사라지기 |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | 공간 기능 연결이 안내 카드와 튜토리얼 진행 처리를 한 번씩 붙이고, 흐름 기능과 씬에 놓인 행인을 연결하는지, 배포 장소 씬의 `ApiSettings` 연결 |

`A305.Npc.PlayModeTests`와 `A305.Tutorial.PlayModeTests`는 각 테스트 전에 `TutorialState.Clear()`로 튜토리얼을 처음부터 시작합니다. 실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
