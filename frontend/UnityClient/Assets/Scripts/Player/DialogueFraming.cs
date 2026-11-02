using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 대화 시점 구도. 1·3인칭과 따로, NPC 얼굴을 기준으로 카메라를 놓는다. 카메라는 얼굴에서 플레이어 쪽으로 정해진 거리·높이에
    /// 두고, 얼굴을 화면의 정해진 자리에 오게 돌린다. 그래서 플레이어가 NPC에게서 얼마나 떨어져 섰든 NPC마다 얼굴이 같은 크기로 보인다.
    /// </summary>
    public static class DialogueFraming
    {
        /// <param name="face">NPC 얼굴(NpcIdentity.LookTarget) 위치</param>
        /// <param name="playerEye">플레이어 눈 위치. 카메라를 얼굴의 어느 쪽에 둘지만 정한다.</param>
        /// <param name="distanceMeters">얼굴에서 카메라까지 수평 거리</param>
        /// <param name="heightOffsetMeters">카메라가 얼굴보다 높은 정도</param>
        public static Vector3 CameraPosition(
            Vector3 face,
            Vector3 playerEye,
            float distanceMeters,
            float heightOffsetMeters
        )
        {
            Vector3 towardPlayer = playerEye - face;
            towardPlayer.y = 0f;
            // 플레이어가 얼굴 바로 위·아래에 겹쳐 방향을 알 수 없으면 얼굴 속에 두지 않도록 한쪽으로 물러난다.
            Vector3 direction =
                towardPlayer.sqrMagnitude > 0.0001f ? towardPlayer.normalized : Vector3.back;
            return face + direction * distanceMeters + Vector3.up * heightOffsetMeters;
        }

        /// <summary>카메라 위치에서 얼굴이 화면의 <paramref name="facePosition"/>(왼쪽 아래 0, 오른쪽 위 1)에 오도록 돌린 자세.</summary>
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
