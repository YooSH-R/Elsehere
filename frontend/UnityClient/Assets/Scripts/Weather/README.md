# Weather 모듈

마을처럼 하늘이 보이는 실외 장소에서 예보한 날씨(맑음·흐림·비)를 보여 줍니다. 날씨를 정하는 일은 하지 않고, 받은 날씨에 맞춰 하늘·해·환경광·안개·구름과 빗방울을 바꿉니다. 지금은 첫날 연계 미션(`Mission` 모듈)이 서버 응답으로 날씨를 정해 `WeatherPresenter.Show`를 부릅니다.

| 날씨 | 화면 |
| --- | --- |
| 맑음 | 씬에 디자인된 하늘·조명 그대로. 흐림·비 구름은 걷습니다. |
| 흐림 | 회색 하늘, 약한 해와 옅은 그림자, 가까워진 안개, 하늘을 덮는 구름 |
| 비 | 흐림보다 어두운 하늘과 조명, 더 가까운 안개, 먹구름과 플레이어 머리 위를 따라오는 빗방울 |

날씨가 바뀌면 설정한 시간(기본 1.5초) 동안 지금 모습에서 새 모습으로 섞습니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `WeatherKind` | 맑음(`Clear`)·흐림(`Cloudy`)·비(`Rain`) |
| `WeatherLook` | 날씨 하나의 하늘 색(`Elsehere/SummerSky`의 `_Zenith`·`_Horizon`), 해의 색·세기·그림자 진하기, 환경광 세 색, 안개 색·거리, 구름 색. 두 모습을 섞는 `Lerp`를 제공합니다. Unity 객체 없이 테스트합니다. |
| `WeatherSettings` | 흐림·비의 `WeatherLook`, 섞는 시간, 빗방울 높이. 맑음은 씬 디자인을 쓰므로 두지 않습니다. |
| `WeatherPresenter` | 씬의 해(Directional Light)·`RenderSettings`·하늘 머티리얼·구름·빗방울에 날씨를 적용합니다. |

`WeatherPresenter`는 깨어날 때 씬의 하늘·조명을 읽어 맑은 날 모습으로 기억합니다. 하늘과 구름 머티리얼은 사본을 만들어 물들이므로 `SummerSky.mat`·`CloudWhiteV2.mat` 원본은 바뀌지 않고, 컴포넌트가 사라지면 원본 머티리얼로 되돌립니다. 흐림·비 구름(`OvercastClouds`)과 맑은 날 구름은 같은 사본 하나로 함께 물듭니다.

## 데이터 에셋

`Assets/Data/Weather/TownWeather.asset`에서 흐림·비의 색과 세기, 섞는 시간(`transitionSeconds`), 빗방울 높이(`rainHeightMeters`)를 조정합니다. 필드 초기값은 에셋을 처음 만들 때 들어가는 값이며, 이후 수정은 에셋에서 합니다.

## 의존성

없습니다. 서버·미션을 참조하지 않는 재사용 모듈입니다. 날씨를 정하는 쪽(`A305.Mission`)이 이 모듈을 참조합니다.

## 다른 기능에서 쓰는 API

| API | 설명 |
| --- | --- |
| `WeatherPresenter.Show(WeatherKind)` | 지금 모습에서 새 날씨로 섞습니다. |
| `WeatherPresenter.ShowImmediately(WeatherKind)` | 섞지 않고 바로 바꿉니다. 프레임이 흐르지 않는 Editor 미리보기에 씁니다. |
| `WeatherPresenter.Current` | 마지막으로 요청한 날씨 |

## Builder와 미리보기

- **Tools → A305 → Weather → Build Town Weather**(`Scripts/Editor/Weather/TownWeatherBuilder.cs`)가 TownScene의 `Lighting/TownWeather`에 `WeatherPresenter`를 두고 해·`SummerClouds`·`OvercastClouds`·`JourneyRain`·플레이어를 연결한 뒤 첫날 미션에 연결합니다. 데이터 에셋·흐림 구름·빗방울은 없을 때만 만들고, 이미 있으면 디자인한 값을 그대로 둡니다. **Mission → Build First Day Journey**와 **World → Build Spaces**도 마을을 연결할 때 같은 연결을 적용합니다.
- **Tools → A305 → Weather → Capture Town Weather Preview**(`TownWeatherPreview.cs`)는 Play나 서버 호출 없이 TownScene을 맑음·흐림·비로 바꿔 같은 자리에서 찍습니다. 환경변수 `TOWN_WEATHER_PREVIEW_OUTPUT`에 저장 폴더를 지정하며 씬은 저장하지 않습니다.

## 테스트

| 어셈블리 | 위치 | 검증 |
| --- | --- | --- |
| `A305.Weather.Tests` | `Assets/Tests/EditMode/Weather/` | 두 모습 섞기와 끝 값에서 멈추기, 맑음은 씬 디자인 유지, 흐림·비가 맑은 날보다 어둡지만 밤처럼 까맣지 않음, 비가 흐림보다 어둡고 안개가 가까움 |
| `A305.Weather.PlayModeTests` | `Assets/Tests/PlayMode/Weather/` | 비·흐림·맑음 전환 뒤 해·환경광·안개·하늘·구름·빗방울, 시간에 따라 섞이는지, 빗방울이 플레이어 위를 따라가는지, 원본 하늘·구름 에셋이 바뀌지 않고 되돌려지는지 |
| `A305.Mission.Tests` | `Assets/Tests/EditMode/Mission/` | 서버 `weather`·`raining`에서 날씨를 정하는 규칙 |
| `A305.World.Tests` | `Assets/Tests/EditMode/World/` | TownScene의 첫날 미션에 날씨 표시와 그 참조가 모두 연결됐는지 |

실행 방법은 [프론트엔드 개발 지침](../../../../AGENTS.md)을 따릅니다.
