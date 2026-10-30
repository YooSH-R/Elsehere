# Weather 모듈

마을처럼 하늘이 보이는 실외 장소에서 예보한 날씨를 보여 줍니다. 날씨를 정하는 일은 하지 않고, 받은 서버 값(예: `RAIN`)에 맞는 날씨 정의를 찾아 하늘·해·환경광·안개·구름과 날씨 효과를 바꿉니다. 지금은 첫날 연계 미션(`Mission` 모듈)이 서버 응답의 `weather`와 대신할 날씨를 넘겨 `WeatherPresenter.Show`를 부릅니다.

| 날씨 | 서버 값 | 화면 |
| --- | --- | --- |
| 맑음 | `CLEAR` | 씬에 디자인된 하늘·조명 그대로. 하늘을 덮던 구름과 효과는 걷습니다. |
| 흐림 | `CLOUDY` | 회색 하늘, 약한 해와 옅은 그림자, 가까워진 안개, 하늘을 덮는 구름 |
| 비 | `RAIN` | 흐림보다 어두운 하늘과 조명, 더 가까운 안개, 먹구름과 플레이어 머리 위를 따라오는 빗방울 |

날씨가 바뀌면 설정한 시간(기본 1.5초) 동안 지금 모습에서 새 모습으로 섞습니다. 목록에 없는 서버 값이 오면 대신할 날씨(첫날 미션은 `raining`에 따라 `RAIN` 또는 `CLEAR`)를 보여 줍니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `WeatherDefinition` | 날씨 하나. 서버 값(`code`), 하늘·조명 모습(`look`) 또는 씬 디자인 유지(`keepDesignedLook`), 하늘을 덮는 구름 여부(`coversSky`), 머리 위를 따라오는 효과 프리팹(`effect`) |
| `WeatherLook` | 하늘 색(`Elsehere/SummerSky`의 `_Zenith`·`_Horizon`), 해의 색·세기·그림자 진하기, 환경광 세 색, 안개 색·거리, 구름 색. 두 모습을 섞는 `Lerp`를 제공합니다. |
| `WeatherSettings` | 장소에서 쓸 날씨 정의 목록, 섞는 시간, 효과 높이. 서버 값으로 날씨를 찾는 `Resolve`를 제공합니다. |
| `WeatherPresenter` | 씬의 해(Directional Light)·`RenderSettings`·하늘 머티리얼·구름에 날씨 모습을 적용하고, 날씨 효과를 처음 쓸 때 만들어 플레이어 위를 따라가게 합니다. |

`WeatherPresenter`는 깨어날 때 씬의 하늘·조명을 읽어 맑은 날 모습으로 기억합니다. 하늘과 구름 머티리얼은 사본을 만들어 물들이므로 `SummerSky.mat`·`CloudWhiteV2.mat` 원본은 바뀌지 않고, 컴포넌트가 사라지면 원본 머티리얼로 되돌립니다. 날씨를 떠날 때 효과는 새로 만들지만 않게 멈추고, 섞기가 끝나면 거둡니다.

## 데이터 에셋

| 에셋 | 내용 |
| --- | --- |
| `Assets/Data/Weather/TownWeather.asset` | 마을 날씨 목록(맑음·흐림·비), 섞는 시간(`transitionSeconds`), 효과 높이(`effectHeightMeters`) |
| `Assets/Data/Weather/Clear.asset`·`Cloudy.asset`·`Rain.asset` | 날씨 정의. 흐림·비의 색과 세기는 여기서 조정합니다. |
| `Assets/Prefabs/Weather/Rain.prefab` | 비의 효과. `Drops` 자식의 파티클이 아래로 떨어집니다. |

### 새 날씨 추가

눈처럼 하늘·조명·효과로 표현되는 날씨는 코드를 고치지 않고 추가합니다.

1. 효과가 필요하면 `Assets/Prefabs/Weather/`에 프리팹을 만듭니다(예: 눈송이 파티클). 파티클은 `Play On Awake`를 끄고 `Simulation Space`를 World로 둡니다. 날씨 표시가 켜고 끄며 플레이어 머리 위로 옮깁니다.
2. **Assets → Create → A305 → Weather → Definition**으로 `Assets/Data/Weather/`에 정의를 만들고 서버 값(예: `SNOW`), 모습, 하늘 덮기, 효과를 채웁니다.
3. `TownWeather.asset`의 날씨 목록에 넣습니다.
4. 서버가 그 값을 보내면 마을에 보입니다. 서버 값 이름은 백엔드와 먼저 맞춥니다.

