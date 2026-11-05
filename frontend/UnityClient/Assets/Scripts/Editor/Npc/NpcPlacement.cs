using UnityEngine;

namespace A305.EditorTools.Npc
{
    /// <summary>NPC를 가구·벽·다른 NPC와 겹치지 않는 자리에 세운다. 거리 매장 직원과 외형 표의 NPC를 처음 놓을 때 쓴다.</summary>
    internal static class NpcPlacement
    {
        // 빈자리를 찾을 때 쓰는 NPC 몸(반지름·키)과 한 번에 옮겨 보는 거리
        private const float BodyRadiusMeters = .32f;
        private const float BodyHeightMeters = 1.6f;
        private const float SearchStepMeters = .3f;
        private const int SearchRings = 6;

        // 정한 자리 바로 아래 바닥만 찾는다. 이보다 높거나 낮은 면(탁자 윗면 등)은 바닥으로 보지 않는다.
        private const float GroundSearchMeters = .3f;

        // NPC 발을 바닥에서 띄우는 높이. 이미 놓인 NPC와 같다.
        private const float FootClearanceMeters = .05f;

        /// <summary>
        /// 정한 자리에서 가장 가까운 빈자리의 월드 위치. 링 모양으로 넓혀 가며 찾고, area를 주면 그 안에서만 찾는다.
        /// stand와 area는 frame 기준 좌표이며 frame이 없으면 월드 좌표다. 바로 아래 바닥이 있으면 그 높이에 세운다.
        /// </summary>
        public static Vector3 FreeSpot(Transform frame, Vector3 stand, Bounds? area = null)
        {
            Physics.SyncTransforms();
            for (int ring = 0; ring <= SearchRings; ring++)
            {
                int steps = ring == 0 ? 1 : ring * 8;
                for (int step = 0; step < steps; step++)
                {
                    float angle = step * Mathf.PI * 2f / steps;
                    Vector3 local =
                        stand
                        + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle))
                            * (ring * SearchStepMeters);
                    if (area.HasValue && !area.Value.Contains(local + Vector3.up))
                    {
                        continue;
                    }

                    Vector3 world = OnGround(frame != null ? frame.TransformPoint(local) : local);
                    bool blocked = Physics.CheckCapsule(
                        world + Vector3.up * (BodyRadiusMeters + .1f),
                        world + Vector3.up * (BodyHeightMeters - BodyRadiusMeters),
                        BodyRadiusMeters,
                        ~0,
                        QueryTriggerInteraction.Ignore
                    );
                    if (!blocked)
                    {
                        return world + Vector3.up * FootClearanceMeters;
                    }
                }
            }

            throw new System.InvalidOperationException(
                "NPC가 설 빈자리가 없습니다: " + (frame != null ? frame.name : stand.ToString())
            );
        }

        private static Vector3 OnGround(Vector3 spot)
        {
            bool hit = Physics.Raycast(
                spot + Vector3.up * GroundSearchMeters,
                Vector3.down,
                out RaycastHit ground,
                GroundSearchMeters * 2f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            return hit ? ground.point : spot;
        }
    }
}
