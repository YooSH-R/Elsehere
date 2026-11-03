# Wayfinding 모듈

퀘스트나 NPC 대화로 어떤 장소를 찾아가야 할 때, 마을에서 그 건물의 출입문 앞을 빛내고 플레이어 발밑에서 출입문까지 도로 위에 야광색 길을 깝니다. 길이 차도를 건너야 하면 횡단보도로 돌아갑니다. 도로 위 길은 사용자가 끌 수 있고, 출입문 빛은 늘 보입니다.

목적지를 정하는 쪽(지금은 첫날 미션)은 장소 코드만 알리고, 길 안내는 누가 정했는지 모른 채 그 값만 봅니다. 새 미션도 같은 방법으로 목적지를 알리면 따로 연결하지 않아도 안내가 붙습니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `GuideDestination` | 지금 안내할 장소 코드(`HOME`·`CAFE`·`HOTEL`·`SHOP` 등). 목적지를 정하는 쪽이 `Shared.Show(placeCode)`·`Shared.Clear()`로 알립니다. 씬을 옮겨도 남도록 정적으로 두며 한 번에 한 곳만 안내합니다. |
| `DestinationGuide` | 마을에 하나 두는 길 안내. 이 씬에서 목적지 장소 씬으로 이어지는 `SpaceDoorway`의 도착 지점(문 앞 인도 1m)을 출입문 앞으로 찾아(`FindEntrance`) 빛 원과 빛기둥을 옮기고, 걸을 바닥(NavMesh)에서 찾은 길을 땅에 눕힌 `LineRenderer`로 그립니다. 길은 실제 바닥 높이에 맞춰 살짝 띄우고, 화살 무늬는 출입문 쪽으로 흐릅니다. 빛기둥은 출입문 3m 안에서 사라져 문을 가리지 않습니다. |
| `GuideVisibility` | 출입문 빛과 길을 보일지 정합니다. Unity 객체 없이 테스트합니다. |
| `GuideRouteClock` | 길을 언제 다시 찾고 얼마나 깔아 보일지 정합니다. Unity 객체 없이 테스트합니다. |
| `GuidePolyline` | 길 길이 재기, 앞부분만 자르기(깔리는 연출), 일정 간격으로 점 채우기(바닥 높이 맞추기). Unity 객체 없이 테스트합니다. |
| `GuideWalkAreas` | 바닥 구역 이름(`Road`·`Crosswalk`)과 차도에 큰 비용을 매기는 길 찾기 필터 |
| `GuideRoutePreference` | 도로 위 길 안내 켜기·끄기. 처음에는 켜져 있고, 서버가 아니라 이 기기(Web 빌드는 브라우저)에 저장합니다. |
| `GuideRouteToggle` | 누를 때마다 길 안내를 켜고 끄는 버튼. 첫날 미션의 크게 본 마을 지도 오른쪽 아래에 있습니다. |
| `DestinationGuideSettings` | 색·폭·속도와 길 찾기 기준(`Data/Wayfinding/DestinationGuideSettings.asset`) |

## 동작 규칙

| 상황 | 동작 |
| --- | --- |
| 목적지가 있고 이 씬에 그 장소로 들어가는 출입문이 있음 | 출입문 앞이 빛나고, 플레이어 발밑에서 출입문 앞까지 0.6초 동안 길이 깔립니다. |
| 목적지가 없음(미션 시작 전·모두 끝냄·튜토리얼 중) | 아무것도 보이지 않습니다. |
| 목적지가 이 씬에 출입문이 없는 곳(마을 거리 `TOWN`, 건물 안에서 다른 건물) | 아무것도 보이지 않습니다. 지금은 마을에만 길 안내를 둡니다. |
| 대화 중이거나 다른 공간으로 가는 중 | 출입문 빛과 길을 모두 숨기고, 다시 보일 때 길을 새로 깝니다. |
| 출입문 앞 1.5m 안에 옴 | 길만 거두고 출입문 빛은 남깁니다. 문에 들어가면 씬이 바뀌어 사라집니다. |
| 걷는 중 | 1m 움직이거나 0.5초가 지나면 길을 다시 찾습니다. 깔린 길은 그대로 이어집니다. |
| 목적지가 바뀜(대화가 끝나 미션 단계가 넘어감) | 새 출입문으로 빛이 옮겨 가고 길을 새로 깝니다. |
| 사용자가 길 안내를 끔 | 길만 숨기고 출입문 빛은 남깁니다. 다음 접속에도 꺼진 채로 둡니다. |

## 횡단보도로 건너기

마을의 걸을 바닥은 NPC와 같이 씁니다. **Tools → A305 → Wayfinding → Build**가 TownScene의 차도(`StreetBase/Road_*`, `CrossRoadWing`)에 `NavMeshModifier`(구역 `Road`)를, 횡단보도 줄무늬를 묶은 자리마다 `Environment/PedestrianCrossings`에 `NavMeshModifierVolume`(구역 `Crosswalk`)을 두고 바닥을 다시 굽습니다. 줄무늬 끝에서 연석까지 남은 차도(약 0.5m)도 함께 덮습니다. 두 구역의 프로젝트 비용은 보통 바닥과 같은 1이라 NPC 걸음은 그대로고, 길 안내만 차도 1m를 인도 50m(`roadCrossingCost`)로 쳐서 차도를 건너야 하면 횡단보도로 돌아갑니다. 구역 상자 이름은 사물 단어 자동 연결(`Crosswalk`·`Zebra`로 시작하면 '횡단보도' 단어)에 걸리지 않게 짓습니다.

