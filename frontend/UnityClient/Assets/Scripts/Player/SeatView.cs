using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 앉은 캐릭터를 플레이어가 다가온 쪽에서 보도록 카메라 수평 방향을 정한다. 다가온 쪽은 플레이어가 서 있던 트인 곳이라 탁자·벽 너머에서 보지 않는다.
    /// </summary>
    public static class SeatView
    {
        // 이보다 가까이 서서 앉으면 다가온 방향을 알 수 없다고 본다.
        private const float MinimumApproachMeters = .05f;

        public static float CameraYawDegrees(
            Vector3 approachPosition,
            Vector3 seatPosition,
            float seatYawDegrees
        )
        {
            Vector3 towardSeat = seatPosition - approachPosition;
            towardSeat.y = 0f;
            if (towardSeat.magnitude < MinimumApproachMeters)
                return seatYawDegrees + 180f;
            return Mathf.Atan2(towardSeat.x, towardSeat.z) * Mathf.Rad2Deg;
        }
    }
}
