using UnityEngine;

namespace A305.Weather
{
    /// <summary>
    /// 날씨 하나. 서버가 보내는 이름, 하늘·조명 모습, 하늘을 덮는 구름 여부, 플레이어 위를 따라오는 효과를 정한다.
    /// 새 날씨는 이 에셋을 만들어 <see cref="WeatherSettings"/> 목록에 넣으면 코드 수정 없이 보인다.
    /// </summary>
    [CreateAssetMenu(fileName = "Weather", menuName = "A305/Weather/Definition")]
    public sealed class WeatherDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("서버 응답 weather 값. 예: CLEAR, CLOUDY, RAIN")]
        private string code;

        [SerializeField]
        [Tooltip(
            "켜면 씬에 디자인된 하늘·조명을 그대로 쓰고 아래 모습은 쓰지 않는다. 맑은 날에 쓴다"
        )]
        private bool keepDesignedLook;

        [SerializeField]
        private WeatherLook look;

        [SerializeField]
        [Tooltip("하늘을 덮는 구름(OvercastClouds)을 띄운다")]
        private bool coversSky;

        [SerializeField]
        [Tooltip(
            "이 날씨 동안 플레이어 머리 위를 따라오는 효과 프리팹. 빗방울·눈송이 파티클 등. 없으면 비워 둔다"
        )]
        private GameObject effect;

        public string Code => code;
        public bool CoversSky => coversSky;
        public GameObject Effect => effect;

        /// <summary>Builder와 테스트에서 새 정의를 만든다. 이미 있는 에셋의 값은 바꾸지 않는다.</summary>
        public static WeatherDefinition Create(
            string code,
            WeatherLook look,
            bool keepDesignedLook = false,
            bool coversSky = false,
            GameObject effect = null
        )
        {
            var definition = CreateInstance<WeatherDefinition>();
            definition.name = code;
            definition.code = code;
            definition.look = look;
            definition.keepDesignedLook = keepDesignedLook;
            definition.coversSky = coversSky;
            definition.effect = effect;
            return definition;
        }

        /// <param name="designed">씬에 디자인된 맑은 날의 모습</param>
        public WeatherLook LookOver(WeatherLook designed) => keepDesignedLook ? designed : look;
    }
}
