using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Weather
{
    /// <summary>장소에서 보여 줄 수 있는 날씨 목록과 날씨를 바꾸는 방식.</summary>
    [CreateAssetMenu(fileName = "TownWeather", menuName = "A305/Weather/Settings")]
    public sealed class WeatherSettings : ScriptableObject
    {
        [SerializeField]
        [Tooltip("이 장소에서 보여 줄 수 있는 날씨. 서버 값(code)으로 찾는다")]
        private List<WeatherDefinition> weathers = new List<WeatherDefinition>();

        [SerializeField]
        [Min(0f)]
        [Tooltip("날씨가 바뀔 때 하늘과 조명을 섞는 시간")]
        private float transitionSeconds = 1.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("날씨 효과(빗방울·눈송이)를 띄우는 높이. 플레이어 머리 위를 따라간다")]
        private float effectHeightMeters = 7f;

        public IReadOnlyList<WeatherDefinition> Weathers => weathers;
        public float TransitionSeconds => transitionSeconds;
        public float EffectHeightMeters => effectHeightMeters;

        /// <summary>Builder와 테스트에서 새 설정을 만든다. 이미 있는 에셋의 값은 바꾸지 않는다.</summary>
        public static WeatherSettings Create(
            float transitionSeconds,
            float effectHeightMeters,
            params WeatherDefinition[] weathers
        )
        {
            var settings = CreateInstance<WeatherSettings>();
            settings.transitionSeconds = transitionSeconds;
            settings.effectHeightMeters = effectHeightMeters;
            settings.weathers = new List<WeatherDefinition>(weathers);
            return settings;
        }

        /// <summary>
        /// 서버 값(code)의 날씨. 비었거나 목록에 없으면 fallbackCode의 날씨를 쓰고, 그것도 없으면 null이다.
        /// </summary>
        public WeatherDefinition Resolve(string code, string fallbackCode)
        {
            // Unity 객체는 ??가 파괴된 객체를 null로 보지 않으므로 != null로 비교한다.
            WeatherDefinition weather = Find(code);
            return weather != null ? weather : Find(fallbackCode);
        }

        private WeatherDefinition Find(string code)
        {
            if (string.IsNullOrEmpty(code))
                return null;
            foreach (WeatherDefinition weather in weathers)
            {
                if (
                    weather != null
                    && string.Equals(weather.Code, code, StringComparison.OrdinalIgnoreCase)
                )
                    return weather;
            }
            return null;
        }
    }
}