차도나 횡단보도를 옮기거나 새로 그리면 Builder를 다시 실행합니다. 새 차도 바닥 이름이 위 이름과 다르면 Builder의 차도 이름 목록에 함께 추가합니다.

## 새 미션에서 목적지 알리기

1. 지금 찾아갈 장소의 서버 장소 코드를 `GuideDestination.Shared.Show(placeCode)`로 알립니다. 안내를 그만둘 때는 `GuideDestination.Shared.Clear()`를 부릅니다.
2. 장소 코드와 Unity 씬 이름은 [World 모듈](../World/README.md)의 `PlaceScenes`로 맞춥니다. 마을에 그 장소 씬으로 이어지는 `SpaceDoorway`가 있으면 그 문 앞이 출입문이 됩니다.
3. 씬이 없는 장소(경찰서처럼 마을 안 건물)는 지금 안내하지 않습니다. 필요해지면 출입문 앞 자리를 표시하는 방법을 이 모듈에 더합니다.

## 의존성

| 대상 | 용도 |
| --- | --- |
| `A305.World` | 출입문(`SpaceDoorway`), 장소 코드와 씬 이름(`PlaceScenes`), 이동 중인지(`SpaceTravel.IsTraveling`), 바닥으로 보지 않을 차(`LoopingCar`) |
| `A305.Player` | 플레이어 위치(`PlayerMover`) |
| `A305.Dialogue` | 대화 중인지(`DialogueController.IsTalking`) |
| Unity AI(NavMesh) | 길 찾기. 바닥은 [Npc 모듈](../Npc/README.md)의 `NpcWalkArea`가 등록합니다. |
| uGUI, TextMeshPro | 길 안내 켜기·끄기 버튼 |

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `GuideDestination.Shared.Show(placeCode)`, `Clear()` | 안내할 장소를 알리거나 안내를 그만둡니다. |
| `GuideRoutePreference.ShowsRoute`, `Changed` | 사용자가 도로 위 길 안내를 켜 두었는지 읽고 바꿉니다. |
| `DestinationGuide.FindEntrance(scene, placeCode)` | 그 씬에서 장소로 들어가는 출입문 앞. 없으면 null |

## Builder

**Tools → A305 → Wayfinding → Build**(`Scripts/Editor/Wayfinding/WayfindingBuilder.cs`)가 차례로 다음을 합니다.

1. 프로젝트 NavMesh 구역 3·4번에 `Road`·`Crosswalk`를 비용 1로 둡니다(`ProjectSettings/NavMeshAreas.asset`).
2. 설정 에셋을 없을 때만 만들고, 빛 그림·재질(`Art/Wayfinding`)도 없을 때만 만듭니다. 그림은 흰색에 진하기만 담고 색은 설정의 야광색으로 칠합니다. 길과 바닥 빛 원은 덮어 칠하고(Alpha), 빛기둥은 빛을 더합니다(Additive).
3. 기능 프리팹 `Prefabs/Wayfinding/DestinationGuide.prefab`의 길·빛 묶음과 참조를 맞춥니다. 빛 모양을 두는 `EntranceGlow/Visual` 아래는 없을 때만 만듭니다.
4. TownScene의 차도·횡단보도 구역을 표시하고, `Systems`에 길 안내를 하나 두어 플레이어·대화와 이은 뒤 걸을 바닥을 다시 굽습니다(`NpcLifeBuilder.RebakeWalkArea`).
5. 첫날 미션의 크게 본 지도에 길 안내 켜기·끄기 버튼을 둡니다(`FirstDayMissionBuilder.ApplyRouteToggle`).

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Wayfinding.Tests` | `Assets/Tests/EditMode/Wayfinding/` | 보일지 판단(대화·이동 중 숨김, 도착하면 길만 거둠, 길을 꺼도 출입문 빛 유지), 길을 다시 찾는 때와 새로 까는 때, 길 자르기·점 채우기, 켜기·끄기 저장과 버튼 글자, 공용 목적지, 마을 길 안내 연결과 장소마다 출입문 찾기, 안내·구역 상자에 사물 단어가 붙지 않음, 차도 구역 표시, NPC 비용 유지, 문과 먼 인도 사이의 길이 횡단보도에서만 차도를 건너는지(`TownGuideRouteTests`) |
| `A305.Wayfinding.PlayModeTests` | `Assets/Tests/PlayMode/Wayfinding/` | 마을을 불러와 목적지를 알리면 출입문이 빛나고 길이 플레이어 발밑에서 출입문까지 이어지는지, 도착·목적지 변경·목적지 지움·길 끄기 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
