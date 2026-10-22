# Npc 모듈

플레이어에게 먼저 다가와 대화를 제안하는 행인을 담당합니다. 튜토리얼에서는 집 밖으로 나오면 행인이 다가와 카페 위치를 묻고, 일반 행인은 마을을 걷다 가까이 가면 말을 겁니다. 수락하면 [Dialogue 모듈](../Dialogue/README.md)로 대화를 시작합니다.

이 서비스의 진행 흐름을 다루는 게임 전용 기능입니다. 플레이어 위치·이동 잠금은 `A305.Player`, 대화 시작·종료는 `A305.Dialogue`의 공개 API로 요청합니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `PasserbyApproach` | 기다림 → 다가감 → 제안 → 대화 → 쉼으로 상태를 바꿉니다. Unity 객체 없이 테스트합니다. |
| `PasserbyRules`, `PasserbySettings` | 감지·제안·무시 거리, 쉬는 시간, 거절 가능 여부, 걷는 속도. 설정은 데이터 에셋입니다. |
| `Passerby` | 행인을 걷게 하고 제안 화면·대화와 연결합니다. 모델은 `Visual` 아래, 이동은 루트의 `CharacterController`가 맡습니다. |
| `NpcIdentity` | NPC 키(ERD `npcs.code`)와 대화할 때 바라볼 머리 위치(`LookTarget`). |
| `PasserbyOfferView` | 행인이 건넨 말과 [대화하기]·[지금은 안 할래요] 버튼. 한 번에 한 행인만 제안합니다. |
| `PasserbyRestRecord` | 행인별로 다시 다가올 수 있는 시각을 공간이 바뀌어도 기억합니다. |
| `NpcLineSource`, `MockNpcLineSource` | 행인이 먼저 건네는 말을 찾는 곳과 서버 연동 전 확인용 인사말입니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Player` | 플레이어 위치, `IsMovementLocked` |
| `A305.Dialogue` | `DialogueController.TryStart`, `IsTalking`, `Ended` |
| `A305.Characters` | 모델 Animator에 이동 상태 전달. 연결하지 않아도 동작합니다. |
| uGUI, TextMeshPro | 제안 화면과 한국어 버튼 |

## 동작 규칙

| 상황 | 동작 |
| --- | --- |
| 플레이어가 감지 거리 안에 들어옴 | 플레이어에게 걸어가고, 제안 거리(2m)에서 멈춰 대화를 제안합니다. 제안 중에는 카메라·이동을 잠그지 않습니다. |
| [대화하기] | 행인을 바라보는 1인칭 대화 시점과 이동 잠금으로 대화를 시작합니다. |
| [지금은 안 할래요] | 제안을 닫고 제자리로 돌아가 쉬는 시간이 지나면 다시 다가올 수 있습니다. 튜토리얼 행인에게는 이 버튼이 없습니다. |
| 제안 중 무시 거리(6m)보다 멀어짐 | 거절과 같이 제안을 닫고 쉽니다. |
| 다가가는 중 플레이어가 감지 거리 밖으로 감 | 무시한 것으로 보고 쉽니다. |
| 대화가 끝남 | 쉬는 시간이 지나면 다시 다가올 수 있습니다. |
| 다른 대화 중이거나 다른 행인이 제안 중 | 끼어들지 않습니다. |

쉬는 시간은 실제 시간으로 세며, 건물에 들어가 있는 동안에도 흐릅니다. 수치는 `Assets/Data/Npc/`의 설정에서 조정합니다.

| NPC 키 | 위치 | 설정 | 감지 거리 | 쉬는 시간 | 거절 |
| --- | --- | --- | --- | --- | --- |
| `TUTORIAL_PASSERBY` | 마을 집 앞 큰길 | `TutorialPasserbySettings` | 8m | 15초 | 불가 |
| `TOWN_PASSERBY` | 마을 동서 길 서쪽 | `TownPasserbySettings` | 6m | 60초 | 가능 |
| `CAFE_STAFF` | 카페 카운터 뒤 | `CafeStaffSettings` | 3.5m | 20초 | 가능 |

- 카페 직원은 새 컴포넌트 없이 행인 프리팹을 쓰고, 걷는 속도 0으로 제자리에 서 있다가 플레이어가 카운터 앞(제안 거리 2.5m)까지 오면 대화를 제안합니다. 제안 화면의 안내 문구("행인이 말을 걸었어요")는 직원에게 맞지 않으므로 화면 문구 작업에서 NPC 종류에 맞게 바꿉니다.

- NPC 키는 ERD `npcs.code`와 같아야 하므로 서버 시드 데이터와 맞춥니다. 이름·성격·대사는 서버 데이터입니다.
- 튜토리얼 진행 상태와 연결하는 일(튜토리얼 중에만 튜토리얼 행인이 다가오게 하기)은 튜토리얼 작업(S15P31A305-191)에서 합니다.
- 거절한 뒤 다시 대화로 들어가는 경로는 기능 명세가 정해지면 쉬는 시간 기준을 조정합니다.

## 행인 추가

1. `Prefabs/Npc/Passerby.prefab`을 씬의 `Characters` 아래에 놓고 `NpcIdentity`의 `Code`에 서버와 같은 NPC 키를 입력합니다. 서 있는 자리와 방향이 돌아갈 제자리입니다.
2. `Passerby`의 `Settings`에 행인 성격에 맞는 설정 에셋을 연결합니다.
3. 서버 연동 전에는 `Assets/Data/Npc/MockNpcLines.asset`에 같은 키의 인사말을 추가합니다.
4. **Tools → A305 → World → Set Up Open Scene**을 실행하면 플레이어·대화 처리·제안 화면과 연결됩니다.

## Builder

**Tools → A305 → Npc → Build**(`Scripts/Editor/Npc/NpcBuilder.cs`)가 다음을 준비합니다. 공간 Builder(**World → Build Spaces**)도 함께 실행합니다.

- 설정(`Data/Npc/`): 없을 때만 만들고, 있으면 값을 유지합니다.
- 확인용 인사말(`Data/Npc/MockNpcLines.asset`): 없으면 만들고, 있으면 아직 없는 NPC 키의 인사말만 덧붙입니다. 이미 있는 인사말은 바꾸지 않습니다.
- 행인 프리팹 `Prefabs/Npc/Passerby.prefab`과 제안 화면 `Prefabs/Npc/PasserbyOffer.prefab`: 컴포넌트와 연결만 맞추고 모델·디자인을 유지합니다.
- 마을(`TownScene`)에 튜토리얼 행인과 일반 행인을, 카페(`CafeScene`) 카운터 뒤에 카페 직원을 둡니다. 같은 NPC 키가 이미 있으면 위치를 바꾸지 않습니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Npc.Tests` | `Assets/Tests/EditMode/Npc/` | 감지·다가감·제안, 바쁠 때 끼어들지 않기, 거절·무시·대화 뒤 쉬기와 다시 다가오기, 튜토리얼 행인 거절 불가, 공간 이동 뒤 쉬는 시간 이어가기 |
| `A305.Npc.PlayModeTests` | `Assets/Tests/PlayMode/Npc/` | 집에서 나오면 튜토리얼 행인이 다가와 거절 없이 제안하는지, 수락하면 대화 시점·이동 잠금으로 들어가고 [대화 종료]로 돌아오는지, 일반 행인 거절·무시 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
