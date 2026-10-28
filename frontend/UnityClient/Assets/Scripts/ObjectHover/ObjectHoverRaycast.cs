using System;
using UnityEngine;

namespace A305.ObjectHover
{
    public sealed class ObjectHoverRaycast
    {
        private RaycastHit[] hits = new RaycastHit[32];

        public HoverableObject Find(
            Ray ray,
            float distance,
            int layers,
            Transform player,
            out Vector3 point
        )
        {
            int count;
            while (true)
            {
                count = Physics.RaycastNonAlloc(
                    ray,
                    hits,
                    distance,
                    layers,
                    QueryTriggerInteraction.Collide
                );
                if (count < hits.Length)
                    break;
                Array.Resize(ref hits, hits.Length * 2);
            }

            point = Vector3.zero;
            HoverableObject target = null;
            float nearest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = hits[index];
                if (player != null && hit.transform.IsChildOf(player))
                    continue;
                ObjectHoverHitArea area = hit.collider.GetComponent<ObjectHoverHitArea>();
                HoverableObject candidate = hit.collider.GetComponentInParent<HoverableObject>();
                if (area == null)
                {
                    if (hit.collider.isTrigger)
                        continue;
                    // 가구의 이동용 상자는 안쪽 컵·가방을 가리지 않는다. 호버는 시각 영역으로 판단한다.
                    if (
                        candidate != null
                        && candidate.GetComponentInChildren<ObjectHoverHitArea>() != null
                    )
                        continue;
                }
                if (hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                target = area != null ? area.Target : candidate;
                point = hit.point;
            }
            return target;
        }
    }
}
