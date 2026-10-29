# Mission 모듈

튜토리얼 뒤에는 **첫날의 작은 소동** 연계 미션을 진행합니다. 왼쪽 위에 지도 버튼과 상황·목표만 두고, 지도 버튼을 누르면 마을 지도와 전체 7단계·목표·소지품을 담은 수첩을 펼칩니다. 지도의 길과 건물 칸은 `TownMapBuilder`가 TownScene 배치에서 그리고, `TownMapView`는 내 위치와 목적지 표시만 바꿉니다. 서버가 `/v1/judge`의 의미 판정 제안을 현재 NPC·단계에 맞춰 적용하고 사용자별 진행을 저장합니다. 새로고침과 씬 이동 후에도 서버 상태를 읽어 복원합니다.

`FirstDayMissionApi`는 공용 API 클라이언트를 사용하고, `FirstDayMissionController`는 씬 진입·NPC 응답·대화 종료 후 상태를 읽습니다. `FirstDayMissionView`는 화면과 수첩만 담당합니다. 마을에서는 상태를 읽을 때마다 `FirstDayMissionState.Forecast`를 [Weather 모듈](../Weather/README.md)의 `WeatherPresenter`에 넘겨 하늘·조명과 비를 바꿉니다. `Forecast`는 서버 `weather`(`CLEAR`/`CLOUDY`/`RAIN`)를 따르고, 값이 없거나 모르는 값이면 `raining`으로 비와 맑음만 나눕니다. 계약·7단계 내용·배포·QA 기준은 [연계 미션 API](../../../../../docs/api/first-day-mission.md)에 있습니다.

**Tools → A305 → Mission → Build First Day Journey**가 다섯 공간의 기능 연결, 수첩 프리팹, 상점과 출입구를 준비합니다. 기존 씬 디자인은 유지합니다. `SpaceSceneSetup`도 같은 기능을 연결하므로 공간을 다시 구성해도 연결이 남습니다.

## 튜토리얼용 기본 미션

기존 `MissionBoard`·`MissionPanelView`는 튜토리얼의 미션 고르기·카페 대화 안내와 서버 없는 데모에 유지합니다. 이 경로는 키워드를 사용하며 서버 연계 여정의 완료 판정에 사용하지 않습니다. 튜토리얼이 끝나면 `MissionController.SetStoryAvailable`로 기본 창을 숨기고 새 수첩을 표시합니다. 아래는 이 기본 미션 경로의 구성입니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `MissionBoard` | 미션 시작(진행 중인 다른 미션은 교체), 완료 판정, 완료한 미션 재시작 거절. Unity 객체 없이 테스트합니다. |
| `MissionContent` | 미션 하나의 내용. 미션 키, 제목, 설명, 장소, 대화 상대 NPC 키, 키워드 |
| `MissionSource`, `MockMissionSource` | 오늘의 미션을 찾는 곳과 서버 연동 전 확인용 미션입니다. |
| `MissionProgress` | 공간 씬이 바뀌어도 미션 진행이 남도록 씬 밖에 둡니다. Play를 시작할 때마다 비웁니다. |
| `MissionController` | 미션 시작과 대화가 끝난 뒤의 완료 판정을 미션 창과 연결합니다. |
| `MissionPanelView`, `MissionRowView` | 미션 창과 미션 한 줄. 미션 수만큼 줄 템플릿을 복제합니다. |
| `MissionText` | 미션 창 문구. 미션 시작·교체 알림과, 완료 전 미션 줄에 보이는 진행 상태와 말할 단어(키워드). |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Dialogue` | `DialogueController.ConversationEnded`, `DialogueRecord`, `KeywordMatcher` |
| `A305.Weather` | 첫날 미션의 예보 날씨를 마을에 보여 주는 `WeatherPresenter`, `WeatherKind` |
| uGUI, TextMeshPro | 미션 창 |

## 동작 규칙

| 상황 | 동작 |
| --- | --- |
| [미션 시작] | 그 미션을 진행 중으로 바꿉니다. 진행 중인 다른 미션이 있으면 바꾸고, 미션 창 안내에 "'A' 대신 'B' 미션을 시작했어요"로 멈춘 미션을 함께 알립니다. |
| 완료 전 미션 줄 | 진행 상태와 함께 대화에 넣어 말할 키워드를 "말할 단어: soy, latte"처럼 보여 줍니다. 키워드가 없는 미션은 진행 상태만 보입니다. |
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
| `A305.Mission.Tests` | `Assets/Tests/EditMode/Mission/` | 미션 시작, 다른 미션으로 교체, 없는 미션 거절, 여러 문장에 나뉜 키워드로 완료, 다른 NPC·빠진 키워드·시작 전에는 미완료, 완료한 미션 재시작 거절, 시작·교체 알림 문구, 미션 줄의 말할 단어 표시 |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | 공간 기능 연결이 미션 창과 미션 처리를 한 번씩 붙이고 대화 처리와 연결하는지 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
