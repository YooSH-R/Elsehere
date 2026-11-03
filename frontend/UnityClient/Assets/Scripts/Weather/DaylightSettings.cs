using System.Collections.Generic;
using UnityEngine;

namespace A305.Weather
{
    /// <summary>
    /// 게임 시각에 따른 낮·노을·밤. 해 뜨고 지는 시각, 한밤중 모습, 노을 색, 밤 불빛 세기를 정한다.
    /// 시각은 1일차 0시부터 센 게임 분(GameClock.Minutes)이며 하루가 지나도 같은 시각이면 같은 모습이다.
    /// 필드 초기값은 에셋을 처음 만들 때 들어가는 값이며, 이후 조정은 에셋에서 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "DayNight", menuName = "A305/Weather/Day Night")]
    public sealed class DaylightSettings : ScriptableObject
    {
        private const float MinutesPerDay = 24 * 60;

        // 해가 막 뜨고 질 때의 높이와, 한낮 방향을 가운데로 아침·저녁에 돌아가는 각도.
        private const float HorizonElevationDegrees = 6f;
        private const float SunSwingDegrees = 70f;

        [Header("시각")]
        [SerializeField]
        [Range(0f, 24f)]
        [Tooltip("해가 뜨는 게임 시각(시)")]
        private float sunriseHour = 6f;

        [SerializeField]
        [Range(0f, 24f)]
        [Tooltip("해가 지는 게임 시각(시)")]
        private float sunsetHour = 19f;

        [SerializeField]
        [Min(0f)]
        [Tooltip(
            "해 뜰 때·질 때 서서히 바뀌는 시간(시). 뜨고 지는 시각을 가운데로 앞뒤 절반씩 쓴다"
        )]
        private float twilightHours = 1f;

        [Header("밤")]
        [SerializeField]
        [Tooltip("한밤중 모습. 해 자리에는 달빛을 적는다. 안개 거리는 날씨 값을 그대로 쓴다")]
        private WeatherLook night = new WeatherLook
        {
            skyZenith = new Color(.035f, .06f, .14f),
            skyHorizon = new Color(.1f, .14f, .26f),
            sunColor = new Color(.62f, .72f, 1f),
            sunIntensity = .3f,
            sunShadowStrength = .45f,
            ambientSky = new Color(.16f, .2f, .34f),
            ambientEquator = new Color(.12f, .13f, .2f),
            ambientGround = new Color(.06f, .06f, .08f),
            fogColor = new Color(.07f, .09f, .16f),
            fogStartMeters = 42f,
            fogEndMeters = 125f,
            cloudColor = new Color(.2f, .23f, .32f),
        };

        [Header("노을")]
        [SerializeField]
        [Tooltip("해 뜰 때·질 때 지평선 색")]
        private Color twilightHorizon = new Color(.98f, .6f, .42f);

        [SerializeField]
        [Tooltip("해 뜰 때·질 때 하늘 꼭대기 색")]
        private Color twilightZenith = new Color(.42f, .43f, .66f);

