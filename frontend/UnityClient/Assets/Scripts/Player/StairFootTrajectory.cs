using UnityEngine;

namespace A305.Player
{
    /// <summary>발을 먼저 들어 모서리를 넘기고, 목표 디딤면 안쪽에서 내려놓는다.</summary>
    public static class StairFootTrajectory
    {
        public static Vector3 Evaluate(
            Vector3 start,
            Vector3 landing,
            float progress,
            float clearance
        )
        {
            float t = Mathf.Clamp01(progress);
            float apex = Mathf.Max(start.y, landing.y) + clearance;
            Vector3 result = Vector3.Lerp(start, landing, Ease(Mathf.InverseLerp(.28f, .72f, t)));
            result.y =
                t < .28f ? Mathf.Lerp(start.y, apex, Ease(t / .28f))
                : t > .72f ? Mathf.Lerp(apex, landing.y, Ease((t - .72f) / .28f))
                : apex;
            return result;
        }

        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6 - 15) + 10);
        }
    }
}
