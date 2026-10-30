using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 벽에 막힌 3인칭 카메라의 거리. 벽이 가까워지면 바로 당기고, 막힘이 풀리면 천천히 원래 거리로 물러나
    /// 카메라가 가까워졌다 멀어졌다 튀지 않게 한다.
    /// </summary>
    public sealed class CameraCollisionDistance
    {
        // 이보다 가는 물체(가로등·나무 기둥·사람)는 잠깐 가려도 카메라를 당기지 않는다.
        private const float ThinObstacleMeters = 0.8f;

        private readonly float recoverSharpness;
        private float currentMeters = float.PositiveInfinity;

        /// <param name="recoverSharpness">막힘이 풀린 뒤 물러나는 빠르기. 클수록 빨리 물러난다.</param>
        public CameraCollisionDistance(float recoverSharpness)
        {
            this.recoverSharpness = recoverSharpness;
        }

        /// <param name="allowedMeters">지금 벽에 막히지 않고 둘 수 있는 가장 먼 거리</param>
        /// <returns>이번 프레임에 쓸 카메라 거리</returns>
        public float Follow(float allowedMeters, float deltaTimeSeconds)
        {
            if (allowedMeters <= currentMeters)
            {
                currentMeters = allowedMeters;
                return currentMeters;
            }

            // 프레임 속도와 관계없이 같은 빠르기로 물러나도록 지수 감쇠로 섞는다.
            float blend = 1f - Mathf.Exp(-recoverSharpness * deltaTimeSeconds);
            currentMeters = Mathf.Lerp(currentMeters, allowedMeters, blend);
            return currentMeters;
        }

        /// <summary>순간 이동한 뒤처럼 섞지 않고 바로 그 거리에 둔다.</summary>
        public float Snap(float meters)
        {
            currentMeters = meters;
            return currentMeters;
        }

        /// <param name="size">물체의 월드 크기(Bounds.size)</param>
        public static bool IsThinObstacle(Vector3 size)
        {
            return Mathf.Max(size.x, size.z) < ThinObstacleMeters;
        }
    }
}
