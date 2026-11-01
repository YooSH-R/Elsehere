# 메인스트리트 외관 아트

> 이미지·ZIP·검수 기록은 Git에서 제외한 로컬 자료다. 아래 `artifacts/...` 경로는 로컬 보관본을 가리킨다.

`Assets/Scenes/Production/TownScene.unity`에 미국 소도시의 벽돌 상점과 주택을 단순한 3D 게임 형태로 배치한다. `Assets/Scenes/Sandbox/TownMainstreetSandbox.unity`는 아트와 크기 시험용으로 유지한다. 캐릭터 전체 키는 **1.6m로 확정**한다. 리틀 멍 v7의 머리 60%·몸 40%를 유지하며, 현재 캐릭터는 완성 모델이 아닌 비율 확인용 대체물이다.

Production TownScene 플레이 시작 화면 (`artifacts/unity/production-town/01-player-start.png`, 로컬 보관)

해당 로컬 이미지는 Production 씬을 Play로 실행한 Unity 카메라 렌더다. 카페와 닫힌 빈 건물 (`artifacts/unity/production-town/05-cafe-and-closed-building.png`, 로컬 보관), 배치 전체 (`artifacts/unity/production-town/04-layout-overview.png`, 로컬 보관), 주거 구간과 공원 (`artifacts/unity/production-town/07-residential-park.png`, 로컬 보관), [치수·검수 방법](mainstreet-sandbox.md)도 참고한다. 목표는 따뜻한 낮의 작은 미국 마을이다. 코럴 벽·크림색 몰딩·초록 차양·둥근 수목을 공유하고, 영업을 나타내는 진열창과 조명은 실제 이용 장소에만 둔다.

## Production 장소 구분과 편집

| 구분 | 배치와 표현 | 기능 |
| --- | --- | --- |
| 이용 장소 | 남동쪽 집, 서쪽 카페, 동쪽 호텔. 집은 포치, 카페는 초록 CAFE 차양, 호텔은 세로 간판 | 기존 HomeDoor·CafeDoor·HotelDoor와 실내 왕복 연결 유지 |
| 빈 건물 | 카페 북쪽 크림색 2층 건물. 셔터와 CLOSED 표시, 비발광 창 | 출입 Trigger·상품·영업 조명 없음 |
| 배경 주거동 | 북쪽 거리와 남서쪽 주택. 코럴·버터·세이지 3색, 1~3층, 닫힌 주거 문 | 배경 외벽 Collider만 사용 |
| 휴식 공간 | 집 앞 대화 마당, 북동쪽 8×8m 공원 | 기존 행인 2명의 ID와 대화 참조 유지. 벤치에는 앉을 수 있고, 분수는 장식이며 물 효과는 미구현 |

`Environment/TownMainstreet` 아래에 공통 거리 프리팹과 `BackgroundBuildings`를 둔다. 기존 이용 건물 루트는 출입구의 참조를 보존하고, 도형 외관을 비활성화한 뒤 새 외관을 중첩한다. Systems·UI의 대화·사물 호버·학습·미션·튜토리얼 연결은 유지한다. 식당·공항의 임시 영업 간판은 현재 배치에서 제거하며 새 이동 장소를 추가하지 않는다.

- `Assets/Prefabs/World/TownProduction/`: StreetBase·StreetFurnishing·NorthStreetDressing·StreetGardens, HomeExterior·HotelExterior, 배경 건물·공원·경계와 1.6m 캐릭터 외형을 저장한다.
- 배경 건물의 `GroundFloor8m`은 4m, `UpperFloor8m`은 3m다. 상층을 Y=4, 7…에 중첩하고 RoofCap·RoofInset을 같은 높이만큼 올린다. 완성 건물을 세로로 늘리지 않는다. 1·2·3층 프리팹 높이는 4.4·7.4·10.4m다. 원점은 전면 바닥 중앙이며 +Z가 도로 방향이다.
- 카페는 `CafeBlenderKit/Buildings/Cafe1F~3F`를 사용한다. 집과 호텔은 기존 외관 조립을 승격한 프리팹이며, 호텔의 층수 자동 조립은 아직 제공하지 않는다.
- 북쪽 길은 Z=70까지 이어지고 경계 정원은 Z=69에 둔다. 확장할 때 경계 프리팹을 이동하고 도로·보도·건물 부지를 함께 연장한다. 동서 경계는 폭 14m 전용 프리팹을 써 수목 비율을 유지한다.
- 카메라·색 보정은 `Assets/Data/World/TownProduction/`의 전용 에셋으로 조정한다. 1.6m 캐릭터 외형은 기존 이동·NPC 루트의 `TownVisual`에 중첩한다. 플레이어 외형 프리팹은 기존 시점별 가림 컴포넌트를 재사용한다.

