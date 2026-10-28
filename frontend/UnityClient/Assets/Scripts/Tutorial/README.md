# Tutorial 모듈

처음 들어온 사용자가 방에서 조작을 익히고, 밖에서 길을 묻는 이웃과 정해진 길 안내 대화를 한 뒤, 피드백과 미션 창을 거쳐 카페 직원과 대화하기까지 단계별로 안내합니다. 단계에 맞게 화면 위쪽 안내 카드, 행인이 다가와도 되는지, 미션 창 표시를 바꿉니다.

이 서비스의 진행 흐름을 다루는 게임 전용 기능이며, 다른 기능 모듈의 공개 API만 써서 흐름을 잇습니다. 지금은 흐름 확인용 초안이라 진행 상태는 Play 동안만 남고, 서버 저장(튜토리얼 완료 여부)과 디자인은 이후 작업에서 붙입니다.

## 단계

| 단계 | 넘어가는 조건 | 안내 카드 |
| --- | --- | --- |
| `Welcome` | [함께 시작하기] | 환영 |
| `Move` | 이 단계에서 걸은 뒤 [이해했어요] | WASD 걷기 |
| `Hover` | 이 단계에서 사물 툴팁을 본 뒤 [다음으로] | 사물 호버 |
| `MissionIntro` | 방 밖 공간에 들어감 | 밖으로 나가기 |
| `NeighborApproach` | 튜토리얼 행인과 대화 시작 | 이웃 만나기 |
| `NeighborDialogue` | 길 안내를 끝까지 마치고 대화 종료 → `NeighborFeedback`. 중간에 끝내면 `NeighborApproach`로 되돌아가 이웃이 다시 다가옵니다. | 숨김 |
| `NeighborFeedback` | 길 안내 피드백의 [닫기] | 피드백 보기 |
| `MissionWindow` | [미션 창 확인하기] 또는 미션 시작 | 미션 창 소개 |
| `MissionSelect` | 미션 시작 | 미션 고르기 |
| `MissionTalk` | 미션 NPC(카페 직원)와 대화 시작 | 카페 직원과 대화 |
| `Completed` | — | 숨김 |

- 방 안내 중에 방 밖 공간에 들어가면 남은 방 안내를 건너뛰고 `NeighborApproach`로 넘어갑니다.
- [튜토리얼 건너뛰기]는 어느 단계에서든 `Completed`로 넘깁니다.
- 대화 제안 화면이 떠 있거나 대화 중이면 안내 카드를 숨깁니다. 카드는 이동을 막지 않습니다.

| NPC | 다가와도 되는 단계 |
| --- | --- |
| 튜토리얼 행인(`TUTORIAL_PASSERBY`) | `NeighborApproach`, `NeighborDialogue` |
| 미션 NPC(`CAFE_STAFF`) | `MissionTalk`, `Completed` |
| 그 밖의 행인 | 항상 |

미션 창은 `MissionWindow`부터 보입니다(건너뛴 뒤에도 보임).

## 구성

| 파일 | 역할 |
| --- | --- |
| `TutorialProgress`, `TutorialStep` | 신호를 받아 단계를 넘기고, 행인이 다가와도 되는지와 미션 창 표시 여부를 알려 줍니다. Unity 객체 없이 테스트합니다. |
| `TutorialState` | 공간 씬이 바뀌어도 진행이 이어지도록 씬 밖에 둡니다. Play를 시작할 때마다 비웁니다. 서버에서 이미 튜토리얼을 끝낸 사용자는 입구 화면이 `MarkFinishedOnServer()`로 알려 `Completed`로 시작합니다. |
| `TutorialFinishRecorder`, `ITutorialApi`, `TutorialApi` | 튜토리얼을 끝낸 방법에 맞춰 완료(TUT-01 `POST /api/v1/me/tutorial/complete`)나 건너뛰기(TUT-02 `/skip`)를 한 번 저장합니다. 네트워크 실패면 기록을 남겨 다음 장소에 들어갈 때 다시 보내고, 409(튜토리얼 이전 단계)면 다시 보내지 않습니다. Unity 객체 없이 테스트합니다. |
| `TutorialGuideDefinition`, `TutorialStepGuide` | 단계별 안내 문구와, 방 씬 이름·튜토리얼 행인·미션 NPC 키. 데이터 에셋 `Assets/Data/Tutorial/TutorialGuide.asset` |
| `TutorialDirector` | 공간 씬마다 하나. 걷기·사물 툴팁·공간 이동·대화·피드백·미션 신호를 진행에 전하고, 안내 카드·행인 허용·미션 창을 바꿉니다. `Completed`가 되면(또는 저장하지 못한 채 다음 장소에 들어오면) 끝낸 기록을 서버에 저장합니다. `apiSettings`가 비어 있으면 저장하지 않습니다. |
| `TutorialGuideView` | 화면 위쪽 가운데의 안내 카드(정렬 순서 115) |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Api` | `ApiClient`, `ApiSettings`, `ApiException` |
| `A305.Player` | `PlayerMover.IsMoving` |
| `A305.ObjectHover` | `WordTooltipView.IsVisible` |
| `A305.Dialogue` | `DialogueController.Started`·`ConversationEnded`·`IsTalking` |
| `A305.Npc` | `Passerby.IsApproachAllowed`, `NpcIdentity.Code`, `PasserbyOfferView.IsShowing` |
| `A305.Learning` | `LearningController.FeedbackClosed` |
| `A305.Mission` | `MissionController.MissionStarted`·`SetPanelVisible` |
| uGUI, TextMeshPro | 안내 카드 |

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `TutorialState.Clear()` | 튜토리얼을 처음부터 다시 시작합니다. PlayMode 테스트에서 상태를 비울 때 씁니다. |
| `TutorialState.MarkFinishedOnServer()` | 서버 단계가 튜토리얼 이후인 사용자로 표시합니다. 입구 화면(`OnboardingScreen`)이 장소로 보낼 때 부릅니다. |

## Builder

**Tools → A305 → Tutorial → Build**(`Scripts/Editor/Tutorial/TutorialBuilder.cs`)가 `Prefabs/Tutorial/TutorialGuide.prefab`과 안내 문구 에셋을 준비합니다. 안내 문구는 없을 때만 만들고, 이후 수정은 에셋에서 합니다. 공간 씬에 붙이고 씬에 놓인 행인 목록을 채우는 일은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 합니다. 행인을 새로 놓았다면 연결 도구를 다시 실행합니다. 배포용 장소 씬 5개의 `TutorialDirector`에 `ApiSettings`만 다시 연결할 때는 **Tools → A305 → Tutorial → Connect Tutorial Saving**을 씁니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Tutorial.Tests` | `Assets/Tests/EditMode/Tutorial/` | 버튼으로 넘기는 단계, 걷기·호버는 그 단계에서 한 것만 인정, 방 밖 진입, 행인·미션 NPC 허용 단계, 길 안내 완료·중단, 피드백 닫기, 미션 시작, 미션 NPC 대화로 완료, 건너뛰기, 끝낸 방법 기록과 서버 저장(완료·건너뛰기·재시도·409 거절·API 경로), 서버에서 이미 끝난 사용자 |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | 공간 기능 연결이 안내 카드와 튜토리얼 진행 처리를 한 번씩 붙이고, 흐름 기능과 씬에 놓인 행인을 연결하는지, 배포 장소 씬의 `ApiSettings` 연결 |

`A305.Npc.PlayModeTests`는 각 테스트 전에 `TutorialState.Clear()`로 튜토리얼을 처음부터 시작합니다. 실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
