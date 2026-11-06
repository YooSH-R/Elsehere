using System;
using A305.ObjectHover;
using A305.Player;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 클릭·탭한 방향에서 맨 앞에 보이는 것이 NPC 몸이면 그 NPC를 찾는다. 플레이어 몸과 보이지 않는 감지 영역(트리거)은 지나친다.
    /// 사물 카드가 뜨는 영역(<see cref="ObjectHoverHitArea"/>)은 사물을 가리킨 것으로 보아, 사물 카드와 NPC 말 걸기가 한 번에 일어나지 않게 한다.
    /// </summary>
    public sealed class NpcPointerRaycast
    {
        private RaycastHit[] hits = new RaycastHit[32];

        public Passerby Find(Ray ray, float maxDistanceMeters)
        {
            int count = CastAll(ray, maxDistanceMeters);
            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.distance < nearestDistance && CanBePointedAt(hit.collider))
                {
                    nearest = hit.collider;
                    nearestDistance = hit.distance;
                }
            }

            if (nearest == null || nearest.GetComponent<ObjectHoverHitArea>() != null)
            {
                return null;
            }

            return nearest.GetComponentInParent<Passerby>();
        }

        private static bool CanBePointedAt(Collider collider)
        {
            if (collider.GetComponentInParent<PlayerMover>() != null)
            {
                return false;
            }

            return !collider.isTrigger || collider.GetComponent<ObjectHoverHitArea>() != null;
        }

        private int CastAll(Ray ray, float maxDistanceMeters)
        {
            while (true)
            {
                int count = Physics.RaycastNonAlloc(
                    ray,
                    hits,
                    maxDistanceMeters,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide
                );
                if (count < hits.Length)
                {
                    return count;
                }

                Array.Resize(ref hits, hits.Length * 2);
            }
        }
    }
}
