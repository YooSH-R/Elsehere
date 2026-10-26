using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>이동 충돌에 영향을 주지 않는 호버 전용 감지 영역.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ObjectHoverHitArea : MonoBehaviour
    {
        public HoverableObject Target => GetComponentInParent<HoverableObject>();
    }
}
