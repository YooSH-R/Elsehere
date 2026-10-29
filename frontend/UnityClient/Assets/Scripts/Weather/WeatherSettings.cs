using UnityEngine;

namespace A305.Weather
{
    /// <summary>
    /// 흐림·비의 하늘과 조명. 맑음은 씬에 디자인된 하늘·조명을 그대로 쓰므로 여기에 두지 않는다.
    /// 필드 초기값은 에셋을 처음 만들 때 들어가는 값이며, 이후 조정은 에셋에서 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "TownWeather", menuName = "A305/Weather/Settings")]
    public sealed class WeatherSettings : ScriptableObject
    {
        [SerializeField]
        [Tooltip("구름이 하늘을 덮어 해가 약해진 흐린 날")]
        private WeatherLook cloudy = new WeatherLook
        {
            skyZenith = new Color(.47f, .53f, .6f),
            skyHorizon = new Color(.76f, .79f, .81f),
            sunColor = new Color(.92f, .93f, .95f),
            sunIntensity = .95f,
            sunShadowStrength = .35f,
            ambientSky = new Color(.56f, .61f, .67f),
            ambientEquator = new Color(.58f, .57f, .54f),
            ambientGround = new Color(.3f, .29f, .28f),
            fogColor = new Color(.7f, .73f, .76f),
            fogStartMeters = 32f,
            fogEndMeters = 110f,
            cloudColor = new Color(.84f, .85f, .87f),
        };

        [SerializeField]
        [Tooltip("먹구름 아래로 비가 내리는 날. 흐린 날보다 어둡고 안개가 가깝다")]
        private WeatherLook rain = new WeatherLook
        {
            skyZenith = new Color(.36f, .41f, .47f),
            skyHorizon = new Color(.58f, .62f, .66f),
            sunColor = new Color(.82f, .86f, .92f),
            sunIntensity = .6f,
            sunShadowStrength = .15f,
            ambientSky = new Color(.45f, .5f, .56f),
            ambientEquator = new Color(.45f, .46f, .47f),
            ambientGround = new Color(.25f, .25f, .26f),
            fogColor = new Color(.53f, .57f, .62f),
            fogStartMeters = 18f,
            fogEndMeters = 80f,
            cloudColor = new Color(.6f, .63f, .67f),
        };

        [SerializeField]
        [Min(0f)]
        [Tooltip("날씨가 바뀔 때 하늘과 조명을 섞는 시간")]
        private float transitionSeconds = 1.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("빗방울을 뿌리는 높이. 플레이어 머리 위를 따라간다")]
        private float rainHeightMeters = 7f;

        public float TransitionSeconds => transitionSeconds;
        public float RainHeightMeters => rainHeightMeters;

        /// <param name="clear">씬에 디자인된 맑은 날의 모습</param>
        public WeatherLook LookFor(WeatherKind kind, WeatherLook clear)
        {
            switch (kind)
            {
                case WeatherKind.Cloudy:
                    return cloudy;
                case WeatherKind.Rain:
                    return rain;
                default:
                    return clear;
            }
        }
    }
}
