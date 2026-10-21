# Dialogue 모듈

NPC와의 대화를 시작하고 끝냅니다. 대화가 시작되면 NPC를 바라보는 1인칭 시점으로 바꾸고 이동을 잠그며, 끝나면 직전 시점과 이동을 되돌립니다. 대화는 한 번에 하나만 진행합니다.

지금은 [대화 종료]만 있는 임시 대화 화면을 보입니다. 대화 내용·음성 입력·추천 표현 같은 대화 UI와 서버·AI 연동은 대화 작업(S15P31A305-168, 169, 170, 193)에서 이 모듈에 붙입니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `DialogueSession` | 대화 중인 NPC 키(ERD `npcs.code`)를 기억하고 대화가 겹쳐 시작되지 않게 막습니다. Unity 객체 없이 테스트합니다. |
| `DialogueController` | 대화 시작·종료 때 카메라 시점, 이동 잠금, 대화 화면을 바꿉니다. |
| `DialoguePanelView` | 대화 중에 보이는 임시 화면. [대화 종료] 버튼이 있습니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Player` | `PlayerCameraRig.EnterDialogueView`·`ExitDialogueView`, `PlayerMover.SetMovementLocked` |
| uGUI, TextMeshPro | 임시 대화 화면 |

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `DialogueController.TryStart(string npcCode, Transform lookTarget)` | 대화를 시작합니다. 이미 대화 중이면 false를 돌려줍니다. |
| `DialogueController.End()` | 대화를 끝내고 시점과 이동을 되돌립니다. [대화 종료] 버튼도 이 동작을 합니다. |
| `DialogueController.IsTalking` | 대화 중인지 |
| `DialogueController.Ended` | 대화가 끝나면 끝난 NPC 키를 알립니다. |

공간 씬의 대화 처리와 임시 화면은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 붙이므로 씬마다 직접 배치하지 않습니다. 임시 화면 프리팹은 **Tools → A305 → Dialogue → Build**(`Scripts/Editor/Dialogue/DialogueBuilder.cs`)로 준비합니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Dialogue.Tests` | `Assets/Tests/EditMode/Dialogue/` | 대화 시작, 대화 중 두 번째 시작 거절, 종료 뒤 다시 시작 |

대화 시점·이동 잠금·[대화 종료]는 Npc 모듈의 PlayMode 테스트에서 함께 확인합니다. 실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
