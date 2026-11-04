using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 몸통 캡슐보다 큰 머리를 벽·키 큰 가구에서 막는다. 머리 구를 움직일 방향으로 밀어 보고,
    /// 막히면 닿기 직전까지만 가고 남은 만큼은 막은 면을 따라 미끄러진다.
    /// 구의 아래 끝이 머리 아래 끝이라 탁자·소파 앉는 면처럼 낮은 가구는 머리 아래로 지나간다.
    /// 구는 어느 쪽을 봐도 같아서 제자리에서 돌아도 머리가 벽에 묻히지 않는다.
    /// </summary>
    public sealed class HeadClearance
    {
        // 막은 면을 따라 한 번 더 미끄러져 본다. 구석에서 두 면에 닿으면 거기서 멈춘다.
        private const int SlidePasses = 2;

        private readonly float radius;
        private readonly float centerHeight;
        private readonly float skin;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        /// <param name="radius">머리 구 반지름. 0이면 머리를 따로 막지 않는다.</param>
        /// <param name="centerHeight">발 기준 머리 구 중심 높이</param>
        /// <param name="skin">벽과 남겨 둘 간격</param>
        public HeadClearance(float radius, float centerHeight, float skin)
        {
            this.radius = radius;
            this.centerHeight = centerHeight;
            this.skin = skin;
        }

        /// <summary>머리가 막히지 않는 만큼으로 줄인 이번 프레임 이동. 수직 이동은 그대로 둔다.</summary>
        /// <param name="feet">플레이어 발 위치</param>
        /// <param name="self">플레이어. 자기 콜라이더에는 막히지 않는다.</param>
        public Vector3 Limit(Vector3 feet, Vector3 motion, Transform self)
        {
            if (radius <= 0f)
            {
                return motion;
            }

            Vector3 head = feet + Vector3.up * centerHeight;
            Vector3 remaining = new Vector3(motion.x, 0f, motion.z);
            Vector3 allowed = Vector3.zero;
            for (int pass = 0; pass < SlidePasses && remaining.sqrMagnitude > 1e-8f; pass++)
            {
                if (!TryHit(head + allowed, remaining, self, out RaycastHit hit))
                {
                    allowed += remaining;
                    break;
                }

                Vector3 direction = remaining.normalized;
                float reach = Mathf.Max(0f, hit.distance - skin);
                allowed += direction * reach;
                Vector3 wall = new Vector3(hit.normal.x, 0f, hit.normal.z);
                remaining =
                    wall.sqrMagnitude > 1e-6f
                        ? Vector3.ProjectOnPlane(remaining - direction * reach, wall.normalized)
                        : Vector3.zero;
            }

            return new Vector3(allowed.x, motion.y, allowed.z);
        }

        private bool TryHit(Vector3 head, Vector3 motion, Transform self, out RaycastHit nearest)
        {
            int count = Physics.SphereCastNonAlloc(
                head,
                radius,
                motion.normalized,
                hits,
                motion.magnitude + skin,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            nearest = default;
            bool found = false;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = hits[index];
                // 처음부터 겹친 물체는 거리 0으로 나온다. 이미 겹친 곳에서 빠져나갈 수 있게 막지 않는다.
                if (hit.distance <= 0f || hit.transform.IsChildOf(self))
                {
                    continue;
                }

                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }

            return found;
        }
    }
}
