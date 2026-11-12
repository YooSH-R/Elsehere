# 택시 승차와 목적지 이동

택시 중심에서 수평 반경 8m 안에 들어오면 방향이나 인도 위치와 관계없이 [택시 타기]가 표시된다. 버튼이나 E 키를 누르면 택시가 현재 위치에 정차하고 바로 뒷좌석에 타서 기사와 대화가 시작된다. 반경을 벗어나면 버튼이 사라지며, 누르는 순간에도 거리를 다시 확인한다. 목적지를 말하기 전에는 정차하며, 서버가 대화 문맥으로 목적지를 알아들으면 출발한다. 신호와 다른 차량을 따르고 도착한 건물 쪽 인도에 내려준다.

택시에 타면 바로 기사와 대화하므로 [택시 타기] 위에 남은 대화 수를 보인다(서버에서 게스트 대화 횟수 제한을 켠 경우). 게스트가 대화를 다 쓰면 버튼 대신 [가입하고 계속 대화하기]를 보이고, 다른 이유로 새 대화를 시작할 수 없으면 버튼 대신 이유를 보인다. 튜토리얼 중이거나 다른 대화·상호작용 중에는 탑승 버튼을 숨긴다([Npc 모듈](../../frontend/UnityClient/Assets/Scripts/Npc/README.md#택시)).

| 장소 | 요청 예 | 실내 진입 |
| --- | --- | --- |
| 호텔 | Could you take me to the hotel, please? | HotelScene |
| 카페 | The cafe, please. | CafeScene |
| 드럭스토어 | Take me to the drugstore, please. | ShopScene |
| 집 | I'd like to go home. | HomeScene |
| 우체국 | Take me to the post office, please. | PostOfficeInterior |
| 햄버거 가게 | The burger shop, please. | MapleDinerInterior |
| 타운 은행 | Could you take me to the bank? | TownBankInterior |
| 빨래방 | Take me to the laundromat, please. | SudsLaundromatInterior |
| 꽃집 | The flower shop, please. | BloomFlowersInterior |
| 이발소 | Take me to the barber shop, please. | MainStreetBarberInterior |
| 경찰서 | Take me to the police station, please. | PoliceStationScene |

택시는 건물 앞 보도에 내려준다. 실내로 자동 이동하지 않으며, 하차 후 사용자가 문으로 걸어 들어간다. 택시는 영업시간과 상관없이 데려다주고, 영업시간이 아닌 장소는 문 앞에서 들어가지 못한다([World 모듈의 영업시간](../../frontend/UnityClient/Assets/Scripts/World/README.md#영업시간)). 북쪽 기존 베이커리·오락실·서점·생활용품점·찻집은 현재 택시 목적지에 포함하지 않는다.

목적지는 정해진 단어가 아니라 서버의 의도 판정(백엔드 `TaxiRide`)이 최근 대화 6줄까지 보고 고른다. 매 턴 부르는 의도 판정에 목적지 선택 칸을 더한 것이라 LLM 호출이 늘지 않는다. 한국어·반말("호텔로 가주세요", "카페 가자"), 앞에서 말한 장소를 가리키는 말("거기 가 주세요"), 기사가 권한 곳에 대한 답("The first one.")도 알아듣는다. 정보 질문(Where is the hotel?), 부정, 고르지 않은 여러 장소, 택시가 가지 않는 곳(북쪽 빵집·공원·오락실 등, 마을 밖)으로는 출발하지 않는다. 이때 기사는 그곳은 가지 않는다고 분명히 말하고 가까운 하차 장소(공원·빵집·오락실·서점은 우체국, 생활용품점·찻집은 식당, 아이스크림 트럭은 경찰서)를 권하며, 승객이 그 제안을 받아들이면("Okay") 그곳으로 출발한다. 기사가 갈 곳을 권할 때도 택시가 가는 곳 안에서만 고른다. 서버는 목적지를 턴 응답의 `taxiDestination`으로 보낸다([MVP API 명세 CONV-03](../api/mvp-api.md)). 판정 문구를 고칠 때는 `ai/tools/taxi_destination_eval.py` 개발셋으로 확인한다.

## 승차 중 동작

- 카메라는 뒷좌석 오른쪽의 `PassengerView`를 따라간다. 정차 중 기사는 손님 쪽으로 고개를 돌리고 주행 중에는 앞을 본다.
- 대화창은 다른 대화와 같이 화면 아래 전체 폭에, 도움 카드는 오른쪽 위에 놓는다.
- "목적지를 기사에게 알려주세요"·"호텔로 이동 중" 같은 운행 안내(`TaxiTripStatus`)는 대화창 이름 줄 바로 위 가운데에 붙인다. 화면 위쪽에 두면 왼쪽 위 목표 카드와 겹친다.
- 가는 도중에 다른 곳을 말하면 도착해 내리기 전까지는 새 목적지로 바꾼다.
- 작별 인사나 "That's all"로는 대화가 끝나지 않는다(택시에서는 대화가 끝나면 내리기 때문). 먼저 내리려면 대화를 종료한다.
- 도착하면 대화를 종료하고 정차한다. 하차 전까지 화면 상단의 작은 상태 표시로 진행을 알리고, 가운데 피드백 로딩 카드와 [나중에 확인하기]는 그 자리에서 사용할 수 있다. 하차가 끝나면 상단 안내가 사라진다.
- 먼저 대화를 종료하면 현재 위치의 도로 바깥쪽에 내려준다. 목적지를 말하지 않았다면 승차 전 위치로 돌아간다.
- 하차 후 시야각, 카메라, 플레이어 이동 컴포넌트와 충돌을 복구한다.

## 구성과 확장

`TaxiCabinBuilder.Build` (**Tools → A305 → World → Apply Taxi Cabin**)는 기존 마을의 택시만 갱신한다. 다른 거리 묶음이나 공용 대화 프리팹을 재생성하지 않는다. `TownStreetLifeBuilder`도 같은 구성을 적용한다.

- `TaxiRide`: 승차·목적지 수신·목적지 정차·하차.
- `LoopingCar` / `CarLoopRoute`: 차선·신호·주행. 승객은 자기 택시의 장애물에서 제외한다.
- `CarLaneBypass`: 남쪽 차로의 기존 주차 트럭을 중앙 쪽으로 천천히 비켜가고 유턴 전에 원래 차선으로 돌아온다. 이 남쪽 구간은 택시와 경찰서 순찰차가 지난다. 비켜 가는 차는 맞은편 차로와 겹치므로 우회를 시작하는 z=-12부터 남쪽 유턴을 돌아 다시 z=-12까지는 한 번에 한 차만 들어간다. 다른 차가 그 구간에 있으면 남행 z=-12에서, 순찰차는 주차 칸에서 기다린다.
- `DialogueController.TaxiDestinationChosen`: 턴 응답의 `taxiDestination`(서버가 알아들은 목적지)을 택시에 전한다. 서버 오류·무례한 발화·복습 대화는 목적지를 보내지 않는다.
- `TaxiDestination`: 장소 코드·화면 이름·차로 정차 위치·보도 하차 위치. `TaxiRide.destinations`에 저장한다.

새 건물은 명칭·영문 별칭, 실제 출입구, 안전한 하차 지점과 운행 경로를 함께 확인한다. 현재는 본길의 동쪽·서쪽 건물만 지원한다. 옆길 건물은 이름만 추가하지 말고 접근 경로를 먼저 구성한다. `TaxiCabinBuilder`의 목적지 구성과 백엔드 `TaxiRide`의 장소 이름표(평가 도구 `taxi_destination_eval.py`도)를 함께 갱신한다. 기존 공간 4곳은 `SpaceDoorway`에서 위치를 구하고 거리 매장 7곳은 `TownStorefrontBuilder.Storefronts`의 장소 코드·정차·하차 메타데이터를 사용한다. 우체국은 가로수를 피해 문보다 북쪽 1.5m에, 경찰서는 문 앞 주차 트럭을 피해 남쪽 3m에 정차한다. 경찰서 순찰차도 같은 목록(`TaxiCabinBuilder.FillDestinations`)에서 경찰서만 빼고 출동 장소로 쓴다.

## 검증

목적지 판정은 백엔드 `TaxiRideTest`·`TaxiRideTurnTest`와 평가 도구, 턴 응답의 목적지 전달은 EditMode `ServerConversationTests`, 실제 마을의 승차·주행·목적지 변경·도착·하차는 PlayMode에서 검증한다. 택시 PlayMode 테스트는 확인용 대본으로 대화하고, 서버가 보낼 목적지는 `TaxiRide.ChooseDestination`으로 넣는다. 자세·카메라·배치는 Unity 렌더로 별도 확인한다.
