using UnityEngine;

namespace A305.Player
{
    public static class DialogueFraming
    {
        public static Pose Pose(
            Vector3 eye,
            Vector3 target,
            float verticalFieldOfView,
            float aspect,
            Vector2 facePosition
        )
        {
            float halfHeight = Mathf.Tan(verticalFieldOfView * .5f * Mathf.Deg2Rad);
            Vector3 screenRay = new Vector3(
                (facePosition.x - .5f) * 2 * halfHeight * aspect,
                (facePosition.y - .5f) * 2 * halfHeight,
                1
            );
            Vector3 ray = screenRay.normalized;
            Vector3 direction = (target - eye).normalized;
            float verticalRadius = Mathf.Sqrt(ray.y * ray.y + ray.z * ray.z);
            float pitch =
                Mathf.Atan2(ray.y, ray.z)
                - Mathf.Asin(Mathf.Clamp(direction.y / verticalRadius, -1, 1));
            float pitchedZ = ray.y * Mathf.Sin(pitch) + ray.z * Mathf.Cos(pitch);
            float yaw = Mathf.Atan2(direction.x, direction.z) - Mathf.Atan2(ray.x, pitchedZ);
            // 수평선은 기울이지 않고 yaw·pitch만으로 얼굴을 지정한 화면 위치에 둔다.
            Quaternion rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0);
            return new Pose(eye, rotation);
        }
    }
}
