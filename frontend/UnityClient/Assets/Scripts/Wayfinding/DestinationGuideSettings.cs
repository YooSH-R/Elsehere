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
        [Tooltip("출입문 앞에서 이 거리 안에 오면 도착한 것으로 보고 길을 거둔다(m)")]
        private float arrivedMeters = 1.5f;

        public float RoadCrossingCost => roadCrossingCost;
        public float ReplanMeters => replanMeters;
        public float ReplanSeconds => replanSeconds;
        public float ArrivedMeters => arrivedMeters;
    }
}
