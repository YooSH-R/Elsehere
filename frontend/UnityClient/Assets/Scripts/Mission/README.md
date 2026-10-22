# Mission 모듈

화면 왼쪽 위 미션 창에서 오늘의 미션을 고르고, NPC와의 대화가 끝나면 진행 중인 미션을 완료했는지 판정합니다. 미션은 한 번에 하나씩 진행하며, 대화에서 내가 말한 문장들에 미션 키워드가 모두 들어 있으면 완료합니다.

이 서비스의 진행 흐름을 다루는 게임 전용 기능입니다. 지금은 흐름 확인용 초안이라 미션 내용은 확인용 데이터이고, 완료 판정은 키워드로 하는 데모입니다. 자정 갱신·보상·서버 저장은 이후 작업에서 붙입니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `MissionBoard` | 미션 시작(진행 중인 다른 미션은 교체), 완료 판정, 완료한 미션 재시작 거절. Unity 객체 없이 테스트합니다. |
| `MissionContent` | 미션 하나의 내용. 미션 키, 제목, 설명, 장소, 대화 상대 NPC 키, 키워드 |
| `MissionSource`, `MockMissionSource` | 오늘의 미션을 찾는 곳과 서버 연동 전 확인용 미션입니다. |
| `MissionProgress` | 공간 씬이 바뀌어도 미션 진행이 남도록 씬 밖에 둡니다. Play를 시작할 때마다 비웁니다. |
| `MissionController` | 미션 시작과 대화가 끝난 뒤의 완료 판정을 미션 창과 연결합니다. |
| `MissionPanelView`, `MissionRowView` | 미션 창과 미션 한 줄. 미션 수만큼 줄 템플릿을 복제합니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Dialogue` | `DialogueController.ConversationEnded`, `DialogueRecord`, `KeywordMatcher` |
| uGUI, TextMeshPro | 미션 창 |

## 동작 규칙

| 상황 | 동작 |
| --- | --- |
| [미션 시작] | 그 미션을 진행 중으로 바꿉니다. 진행 중인 다른 미션이 있으면 바꿉니다. |
| 대화가 끝남 | 진행 중인 미션의 대화 상대와 같고, 내가 말한 문장들에 키워드가 모두 있으면 완료합니다. 키워드는 여러 문장에 나뉘어 있어도 됩니다. |
| 미션을 시작하지 않고 키워드를 말함 | 완료하지 않습니다. |
| 완료한 미션 | [미션 시작]이 사라지고 다시 시작할 수 없습니다. |

미션 창을 언제 보일지는 이 모듈이 정하지 않고, 튜토리얼 같은 진행 흐름이 `MissionController.SetPanelVisible`로 정합니다. 화면 정렬 순서는 105입니다.

## 확인용 미션

`Assets/Data/Mission/MockMissions.asset`

| 미션 키 | 미션 | 대화 상대 | 키워드 |
| --- | --- | --- | --- |
| `CAFE_SOY_LATTE` | 나만의 카페라테 주문하기 | `CAFE_STAFF` | `soy`, `latte` |
| `CAFE_DESSERT` | 달콤한 디저트 찾기 | `CAFE_STAFF` | `dessert` |

필드 초기값은 에셋을 처음 만들 때 들어가는 예시이며, 이후 수정은 에셋에서 합니다. 미션 내용은 서버 데이터라 서버 연동 후에는 서버 값을 씁니다.

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `MissionController.MissionStarted` | 미션을 시작하면 미션 키를 알립니다. |
| `MissionController.SetPanelVisible(bool)` | 미션 창을 보이거나 숨깁니다. |
| `MissionProgress.Clear()` | 미션 진행을 비웁니다. 테스트에서 상태를 비울 때 씁니다. |

## Builder

**Tools → A305 → Mission → Build**(`Scripts/Editor/Mission/MissionBuilder.cs`)가 `Prefabs/Mission/MissionPanel.prefab`과 확인용 미션을 준비합니다. 화면은 없는 요소만 만들고 이미 있는 요소의 디자인은 유지합니다. 공간 씬에 붙이는 일은 [World 모듈의 공간 기능 연결](../World/README.md#새-공간-씬-만들기)이 합니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Mission.Tests` | `Assets/Tests/EditMode/Mission/` | 미션 시작, 다른 미션으로 교체, 없는 미션 거절, 여러 문장에 나뉜 키워드로 완료, 다른 NPC·빠진 키워드·시작 전에는 미완료, 완료한 미션 재시작 거절 |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | 공간 기능 연결이 미션 창과 미션 처리를 한 번씩 붙이고 대화 처리와 연결하는지 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
