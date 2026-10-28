# Dialogue 모듈

NPC와의 대화를 시작하고 끝냅니다. 대화가 시작되면 NPC를 바라보는 1인칭 시점으로 바꾸고 이동을 잠그며, 끝나면 직전 시점과 이동을 되돌립니다. 대화는 한 번에 하나만 진행합니다.

튜토리얼 길 안내처럼 정해진 흐름이 있는 NPC는 확인용 대본(`MockDialogueScripts`)으로, 그 밖의 NPC는 서버 AI와 자유 대화로 진행합니다. 대화 화면 프리팹의 `DialogueServerLink`에서 서버를 끄면 모든 대화를 확인용 대본으로 진행하며, 이때 자유 대화는 키워드로 답하는 대본이 흉내 냅니다. 지금 화면은 흐름 확인용 초안입니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `DialogueSession` | 대화 중인 NPC 키(ERD `npcs.code`)를 기억하고 대화가 겹쳐 시작되지 않게 막습니다. Unity 객체 없이 테스트합니다. |
| `DialogueConversation` | 대화 한 번의 진행. 사용자가 보낸 문장 뒤에 대본에 따라 NPC 대사를 붙이고, 정해진 흐름의 읽을 대사·완료 여부를 알려 줍니다. Unity 객체 없이 테스트합니다. |
| `DialogueScript`, `GuidedTurn`, `KeywordReply`, `DialogueLine` | 대본과 대사. 정해진 턴이 있으면 정해진 흐름, 없으면 키워드 응답 규칙과 기본 응답으로 답합니다. |
| `DialogueRecord` | 끝난 대화의 기록. 피드백·미션·튜토리얼이 대화 결과를 판단할 때 씁니다. |
| `DialogueScriptSource`, `MockDialogueScriptSource` | NPC 키로 대본을 찾는 곳과 서버 연동 전 확인용 대본입니다. |
| `ServerConversation`, `IConversationApi`, `ConversationApi` | 서버 AI 자유 대화 한 번. 대화 시작(CONV-01)·발화(CONV-03)·녹음 전사(CONV-02)·뜻 보기(CONV-04)·힌트(CONV-05)·종료(CONV-08)를 부릅니다. 가짜 API로 Unity 객체 없이 테스트합니다. |
| `DialogueController` | 대화 시작·종료 때 카메라 시점, 이동 잠금, 대화 화면을 바꾸고 대화 진행을 화면과 연결합니다. |
| `DialoguePanelView` | 대화 중 화면. 최근 대사, 이번에 읽을 대사, 추천 단어·추천 문장, 텍스트 입력과 [보내기]·[예시 문장]·[뜻 보기]·[표현 저장]·[대화 종료], 서버 대화의 [누르고 말하기]·[다시 듣기]·[힌트]가 있습니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Player` | `PlayerCameraRig.EnterDialogueView`·`ExitDialogueView`, `PlayerMover.SetMovementLocked` |
| uGUI, TextMeshPro | 대화 화면과 텍스트 입력 |

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `DialogueController.TryStart(string npcCode, Transform lookTarget)` | 대화를 시작합니다. 이미 대화 중이면 false를 돌려줍니다. |
| `DialogueController.End()` | 대화를 끝내고 시점과 이동을 되돌립니다. [대화 종료] 버튼도 이 동작을 합니다. |
| `DialogueController.IsTalking` | 대화 중인지 |
| `DialogueController.Started` | 대화가 시작되면 NPC 키를 알립니다. |
| `DialogueController.Ended` | 대화가 끝나면 끝난 NPC 키를 알립니다. |
| `DialogueController.NpcResponded` | 서버가 NPC 턴을 확정하면 알립니다. 첫날 연계 미션 UI가 서버 진행을 다시 읽을 때 사용합니다. |
| `DialogueController.ConversationEnded` | 대화가 끝나면 `DialogueRecord`를 알립니다. 시점과 이동을 되돌린 뒤 가장 마지막에 알리므로, 받는 쪽이 화면을 열며 이동을 잠가도 풀리지 않습니다. |
| `DialogueController.ExpressionSaveRequested` | [표현 저장]을 누르면 가장 최근 NPC 대사의 영어 문장과 한국어 뜻을 알립니다. 서버 대화는 [뜻 보기]로 뜻을 받기 전이면 뜻이 빈 문자열입니다. 단어장 저장은 받는 쪽(Learning 모듈)이 합니다. |

## 확인용 대본

`Assets/Data/Dialogue/MockDialogueScripts.asset`에 NPC 키별 대본이 있습니다. 목록에 없는 NPC는 `Fallback` 대본으로 대화합니다.

| NPC 키 | 이름 | 대화 |
| --- | --- | --- |
| `TUTORIAL_PASSERBY` | Alex | 카페 가는 길을 묻는 정해진 3턴. 화면에 읽을 대사를 보여 주고, 무엇을 보내도 다음 턴으로 넘어갑니다. 마지막 턴 뒤에는 입력을 받지 않습니다. |
| `CAFE_STAFF` | Emma | 키워드 응답. `soy`+`latte`, `latte`, `dessert`, `coffee`, `thank` 순서로 확인합니다. |
| `TOWN_PASSERBY` | Sam | 키워드 응답. `weather`, `cafe`, `thank` |

- 키워드는 대소문자를 구분하지 않고 단어 일부여도 맞습니다(`desserts`도 `dessert`에 맞음). 규칙은 위에서부터 확인해 처음 맞는 규칙으로 답합니다.
- 필드 초기값은 에셋을 처음 만들 때 들어가는 예시이며, 이후 수정은 에셋에서 합니다. 이름·대사는 서버 데이터라 서버 연동 후에는 서버 값을 씁니다.
- Web 빌드의 Unity 입력창은 한국어 입력이 되지 않습니다(S15P31A305-200 PoC 결론). 입력 UI를 HTML로 옮기는 작업 전까지 영어로 입력합니다.

공간 씬의 대화 처리와 화면은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 붙이므로 씬마다 직접 배치하지 않습니다. 화면 프리팹과 확인용 대본은 **Tools → A305 → Dialogue → Build**(`Scripts/Editor/Dialogue/DialogueBuilder.cs`)로 준비합니다. 화면은 없는 요소만 만들고, 이미 있는 요소의 디자인은 유지합니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Dialogue.Tests` | `Assets/Tests/EditMode/Dialogue/` | 대화 시작, 대화 중 두 번째 시작 거절, 종료 뒤 다시 시작. 첫 대사, 키워드 응답 순서, 기본 응답, 빈 입력 무시, 정해진 흐름의 턴 진행·완료, 예시 문장, 최근 NPC 대사의 뜻, 대화 기록. 서버 대화의 뜻 보기·힌트(한 번만 요청, 실패 안내, 새 NPC 대사에서 비움, 대화가 끝난 뒤 힌트 요청 안 함) |

대화 시점·이동 잠금·[대화 종료]는 Npc 모듈의 PlayMode 테스트에서 함께 확인합니다. 실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
