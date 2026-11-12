using System.Collections.Generic;
using UnityEngine;

namespace A305.EditorTools.Mission
{
    /// <summary>앉을 수 있는 가구를 고르고, 등받이 없는 자리가 바라볼 방향을 정한다.</summary>
    public static class RestSeatLayout
    {
        // 앉아서 마주한다고 볼 거리. 오락기 앞 스툴은 0.2m, 탁자 옆 의자는 0.3m 안팎이고, 세탁소 대기 의자 옆 탁자(0.74m)는 마주하지 않는다.
        private const float FacingReachMeters = .6f;

        private static readonly HashSet<string> SeatWords = new HashSet<string>
        {
            "chair",
            "bench",
            "sofa",
            "stool",
            "barber chair",
        };

        /// <summary>호버 단어(영어)가 앉으라고 만든 가구인지. 침대·계단은 앉는 가구로 보지 않는다.</summary>
        public static bool IsSeatWord(string englishWord)
        {
            return englishWord != null && SeatWords.Contains(englishWord);
        }

        /// <summary>
        /// 등받이 없는 자리가 바라볼 방향. 가구 앞(+z) 기준 0·90·180·270도 중 하나다.
        /// 마주할 면이 가까이 없으면 가구가 놓인 방향 그대로 본다.
        /// </summary>
        /// <param name="surfaceOffsets">가구 방향 좌표에서 잰, 자리 중심에서 주변 탁자·카운터·오락기의 가장 가까운 점까지</param>
        public static float BacklessYawDegrees(IReadOnlyList<Vector3> surfaceOffsets)
        {
            Vector3 nearest = Vector3.zero;
            float nearestDistance = FacingReachMeters;
            foreach (Vector3 offset in surfaceOffsets)
            {
                var horizontal = new Vector3(offset.x, 0f, offset.z);
                if (horizontal.magnitude > nearestDistance)
                    continue;
                nearest = horizontal;
                nearestDistance = horizontal.magnitude;
            }
            if (nearest == Vector3.zero)
                return 0f;

            float yaw = Mathf.Atan2(nearest.x, nearest.z) * Mathf.Rad2Deg;
            float squared = Mathf.Round(yaw / 90f) * 90f;
            return Mathf.Repeat(squared, 360f);
        }
    }
}
