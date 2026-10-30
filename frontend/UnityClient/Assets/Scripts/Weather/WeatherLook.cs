using System;
using UnityEngine;

namespace A305.Weather
{
    /// <summary>날씨 하나의 하늘·해·환경광·안개·구름 색. 날씨가 바뀔 때 두 모습 사이를 섞는다.</summary>
    [Serializable]
    public struct WeatherLook
    {
        [Tooltip("하늘 꼭대기 색. Elsehere/SummerSky의 _Zenith")]
        public Color skyZenith;

        [Tooltip("지평선 색. Elsehere/SummerSky의 _Horizon")]
        public Color skyHorizon;

        public Color sunColor;

        [Min(0f)]
        public float sunIntensity;

        [Range(0f, 1f)]
        [Tooltip("그림자 진하기. 구름이 해를 가리면 옅어진다")]
        public float sunShadowStrength;

        public Color ambientSky;
        public Color ambientEquator;
        public Color ambientGround;
        public Color fogColor;

        [Min(0f)]
        public float fogStartMeters;

        [Min(0f)]
        public float fogEndMeters;

        [Tooltip("하늘의 구름 색")]
        public Color cloudColor;

        /// <summary>amount 0은 from, 1은 to이며 범위를 넘으면 끝 값에 멈춘다.</summary>
        public static WeatherLook Lerp(WeatherLook from, WeatherLook to, float amount)
        {
            return new WeatherLook
            {
                skyZenith = Color.Lerp(from.skyZenith, to.skyZenith, amount),
                skyHorizon = Color.Lerp(from.skyHorizon, to.skyHorizon, amount),
                sunColor = Color.Lerp(from.sunColor, to.sunColor, amount),
                sunIntensity = Mathf.Lerp(from.sunIntensity, to.sunIntensity, amount),
                sunShadowStrength = Mathf.Lerp(
                    from.sunShadowStrength,
                    to.sunShadowStrength,
                    amount
                ),
                ambientSky = Color.Lerp(from.ambientSky, to.ambientSky, amount),
                ambientEquator = Color.Lerp(from.ambientEquator, to.ambientEquator, amount),
                ambientGround = Color.Lerp(from.ambientGround, to.ambientGround, amount),
                fogColor = Color.Lerp(from.fogColor, to.fogColor, amount),
                fogStartMeters = Mathf.Lerp(from.fogStartMeters, to.fogStartMeters, amount),
                fogEndMeters = Mathf.Lerp(from.fogEndMeters, to.fogEndMeters, amount),
                cloudColor = Color.Lerp(from.cloudColor, to.cloudColor, amount),
            };
        }
    }
}
