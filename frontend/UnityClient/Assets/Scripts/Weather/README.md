# Weather 모듈

마을처럼 하늘이 보이는 실외 장소에서 예보한 날씨를 보여 줍니다. 날씨를 정하는 일은 하지 않고, 받은 서버 값(예: `RAIN`)에 맞는 날씨 정의를 찾아 하늘·해·환경광·안개·구름과 날씨 효과를 바꿉니다. 지금은 첫날 연계 미션(`Mission` 모듈)이 서버 응답의 `weather`와 대신할 날씨를 넘겨 `WeatherPresenter.Show`를 부릅니다.

| 날씨 | 서버 값 | 화면 |
| --- | --- | --- |
| 맑음 | `CLEAR` | 씬에 디자인된 하늘·조명 그대로. 하늘을 덮던 구름과 효과는 걷습니다. |
| 흐림 | `CLOUDY` | 회색 하늘, 약한 해와 옅은 그림자, 가까워진 안개, 하늘을 덮는 구름 |
| 비 | `RAIN` | 흐림보다 훨씬 어두운 하늘과 조명(그림자가 거의 없음), 6~48m의 가까운 안개, 먹구름과 플레이어 둘레 28m에 쏟아지는 가늘고 긴 빗줄기 |

날씨가 바뀌면 설정한 시간(기본 1.5초) 동안 지금 모습에서 새 모습으로 섞습니다. 목록에 없는 서버 값이 오면 대신할 날씨(첫날 미션은 `raining`에 따라 `RAIN` 또는 `CLEAR`)를 보여 줍니다.

## 밤낮

다섯 장소(마을·집·카페·호텔·상점)는 사용자별 게임 시각(`A305.World`의 `GameClock`, 게임 하루 = 실제 10분)에 맞춰 낮·노을·밤으로 바뀝니다. 각 씬의 `GameClockDaylight`가 매 프레임 게임 시각을 그 장소의 `DaylightView`에 넘깁니다.

| 시각 | 마을 | 실내 |
| --- | --- | --- |
| 한낮(6:30~18:30) | 씬 디자인(+날씨) 그대로. 해는 한낮에 디자인한 방향이고 아침·저녁에는 지평선 가까이 반대쪽에서 비춥니다. | 씬 디자인 그대로 |
| 새벽·노을(6:00·19:00 앞뒤 30분) | 낮과 밤 사이로 섞이며 지평선·햇빛이 노을빛으로 물들고 그림자가 길어집니다. 흐림·비에서는 노을이 옅습니다. | 낮과 밤 사이 |
| 밤(19:30~5:30) | 어두운 남색 하늘, 별과 달(흐림·비에는 별이 숨음), 약한 달빛, 어두운 환경광·안개·구름, 2배로 밝힌 가로등·상점 불빛과 2.2배로 밝힌 창빛·전구. 입점한 상점·숙소의 창은 안에서 불을 켠 것처럼 따뜻하게 빛납니다(주택·빈 건물 창은 그대로). | 불을 켠 방처럼 밝습니다. 창으로 드는 빛은 80% 세기의 따뜻한 실내등 색 방 조명으로 바꾸고(상점처럼 이 빛 하나로 밝히는 방도 어두워지지 않음), 낮빛 보조광·환경광은 90%로 조금만 낮추며 실내등은 2배, 창밖 유리는 밤하늘 색 |

밤 모습은 날씨 모습 위에 입히므로 밤에 비가 오면 어두운 밤비가 됩니다. 발광·창 유리 재질은 사본을 칠해 원본 에셋을 바꾸지 않습니다.

입점한 상점·숙소는 마을에서 들어갈 수 있는 내 숙소(집)·카페·호텔·드럭스토어와, 배경 건물 가운데 매장 실내(`Interior`)가 있는 건물(북쪽 거리 매장·경찰서·북쪽 상가)입니다. 이 건물들의 창 유리(`DayNight.asset`의 입점 건물 창 재질)는 주택·빈 건물과 같은 재질이어서, 입점 건물에만 따로 사본을 끼워 밤에 실내등 색으로 칠하고 빛을 내게 합니다.

