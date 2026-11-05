# Npc 모듈

플레이어에게 먼저 다가와 대화를 제안하는 행인과, 다가오지 않는 동안 NPC가 게임 시각의 일과와 욕구에 따라 지내는 [생활](#생활)을 담당합니다. 튜토리얼에서는 집 밖으로 나오면 행인이 다가와 카페 위치를 묻고, 일반 행인은 마을을 걷다 가까이 가면 말을 겁니다. 수락하면 [Dialogue 모듈](../Dialogue/README.md)로 대화를 시작합니다. NPC가 다가오지 않는 동안(대화를 마치고 쉬는 중 등)에는 플레이어가 가까이 가서 [말 걸기]로 먼저 대화를 걸 수 있습니다(기능 명세 F-22). NPC가 많아 [말 걸기]만으로 원하는 NPC를 고르기 어려우므로, 8m 안의 NPC는 몸이나 머리 위 이름표를 클릭·탭해도 말을 걸 수 있고 PC에서는 커서를 올린 NPC에 테두리를 그립니다([클릭·탭으로 말 걸기](#클릭탭으로-말-걸기)).

이 서비스의 진행 흐름을 다루는 게임 전용 기능입니다. 플레이어 위치·이동 잠금은 `A305.Player`, 대화 시작·종료는 `A305.Dialogue`의 공개 API로 요청합니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `PasserbyApproach` | 기다림 → 다가감 → 제안 → 대화 → 쉼으로 상태를 바꾸고, 플레이어가 [말 걸기]나 클릭으로 먼저 말을 걸 수 있는지 정합니다. 준비해 둔 것이 다 되면 다가가지 않고 제자리에서 부르는 제안(`TryCall`)도 맡습니다. Unity 객체 없이 테스트합니다. |
| `PasserbyRules`, `PasserbySettings` | 감지·제안·무시 거리, 쉬는 시간, 거절 가능 여부, 하루에 한 번만 다가오기, 준비해 둔 것을 건네려고 부르는 거리(기본 6m), 걷는 속도. 설정은 데이터 에셋입니다. |
| `Passerby` | 행인을 걷게 하고 제안 화면·대화와 연결합니다. 다가오지 않는 동안 `NpcRoutine`이 있으면 생활을 맡깁니다. 모델은 `Visual` 아래, 이동은 루트의 `CharacterController`가 맡습니다. |
| `NpcIdentity` | NPC 키(ERD `npcs.code`)와 머리 위치(`LookTarget`). 실행 시 해당 NPC의 공통 음성·표정 연결을 준비합니다. |
| `PasserbyOfferView` | 누가 말을 걸었는지, 건넨 말과 [대화하기]·[지금은 안 할래요] 버튼. 한 번에 한 행인만 제안합니다. 같은 자리에 플레이어가 먼저 말을 거는 [말 걸기]와 키 안내(E)를 두며, 둘은 함께 뜨지 않습니다. 보이는 [대화하기] 제안도 E로 수락할 수 있습니다. 제안 창은 다른 창과 같은 테마 바탕을 쓰고, [말 걸기]·[택시 세우기]는 바탕 상자 없이 버튼만 보입니다. 대화를 다 쓴 게스트에게는 같은 자리에 [가입하고 계속 대화하기]를 보이고, 누르거나 E를 누르면 가입 안내 창을 엽니다. |
| `NpcClickTalk` | NPC 몸이나 머리 위 이름표를 클릭·탭하면 말을 겁니다. 8m(`ReachMeters`) 안에서 말을 걸 수 있는 NPC만 받고, PC에서는 커서를 올린 그 NPC에 테두리를 그립니다. 누른 자리의 맨 위 UI가 이름표면 그 이름표의 NPC를(`NpcNameTagView.NpcOf`), 다른 UI면 막고, UI가 없으면 맨 앞에 보이는 NPC 몸을 고릅니다. 제안 화면 프리팹(`PasserbyOffer`)에 붙어 있어 장소 씬을 고치지 않고도 NPC가 있는 모든 장소에서 동작합니다(`NpcBuilder.PrepareAssets`). |
| `NpcPointerRaycast` | 클릭·탭한 방향에서 맨 앞에 보이는 것이 NPC 몸일 때만 그 NPC를 찾습니다. 플레이어 몸과 보이지 않는 감지 영역은 지나치고, 사물 카드가 뜨는 영역(`ObjectHoverHitArea`)이 앞에 있으면 사물을 누른 것으로 봅니다. |
| `RendererOutline` | 대상 아래 모든 몸 그림에 테두리 재질(`Art/Npc/NpcHoverOutline.mat`, 셰이더 `A305/NpcHoverOutline`)을 하나 더 붙였다가 원래 재질 목록으로 되돌립니다. 재질 에셋은 바꾸지 않습니다. 테두리 색·두께는 재질에서 조정합니다. |
| `GuestSignupDialogView` | 대화를 다 쓴 게스트의 가입 안내 창. [가입하고 계속 대화하기]를 누를 때와, 마지막 대화의 피드백을 닫아 가입을 권할 때(`A305.Api`의 `GuestSignupPrompt`) 열립니다. 가입하면 대화·단어·NPC와의 사이가 그대로 이어진다고 알리고, [Google로 가입하기]는 같은 탭에서 Google 로그인(AUTH-01, `A305.Api`의 `BrowserRedirect`)으로 갑니다. 창이 열릴 때 설문을 권해(`A305.Api`의 `SurveyPrompt.Request`) 화면 위 가운데에 설문 카드가 따로 내려옵니다(창 안에는 설문 링크를 두지 않음). 제안 화면 프리팹(`PasserbyOffer`)에 있어 장소 씬마다 배치하지 않습니다(**Tools → A305 → Npc → Apply Guest Signup Dialog**). |
| `PasserbyOfferText` | 제안 화면 문구. 머리말은 대화 대본의 NPC 이름으로 "Emma이(가) 말을 걸었어요", 이름이 없으면 "누군가 말을 걸었어요"이고, 먼저 말 걸기 버튼은 "Emma에게 말 걸기", 이름이 없으면 "말 걸기"입니다. 준비해 둔 것을 건네려고 부를 때는 "Mia이(가) 불러요"와 "기다리던 아이스 라테가 준비됐어요"이고, 조사는 받침에 맞춥니다(`SubjectParticle`). |
| `PasserbyRestRecord` | 행인별로 다시 다가올 수 있는 시각을 공간이 바뀌어도 기억합니다. |
| `PasserbyOfferRecord` | 하루에 한 번만 다가오는 행인이 마지막으로 먼저 말을 건 날짜(기기 날짜)를 기억합니다. 게임을 껐다 켜도 이어지게 이 기기(Web 빌드는 브라우저)의 `PlayerPrefs`에 저장합니다. |
| `PoliceReportDesk` | 경찰서 창구. 신고인지와 장소는 서버 의도 판정이 정해진 단어가 아니라 문맥으로 고르고, 신고를 받은 턴 응답의 `policeDispatch`가 `DialogueController.PoliceDispatchRequested`로 옵니다. 창구는 화면 위쪽 상태 판에 "신고 접수 · 장소"를 보이고, 경찰관이 대화를 마치면(신고를 받은 경찰관이 직접 가려고 그 턴에 대화를 마침) 그 경찰관이 창구를 떠나 순찰차(`PoliceCar`)를 직접 몰고 그 장소로, 장소가 없으면 본길 순찰로 출동합니다. 돌아와 주차하면 창구에 다시 섭니다. 신고가 아닌 말·무례한 말에는 서버가 출동 장소를 보내지 않습니다. |
| `TaxiRide` | 본길을 도는 택시를 인도에서 세우면 손님 옆에 세워 바로 태우고 기사와 대화하게 합니다. 버튼은 [말 걸기]와 같은 자리에 뜹니다. 태우지 못하면 선 택시 곁에 [기사에게 말 걸기]를 보입니다. `IsHailAllowed`가 false면(튜토리얼 동안) [택시 세우기]를 보이지 않습니다. 어떤 버튼을 보일지는 `TaxiPrompt`가 고릅니다. |
| `NpcLifeData`, `NpcLife` | 생활 데이터(`Data/Npc/NpcLife.json`)와 NPC 한 명의 일과 활동·욕구·할 일 고르기. AI 하루 시뮬레이션과 같은 규칙이며 Unity 객체 없이 테스트합니다. |
| `NpcRoutine` | 다가오지 않는 동안 `NpcLife`가 고른 사물까지 걸어가 쓰고, 의자·벤치·소파면 앉습니다. 걷기·애니메이션은 `Passerby`의 것을 씁니다. 구역(`stayInArea`·`area`)을 켜면 그 안의 사물·자리만 고릅니다. |
| `NpcDuty` | 직원이 지금 근무 중인지(교대 밖이면 근무 외)와 같은 카운터를 맡은 직원 중 누가 응대할지 정합니다([일터와 응대](#일터와-응대)). Unity 객체 없이 테스트합니다. |
| `NpcDetailDistance` | 카메라가 12m 안에 있을 때만 NPC의 팔 관통 보정(`LittleMungArmClearance`)을 켭니다. 이 보정은 매 프레임 피부 정점을 계산해 캐릭터 하나에 수 ms가 들어, 마을을 걷는 NPC 모두에게 돌리면 마을이 느려집니다(마을 NPC 16명 기준 한 프레임 약 125ms). 공용 `Passerby.prefab`에 붙어 있어 이 프리팹으로 놓는 NPC가 모두 받습니다. 앉아 있는 동안에는 `StandingPoseCorrections`가 보정을 꺼 두므로 건드리지 않고, 일어선 뒤 거리에 맞춥니다. |
| `NpcNameTags`, `NpcNameTagView` | 플레이어에서 8m 안의 NPC 머리 위 이름표. 근무 중인 직원(생활 데이터 역할 `STAFF`, 일과 `WORK`)은 이름 위 파란 칩에 역할(예: 벨보이·경찰관)을 함께 보이고, 행인과 쉬는 시간·퇴근한 직원은 이름만 보입니다(`NpcRoutine.IsOffDuty`, 첫날 미션이 찾아가라는 직원은 근무 중으로 봄). 이름·역할은 대화 대본(`Data/Dialogue/MockDialogueScripts.asset`)에서 읽고, 이름이 없는 NPC는 띄우지 않습니다. 대화나 창 때문에 플레이어가 움직이지 못하는 동안에는 숨깁니다. NPC마다 3D 글자판을 두지 않고 제안 화면 프리팹(`PasserbyOffer`)의 `NameTags` 층 한 장에서 그립니다. 이 층은 다른 화면 UI와 휴대폰 터치 조작판(-10)보다 아래(-20)에 놓이고, 이름표 바탕 전체가 클릭을 받아 그 NPC에게 말을 겁니다([클릭·탭으로 말 걸기](#클릭탭으로-말-걸기), **Tools → A305 → Npc → Apply Name Tags**). 나란히 선 NPC의 이름표는 서로 겹칠 수 있으며, 카메라에서 가까운 NPC 이름표를 위에 그립니다. 매 프레임 NPC를 훑을 때 장면 전체를 찾지 않도록 `NpcIdentity.Active`(지금 켜진 NPC 목록)를 씁니다. 판단은 EditMode `NpcNameTagsTests`가 Unity 객체 없이, 제안 화면 프리팹의 이름표 층 연결·그리는 순서·클릭 받기는 `NpcNameTagPrefabTests`가, 실제 마을 장면은 PlayMode `NpcNameTagViewTests`가 확인합니다(가까운 경찰관의 역할 칩, 쉬는 시간의 경찰관과 행인은 이름만, 먼 NPC·움직이지 못할 때는 없음). 이름표 클릭과 겹친 이름표는 `NpcClickTalkTests`가 확인합니다. |
| `NpcStayArea` | 구역을 켠 NPC가 고를 수 있는 위치인지 정합니다. Unity 장면 없이 테스트합니다. |
| `NpcWalkArea` | Builder가 장면마다 구운 NPC 바닥(NavMesh)을 장면이 켜져 있는 동안 등록합니다. |
| `NpcAnimation`, `NpcAnimations` | 사물을 쓰는 동안 재생할 동작(일하기·물건 집기·앉기)을 행동 이름으로 고릅니다. 값은 NPC 전용 Animator의 `Action` 번호와 같습니다. |
| `INpcSeat` | NPC도 앉을 수 있는 자리. [Mission 모듈](../Mission/README.md#의자벤치소파-앉기)의 `RestSeat`가 구현합니다. |
| `NpcLineSource`, `MockNpcLineSource` | 행인이 먼저 건네는 말을 찾는 곳과 서버 연동 전 확인용 인사말입니다. |

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.Api` | 게스트 가입 안내 창의 Google 로그인 주소(`ApiSettings`, `AuthPaths`)와 같은 탭에서 열기(`BrowserRedirect`), 피드백을 닫아 가입을 권하는 신호(`GuestSignupPrompt`) |
| `A305.Player` | 플레이어 위치, `IsMovementLocked`, 키 안내를 숨길 휴대폰·태블릿 판단(`PlayerTouchControls.ShouldShowControls`) |
| `A305.Dialogue` | `DialogueController.TryStart`(먼저 말을 걸면 `ConversationInitiator.User`), `IsTalking`, `Ended`, 제안 머리말에 쓸 `FindNpcName`, 건넬 것이 다 됐는지 보는 `FollowUpStore.FindReady` |
| `A305.Characters` | 모델 Animator에 이동 상태 전달. 연결하지 않아도 동작합니다. |
| `A305.World` | 택시의 주행(`LoopingCar`)과 세우기 흐름(`TaxiFare`), 생활에 쓰는 게임 시각(`GameClock.Shared.Minutes`) |
| `A305.ObjectHover` | 생활 행동의 사물을 사물 단어 키(`HoverableObject.UnityKey`)로 찾기. NPC 클릭은 사물 카드와 같은 클릭 판정(`PointerClick`)·UI 위 클릭 막기(`WorldClickBlocker`)를 쓰고, 사물 카드 영역(`ObjectHoverHitArea`)이 앞에 있으면 NPC를 고르지 않습니다. |
| Unity AI(NavMesh) | 생활 중 사물까지 걷는 경로. 바닥은 Builder가 굽습니다. |
| uGUI, TextMeshPro | 제안 화면과 한국어 버튼 |
| Input System | [말 걸기]의 E 키, NPC 클릭·탭의 포인터 |

## 동작 규칙

| 상황 | 동작 |
| --- | --- |
| 플레이어가 감지 거리 안에 들어옴 | 플레이어에게 걸어가고, 제안 거리(2m)에서 멈춰 대화를 제안합니다. 제안 중에는 카메라·이동을 잠그지 않습니다(튜토리얼은 Ben을 기다리는 동안 따로 잠급니다). |
| [대화하기] | 행인 얼굴을 비추는 대화 시점과 이동 잠금으로 대화를 시작합니다. 대화가 끝나면 플레이어는 행인 쪽을 본 채로 다시 움직입니다. |
| [지금은 안 할래요] | 제안을 닫고 제자리로 돌아가 쉬는 시간이 지나면 다시 다가올 수 있습니다. 튜토리얼 행인에게는 이 버튼이 없습니다. |
| 제안 중 무시 거리(6m)보다 멀어짐 | 거절과 같이 제안을 닫고 쉽니다. |
| 다가가는 중 플레이어가 감지 거리 밖으로 감 | 무시한 것으로 보고 쉽니다. |
| 튜토리얼이 플레이어를 붙잡아 이 행인을 기다리게 함(`IsPlayerWaiting`) | 이동이 잠긴 플레이어에게도, 감지 거리 밖에서도 다가가고 멀다고 그만두지 않습니다. |
| 대화가 끝남 | 쉬는 시간이 지나면 다시 다가올 수 있습니다. |
| 하루에 한 번만 다가오는 행인이 그날 이미 말을 걸었음 | 쉬는 시간이 지나도 다음 날(기기 날짜)까지 다가오지 않습니다. 대화를 제안한 순간(수락·거절·무시와 상관없이)을 그날 말을 건 것으로 셉니다. 플레이어가 먼저 거는 [말 걸기]는 횟수 제한 없이 받습니다. |
| 기다리거나 쉬는 NPC의 제안 거리 안으로 감 | 화면 아래에 [Mia에게 말 걸기]와 키 안내(E)를 보입니다. 휴대폰·태블릿에서는 키 안내를 숨깁니다. 버튼이나 E를 누르면 사용자가 먼저 건 대화(CONV-01 `initiatedBy: USER`)로 시작하고, 끝나면 다시 쉽니다. 멀어지면 버튼을 닫습니다. |
| 다른 대화 중이거나 다른 행인이 제안 중 | 끼어들지 않고 [말 걸기]도 보이지 않습니다. 여러 NPC가 가까이 있으면 먼저 가까워진 NPC 하나만 보입니다. |
| 게스트 | 제안 화면과 [말 걸기] 버튼 위에 "게스트 남은 대화 2번"을 보입니다(AI-01 `remainingConversations`). [말 걸기] 위의 횟수는 진한 바탕과 흰 글씨의 배지로 표시하고, E 안내는 말 걸기 버튼 안에서 문구 바로 뒤에 작은 키보드 키 모양으로 표시합니다. 키의 테두리는 실제 E 글자 위치를 따라가며, 아래쪽 두께를 살짝 줍니다. 가입 사용자에게는 횟수 배지를 숨기며, 게스트가 대화를 모두 쓰면 같은 배지에 소진 안내를 표시하고 버튼과 E 키는 가입 안내 창을 엽니다. **Tools → A305 → UI → Update Talk Prompt Hints**로 이 안내 디자인만 갱신합니다. |
| 게스트가 대화 횟수를 다 씀 | 먼저 다가오지 않고(기능 명세 F-39), [말 걸기] 자리에 "게스트 대화를 모두 썼어요"와 [가입하고 계속 대화하기]를 보입니다. 버튼이나 E 키는 대화 대신 가입 안내 창([닫기]·[Google로 가입하기])을 엽니다. 가입하면 게스트 기록이 이어집니다(AUTH-04). |
| 대화에서 준비해 주기로 한 것이 다 됨 | 음료·식사·빨래처럼 서버가 대화 종료에서 알려 준 것([Mission 모듈의 기다리는 카드](../Mission/README.md#기다리는-카드))이 다 되고 플레이어가 그 NPC의 부르는 거리(기본 6m) 안에 있으면, NPC가 자리를 떠나지 않고 같은 제안 화면으로 "Mia이(가) 불러요"·"기다리던 아이스 라테가 준비됐어요"를 보입니다. 직원은 일터를 떠나지 않고 그 자리에서 플레이어 쪽으로 돌아섭니다. 하루 한 번 다가오기·카운터 자리·대화 뒤 쉬는 시간과 상관없이 부르고, 부른 것은 그날 먼저 말을 건 것으로 세지 않습니다. 다른 대화·제안 중이거나 새 대화를 시작할 수 없으면 부르지 않습니다. |
| 부른 제안에 [대화하기] | NPC가 건 대화(`ConversationInitiator.Npc`)로 시작하고, 서버가 첫 대사로 건넵니다(CONV-01 `deliveredFollowUpId`). [말 걸기]로 먼저 말을 걸어도 서버가 같은 방법으로 건넵니다. 어느 쪽이든 기다리는 카드에서 그 줄이 사라집니다. |
| 부른 제안에 [지금은 안 할래요]·부르는 거리 밖으로 감 | 제안을 닫고, 쉬는 시간이 지나면 아직 남아 있을 때 다시 부릅니다. 불러서 대화했는데 건네지 못해 남아 있어도 쉬는 시간이 지난 뒤에 다시 부릅니다. |
| 다른 이유로 새 대화를 시작할 수 없음 | [말 걸기] 자리에 누를 수 없는 버튼으로 "지금은 대화할 수 없어요"를 보입니다. E 키도 대화를 시작하지 않습니다. |

쉬는 시간은 실제 시간으로 세며, 건물에 들어가 있는 동안에도 흐릅니다. 하루 한 번 다가오기는 게임 시각(실제 10분이 게임 하루)이 아니라 기기의 현실 날짜로 셉니다. 기록이 기기에 남으므로 다른 기기·브라우저에서는 따로 셉니다. 수치는 `Assets/Data/Npc/`의 설정에서 조정합니다. 준비해 둔 것을 건네려고 부르는 거리(`followUpCallDistanceMeters`)는 아래 모든 설정에서 기본값 6m이고, 부르는 동안에는 무시 거리가 이보다 짧아도 부르는 거리 안이면 제안을 닫지 않습니다.

| NPC 키 | 위치 | 설정 | 감지 거리 | 쉬는 시간 | 거절 | 먼저 다가오기 |
| --- | --- | --- | --- | --- | --- | --- |
| `TOWN_PASSERBY`(Ben), 튜토리얼 중 | 마을 집 앞 큰길 | `TutorialPasserbySettings` | 8m | 15초 | 불가 | 제한 없음 |
| `TOWN_PASSERBY`(Ben), 튜토리얼 뒤 | 집 앞에서 시작해 마을에서 생활 | `TownPasserbySettings` | 6m | 60초 | 가능 | 하루에 한 번 |
| `TOWN_PASSERBY`(Ben), 퀘스트가 붙잡아 둔 동안(비 오는 거리 단계) | 카페 출입문 앞에서 기다림 | `TownPasserbySettings` | 6m | 60초 | 가능 | 그날 이미 말을 걸었어도 다가옴 |
| 마을 행인 10명(Ruby·Ray·Ethan·Arthur·Margaret·Helen·Rachel·Frank·Andre·Maya) | 본길 북쪽 인도와 공원에서 생활 | `TownPasserbySettings`(Ben과 같음) | 6m | 60초 | 가능 | 하루에 한 번 |
| `CAFE_STAFF`(Mia)·`CAFE_BARISTA`(Zoe)·`CAFE_SERVER`(Leo) | 카페 카운터 뒤와 양 끝 | `CafeStaffSettings` | 3.5m | 20초 | 가능 | 제한 없음 |
| `HOTEL_STAFF`(Jake)·`HOTEL_RECEPTIONIST`(Owen)·`HOTEL_NIGHT_MANAGER`(Clara)·`HOTEL_CONCIERGE`(Walter)·`HOTEL_BELLHOP`(Diego) | 호텔 리셉션 데스크 뒤와 양옆 | `Data/Hotel/HotelStaffSettings` | 3.5m | 20초 | 가능 | 제한 없음 |
| `SHOP_STAFF`(Alex)·`SHOP_PHARMACIST`(Hannah)·`SHOP_NIGHT_CLERK`(Samuel) | 드럭스토어 계산대 뒤 | `Data/Mission/ShopStaffSettings` | 4m | 5초 | 가능 | 제한 없음 |
| `POLICE_OFFICER`(Dan)·`POLICE_SERGEANT`(Victor)·`POLICE_PATROL_OFFICER`(Kevin)·`POLICE_ROOKIE`(Tyler) | 마을 경찰서 창구 뒤, 경찰서 1층 안에서 생활 | `PoliceOfficerSettings` | 4m | 5초 | 가능 | 제한 없음 |
| 거리 매장 직원 9명([아래](#거리-매장-직원)) | 마을 매장 1층 안에서 생활 | `BuildingStaffSettings` | 3.5m | 20초 | 가능 | 제한 없음 |

- 마을 행인 Ben은 튜토리얼에서 길을 묻는 사람과 같은 사람입니다. 튜토리얼 진행(`TutorialDirector`)이 단계마다 `Passerby.SetTutorialScript`를 불러, 튜토리얼 중에는 튜토리얼 대본·인사말(키 `TUTORIAL_PASSERBY`)과 튜토리얼 설정으로 집 앞에서 길을 묻고 생활하지 않게 합니다. 튜토리얼을 마치면 NPC 키로 평소 대화하고 평소 설정으로 생활합니다. 튜토리얼에서 길을 물은 것도 그날 먼저 말을 건 것으로 세므로, 튜토리얼을 마친 그 게임 날에는 다시 다가오지 않습니다.
- 카페 직원은 새 컴포넌트 없이 행인 프리팹을 쓰고, 걷는 속도 0이라 플레이어에게 걸어가지 않습니다. 플레이어가 카운터 앞 2m 안에 오면 카운터 뒤 자리로 돌아가([생활](#생활)) 제안 거리 2.5m에서 대화를 제안합니다. 행인과 직원이 같은 제안 화면을 쓰므로 머리말은 NPC 종류 대신 대화 대본의 이름으로 누가 말을 걸었는지 보여 줍니다.

- NPC 키는 ERD `npcs.code`와 같아야 하므로 서버 시드 데이터와 맞춥니다. 이름·성격·대사는 서버 데이터입니다.
- 퀘스트에서 이 행인을 만나야 하는 동안에는 `Passerby.WaitAt`으로 정한 자리에 붙잡아 둡니다. 그 자리로 옮겨 생활 대신 그 자리를 지키고, 거절하면 그 자리로 돌아가며, 그날 이미 말을 걸었어도 다가와 제안합니다. `StopWaiting`으로 풀면 평소 생활로 돌아갑니다. 자리는 [Mission 모듈](../Mission/README.md)의 `QuestNpcWaitSpot`이 정합니다.
- 행인이 다가와도 되는지는 `Passerby.IsApproachAllowed`로 진행 흐름이 정합니다. false면 플레이어가 바쁠 때처럼 다가오지 않고 [말 걸기]도 보이지 않으며, 이미 제안 중이면 제안을 거두지 않습니다. 지금은 [Tutorial 모듈](../Tutorial/README.md)이 튜토리얼 동안 Ben만 Ben 만나기·길 안내 단계에서 다가오게 하고, 다른 NPC는 누구와 말해야 하는지 헷갈리지 않게 튜토리얼을 마친 뒤 다가오게 합니다. 하루에 한 번만 먼저 다가오는 행인도 튜토리얼에서 플레이어가 기다리며 붙잡혀 있으면(`IsPlayerWaiting`) 그날 이미 말을 걸었어도 다가옵니다.
- 거절하거나 대화를 마친 뒤에도 [말 걸기]로 언제든 다시 대화할 수 있으므로, 쉬는 시간과 하루 한 번 제한은 NPC가 다시 먼저 다가오는 것에만 씁니다. 준비해 둔 것을 건네려고 부르는 것은 이 제한을 받지 않고, 부른 것을 거절·무시했을 때만 쉬는 시간을 기다립니다.

### 클릭·탭으로 말 걸기

마을에 NPC가 많아 가까이 가서 뜨는 [말 걸기]만으로는 원하는 NPC를 고르기 어렵습니다. 그래서 NPC 몸이나 머리 위 이름표를 클릭(휴대폰·태블릿은 탭)해도 말을 걸 수 있습니다(`NpcClickTalk`). 이름표를 누르면 그 이름표의 NPC 몸을 누른 것과 같아, 아래 표의 동작이 모두 같습니다.

| 상황 | 동작 |
| --- | --- |
| PC에서 8m 안의 말을 걸 수 있는 NPC나 그 이름표에 커서를 올림 | 그 NPC의 몸·옷·머리 둘레에 옅은 노란 테두리를 그립니다. 커서가 떠나거나, 8m 밖이거나, 말을 걸 수 없는 상태가 되면 지웁니다. 휴대폰·태블릿에는 커서가 없어 테두리를 그리지 않습니다. |
| 8m 안의 기다리거나 쉬는 NPC, 다가오는 NPC를 클릭·탭 | 그 자리에서 바로 사용자가 먼저 건 대화(CONV-01 `initiatedBy: USER`)로 시작합니다. 대화 화면은 NPC 얼굴 기준이라 떨어져 있어도 구도가 같습니다. 끝나면 [말 걸기]로 건 대화처럼 다시 쉽니다. |
| 다가와 제안하는 NPC를 클릭·탭 | [대화하기]를 누른 것과 같습니다(`ConversationInitiator.Npc`). |
| 다른 NPC가 제안하는 중에 다른 NPC를 클릭·탭 | 제안한 NPC는 [지금은 안 할래요]를 누른 것처럼 쉬고, 누른 NPC와 대화합니다. 거절할 수 없는 제안(튜토리얼 Ben)이 떠 있으면 반응하지 않습니다. |
| 대화를 다 쓴 게스트가 클릭·탭 | 대화 대신 [가입하고 계속 대화하기]를 누른 것처럼 가입 안내 창을 엽니다. |
| 8m보다 멀거나, 다른 대화 중이거나, 창이 열려 이동이 잠겼거나, 진행 흐름이 막은 NPC(`IsApproachAllowed` false) | 반응하지 않고 테두리도 그리지 않습니다. [말 걸기]를 보이지 않는 때와 같은 기준입니다. |
| 화면을 끌어 시점을 돌림, UI 위를 누름 | 사물 카드와 같은 기준으로 클릭으로 보지 않습니다. 화면을 덮는 터치 조작판 위의 탭은 받습니다. |
| NPC 앞을 벽이나 사물 카드가 뜨는 사물이 가림 | NPC를 고르지 않습니다. 사물이면 사물 카드가 뜹니다. 플레이어 몸과 문턱 같은 보이지 않는 감지 영역은 지나칩니다. |
| 이름표 위에 커서를 올리거나 이름표를 누름 | 이름표 바탕 전체(이름·역할 칩 포함)가 클릭을 받습니다. 이름표가 사물 앞에 떠 있어도 사물 카드는 뜨지 않고 그 이름표의 NPC만 고릅니다. |
| 나란히 선 NPC의 이름표가 겹친 곳을 누름 | 카메라에서 가까운 NPC 이름표가 위에 그려지고, 그 NPC에게 말을 겁니다. |
| 메뉴·창·HUD 버튼이 이름표를 덮음 | 이름표는 다른 화면 UI보다 아래에 그려지므로 덮은 UI를 누른 것으로 보고 말을 걸지 않습니다. |

이름표 층(`NameTags`)은 휴대폰 터치 조작판(정렬 -10)보다 아래(-20)에 그립니다. 이름표가 클릭을 받아도 이름표 위에서 시작한 끌기는 조작판이 받아 시점·이동이 그대로 되고, 탭만 이름표 클릭으로 봅니다. PC의 시점 끌기는 끌기를 받는 UI(스크롤 목록 등)만 막으므로 이름표 위에서도 됩니다(`UiDragTarget`).

거리는 [말 걸기]와 같이 수평 거리로 재고, 8m는 사물 카드를 띄우는 최대 거리와 같습니다. 택시 기사는 지금처럼 [택시 세우기]·[기사에게 말 걸기]로만 부릅니다(`Passerby`가 아닌 NPC는 클릭 대상이 아닙니다).

## Ben의 외형과 걷기

Ben(`TOWN_PASSERBY`)은 마스터 골격(Humanoid)의 비즈니스맨 외형을 씁니다([Builder](#builder)). 정지·걷기·벤치 앉기는 다른 Humanoid NPC와 같은 `NpcLife` 컨트롤러로 재생하고, 대기 자세는 비즈니스맨 전용 Idle을 씁니다. 이동 위치는 계속 `Passerby`가 맡습니다.

`Prefabs/Npc/BenVisual.prefab`은 예전 도형 캐릭터에 짧은 팔다리용 **Generic 뼈대**와 전용 걷기 동작(`Art/Characters/Npc/Ben/`의 `BenLocomotion.controller`·`BenIdle.anim`·`BenWalk.anim`)을 붙인 외형으로, 지금은 Ben에 연결하지 않습니다. **Tools → A305 → Npc → Build Ben Rig**는 Ben의 외형이 예전 도형 외형이나 `BenVisual`이 아니면 바꾸지 않고 멈춥니다. **Preview Ben Walk**는 `Scenes/Sandbox/BenWalkSandbox.unity`와 정면·측면 렌더로 이 Generic 외형의 걷기를 보여 줍니다. Sandbox를 Play한 뒤 Ben의 자식 `LittleMungV7Visual`을 선택하고 Animator 창의 Parameters에서 `Speed`를 1.1로 바꾸면 걷고, 0이면 멈춥니다.

## 생활

생활 데이터가 있는 NPC는 게임 시각의 교대에 따라 근무지·타운·숙소를 오갑니다. 근무 중에는 기존 생활·응대 동작을 사용하고, 근무 밖의 출현과 출퇴근은 `NpcPopulation`이 관리합니다. 규칙과 숫자는 [NPC 생활·관계 설계](../../../../../docs/npc-life-design.md)를 따릅니다. 택시 기사 Joe는 운전석에 있어 걸어서 출퇴근하는 대상에서 제외합니다.

| 상황 | 동작 |
| --- | --- |
| 근무(`WORK`) | 일만 합니다. 카페 직원은 커피 머신에서 음료를 만들고, 호텔리어는 리셉션 데스크에서 손님을 맞습니다. |
| 휴식·자유(`BREAK`·`FREE`) | 직원의 휴식은 근무지 밖에서 지냅니다. 행인은 자유 시간에 배고픔·에너지·사교·재미 중 가장 급한 욕구를 채울 사물을 고릅니다. |
| 급한 일이 없음 | 한곳에 서 있지 않고, 처음 놓인 자리에서 8m 안의 다른 사물(1.5m 넘게 떨어진 것)로 걸어가 2~5초 둘러본 뒤 또 옮겨 갑니다. 앉아 있었으면 일어납니다. |
| 카운터 응대 | 카운터를 맡은 직원(자리 1.5m 안에 카페·드럭스토어 `counter`·호텔과 경찰서 `reception_desk`가 있는 NPC) 중 [응대할 한 명](#일터와-응대)은 플레이어가 카운터에서 2m(`serveRadiusMeters`) 안에 오면 하던 일을 그만두고 카운터 뒤 자리로 돌아가 손님 쪽으로 돌아서고, 자리에 선 뒤에만 다가와 제안합니다. 다른 곳에서는 먼저 다가오지 않고 [말 걸기]만 받습니다. |
| 건물 안에서만 지냄 | 구역을 켠 NPC(거리 매장 직원, 경찰관)는 자기 건물 1층 안의 사물·자리만 고릅니다. 마을의 벤치·분수처럼 건물 밖 사물은 고르지 않고, 할 일이 없으면 건물 안 사물 사이를 옮겨 다닙니다. 2층은 다른 가게라 고르지 않습니다. |
| 퇴근(`OFF`) | 근무지에서 빠지고 타운의 공원으로 걸어가 지낸 뒤 숙소로 돌아갑니다. 숙소 안에서는 보이지 않습니다. 다시 근무가 시작되면 욕구가 시작값으로 돌아갑니다. |
| 사물 고르기 | 같은 사물이 여럿이면 걸어서 가장 가까운 것을 씁니다. 의자·벤치·소파는 빈 자리에 앉고, 그 밖의 사물은 다른 NPC의 예약한 목적지와 1.25m 이상 떨어진 옆자리를 골라 행동 시간(게임 분) 동안 씁니다. 응대 직원의 자리와 가까운 작업 자리는 비우고 다시 고릅니다. Humanoid 모델이 아니면 앉지 않고 자리 앞에 서서 쉽니다. 앉아 있는 동안에는 리틀 멍 외형의 서 있는 자세 보정(`StandingPoseCorrections`)을 끄고, 일어서면 다시 켭니다. |
| 갈 수 없음 | NavMesh 경로가 없거나 2.5초 동안 나아가지 못하면 그 사물은 20초 동안 고르지 않습니다. |
| 다른 NPC와 충돌 | NPC끼리도 `CharacterController` 충돌을 적용해 몸을 관통하지 못합니다. 작업·구경 목적지는 서로 예약해 한 자리에 모이지 않고, 응대 자리 주변은 다른 직원이 비웁니다. 작은 사물 바로 옆이 차 있으면 조금 물러선 자리도 고릅니다. 길이 막혀 2.5초 동안 나아가지 못하면 기존 경로 실패 처리로 다른 행동을 고릅니다. |
| 동작 | 커피 만들기·손님 맞기는 손으로 다루기(`Interact`), 머핀·커피는 집어 들기(`PickUp_Table`), 의자·벤치·소파는 앉기(`Sitting_Idle_Loop`, 다리는 `SeatedLegs`로 앉는 면에 맞춤), 제안·대화 중에는 손짓하며 말하기(`Idle_Talking_Loop`)입니다. `Passerby.talkGesture`를 끈 NPC(마을 행인 Ben)는 제안·대화 중에도 자기 대기 자세로 섭니다. 서 있기·걷기·뛰기는 플레이어와 같은 리틀멍 이동 동작입니다. 동작은 [Quaternius Universal Animation Library](../../ThirdParty/CREDITS.md)(CC0)를 Humanoid로 리타겟팅했고, 먹기·마시기 전용 동작은 무료판에 없어 집어 들기로 대신합니다. |
| 다가와 말 걸기·대화 | 하던 일을 그만두고 앉아 있었으면 일어납니다. 대화가 끝나면 다시 고릅니다. |
| 장면을 다시 불러옴 | 그 시각의 일과에서 욕구 시작값으로 다시 지냅니다. |

생활 데이터는 AI 규칙 원본(`ai/npcs/rules/world-rules.json`, `ai/npcs/life/`)에서 `ai/tools/npc_life_export.py`가 내보낸 파일이라 직접 고치지 않습니다. 규칙이나 NPC 생활 파일을 바꾸면 다시 내보내며, AI 테스트가 다시 내보낸 결과와 같은지 확인합니다.

```bash
ai/.venv/Scripts/python ai/tools/npc_life_export.py
```

## 일터와 응대

일터마다 여러 직원이 교대(`NpcLife.json`의 `schedule`, AI 생활 파일의 `shift`)를 나눠 맡습니다. 일터에는 근무 중인 직원만 나타납니다. 첫날 미션·튜토리얼이 붙잡아 둔 NPC는 진행을 위해 자리를 지킵니다.

### 출퇴근과 숙소

`NpcDayPlan`은 근무 한 시간 전부터 숙소에서 일터로 이동하고, 퇴근 후 30분은 공원으로 이동, 이후 90분은 공원에서 지내기, 다음 30분은 숙소로 돌아가기로 나눕니다(모두 게임 분). 짧은 휴식은 일터 앞에서 쉬고 돌아옵니다. 행인은 `FREE` 일과 동안 기존 타운 생활을 하고, 일과 밖에는 같은 방식으로 숙소를 이용합니다. 이동 중인 직원에게 먼저 말을 걸면 근무 외 일상 대화로 이어집니다.

이동은 타운의 NavMesh 경로를 따릅니다. 다른 씬에 다녀와도 현재 게임 시각에 해당하는 경로 위치에서 이어집니다. 베이커리 뒤 `NpcResidence`는 막힌 주거 외관과 NPC 출입 위치만 있으며, 플레이어 출입구나 실내 씬은 없습니다. **Tools → A305 → Npc → Build Population And Housing**으로 기존 외형을 재사용한 출퇴근 프리팹, 네 장소 씬의 출현 연결, 숙소와 타운 NavMesh를 생성합니다. 거리 실내 로드가 비번 직원을 다시 켜지 않도록 `NpcWorkPresence`가 출현을 확인합니다.

| 일터 | 직원(교대 순) | 자리 |
| --- | --- | --- |
| 카페 `CafeScene` | Mia(아침)·Leo(한낮)·Zoe(오후~저녁) | Mia는 카운터 뒤, Leo·Zoe는 카운터 양 끝의 손님 쪽 모서리 |
| 호텔 `HotelScene` | Jake(아침)·Walter·Diego(한낮)·Owen(오후~저녁)·Clara(밤) | Jake는 데스크 뒤, Owen·Clara는 데스크 손님 쪽 양옆, Walter·Diego는 데스크 뒤쪽 양 끝 |
| 드럭스토어 `ShopScene` | Alex(아침)·Hannah(오후~밤)·Samuel(밤~새벽) | 모두 계산대 뒤. Alex가 계산대에 가장 가깝다 |
| 경찰서 `TownScene` | Dan(아침)·Tyler(한낮)·Kevin(오후~저녁)·Victor(밤) | 모두 창구 뒤나 창구 끝. Dan이 창구에 가장 가깝다 |

| 상황 | 동작 |
| --- | --- |
| 근무(`WORK`) 중 | 카운터를 맡은 직원이면 응대 후보가 됩니다. 카운터가 없는 직원은 가까이 온 플레이어에게 제안합니다. |
| 쉬는 시간·퇴근 | 근무 외라 먼저 다가오거나 카운터로 돌아가지 않습니다. 플레이어가 [말 걸기]로 먼저 말을 걸면 대화하고, 대화 시작(CONV-01)에 `npcOffDuty: true`를 보내 서버가 쉬는 중인 직원으로 답하게 합니다. 행인은 맡은 근무가 없어 보내지 않습니다. |
| 같은 카운터의 직원이 여럿 근무 중 | 손님이 카운터 앞에 오면 `NpcDuty.PickServer`가 한 명만 고릅니다. 첫날 미션이 지금 찾아가라고 한 NPC가 먼저이고, 그다음은 하는 일(음료 만들기 등)이 없는 직원 중 지금 서 있는 곳이 카운터에 가장 가까운 직원, 모두 일하는 중이면 가장 가까운 직원, 거리가 같으면 NPC 키 순서입니다. 고른 직원은 손님이 카운터를 떠날 때까지 바뀌지 않고, 하던 일을 그만두고 자기 자리로 와서 응대합니다. 나머지는 하던 일을 계속하고 [말 걸기]만 받습니다. |
| 첫날 미션이 찾아가라고 한 NPC | 교대 밖이어도 근무 중으로 보고 자리를 지켜 미션 대화가 이어지게 합니다(`NpcDuty.MissionNpcCode`, 첫날 미션 상태를 읽을 때마다 정함). |

카페·호텔의 카운터 뒤 통로는 한 사람 폭이라 원래 직원만 그 안에 서고, 새 직원은 카운터 양 끝에 섭니다. 누가 응대할지는 자리가 아니라 그때 한가한지와 카운터까지의 거리로 정합니다. 마을 행인 10명은 다른 확인이 플레이어를 세우는 집 앞·경찰서·동서 길 서쪽에서 감지 거리보다 먼 본길 북쪽 인도와 공원에 둡니다. Arthur·Margaret 부부는 나란히 서서 시작하지만 함께 걷는 동작은 없고, 함께 산책하는 개(Pepper) 모델도 아직 없습니다.

## 택시

마을 본길의 노란 택시 운전석에 택시 기사(`TAXI_DRIVER`)가 앉아 있습니다. 기사는 다가오지 않고, 택시를 세운 뒤 플레이어가 먼저 말을 겁니다.

| 상황 | 동작 |
| --- | --- |
| 택시가 달리는 차로 쪽 인도에 서 있고, 택시가 4~18m 앞에서 다가옴 | [택시 세우기]와 키 안내(E)를 보입니다. 길 건너편, 택시 뒤, 유턴 구간 너머에서는 보이지 않습니다. |
| [택시 세우기] | 택시가 플레이어 옆 길가로 와서 섭니다. |
| 선 택시 곁(3.5m 안) | [Joe에게 말 걸기]로 뒷좌석에 타서 대화합니다(`ConversationInitiator.User`). |
| 게스트 | [택시 세우기]·[Joe에게 말 걸기] 위에 "게스트 남은 대화 2번"을 보입니다. 택시를 세우면 바로 기사와 대화하기 때문입니다. |
| 게스트가 대화를 다 씀 | 두 버튼 대신 행인 [말 걸기]와 같이 "게스트 대화를 모두 썼어요"와 [가입하고 계속 대화하기]를 보이고, 누르면 가입 안내 창을 엽니다. 택시는 세우지 않습니다. |
| 다른 이유로 새 대화를 시작할 수 없음 | 두 버튼 대신 누를 수 없는 버튼으로 이유("지금은 대화할 수 없어요")를 보입니다. E 키도 택시를 세우지 않습니다. |
| 목적지를 말함 | 카페·호텔·드럭스토어·집과 우체국·다이너·은행·빨래방·꽃집·이발소·경찰서의 총 11곳으로 이동합니다. 도착 후 하차하고 기존 피드백 흐름을 이어갑니다. |
| 도착 전에 대화를 마침 | 길 바깥쪽에 내려준 뒤 택시가 다시 달립니다. |
| 대화하지 않고 8m보다 멀어짐 | 기다리지 않고 다시 달립니다. 세우러 오는 중에는 세운 자리까지의 거리만큼 더 기다립니다. |

다른 대화 중이거나 행인이 제안 중이면 택시 버튼을 보이지 않습니다. 승용차는 세울 수 없고, 택시와 마찬가지로 신호와 앞을 막은 사람에 맞춰 섭니다. 기사 Joe는 중년 남성 리틀 멍 외형(`Prefabs/Npc/Review/LittleMung_MiddleAge_Male.prefab`)이 운전석에 앉아 운전대를 잡은 자세입니다. 머리 꼭대기가 지붕 안감 아래(택시 기준 1.5m)에 오도록 배율을 줄여 앉힙니다. 배치와 연결은 **Tools → A305 → World → Apply Town Street Life**가, 이미 배치된 기사의 외형 교체는 **Tools → A305 → World → Replace Taxi Driver Model**(NPC 키·사물 단어·택시 대화 연결 유지)이, 운전 자세는 **Apply Taxi Cabin**이 맡습니다. 운전석에서는 서 있는 자세 보정과 함께 외형의 얼굴 제어 스크립트도 지우지만, 대화가 시작되면 공통 연결(`DialogueSpeaker`)이 기사 얼굴에 립싱크·눈 깜박임을 적용합니다. 대화 밖의 자동 깜박임은 하지 않습니다.

[목적지와 구성·확장 방법](../../../../../docs/unity/taxi-ride.md)을 참고합니다.

## 경찰서

마을 남서쪽 끝 경찰서 창구 뒤에 경찰관 Dan(`POLICE_OFFICER`)·Victor(`POLICE_SERGEANT`)·Kevin(`POLICE_PATROL_OFFICER`)·Tyler(`POLICE_ROOKIE`)가 24시간 교대로 있습니다([일터와 응대](#일터와-응대)). 카페 직원처럼 행인 프리팹을 쓰고 플레이어에게 걸어가지 않으며, 경찰서 1층 안에서만 지냅니다. 경찰관마다 신고 창구(`PoliceReportDesk`)가 있어 누구에게 신고해도 순찰차가 출동합니다. 신고와 장소는 서버의 의도 판정(백엔드 `PoliceDesk`)이 문맥으로 고릅니다([MVP API 명세의 경찰 신고](../../../../../docs/api/mvp-api.md)).

| 대화에서 말한 내용 | 대화를 마친 뒤 순찰차 |
| --- | --- |
| 장소를 하나 말한 신고 (My wallet was stolen at the diner.) | 경광등을 켜고 그 장소 앞에 20초 서 있다가 돌아옵니다. |
| 신고한 뒤에 장소를 하나 말함 (My bag was stolen. → It was the cafe.) | 마지막에 말한 장소로 출동합니다. |
| 장소 없는 신고 (I lost my wallet.) 또는 장소를 여럿 말한 신고 | 경광등을 켜고 본길을 한 바퀴 순찰하고 돌아옵니다. |
| 신고가 아닌 말 (Hello, officer.), 부정·가정 (Nothing was stolen.) | 출동하지 않습니다. 신고 전에 말한 장소는 기억하지 않습니다. |
| 순찰차가 나가 있을 때 한 신고 | 출동하지 않고 "신고 접수 · 순찰차가 이미 출동해 있어요"를 보입니다. |

신고는 대화 중에 접수만 하고, 대화를 마친 뒤 출발해 밖으로 나온 플레이어가 출동하는 모습을 보게 합니다. 카페는 배우는 사람이 자주 틀리는 caffe·caffee도 알아봅니다(택시 목적지와 같은 장소 이름). 배치와 연결은 **Tools → A305 → World → Apply Police Station**이 합니다([World 모듈](../World/README.md#경찰서와-순찰차)).

## 거리 매장 직원

마을 본길의 빈 매장 9곳 1층에 직원을 둡니다. 모두 행인 프리팹을 쓰고 걷는 속도 0이라 플레이어에게 걸어가지 않습니다. 플레이어가 2.5m 안에 오면 대화를 제안하고, 거절하거나 대화를 마친 뒤 20초 동안은 [말 걸기]로 대화합니다. 이름·인격·목소리·상황은 서버 데이터(`ai/npcs/<NPC 키>.json`, `ai/npcs/scenarios/`)입니다.

| 건물 | NPC 키 | 이름 | 외형(색만 바꿈) |
| --- | --- | --- | --- |
| 우체국 `PostOffice` | `POST_OFFICE_STAFF` | Linda | 중년 여성, 자주색 옷·갈색 눈 |
| 다이너 `MapleDiner` | `DINER_STAFF` | Rosa | 중년 여성, 빨간 옷·초록 눈 |
| 은행 `TownBank` | `BANK_STAFF` | Henry | 비즈니스맨, 은색 머리·청회색 정장·회색 눈 |
| 빨래방 `SudsLaundromat` | `LAUNDROMAT_STAFF` | Gus | 중년 남성, 겨자색 셔츠·갈색 눈 |
| 꽃집 `BloomFlowers` | `FLOWER_SHOP_STAFF` | Lily | 할머니, 연보라 머리·초록 옷·청록 눈 |
| 이발소 `MainStreetBarber` | `BARBER_STAFF` | Tony | 중년 남성, 검은 머리·흰 셔츠·갈색 눈 |
| 빵집 `MapleBakery` | `BAKERY_STAFF` | Grace | 할머니, 흰 머리·적갈색 옷·파란 눈 |
| 오락실 `LittleArcade` | `ARCADE_STAFF` | Max | 흑인 청년, 적갈색 머리·초록 셔츠·보라 바지·호박색 눈 |
| 생활용품점 `HomeLiving` | `HOME_STORE_STAFF` | Nina | 중년 여성, 연두 옷·담갈색 눈 |

- 외형과 배색은 [외형 표](#외형-표)를 따릅니다. Gus와 Tony는 같은 외형에 눈동자 색이 같아, 서로 다른 사람으로 보이게 Tony의 머리를 검게 바꿨습니다.
- 2층 가게(서점·찻집)에는 아직 직원이 없습니다.

## 외형 표

모든 NPC의 외형(리틀 멍 외형 프리팹)과 머리·옷·신발·눈동자 배색은 `Scripts/Editor/Npc/NpcCast.cs` 한 곳에서 정합니다. 외형별로 바꿀 수 있는 색 범위는 `NpcLooks.cs`, 색을 바꾼 텍스처·재질 사본은 `NpcCastDresser`가 `Art/Characters/NpcCast/<NPC 키>/`에 만듭니다. 배색을 비운 부분은 외형의 원래 색이고, 배색이 없는 NPC는 원래 재질을 씁니다.

| 규칙 | 내용 |
| --- | --- |
| 쓰는 외형 | 플레이어(기본) 외형은 NPC에 쓰지 않습니다. 바리스타·카페 남자 유니폼은 카페 직원만, 남녀 호텔 유니폼은 호텔 직원만, 검수한 남녀 경찰 제복은 경찰관만 입습니다. 일반 행인 외형은 다른 역할에서도 쓸 수 있습니다. 경로와 배정은 `NpcLooks`·`NpcCast`가 관리합니다. |
| 성별·나이 | 인격 카드(`ai/npcs/<NPC 키>.json`)의 나이대·배경에 맞는 외형을 고릅니다. Arthur는 전용 할아버지 검수 사본을 씁니다. 역할이 정해지지 않은 외형을 기존 NPC의 성별 변경으로 임의 배정하지 않습니다. |
| 서로 다른 사람 | 같은 외형을 쓰는 두 NPC의 눈동자 색이 같으면 머리와 옷 색이 모두 달라야 하고, 옷 색이 같으면 눈동자·머리·신발 색이 모두 달라야 합니다. 경찰관 네 명은 같은 제복이라 눈동자·머리·신발로 구분합니다. |
| 색 이름 | 같은 색인지는 `NpcCast.Palette`의 색 이름으로 가립니다. 같은 이름에는 같은 값을 씁니다. |

- 머리카락과 피부·바지가 비슷한 색인 외형(중년 남녀·할머니·바리스타)은 몸 높이로 머리만 고르고(머리 꼭대기 1, 발바닥 0), 가닥 사이에 남는 가는 선은 같은 색으로 메웁니다(`NpcColorSwap.FillGaps`).
- 배색이 같으면 텍스처를 다시 쓰지 않습니다(가져오기 설정의 userData에 배색을 적어 둠). 색을 칠하는 규칙을 바꾸면 `NpcCastMember`의 `RecipeVersion`을 올립니다.
- **Tools → A305 → Npc → Render Cast Lineups**는 같은 외형의 NPC를 외형 표 순서대로 세워 몸 전체·얼굴·뒤통수를 찍은 그림을 외형마다 저장합니다(기본 `artifacts/unity/npc-cast/`, 환경 변수 `NPC_CAST_LINEUP_OUTPUT`로 바꿈). 외형 표를 바꾸면 이 그림으로 서로 다른 사람으로 보이는지 확인합니다. 경찰 검수 사본에는 모자까지 포함됩니다.

## NPC 추가

1. 서버 시드(`backend/.../db/seed/world.sql`)·AI 인격 카드·생활 파일에 NPC 키가 먼저 있어야 합니다. 생활 데이터는 `npc_life_export.py`로 내보냅니다.
2. [외형 표](#외형-표)(`NpcCast.Members`)에 외형과 배색을 더합니다. `NpcCastTests`가 시드의 모든 NPC에 외형이 하나씩 있는지, 같은 외형끼리 서로 다른 사람으로 보이는지, 유니폼을 그 일터 직원만 입는지 확인합니다.
3. 둘 자리를 적습니다. 카페·호텔·드럭스토어 직원과 마을 행인은 `NpcCastBuilder`의 `Newcomers`(자리·방향과 설정을 따를 같은 일터의 원래 NPC), 경찰관은 `PoliceStationBuilder`의 `Officers`, 거리 매장 직원은 `BuildingStaffBuilder`의 표입니다. 카운터 직원은 원래 직원보다 카운터에서 조금 멀게 둡니다([일터와 응대](#일터와-응대)).
4. 확인용 인사말(`NpcBuilder`의 `MockGreetings`)과 확인용 대본(`MockDialogueScriptSource`의 예시, 이름·역할 이름은 시드와 같게)을 더합니다.
5. **Tools → A305 → Npc → Build Cast**와 **Dialogue → Build**를 실행하고, **Render Cast Lineups**로 외형을 확인합니다.

행인 프리팹을 손으로 놓을 때는 `Prefabs/Npc/Passerby.prefab`을 씬의 `Characters` 아래에 놓고 `NpcIdentity`의 `Code`와 `Passerby`의 `Settings`를 정한 뒤 **Tools → A305 → World → Set Up Open Scene**으로 플레이어·대화 처리·제안 화면과 잇습니다. 서 있는 자리와 방향이 돌아갈 제자리입니다.

## Builder

**Tools → A305 → Npc → Connect Reviewed Staff Visuals**는 기존 카페 Leo, 호텔 Clara, 행인 Arthur, 경찰 직원의 외형만 검수 프리팹으로 갱신합니다. 골격·동작·13개 얼굴 채널 검사 후 연결하며, 새 NPC 생성·배치 이동·생활 데이터·NavMesh 재생성을 하지 않습니다. 이전에 연결된 외형은 교체하여 얼굴과 Animator가 하나씩 남게 합니다. 캡슐은 실제 몸 높이, 대화 시선은 이식한 눈 메시의 실제 위치에 맞춥니다. 경찰 여자 프리팹은 외형 표에 등록되어 있지만 기존 역할의 성별을 바꾸지 않도록 미배정 상태입니다. 원래 배치부터 새로 만드는 경우는 아래 **Build Cast**를 사용합니다.

**Tools → A305 → Npc → Build**(`Scripts/Editor/Npc/NpcBuilder.cs`)가 다음을 준비합니다. 공간 Builder(**World → Build Spaces**)도 함께 실행합니다.

- 설정(`Data/Npc/`): 없을 때만 만들고, 있으면 값을 유지합니다.
- 확인용 인사말(`Data/Npc/MockNpcLines.asset`): 없으면 만들고, 있으면 아직 없는 NPC 키의 인사말만 덧붙입니다. 이미 있는 인사말은 바꾸지 않습니다.
- 행인 프리팹 `Prefabs/Npc/Passerby.prefab`과 제안 화면 `Prefabs/Npc/PasserbyOffer.prefab`(제안 패널과 [말 걸기], NPC 클릭·탭 `NpcClickTalk`과 테두리 재질 `Art/Npc/NpcHoverOutline.mat`): 컴포넌트와 연결만 맞추고 모델·디자인을 유지합니다. 없는 요소만 새로 만듭니다. 행인 프리팹에는 카메라에서 먼 동안 팔 관통 보정을 끄는 `NpcDetailDistance`도 붙입니다. 제안 화면에는 머리 위 이름표 층(`NameTags`)도 더합니다(`NpcNameTagBuilder`).
- 마을(`TownScene`) 집 앞에 행인 Ben을, 카페(`CafeScene`) 카운터 뒤에 카페 직원을 둡니다. 같은 NPC 키가 이미 있으면 위치를 바꾸지 않습니다. 예전처럼 집 앞 튜토리얼 행인(`TUTORIAL_PASSERBY`)과 동서 길 행인이 따로 있으면, 집 앞 행인을 Ben으로 바꾸고 동서 길 행인은 지웁니다. Ben에게는 평소 설정과 튜토리얼 설정을 함께 연결하고, 외형을 아래처럼 연결합니다.
- 서서 지내는 NPC의 외형: **Tools → A305 → Npc → Connect Character Visuals**(`Scripts/Editor/Npc/NpcVisualBuilder.cs`)가 NPC 키별 외형을 장면의 NPC 아래에 둡니다. `TownScene`의 Ben은 비즈니스맨 `Prefabs/Npc/Review/LittleMung_Business_Male.prefab`, `ShopScene`의 드럭스토어 직원 Alex(`SHOP_STAFF`)는 흑인 남성 `Prefabs/Npc/Review/LittleMung_BlackYoung_Male.prefab`입니다. 다른 행인·직원도 쓰는 공용 `Passerby.prefab`의 `Visual`은 바꾸지 않고 꺼 둡니다. 예전 외형(도형 `LittleMungV7NpcVisual`, Generic 뼈대 `BenVisual`)은 NPC 아래에서 지우고, 대기 자세의 발바닥을 바닥에 맞춘 뒤 `CharacterAnimationDriver`의 Animator, 캡슐 높이(외형의 키), 시선(`LookTarget`)을 눈 높이로 맞춥니다. 공용 손짓 동작은 이 체형에서 무릎을 굽히고 팔을 내민 자세가 되므로 Ben은 `Passerby.talkGesture`를 꺼, 말을 건네고 대화하는 동안 자기 Idle로 서게 합니다. Alex는 다른 직원과 같이 손짓합니다. 이미 연결한 외형은 다시 만들지 않습니다. **Npc → Build**(Ben), 드럭스토어 Builder와 **Mission → Adjust Shop Counter Sightline**(Alex)도 같은 연결을 실행합니다.

**Tools → A305 → Npc → Build Building Staff**(`Scripts/Editor/Npc/BuildingStaffBuilder.cs`)는 [거리 매장 직원](#거리-매장-직원)을 `TownScene`의 각 건물 1층에 둡니다. 정한 자리가 카운터·가구와 겹치면 1층 안의 가장 가까운 빈자리로 옮기고(`NpcPlacement`), 문 쪽을 보게 세웁니다. 같은 NPC 키가 이미 있으면 위치를 바꾸지 않습니다. 외형은 [외형 표](#외형-표)의 외형을 `NpcVisualBuilder`로 붙이고, 배색은 `NpcCastDresser`가 입힙니다(몸 텍스처는 1024로 줄임, 머티리얼 사본은 실행할 때마다 외형의 원래 머티리얼 설정에 맞춤). 확인용 인사말·설정(`Data/Npc/BuildingStaffSettings.asset`)도 연결합니다. 이어서 **Build Daily Life**를 실행해 생활을 붙이고, 직원마다 구역을 자기 건물 1층으로 정합니다. 생활 데이터에 직원이 없으면 내보내기(`npc_life_export.py`)를 먼저 하라고 알리고 멈춥니다.

**Tools → A305 → Npc → Build Cast**(`Scripts/Editor/Npc/NpcCastBuilder.cs`)는 다른 Builder가 두지 않는 카페·호텔·드럭스토어 직원과 마을 행인을 [일터](#일터와-응대)에 두고, 경찰관은 `PoliceStationBuilder.PlaceOfficers`로 창구 뒤에 둡니다. 같은 NPC 키가 이미 있으면 위치를 바꾸지 않고, 정한 자리가 가구와 겹치면 가까운 빈자리로 옮깁니다. 설정은 같은 일터의 원래 NPC를 따르고, Mia·Jake와 같은 외형을 쓰는 직원은 기존 연결을 재사용하고, 새 검수 외형을 쓰는 직원은 자기 메시에서 캡슐·시선 높이를 측정합니다. 이어서 네 장면의 모든 NPC에 외형 표의 배색을 입히고 **Build Daily Life**를 실행한 뒤 경찰관이 경찰서 1층 안에서만 지내게 합니다. 확인용 인사말도 덧붙입니다.

**Tools → A305 → Npc → Build Daily Life**(`Scripts/Editor/Npc/NpcLifeBuilder.cs`)는 카페·호텔·드럭스토어·마을에서 생활 데이터가 있는 NPC에 `NpcRoutine`을 붙이고, NPC 몸 크기(반지름 0.3m, 키 1.8m, 턱 0.3m)로 바닥을 구워 `Data/Npc/WalkAreas/`에 저장한 뒤 `NpcWalkArea`로 연결합니다. 사람·차·트리거 콜라이더는 굽지 않습니다. 장면의 `NavMeshModifier`·`NavMeshModifierVolume`이 정한 바닥 구역을 따르며, 마을의 차도(`Road`)·횡단보도(`Crosswalk`) 구역은 [길 안내](../Wayfinding/README.md#횡단보도로-건너기)가 두고 비용은 보통 바닥과 같아 NPC 걸음은 바뀌지 않습니다. 가구 배치를 바꾸면 다시 실행합니다. 같은 메뉴가 `NpcAnimationBuilder`로 동작 FBX를 Humanoid로 가져오고 NPC 전용 Animator(`Art/Characters/Npc/NpcLife.controller`)를 만들어 `NpcRoutine`에 연결합니다. 장면에 저장된 모델은 플레이어와 같은 이동 컨트롤러 그대로 두고, 생활이 켜질 때 Humanoid 모델에 바꿔 끼웁니다(장면 모델의 키·발 높이 검사가 그대로 유지됩니다). 모델이 자기 Idle처럼 캐릭터 전용 클립을 덮어쓴 컨트롤러(`AnimatorOverrideController`)를 쓰면 `NpcLifeAnimator`가 그 클립을 생활 컨트롤러에 이어받습니다. 컨트롤러는 없을 때만 만들므로 구성을 바꾸려면 지우고 다시 실행합니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Npc.Tests` | `Assets/Tests/EditMode/Npc/` | 행인의 다가감·제안·부르기·[말 걸기]·클릭으로 말 걸기와 게스트 안내, 클릭한 NPC 고르기(`NpcPointerRaycastTests`)·테두리(`RendererOutlineTests`), 택시 버튼, 생활 일과·욕구·행동 고르기, 근무와 카운터 응대(`NpcDutyTests`), 외형 표(`NpcCastTests`), 동작 고르기, 카메라에서 먼 NPC의 팔 관통 보정 끄기, 앉아 있는 동안 다시 켜지 않기와 네 장면의 보정 쓰는 NPC가 모두 거리 끄기를 가졌는지(`NpcDetailDistanceTests`·`NpcDetailDistanceSceneTests`) |
| `A305.Npc.PlayModeTests` | `Assets/Tests/PlayMode/Npc/` | 실제 장면의 행인·튜토리얼 행인 대화 흐름, NPC 몸·이름표 클릭·탭으로 말 걸기와 커서 테두리, 겹친 이름표(`NpcClickTalkTests`), 택시 주행과 세우기, 경찰 신고와 순찰차 출동, 직원의 생활·카운터 응대와 동작 재생. 대화를 다 쓴 게스트가 피드백을 닫으면 가입 안내 창과 설문 카드가 뜨는지(`GuestSignupAfterFeedbackTests`, 서버 없이 피드백까지 가는 튜토리얼 길 안내로 확인) |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.

## 공통 표정·립싱크 연결

`NpcIdentity`가 있는 NPC는 카페 전용 Builder 없이 `DialogueSpeaker`를 통해 자기 얼굴을 자동 연결합니다. 공통 얼굴 채널이 있는 모델에만 립싱크·미소·눈 깜박임을 적용하며, 부족한 모델은 누락 목록을 남기고 음성을 유지합니다. 이동·대화 제안·카메라 코드는 바꾸지 않습니다. 모델 준비 규격과 검사 메뉴는 [NPC 얼굴 재사용 기준](../../../../../docs/unity/cafe-female-face.md#다른-npc의-자동-연결)을 참고합니다.

마을 행인 Ben(비즈니스맨), 드럭스토어 직원 Alex(흑인 청년 남성), 택시 기사 Joe(중년 남성)의 외형은 입 모양 9개, 좌우 미소, 좌우 눈 감기를 모두 갖춰 대화 중 같은 방식으로 립싱크·미소·눈 깜박임을 적용합니다. 서 있는 Ben·Alex의 대기 중 깜박임은 외형의 `CharacterIdleBlink`가 맡습니다([행인 눈 감기](../../../../ArtSources/Characters/ServiceNpcs/README.md#나머지-행인의-base-눈-이식)).
