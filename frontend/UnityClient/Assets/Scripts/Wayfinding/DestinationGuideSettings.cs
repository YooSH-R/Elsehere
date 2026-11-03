using UnityEngine;

namespace A305.Wayfinding
{
    /// <summary>길 안내의 색·크기·속도와 길을 찾는 기준</summary>
    [CreateAssetMenu(
        fileName = "DestinationGuideSettings",
        menuName = "A305/Wayfinding/Destination Guide Settings"
    )]
    public sealed class DestinationGuideSettings : ScriptableObject
    {
        [Header("길 찾기")]
        [SerializeField]
        [Min(1f)]
        [Tooltip(
            "차도 1m를 인도 몇 m로 칠지. 크면 차도를 건널 때 멀어도 횡단보도로 돌아간다. NPC 걸음에는 영향이 없다."
        )]
        private float roadCrossingCost = 50f;

        [SerializeField]
        [Min(.1f)]
        [Tooltip("길을 찾은 자리에서 이만큼 움직이면 길을 다시 찾는다(m)")]
        private float replanMeters = 1f;

        [SerializeField]
        [Min(.1f)]
        [Tooltip("길을 찾은 뒤 이만큼 지나면 길을 다시 찾는다(초)")]
        private float replanSeconds = .5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip(
            "출입문 앞에서 이 거리 안에 오면 도착한 것으로 보고 길과 출입문 빛을 거둔다(m). 다른 목적지를 알릴 때까지 다시 보이지 않는다"
        )]
        private float arrivedMeters = 1.5f;

        [Header("모양")]
        [SerializeField]
        [ColorUsage(false, true)]
        [Tooltip("길과 출입문 빛의 색. 1보다 밝게 두면 화면 번짐(Bloom)으로 야광처럼 보인다")]
        private Color glowColor = new Color(.9f, 1.6f, .1f);

        [SerializeField]
        [Min(.1f)]
        [Tooltip("도로 위 길의 폭(m)")]
        private float routeWidthMeters = .6f;

        [SerializeField]
        [Min(0f)]
        [Tooltip(
            "길과 출입문 빛을 바닥에서 띄우는 높이(m). 바닥과 겹쳐 깜박이지 않을 만큼만 띄운다"
        )]
        private float liftMeters = .04f;

        [SerializeField]
        [Min(.1f)]
        [Tooltip("길을 바닥 높이에 맞춰 누일 점 간격(m)")]
        private float routeSampleMeters = .5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("새 길이 플레이어 발밑에서 출입문까지 깔리는 시간(초)")]
        private float revealSeconds = .6f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("길 위 화살 무늬가 출입문 쪽으로 흐르는 속도(m/s)")]
        private float arrowMetersPerSecond = 1.2f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("출입문 빛이 1초에 밝아졌다 어두워지는 횟수")]
        private float pulsesPerSecond = .8f;

        public float RoadCrossingCost => roadCrossingCost;
        public float ReplanMeters => replanMeters;
        public float ReplanSeconds => replanSeconds;
        public float ArrivedMeters => arrivedMeters;
        public Color GlowColor => glowColor;
        public float RouteWidthMeters => routeWidthMeters;
        public float LiftMeters => liftMeters;
        public float RouteSampleMeters => routeSampleMeters;
        public float RevealSeconds => revealSeconds;
        public float ArrowMetersPerSecond => arrowMetersPerSecond;
        public float PulsesPerSecond => pulsesPerSecond;
    }
}