UI는 씬의 시각을 따르지 않습니다. 유리 UI(`A305/UI/Glass`)는 흐린 뒤 화면을 비추므로 씬이 어두워지면 함께 어두워집니다. 그래서 `WeatherPresenter`·`InteriorDaylight`가 밤인 정도(0~1)를 `A305.Theme`의 `UiBackdropLift`로 알리고, 유리는 그만큼 뒤 화면을 낮 밝기로 밝혀 낮과 같은 밝기로 보입니다. 장소를 떠나면 0으로 돌립니다([Theme 모듈의 흐린 뒤 화면](../Theme/README.md#흐린-뒤-화면)).

## 구성

| 파일 | 역할 |
| --- | --- |
| `WeatherDefinition` | 날씨 하나. 서버 값(`code`), 하늘·조명 모습(`look`) 또는 씬 디자인 유지(`keepDesignedLook`), 하늘을 덮는 구름 여부(`coversSky`), 머리 위를 따라오는 효과 프리팹(`effect`) |
| `WeatherLook` | 하늘 색(`Elsehere/SummerSky`의 `_Zenith`·`_Horizon`), 해의 색·세기·그림자 진하기, 환경광 세 색, 안개 색·거리, 구름 색. 두 모습을 섞는 `Lerp`를 제공합니다. |
| `WeatherSettings` | 장소에서 쓸 날씨 정의 목록, 섞는 시간, 효과 높이. 서버 값으로 날씨를 찾는 `Resolve`를 제공합니다. |
| `WeatherPresenter` | 씬의 해(Directional Light)·`RenderSettings`·하늘 머티리얼·구름에 날씨 모습을 적용하고, 날씨 효과를 처음 쓸 때 만들어 플레이어 위를 따라가게 합니다. 마을의 `DaylightView`로서 그 위에 시각을 입히고 밤 불빛(`lamps`, 발광 재질)과 입점 건물(`litBuildings`)의 창을 밝힙니다. |
| `DaylightSettings` | 해 뜨고 지는 시각, 새벽·노을 길이, 한밤중 모습, 노을 색, 밤 불빛 배율, 밤에 밝힐 발광 재질·창밖 유리 재질, 입점 건물 창 재질과 불 켠 창 색·빛, 실내 밤 배율과 방 조명 색. 밤인 정도·노을인 정도·해 방향·별을 계산합니다. Unity 객체 없이 테스트합니다. |
| `DaylightView` | 게임 시각을 받아 장소의 낮·밤을 보이는 컴포넌트의 공통 부모(`ShowTime`) |
| `InteriorDaylight` | 실내의 `DaylightView`. 창으로 드는 빛(`windowLights`)·낮빛 보조광(`fills`)·실내등(`lamps`)·환경광·창밖 유리를 시각에 맞춥니다. 하늘상자 환경광을 쓰는 방은 환경광 세기로 줄입니다. |
| `NightLights` | 마을·실내가 함께 쓰는 등 세기와 발광·창 유리·입점 건물 창 재질 사본 관리 |

`WeatherPresenter`는 깨어날 때 씬의 하늘·조명을 읽어 맑은 날 모습으로 기억합니다. 하늘과 구름 머티리얼은 사본을 만들어 물들이므로 `SummerSky.mat`·`CloudWhiteV2.mat` 원본은 바뀌지 않고, 컴포넌트가 사라지면 원본 머티리얼로 되돌립니다. 날씨를 떠날 때 효과는 새로 만들지만 않게 멈추고, 섞기가 끝나면 거둡니다.

## 데이터 에셋

| 에셋 | 내용 |
| --- | --- |
| `Assets/Data/Weather/TownWeather.asset` | 마을 날씨 목록(맑음·흐림·비), 섞는 시간(`transitionSeconds`), 효과 높이(`effectHeightMeters`) |
| `Assets/Data/Weather/Clear.asset`·`Cloudy.asset`·`Rain.asset` | 날씨 정의. 흐림·비의 색과 세기는 여기서 조정합니다. |
| `Assets/Prefabs/Weather/Rain.prefab` | 비의 효과. `Drops` 자식의 파티클이 아래로 떨어집니다. |
| `Assets/Data/Weather/DayNight.asset` | 다섯 장소가 함께 쓰는 밤낮 설정(`DaylightSettings`). 해 뜨고 지는 시각, 한밤중 모습, 노을 색, 밤 불빛 배율과 밤에 밝힐 재질 목록, 입점 건물 창 색·빛, 실내 밤 밝기를 여기서 조정합니다. |

### 새 날씨 추가

눈처럼 하늘·조명·효과로 표현되는 날씨는 코드를 고치지 않고 추가합니다.

1. 효과가 필요하면 `Assets/Prefabs/Weather/`에 프리팹을 만듭니다(예: 눈송이 파티클). 파티클은 `Play On Awake`를 끄고 `Simulation Space`를 World로 둡니다. 날씨 표시가 켜고 끄며 플레이어 머리 위로 옮깁니다.
2. **Assets → Create → A305 → Weather → Definition**으로 `Assets/Data/Weather/`에 정의를 만들고 서버 값(예: `SNOW`), 모습, 하늘 덮기, 효과를 채웁니다.
3. `TownWeather.asset`의 날씨 목록에 넣습니다.
4. 서버가 그 값을 보내면 마을에 보입니다. 서버 값 이름은 백엔드와 먼저 맞춥니다.

천둥번개처럼 번쩍임·소리가 필요한 효과는 그 동작을 맡는 컴포넌트를 효과 프리팹에 붙입니다. 날씨 표시 코드는 고치지 않습니다.

## 의존성

없습니다. 서버·미션·게임 시계를 참조하지 않는 재사용 모듈입니다. 날씨를 정하는 쪽(`A305.Mission`의 `FirstDayMissionController`)과 게임 시각을 넘기는 쪽(`A305.World`의 `GameClockDaylight`)이 이 모듈을 참조합니다.

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `WeatherPresenter.Show(code, fallbackCode)` | 서버 값의 날씨로 섞어 바꿉니다. 목록에 없으면 `fallbackCode`의 날씨를 쓰고, 그것도 없으면 경고만 남기고 그대로 둡니다. |
| `WeatherPresenter.ShowImmediately(code)` | 섞지 않고 바로 바꿉니다. 프레임이 흐르지 않는 Editor 미리보기에 씁니다. |
| `WeatherPresenter.Current` | 마지막으로 보여 준 날씨 정의. 바꾸기 전에는 null(씬 디자인)입니다. |
| `WeatherSettings.Resolve(code, fallbackCode)` | 목록에서 서버 값의 날씨를 찾습니다. 대소문자를 구분하지 않습니다. |
| `DaylightView.ShowTime(gameMinutes)` | 1일차 0시부터 센 게임 시각(분)의 낮·밤을 보입니다. 시각을 받기 전에는 씬 디자인의 낮입니다. |

## Builder와 미리보기

- **Tools → A305 → Weather → Build Town Weather**(`Scripts/Editor/Weather/TownWeatherBuilder.cs`)가 TownScene의 `Lighting/TownWeather`에 `WeatherPresenter`를 두고 해·`SummerClouds`·`OvercastClouds`·플레이어와 날씨 목록을 연결한 뒤 첫날 미션에 연결합니다. 날씨 목록·정의·비 효과 프리팹·흐림 구름은 없을 때만 만들고, 이미 있으면 조정한 값과 더한 날씨를 그대로 둡니다. **Mission → Build First Day Journey**와 **World → Build Spaces**도 마을을 연결할 때 같은 연결을 적용합니다.
- **Tools → A305 → Weather → Apply Heavy Rain**은 이미 만든 비 날씨(`Rain.asset`)의 모습과 비 효과(`Rain.prefab`)의 빗줄기를 쏟아지는 비 값으로 바꿉니다. 맑음·흐림과 날씨 목록은 그대로 둡니다. 새로 만들 때의 기본값도 같은 값입니다.
- **Tools → A305 → Weather → Build Day Night**(`DaylightBuilder.cs`)가 다섯 장소 씬에 `GameClockDaylight`를 두고, 마을은 날씨 표시에 밤낮 설정과 가로등·상점·창 불빛(`LanternLight`·`FixtureLight`·`InteriorLight`), 입점 건물(집·카페·호텔·드럭스토어와 `BackgroundBuildings` 가운데 켜져 있고 `Interior`가 있는 건물)을, 실내는 `Lighting/InteriorDaylight`에 Directional Light(창으로 드는 빛)·이름에 `Bounce`가 든 Point Light(낮빛 보조광)·나머지 Point Light(실내등)를 연결합니다. 순찰차 경광등처럼 따로 켜고 끄는 빛은 넣지 않습니다. `DayNight.asset`은 없을 때만 만들고, 씬의 조명·재질 값은 바꾸지 않습니다. **World → Build Spaces**와 **Build Town Weather**도 같은 연결을 적용합니다.
- **Tools → A305 → Weather → Weather Preview** 창(`TownWeatherPreview.cs`)
  - Play 중: 새벽·한낮·노을·밤 버튼은 게임 시각을 그 시각까지 앞으로 흘립니다(서버에 연결돼 있으면 다음 신호에 서버 시각으로 돌아감). 마을에서는 목록의 날씨마다 버튼도 생겨, 누르면 서버 없이 그 자리에서 날씨가 섞여 바뀝니다. 서버 상태를 다시 읽으면(씬 진입·NPC 응답·대화 종료) 서버 예보로 돌아갑니다.
  - Play 밖: **Capture Town Weather Preview**로 TownScene의 날씨별(한낮)·시각별(맑음) 모습과 밤비, 실내 네 곳의 한낮·밤을 같은 자리에서 찍습니다. 같은 기능이 메뉴 **Tools → A305 → Weather → Capture Town Weather Preview**에도 있으며, batchmode에서는 환경변수 `TOWN_WEATHER_PREVIEW_OUTPUT`에 저장 폴더를 지정합니다. 시작할 때 저장하지 않은 씬을 저장할지 묻고, 다 찍으면 디스크의 TownScene을 다시 열어 날씨·시각을 입힌 씬이 저장되지 않게 합니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Weather.Tests` | `Assets/Tests/EditMode/Weather/` | 두 모습 섞기, 서버 값으로 날씨 찾기(대소문자·빈 값·모르는 값은 대신할 날씨), 새 정의만 더해도 찾아지는지, 맑음은 씬 디자인 유지, 마을 에셋의 흐림·비가 맑은 날보다 어둡지만 밤처럼 까맣지 않고 비가 흐림보다 어둡고 안개가 가까운지, 비만 빗방울 효과가 있는지. 밤낮: 해 뜨고 지는 시각의 밤인 정도·노을인 정도(다음 날도 같음), 한낮은 그대로·한밤은 어둡되 날씨 안개 유지, 노을 붉기와 흐린 날 옅은 노을, 맑은 밤에만 별, 한낮·아침·저녁·해 질 때·한밤의 해 방향, `DayNight.asset`의 밤 밝기·불빛 배율·불 켠 실내 밝기·재질 목록·입점 건물 창 |
| `A305.Weather.PlayModeTests` | `Assets/Tests/PlayMode/Weather/` | 비·흐림·맑음 전환 뒤 해·환경광·안개·하늘·구름·효과, 코드에 없는 새 날씨(눈) 정의의 모습과 효과, 모르는 값의 대신할 날씨, 시간에 따라 섞이는지, 효과가 플레이어 위를 따라가는지, 원본 하늘·구름 에셋이 바뀌지 않고 되돌려지는지. 밤낮: 마을의 한밤 하늘·해·환경광·별, 가로등·발광 재질, 한낮 복귀, 노을의 낮은 해, 밤비, 실내의 창으로 드는 빛·보조광·실내등·창밖 유리·환경광(하늘상자 포함)과 밤에도 불 켠 방 밝기, 원본 재질 복구, 밤에 유리 UI 뒤 화면을 밝히고 떠나면 되돌리는지, 입점 건물 창만 밤에 빛나고 주택 창은 그대로인지 |
| `A305.Mission.Tests` | `Assets/Tests/EditMode/Mission/` | 서버 `weather` 읽기와 `raining`으로 정하는 대신할 날씨 |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | TownScene의 첫날 미션에 날씨 표시와 그 참조가 모두 연결됐는지, 다섯 장소에 게임 시각 연결과 밤낮 표시·설정, 실내의 창으로 드는 빛, 마을 가로등 연결, 입점 건물(상점·숙소, 내 집 포함)만 창 불빛에 연결되고 주택·빈 건물은 빠지는지 |
| `A305.World.PlayModeTests` | `Assets/Tests/PlayMode/World/` | `GameClockDaylight`가 매 프레임 게임 시각을 넘기는지 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