천둥번개처럼 번쩍임·소리가 필요한 효과는 그 동작을 맡는 컴포넌트를 효과 프리팹에 붙입니다. 날씨 표시 코드는 고치지 않습니다.

## 의존성

없습니다. 서버·미션을 참조하지 않는 재사용 모듈입니다. 날씨를 정하는 쪽(`A305.Mission`의 `FirstDayMissionController`)이 이 모듈을 참조합니다.

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `WeatherPresenter.Show(code, fallbackCode)` | 서버 값의 날씨로 섞어 바꿉니다. 목록에 없으면 `fallbackCode`의 날씨를 쓰고, 그것도 없으면 경고만 남기고 그대로 둡니다. |
| `WeatherPresenter.ShowImmediately(code)` | 섞지 않고 바로 바꿉니다. 프레임이 흐르지 않는 Editor 미리보기에 씁니다. |
| `WeatherPresenter.Current` | 마지막으로 보여 준 날씨 정의. 바꾸기 전에는 null(씬 디자인)입니다. |
| `WeatherSettings.Resolve(code, fallbackCode)` | 목록에서 서버 값의 날씨를 찾습니다. 대소문자를 구분하지 않습니다. |

## Builder와 미리보기

- **Tools → A305 → Weather → Build Town Weather**(`Scripts/Editor/Weather/TownWeatherBuilder.cs`)가 TownScene의 `Lighting/TownWeather`에 `WeatherPresenter`를 두고 해·`SummerClouds`·`OvercastClouds`·플레이어와 날씨 목록을 연결한 뒤 첫날 미션에 연결합니다. 날씨 목록·정의·비 효과 프리팹·흐림 구름은 없을 때만 만들고, 이미 있으면 조정한 값과 더한 날씨를 그대로 둡니다. **Mission → Build First Day Journey**와 **World → Build Spaces**도 마을을 연결할 때 같은 연결을 적용합니다.
- **Tools → A305 → Weather → Weather Preview** 창(`TownWeatherPreview.cs`)
  - Play 중: 목록의 날씨마다 버튼이 생기고, 누르면 서버 없이 그 자리에서 날씨가 섞여 바뀝니다. 서버 상태를 다시 읽으면(씬 진입·NPC 응답·대화 종료) 서버 예보로 돌아갑니다.
  - Play 밖: **Capture Town Weather Preview**로 목록의 날씨를 TownScene 같은 자리에서 찍습니다. 같은 기능이 메뉴 **Tools → A305 → Weather → Capture Town Weather Preview**에도 있으며, batchmode에서는 환경변수 `TOWN_WEATHER_PREVIEW_OUTPUT`에 저장 폴더를 지정합니다. 시작할 때 저장하지 않은 씬을 저장할지 묻고, 다 찍으면 디스크의 TownScene을 다시 열어 날씨를 입힌 씬이 저장되지 않게 합니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Weather.Tests` | `Assets/Tests/EditMode/Weather/` | 두 모습 섞기, 서버 값으로 날씨 찾기(대소문자·빈 값·모르는 값은 대신할 날씨), 새 정의만 더해도 찾아지는지, 맑음은 씬 디자인 유지, 마을 에셋의 흐림·비가 맑은 날보다 어둡지만 밤처럼 까맣지 않고 비가 흐림보다 어둡고 안개가 가까운지, 비만 빗방울 효과가 있는지 |
| `A305.Weather.PlayModeTests` | `Assets/Tests/PlayMode/Weather/` | 비·흐림·맑음 전환 뒤 해·환경광·안개·하늘·구름·효과, 코드에 없는 새 날씨(눈) 정의의 모습과 효과, 모르는 값의 대신할 날씨, 시간에 따라 섞이는지, 효과가 플레이어 위를 따라가는지, 원본 하늘·구름 에셋이 바뀌지 않고 되돌려지는지 |
| `A305.Mission.Tests` | `Assets/Tests/EditMode/Mission/` | 서버 `weather` 읽기와 `raining`으로 정하는 대신할 날씨 |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | TownScene의 첫날 미션에 날씨 표시와 그 참조가 모두 연결됐는지 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