**Tools → A305 → World → Apply Production Town Design**은 초기 도형 씬에 디자인을 이식한다. 이미 `TownMainstreet`가 있으면 배치·외관을 덮어쓰지 않는다. 이후 편집은 Scene과 Prefab Mode에서 한다. **Verify Production Town Design**은 집 퇴장 후 튜토리얼 행인의 접근·제안·수락, Play 카메라, 보행, 대화 시점, 세 장소의 출입구 왕복을 확인하고 `artifacts/unity/production-town`에 화면과 검수 결과를 저장한다. 검수는 Play 중 튜토리얼 진행과 행인 휴식 기록을 초기화한다. 검수 중 이동·카메라 변경은 Play 종료 시 버린다.

**Apply Town Follow Camera**는 TownScene의 3인칭 카메라를 캐릭터보다 키가 큰 사람이 바로 뒤에서 내려다보는 구도로 맞춘다. `Data/World/TownProduction/CameraSettings.asset`의 거리 4.2m·pitch 16도·조준 높이 1.3m를 저장하며, 캐릭터가 조준 높이를 따로 정했으면 그 값을 쓴다. Sandbox 전용 아트 검토 카메라는 바꾸지 않는다. 대화를 시작하면 기존과 같이 상대를 바라보는 1인칭으로 바뀌고 V키 1인칭 전환도 유지한다.

튜토리얼 행인은 집의 `HomeDoor/ArrivalPoint`에서 약 5.4m 떨어진 보도에 둔다. **Repair Tutorial Passerby Placement**는 집 도착 지점을 기준으로 바깥쪽 2m·문 기준 오른쪽 5m에 행인의 위치와 방향만 다시 저장한다. 집·보도를 이동하면 감지 반경 8m 안에 있는지뿐 아니라 행인과 도착 지점 사이에 장애물이 없는지도 Play에서 확인한다. 기존 `PasserbyTests.LeavingHome_TutorialPasserbyApproachesAndOffersWithoutDecline`를 유지해 집에서 나온 플레이어에게 실제로 다가와 제안하는지 검증한다.

집 내부는 [HomeScene 실내 디자인](home-interior.md), 호텔 로비는 [HotelScene 로비 디자인](hotel-interior.md)을 적용한다. CafeScene의 실내 아트 교체와 최종 캐릭터 리깅은 별도 작업이다. Editor 검수는 Web 빌드의 FPS·메모리 성능 보증이 아니다.

## 공통 규격

| 대상 | 기준 |
| --- | --- |
| 캐릭터 | 높이 1.6m, 머리 약 0.96m·몸 약 0.64m, 검수용 최대 헤어 폭 1.6m |
| 카페·상점 | 각각 8×8m, 높이 7.4m |
| 호텔 | 10×10m, 높이 7.4m |
| 집 | 8×8m, 벽 높이 4m + 박공지붕 1.2m |
| 문 / 비움 영역 | 유효 폭 2m·높이 2.2m / 전면 3×2.4m |
| 카메라 | 거리 6.2m, 피벗 2.5m, 눈높이 약 1.074m, pitch 6°, 세로 FOV 55° |
| 확장 | 6m 차도 + 양쪽 4m 보도, 북쪽 보행 구간 40m 추가 |