        [SerializeField]
        [Tooltip("해 뜰 때·질 때 햇빛 색")]
        private Color twilightSunColor = new Color(1f, .62f, .38f);

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("맑은 하늘에서 노을이 물드는 정도")]
        private float twilightStrength = .75f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("하늘을 덮는 날씨(흐림·비)에서 노을이 물드는 정도")]
        private float coveredTwilightStrength = .25f;

        [Header("불빛")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("한밤중 가로등·실내등 세기 배율. 낮에는 씬 디자인 세기 그대로다")]
        private float nightLampScale = 2f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("한밤중 창빛·전구 같은 발광 재질 밝기 배율")]
        private float nightGlowScale = 2.2f;

        [SerializeField]
        [Tooltip("밤에 더 밝히는 발광 재질. 창빛·가로등 전구·실내 조명 갓 등")]
        private List<Material> glowMaterials = new List<Material>();

        [SerializeField]
        [Tooltip("창밖 하늘을 그린 유리 재질. 밤에는 밤하늘 색으로 바꾼다")]
        private List<Material> windowMaterials = new List<Material>();

        [SerializeField]
        [Tooltip("한밤중 창밖 유리 색")]
        private Color nightWindowColor = new Color(.1f, .14f, .24f);

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("한밤중 창밖 유리의 발광 배율")]
        private float nightWindowEmissionScale = .1f;

        [Header("실내")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("한밤중 창으로 드는 빛(실내 Directional Light) 세기 배율")]
        private float nightWindowLightScale = .12f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("한밤중 낮빛 반사를 흉내 낸 보조광(Bounce) 세기 배율")]
        private float nightFillScale = .35f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("한밤중 실내 환경광 배율")]
        private float nightInteriorAmbientScale = .45f;

        public float NightLampScale => nightLampScale;
        public float NightGlowScale => nightGlowScale;
        public IReadOnlyList<Material> GlowMaterials => glowMaterials;
        public IReadOnlyList<Material> WindowMaterials => windowMaterials;
        public Color NightWindowColor => nightWindowColor;
        public float NightWindowEmissionScale => nightWindowEmissionScale;
        public float NightWindowLightScale => nightWindowLightScale;
        public float NightFillScale => nightFillScale;
        public float NightInteriorAmbientScale => nightInteriorAmbientScale;
        public Color MoonColor => night.sunColor;

        /// <summary>Builder와 테스트에서 새 설정을 만든다. 이미 있는 에셋의 값은 바꾸지 않는다.</summary>
        public static DaylightSettings Create(
            IEnumerable<Material> glowMaterials = null,
            IEnumerable<Material> windowMaterials = null
        )
        {
            var settings = CreateInstance<DaylightSettings>();
            if (glowMaterials != null)
                settings.glowMaterials = new List<Material>(glowMaterials);
            if (windowMaterials != null)
                settings.windowMaterials = new List<Material>(windowMaterials);
            return settings;
        }

        /// <summary>밤인 정도. 낮 0, 한밤 1이며 해 뜰 때·질 때 서서히 바뀐다.</summary>
        public float NightAmount(double gameMinutes)
        {
            float minute = MinuteOfDay(gameMinutes);
            float half = TwilightHalfMinutes;
            float sunrise = sunriseHour * 60f;
            float sunset = sunsetHour * 60f;
            float risen = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(sunrise - half, sunrise + half, minute)
            );
            float set = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(sunset - half, sunset + half, minute)
            );
            return 1f - risen * (1f - set);
        }

        /// <summary>노을인 정도. 해가 뜨고 지는 시각에 1이고 바뀌는 시간 밖에서는 0이다.</summary>
        public float TwilightAmount(double gameMinutes)
        {
            float minute = MinuteOfDay(gameMinutes);
            float fromSunrise = Mathf.Abs(minute - sunriseHour * 60f);
            float fromSunset = Mathf.Abs(minute - sunsetHour * 60f);
            return Mathf.Clamp01(1f - Mathf.Min(fromSunrise, fromSunset) / TwilightHalfMinutes);
        }

        /// <summary>
        /// 낮 모습(날씨를 입힌 씬 디자인)에 시각을 입힌 모습. 밤에는 한밤중 모습으로 바꾸고, 해 뜰 때·질 때는 하늘과 햇빛을 노을빛으로 물들인다.
        /// 안개 거리는 날씨가 정하므로 그대로 둔다.
        /// </summary>
        public WeatherLook Mix(WeatherLook day, double gameMinutes, bool skyCovered)
        {
            WeatherLook midnight = night;
            midnight.fogStartMeters = day.fogStartMeters;
            midnight.fogEndMeters = day.fogEndMeters;
            WeatherLook look = WeatherLook.Lerp(day, midnight, NightAmount(gameMinutes));
            float glow =
                TwilightAmount(gameMinutes)
                * (skyCovered ? coveredTwilightStrength : twilightStrength);
            look.skyHorizon = Color.Lerp(look.skyHorizon, twilightHorizon, glow);
            look.skyZenith = Color.Lerp(look.skyZenith, twilightZenith, glow * .6f);
            look.sunColor = Color.Lerp(look.sunColor, twilightSunColor, glow);
            return look;
        }

        /// <summary>별이 보이는 정도. 하늘을 덮는 날씨에서는 보이지 않는다.</summary>
        public float StarAmount(double gameMinutes, bool skyCovered) =>
            skyCovered ? 0f : NightAmount(gameMinutes);

        /// <summary>
        /// 시각에 따른 해 방향. 한낮(뜨고 지는 시각 가운데)에는 씬에 디자인한 방향이고, 뜰 때·질 때는 지평선 가까이 낮아지며
        /// 아침과 저녁은 반대쪽에서 비춘다. 밤에는 디자인한 방향에서 달빛이 든다.
        /// </summary>
        public Quaternion SunRotation(Quaternion designed, double gameMinutes)
        {
            Vector3 angles = designed.eulerAngles;
            float designedElevation = angles.x > 180f ? angles.x - 360f : angles.x;
            float progress = Mathf.InverseLerp(
                sunriseHour * 60f,
                sunsetHour * 60f,
                MinuteOfDay(gameMinutes)
            );
            float height = Mathf.Sin(progress * Mathf.PI);
            Quaternion sunPath = Quaternion.Euler(
                Mathf.Lerp(HorizonElevationDegrees, designedElevation, height),
                angles.y + Mathf.Lerp(-SunSwingDegrees, SunSwingDegrees, progress),
                angles.z
            );
            // 해가 진 뒤(밤이 반을 넘은 뒤)부터 달빛 방향으로 옮겨, 노을 때는 낮게 깔린 햇빛과 긴 그림자가 남는다.
            float moonlight = Mathf.InverseLerp(.5f, 1f, NightAmount(gameMinutes));
            return Quaternion.Slerp(sunPath, designed, moonlight);
        }

        // 바뀌는 시간이 0이어도 나눗셈이 되도록 최소 30초를 둔다.
        private float TwilightHalfMinutes => Mathf.Max(twilightHours * 30f, .5f);

        private static float MinuteOfDay(double gameMinutes)
        {
            double minute = gameMinutes % MinutesPerDay;
            return (float)(minute < 0 ? minute + MinutesPerDay : minute);
        }
    }
}
