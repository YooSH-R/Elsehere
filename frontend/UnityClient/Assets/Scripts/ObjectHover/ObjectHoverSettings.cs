using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>사물 호버 툴팁의 표시 조건 수치.</summary>
    [CreateAssetMenu(menuName = "A305/ObjectHover/Settings", fileName = "ObjectHoverSettings")]
    public class ObjectHoverSettings : ScriptableObject
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip(
            "같은 사물을 이만큼 가리키면 툴팁을 보인다. 마우스가 스쳐 지나갈 때 깜박이지 않게 한다."
        )]
        private float showDelaySeconds = 0.15f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("플레이어와 가리킨 지점 사이가 이 거리 안이어야 툴팁을 보인다.")]
        private float maxDistanceMeters = 8f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("카메라에서 포인터 방향으로 사물을 찾는 최대 거리")]
        private float rayLengthMeters = 30f;

        [SerializeField]
        [Tooltip("가리킬 수 있는 레이어")]
        private LayerMask hoverLayers = ~0;

        public float ShowDelaySeconds => showDelaySeconds;

        public float MaxDistanceMeters => maxDistanceMeters;

        public float RayLengthMeters => rayLengthMeters;

        public LayerMask HoverLayers => hoverLayers;
    }
}