카페는 [Blender 카페 모듈](cafe-blender-kit.md)의 공통 1층·상층·지붕 프리팹으로 조립한다. 집·호텔은 기존 벽·바닥·문 개구부의 Collider를 유지하고 장식 외관을 덧붙인다. 창은 불투명한 유리·진열 면으로 표현한다. Sandbox의 GeneralStore·DistantShop 시안에는 진열창이 있으므로 Production의 빈 건물에는 사용하지 않는다. 이용 건물 입구 너머의 카운터는 임시 배경이며 실내 기능 완성을 의미하지 않는다.

## 재사용 모듈

카페의 신규 부품 15종, 1층·반복 상층·지붕과 1·2·3층 예시는 `Assets/Prefabs/World/CafeBlenderKit/`에 별도로 둔다. [층수 변경과 Blender 편집 방법](cafe-blender-kit.md)을 따른다. 아래 MainstreetArt 모듈은 나머지 건물과 거리에서 계속 사용한다.

`Assets/Prefabs/World/MainstreetArt/`의 공통 프리팹을 복제 배치한다. 같은 부품의 내부 메시를 재질별로 묶고, 메시·머티리얼을 공유한다. 치수에 따라 달라지는 차양·띠장·지붕은 Builder가 조립한다.

| 모듈 | 조합 방식 |
| --- | --- |
| TallWindowV2 | 폭 1.62m·높이 약 2.28m. 상점 상층·측면 공용. 전면은 큰 창 두 개와 넓은 벽 여백 |
| WarmShopWindowV2 | 폭 1.85m·높이 2.5m. 따뜻한 진열 면·선반·펜던트 조명 |
| GreenAwningStripV2 / StripedAwningStripV2 | 폭 0.55m 단위의 경사진 천과 물결 끝단. 카페 글자는 차양 면에 배치 |
| HotelArchV2 | 호텔의 곡선 차양 메시. 남색 세로 간판은 가까운 모서리에 배치 |
| WindowFlowerBox / FlowerPlanter | 창 아래 화단 / 보도 화분. 같은 잎·꽃 메시 사용 |
| GardenTreeV2 / GardenBedV2 | 약 4.35m 나무 / 0.95×1.5m 화단. 큰 수관과 꽃을 반복 조합, 나무 몸통 Collider 사용 |
| StreetLampV2 | 약 3.4m. 발광 전구와 범위 5m의 따뜻한 Point Light |
| MarketCrateV2 / ChalkboardV2 | 상점 진열 상자 / 카페 입간판. 문 앞 비움 영역 밖에 배치 |
| DistantShopV2 3종 | 폭 6m·깊이 5.2m, 높이 5.8~7.3m의 닫힌 배경 외관. 북쪽 확장부에 배치 |
| ParkBench / CafeTable | 작은 몸 비율에 맞춘 가구. 벤치는 폭 1.6m를 반씩 나눈 두 자리에 앉는다. 앉는 면 치수는 [Mission 모듈](../../frontend/UnityClient/Assets/Scripts/Mission/README.md#의자벤치소파-앉기)에 있다 |
| PicketFence2m | 주택 앞마당 가장자리에 연결 |
| SidewalkTile2m | 2×2m 보도 반복. 교차로 끝은 길이를 절반으로 조정 |
| FireHydrant | 빨간 소화전 한 종류 재사용 |
| KenneyDeliveryTruck | CC0 모델 한 개를 길이 3.8m의 장식 차량으로 배치 |

카페는 코럴 벽·초록 차양, 상점은 버터색 벽·크림/코럴 줄무늬 차양, 호텔은 코럴 벽·남색 세로 간판, 집은 크림 외벽·남색 박공지붕·포치로 구분한다. 노출 모서리는 둥글린다. 벽돌은 개별 메시를 쌓지 않고 월드 좌표의 옅은 색 변화로 표현하며, 사실적인 균열·오염·직물 질감은 생략한다. 주택의 기존 UpperWindow와 공용 화분 등은 계속 재사용한다.

| 재질 | sRGB HEX |
| --- | --- |
| CoralMasonry / ButterStuccoV2 | #E58B63 / #F0D29C |
| WarmCream / DeepGreen | #F2DFBA / #326B57 |
| SignNavy / HomeCreamV2 | #293E57 / #E8DFBF |
| GardenGreen / HoneyWood | #79A331 / #A77C4C |
| SkyGlassV2 / SandPaving | #4F9EB4 / #D8C9AD |
| RoadBlueV2 / WindowAmberGlow | #667FA7 / #FFCE76 |

일반 머티리얼은 URP/Lit, Metallic 0, Smoothness 0.18로 시작한다. 벽돌·수관·도로는 `Elsehere/SoftMasonry`의 확산광·그림자·환경광·안개를 사용한다. 셰이더의 색·그림자·깊이 패스는 같은 머티리얼 상수 버퍼를 공유한다. 구름은 Unlit, 하늘은 `Elsehere/SummerSky` 그라데이션으로 표현한다.

기본 낮 조명은 Directional Light 하나(회전 48°, -35°, 0°; 강도 1.7)와 하늘·수평·지면의 환경광이며, 가로등과 매장의 Point Light를 더한다. Sandbox 전용 Volume은 exposure 0, contrast 10, saturation 8, bloom 0.15, tonemapping None이다. 선형 안개는 42~125m이며 피사계심도는 사용하지 않는다. 이 수치는 이미지에서 측정한 물성값이나 Web 성능 검증값이 아닌 Unity 아트 시작값이다.

위 하늘·조명·안개는 맑은 날 기준이다. TownScene의 `Lighting/TownWeather`(`WeatherPresenter`)가 예보 날씨에 따라 해의 색·세기·그림자, 환경광, 안개, `SummerSky` 색, 구름 색을 섞어 바꾸고, 하늘을 덮는 날씨에는 `OvercastClouds`를, 비에는 `Assets/Prefabs/Weather/Rain.prefab` 빗방울을 띄운다. 날씨마다 `Assets/Data/Weather/`의 정의 에셋(`Clear`·`Cloudy`·`Rain`)이 있고 `TownWeather.asset` 목록에 모여 있으며, 맑음은 씬에 디자인한 값을 그대로 쓴다. 하늘·구름 머티리얼은 실행 중 사본을 물들이므로 원본 에셋은 바뀌지 않는다. 구성과 미리보기 방법은 [Weather 모듈](../../frontend/UnityClient/Assets/Scripts/Weather/README.md)에 있다.

## 가로등과 매장 조명

`Assets/Prefabs/World/MainstreetArt/StreetLampV2.prefab`의 `LanternLight`가 주변을 비추며, 전구의 `LanternAmberV2` 재질이 발광한다. 같은 프리팹을 배치하면 외형과 광원을 함께 재사용한다.

| 항목 | 기본값 |
| --- | --- |
| 광원 | Realtime Point Light, 가로등마다 1개 |
| 위치 | 프리팹 바닥에서 Y=2.88m |
| 색상 / 강도 / 범위 | #FFE1B3 / 4 / 5m |
| 그림자 | 개별 가로등 그림자 끔 |
| 현재 Sandbox | 가로등 8개, 활성 매장 조명 15개 |

`LanternLight`의 Light Inspector에서 색·강도·범위를 조절한다. **Tools → A305 → World → Apply And Verify Streetlights**는 기존 외형을 유지하면서 누락된 광원을 추가하고, 전구 발광을 활성화한 뒤 Play에서 검수한다. 이미 있는 광원의 조절값은 덮어쓰지 않는다. 일반 Sandbox Builder로 다시 만들 때도 같은 공통 프리팹을 사용한다.

도로·벽·수관의 `Elsehere/SoftMasonry`는 URP Forward/Forward+의 추가 광원을 받아 거리와 표면 방향에 따라 밝아진다. 가로등마다 그림자 맵을 만들지 않으므로 사물의 가림까지 정확하게 표현하지는 않는다. Web 프레임 시간은 별도로 측정해야 한다.

가로등·카페 벽등·펜던트는 `Elsehere/WarmLamp`를 사용한다. 중심 밝기 1.2, 가장자리 0.45, 색상 RGB (1, 0.73, 0.3)으로 둥근 노란 테두리가 남도록 표현한다. 전구 내부 Point Light가 전구 표면을 다시 비춰 하얗게 날아가는 현상을 막는다. 창 배경의 `WindowAmberGlow`는 약한 발광을 유지한다. 상층 창과 원경 창에는 광원을 반복 추가하지 않는다.

| 대상 | 재사용 프리팹 | 광원 강도 / 범위 |
| --- | --- | --- |
| 일반 상가·호텔 벽등 | `MainstreetArt/WallLampWarm` | 0.35 / 2.8m |
| Blender 카페 벽등 | `CafeBlenderKit/Parts/WallLamp` | 0.35 / 2.8m |
| 카페 쇼윈도 | `CafeBlenderKit/Parts/WindowShop2m` | 0.1 / 1.8m |
| 일반 상가 쇼윈도 | `MainstreetArt/WarmShopWindowV2` | 0.1 / 1.8m |

매장 조명의 색상은 RGB (1, 0.88, 0.65)이며 모두 실시간 Point Light, 그림자 없음이다. `FixtureLight`를 Inspector에서 조절한다. 일반 씬 Builder는 이미 조절한 광원 값을 유지한다. **Tools → A305 → World → Apply And Verify All Fixture Lights**는 위 강도·범위·위치 프리셋을 명시적으로 다시 적용하고 기존 Sandbox의 벽등을 공통 프리팹으로 연결한다. 이 메뉴는 수동으로 조절한 매장 광원 값을 프리셋으로 덮어쓴다. 건물 크기와 배치는 유지한다.

낮의 전체 조명 (`artifacts/unity/mainstreet-fixtures/01-day-fixtures.png`, 로컬 보관), 카페 조명 검수 (`artifacts/unity/mainstreet-fixtures/03-cafe-fixtures-on.png`, 로컬 보관), 호텔 입구 조명 검수 (`artifacts/unity/mainstreet-fixtures/05-hotel-fixtures-on.png`, 로컬 보관)는 실제 Unity Play 렌더다. 검수 폴더의 OFF/ON은 **매장 Point Light만** 끄고 켠 비교이며, 가로등과 전구 발광은 계속 켜져 있다. 어두운 화면은 빛의 도달 범위를 보기 위해 검수 중에만 햇빛을 낮춘 것이며 저장된 마을의 시간대나 조명 설정을 바꾸지 않는다.

검수는 광원 개수·전구 발광 상태, 도로와 보도 표본의 점등 전후 밝기 변화를 확인한다. Mobile 렌더 검수는 Editor에서 Mobile URP 에셋을 임시 적용하는 것이며 실제 모바일 기기·WebGL 브라우저 성능 검증과는 구분한다. 조명·셰이더 시각 변경은 단위 테스트 대신 Play 렌더 비교로 확인한다.

## 제작과 변경

1. `TownBlockoutSettings`에서 건물·캐릭터 치수를 확인한다. 기준 키는 1.6m다.
2. `MainstreetArtKit`은 메시·재질·장식 프리팹을 없을 때만 생성한다. 이미 디자인한 프리팹이나 머티리얼을 재실행으로 덮어쓰지 않는다.
3. `MainstreetArtBuilder`가 주요 건물, `MainstreetStreetDressing`이 원경 외관과 정원, `MainstreetAtmosphere`가 조명·하늘·후처리를 조립한다. `MainstreetReferenceKit`은 새 외관 공통 부품을 제공한다.
4. **Tools → A305 → World → Build And Verify Reference Look**은 전용 카메라 거리·피벗·각도와 색 보정을 위 값으로 적용하고 씬을 재생성한다. 기존 수치를 유지하려면 **Build Mainstreet Sandbox**를 사용한다. 씬에서 직접 움직인 위치는 사라지므로 유지할 배치는 Builder에 반영한다.
5. **Verify Mainstreet Sandbox**로 Play 검수와 실제 카메라 캡처를 실행한다. 새 아트 시점은 `08-art-street.png`, `09-art-cafe.png`다.

원형 프리팹의 생김새를 바꿀 때는 Inspector에서 해당 프리팹을 수정한다. Builder 코드만 바꿔도 이미 만들어진 프리팹이 자동 교체되는 구조가 아니다. 구조 변경은 별도 이름의 모듈로 시험한 뒤 사용처를 변경한다. 늘어나는 건물은 이미 있는 창·화단·차양·가로 시설을 우선 조합한다.

## 외부 에셋과 Tripo

차량은 [Kenney Car Kit 3.1](https://kenney.nl/assets/car-kit)의 `truck-flat.fbx`와 `colormap.png`를 사용한다. 공식 라이선스는 **CC0**이며 원본 `License.txt`와 함께 `Assets/ThirdParty/KenneyCarKit/`에 둔다. 원본 파일을 보존하고 팀용 프리팹에서 크기·URP 머티리얼·Collider를 연결한다. 차량 주행 기능은 포함하지 않는다.

### 도입한 무료 소품

Tiny Treats의 둥근 모서리·단순한 형태·그라데이션 atlas는 현재 마을의 무광 재질과 조합하기 쉬워 우선 사용한다. 아래 수량은 팩 전체 수량이 아니라 프로젝트에 가져온 모델 수다. 공식 무료판과 동봉 라이선스가 모두 CC0인 모델만 사용하며, 유료 `.blend` 원본과 데모는 가져오지 않는다.

| 원본 팩 | 가져온 모델 | 사용 위치 |
| --- | --- | --- |
| [Bakery Interior 1.1](https://tinytreats.itch.io/bakery-interior) | 12개: 커피 머신·계산대·컵·머그·카운터·진열장·테이블·의자·트레이·접시·케이크 받침·가격표 | 카페 집기와 테이블 |
| [Baked Goods 1.0](https://tinytreats.itch.io/baked-goods) | 5개: 빵·크루아상·머핀·초콜릿 케이크·도넛 | 진열대와 단어 학습용 소품 후보 |
| [House Plants 1.0](https://tinytreats.itch.io/house-plants) | 3개: 몬스테라·산세베리아·다육이 화분 | 상점 입구·창가·카운터 |
| [Pretty Park 1.0](https://tinytreats.itch.io/pretty-park) | 5개: 분수·벤치·직선/모서리 생울타리·휴지통 | 향후 광장·보행 공간 |

**Project → Assets/Prefabs/World/TinyTreats/**에서 원하는 프리팹을 씬으로 드래그한다. 개별 소품 25개와 `CafeCounterDisplay.prefab` 조합 예시가 있다. 카운터 예시는 개별 프리팹 8개를 중첩해 재사용한다. `Assets/Scenes/Sandbox/WorldPropsSandbox.unity`에서 팩별 실물을 비교할 수 있다. 마을 메인스트리트에 자동 배치하거나 기존 건물을 교체하지 않는다.

원본 FBX·텍스처·라이선스는 `Assets/ThirdParty/TinyTreats/<팩>/`, 조절용 URP 재질은 `Assets/Art/World/TinyTreats/`에 둔다. 출처는 [CREDITS](../../frontend/UnityClient/Assets/ThirdParty/CREDITS.md)에서 관리한다. 모델은 원본 연결을 유지한 자식으로 두고 크기와 위치만 조절한다. 프리팹 루트 Scale은 1, 피벗은 바닥 중앙이며 BoxCollider가 있다. 빵 등 학습 대상은 아직 ObjectHover 단어 데이터에 연결하지 않았다. 분수 물은 정적 메시이며 물 흐름 효과는 없다.

기준 크기는 카운터 높이 0.9m, 테이블 높이 0.65m, 의자 전체 높이 0.8m, 커피 머신 높이 0.5m, 분수 폭 2.6m, 생울타리 높이 0.9m다. 음식·화분은 대두 캐릭터 옆에서도 읽히도록 조금 크게 잡은 시작값이다. 앉기·손에 잡기·NPC 도달 범위는 아직 검증하지 않았다. 각 프리팹의 실제 XYZ 치수와 삼각형 수는 검수 목록 (`artifacts/unity/free-props/verification.txt`, 로컬 보관)에 있다.

**Tools → A305 → World → Build And Preview Tiny Treats Props**는 없는 재질·프리팹만 만들고 비교용 Sandbox를 재생성한다. 기존 프리팹과 조합 예시의 수동 편집은 유지한다. 유리 재질은 투명 URP/Lit, 나머지는 Metallic 0·Smoothness 0.18이다. 카운터 조합 (`artifacts/unity/free-props/CafeCounterDisplay.png`, 로컬 보관), 공원 소품 (`artifacts/unity/free-props/PrettyPark.png`, 로컬 보관)은 실제 Unity Editor 렌더이며 생성형 이미지가 아니다. 검수는 25개 모델의 URP 참조·바닥 피벗·콜라이더·실측 치수와 화면을 확인한다. WebGL 성능·기능 연결 완료를 의미하지 않는다.

### 미국 소도시 거리와 생활 소품

학습자가 미국 소도시에 온 상황을 연습하므로, 거리도 미국 소도시처럼 보이게 한다. 우산을 사는 상점도 미국에서 흔한 드럭스토어로 정했다. **Apply American Street Details**는 도로 전체에 이어지는 중앙선, 교차로의 횡단보도·정지선·신호등과 MAPLE AVE 이름판, 속도 제한 25·버스 정류장·보행자 횡단 표지판, 주차 미터기, 신문 판매함, 파란 우체통, 드럭스토어 앞 ICE 냉동고와 OPEN 네온, 카페 입간판을 둔다. 성조기는 호텔 외벽에 하나만 단다. 이 소품들은 Builder가 기본 도형으로 만든다.

**Apply Town Street Life**는 거리가 비어 보이지 않게 집 남쪽 잔디에 아이스크림 트럭 광장을 따로 두고, 카페 앞 파라솔 테라스, 본길을 도는 택시·승용차, 자전거 거치대, 꽃과 동물을 둔다. 택시와 승용차는 교차로 신호를 지키며 본길을 돌고, 기존 픽업트럭만 길가에 세워 둔다. 택시는 인도에서 세워 운전석의 기사와 대화할 수 있다. 테이블·의자·휴지통·컵은 위 Tiny Treats 프리팹을 그대로 쓴다. 외부 모델의 출처와 라이선스는 [CREDITS](../../frontend/UnityClient/Assets/ThirdParty/CREDITS.md#마을-거리)에서 관리한다.

### 나머지 후보의 적용 순서

아래는 미도입 후보에 대한 디자인 판단이다. 건물은 미국 소도시의 벽돌 상가·크림색 몰딩·녹색 차양과 반복 층 구조를 유지하고, 필요한 소품부터 보강한다.

| 후보 | 적용 판단 |
| --- | --- |
| [Styloo Village](https://styloo.itch.io/vil) | 무료 건물 7개와 CC0는 확인했다. 원경 건물 후보로 남기고, 기존 층고·문 폭·재질을 맞추기 전에는 메인 상가를 교체하지 않는다. |
| [Tiny Treats Homely House](https://tinytreats.itch.io/homely-house) | 주택 구역을 확장할 때 검토한다. 상가 층 반복 모듈을 대체하는 용도로 사용하지 않는다. |
| [Kenney Fantasy Town](https://kenney.nl/assets/fantasy-town-kit), [Quaternius Medieval Village](https://quaternius.com/packs/medievalvillagemegakit.html), [Slavic Medieval Village](https://assetstore.unity.com/packages/3d/environments/fantasy/slavic-medieval-village-free-modular-environment-kit-167010) | 중세 테마 건물은 현재 미국 소도시 상가 외관의 우선순위에서 제외한다. 모듈 조합 구조만 참고할 수 있다. |
| [KayKit Forest Nature](https://kaylousberg.itch.io/kaykit-forest) | 북쪽 확장 구간의 녹지가 필요할 때 추가한다. 현재 가로수 교체보다 반복 배치 수와 성능을 먼저 확인한다. |
| [Styloo Cozy Farm](https://styloo.itch.io/farm) | 농장 건물보다 우편함·상자 등 부족한 거리 소품을 선택한다. |
| Cloudy Creates [Street Essentials](https://cloudycreates.itch.io/street-props-vol01-street-essentials), [Garden Props](https://cloudycreates.itch.io/lowpoly-garden-props), [Cafe Essentials](https://cloudycreates.itch.io/cafe-essentials-vol2) | 필요한 소품이 기존 프리팹에 없을 때 Starter에서 선택한다. 무료/상업 이용 표기와 CC0는 같지 않으므로 도입할 파일의 동봉 라이선스를 확인한다. 현재 미도입이다. |
| Tiny Treats [Fun Playground](https://tinytreats.itch.io/fun-playground), [Pleasant Picnic](https://tinytreats.itch.io/pleasant-picnic) | 놀이터·피크닉 구역의 배치가 정해지면 같은 계열 소품으로 확장한다. |
| KayKit [Furniture Bits](https://kaylousberg.itch.io/furniture-bits), [Restaurant Bits](https://kaylousberg.itch.io/restaurant-bits), Tiny Treats [Charming Kitchen](https://tinytreats.itch.io/charming-kitchen) | 카페 실내 좌석·주방을 구현할 때 부족한 구성만 추가한다. |
| [Cozy Cartoon Pack 2](https://assetstore.unity.com/packages/3d/props/interior/interiors-free-cozy-cartoon-pack-2-366624), [Synty Coffee Shop](https://syntystore.com/products/polygon-coffee-shop) | 현재 집기와 역할이 겹쳐 후순위다. 공식 무료 제공은 확인했으나 각각 별도 Asset Store/스토어 라이선스이며 CC0 원본으로 취급하지 않는다. |

건물 전체나 규격 벽·창문을 Tripo로 반복 생성하지 않는다. 이 모듈들은 Unity에서 치수와 재질을 조절할 수 있어 이번 외관 제작에 **Tripo 생성 크레딧은 0**이다. 추가 계정 연결이나 유료 패키지 없이 현재 씬을 열 수 있다.

이후 조형이 복잡한 공통 랜드마크 소품이 필요하면 대표 모델 하나만 후보로 정한다. 이미지·목표 폴리곤·출력 형식을 먼저 고정하고 CLI 설치·로그인 상태와 dry-run 비용을 확인한 다음 승인된 한 건만 생성한다. Unity에서 크기·재질·Collider를 검사한 뒤 색상·배치 변형으로 재사용한다. 캐릭터 전체를 NPC별로 새로 생성하는 방식은 사용하지 않는다.

## 적용 범위

메인스트리트 Sandbox는 외관 아트와 크기·통행·카메라 검토용이다. 북쪽 기존 건물 세 동의 실내와 층별 상가는 별도의 `NorthBlockInteriorSandbox`에서 검토하고 `TownScene`에 배치한다. 복층 베이커리, 공용 계단을 쓰는 오락실·서점, 외부 계단을 쓰는 생활용품점·찻집으로 동선을 구분한다. 위치·출입 방식·Builder 메뉴는 [World 모듈](../../frontend/UnityClient/Assets/Scripts/World/README.md#북쪽-상가), 무료 Fab 에셋과 사용 조건은 [CREDITS](../../frontend/UnityClient/Assets/ThirdParty/CREDITS.md)에서 관리한다. 다른 배경 주거동과 호텔 앞 드럭스토어 구역은 이 작업의 대상이 아니다.

부지 마커와 임시 경계 벽은 아트 화면에서 숨기고 경계 Collider는 유지한다. 공유 메시와 프리팹 사용만으로 Web 성능을 검증했다고 판단하지 않는다. Web 적용 전에 꽃·창문 반복 수, Renderer·재질 수와 프레임 시간을 측정해 단순화한다.
