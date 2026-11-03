# 택시 승차와 목적지 이동

마을 인도에서 [택시 세우기]를 누르면 택시가 손님 옆 길가에 서면서 바로 뒷좌석에 타고 기사와 대화가 시작된다. 택시가 다가와 서는 모습은 보이지 않는다. 목적지를 말하기 전에는 정차하며, 서버가 수락한 무례하지 않은 발화에서 명확한 이동 요청을 찾으면 출발한다. 신호와 다른 차량을 따르고 도착한 건물 쪽 인도에 내려준다.

택시를 세우면 바로 기사와 대화하므로 게스트에게는 [택시 세우기] 위에 남은 대화 수를 보이고, 새 대화를 시작할 수 없으면(게스트 대화를 다 씀 등) 버튼 대신 이유를 보인다([Npc 모듈](../../frontend/UnityClient/Assets/Scripts/Npc/README.md#택시)).

| 장소 | 예시 요청 | 실내 진입 |
| --- | --- | --- |
| 호텔 | Could you take me to the hotel, please? | HotelScene |
| 카페 | The cafe, please. | CafeScene |
| 드럭스토어 | Take me to the drugstore, please. | ShopScene |
| 집 | I'd like to go home. | HomeScene |
| 우체국 | Take me to the post office, please. | PostOfficeInterior |
| 메이플 다이너 | The diner, please. | MapleDinerInterior |
| 타운 은행 | Could you take me to the bank? | TownBankInterior |
| 빨래방 | Take me to the laundromat, please. | SudsLaundromatInterior |
| 꽃집 | The flower shop, please. | BloomFlowersInterior |
| 이발소 | Take me to the barber shop, please. | MainStreetBarberInterior |
| 경찰서 | Take me to the police station, please. | PoliceStationInterior |

택시는 건물 앞 보도에 내려준다. 실내로 자동 이동하지 않으며, 하차 후 사용자가 문으로 걸어 들어간다. 택시는 영업시간과 상관없이 데려다주고, 영업시간이 아닌 장소는 문 앞에서 들어가지 못한다([World 모듈의 영업시간](../../frontend/UnityClient/Assets/Scripts/World/README.md#영업시간)). 북쪽 기존 베이커리·오락실·서점·생활용품점·찻집은 현재 택시 목적지에 포함하지 않는다.

한국어 장소·이동 요청도 일부 지원한다. 목적지는 `TaxiDestinationRequest`의 보수적인 규칙으로 찾으므로 모든 자연어를 이해하는 의도 모델은 아니다. 정보 질문, 부정, 여러 장소 중 선택하지 않은 문장, 지원하지 않는 장소로는 출발하지 않는다. 목적지 처리에 별도 LLM 호출을 추가하지 않는다.

## 승차 중 동작

- 카메라는 뒷좌석 오른쪽의 `PassengerView`를 따라간다. 정차 중 기사는 손님 쪽으로 고개를 돌리고 주행 중에는 앞을 본다.
- 대화창은 다른 대화와 같이 화면 아래 전체 폭에, 도움 카드는 오른쪽 위에 놓는다.
- "목적지를 기사에게 알려주세요"·"호텔로 이동 중" 같은 운행 안내(`TaxiTripStatus`)는 대화창 이름 줄 바로 위 가운데에 붙인다. 화면 위쪽에 두면 왼쪽 위 목표 카드와 겹친다.
- 목적지를 확정한 뒤에는 도착할 때까지 바꾸지 않는다.
- 도착하면 대화를 종료하고 기존 피드백 흐름을 이어간다. 서버 피드백 대기 중에는 정차한다.
- 먼저 대화를 종료하면 현재 위치의 도로 바깥쪽에 내려준다. 목적지를 말하지 않았다면 승차 전 위치로 돌아간다.
- 하차 후 시야각, 카메라, 플레이어 이동 컴포넌트와 충돌을 복구한다.

## 구성과 확장

`TaxiCabinBuilder.Build` (**Tools → A305 → World → Apply Taxi Cabin**)는 기존 마을의 택시만 갱신한다. 다른 거리 묶음이나 공용 대화 프리팹을 재생성하지 않는다. `TownStreetLifeBuilder`도 같은 구성을 적용한다.

- `TaxiRide`: 승차·발화 수신·목적지 정차·하차.
- `LoopingCar` / `CarLoopRoute`: 차선·신호·주행. 승객은 자기 택시의 장애물에서 제외한다.
- `CarLaneBypass`: 남쪽 차로의 기존 주차 트럭을 중앙 쪽으로 천천히 비켜가고 유턴 전에 원래 차선으로 돌아온다. 현재 이 남쪽 구간까지 운행하는 차량은 택시뿐이다. 다른 차량의 운행 구간을 넓히면 양방향 통행 우선권도 검토한다.
- `DialogueController.UserTurnAccepted`: 발화 확정 후 월드 상호작용. 서버 오류·무례한 발화·NPC의 대화 종료는 출발을 유발하지 않는다.
- `TaxiDestinationRequest`: 지원 장소 이름과 이동 요청 구분.
- `TaxiDestination`: 장소 코드·화면 이름·차로 정차 위치·보도 하차 위치. `TaxiRide.destinations`에 저장한다.

새 건물은 명칭·영문 별칭, 실제 출입구, 안전한 하차 지점과 운행 경로를 함께 확인한다. 현재는 본길의 동쪽·서쪽 건물만 지원한다. 옆길 건물은 이름만 추가하지 말고 접근 경로를 먼저 구성한다. `TaxiCabinBuilder`의 목적지 구성과 `TaxiDestinationRequest`의 별칭을 함께 갱신한다. 기존 공간 4곳은 `SpaceDoorway`에서 위치를 구하고 거리 매장 7곳은 `TownStorefrontBuilder.Storefronts`의 장소 코드·정차·하차 메타데이터를 사용한다. 우체국은 가로수를 피해 문보다 북쪽 1.5m에, 경찰서는 문 앞 주차 트럭을 피해 남쪽 3m에 정차한다. 경찰서 순찰차도 같은 목록(`TaxiCabinBuilder.FillDestinations`)에서 경찰서만 빼고 출동 장소로 쓴다.

## 검증

목적지 판별과 서버 발화 확정은 EditMode, 실제 마을의 승차·주행·도착·하차는 PlayMode에서 검증한다. 택시 PlayMode 테스트는 확인용 대본을 사용한다. 자세·카메라·배치는 Unity 렌더로 별도 확인한다.
