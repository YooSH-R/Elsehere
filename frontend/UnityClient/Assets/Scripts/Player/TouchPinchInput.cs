using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 시점 영역에 댄 두 손가락 사이가 얼마나 벌어지고 좁아졌는지 모은다. 벌어지면 +, 좁아지면 -다.
    /// 처음 닿은 두 손가락만 세고, 한 손가락을 떼면 두 손가락 줌이 끝난다.
    /// </summary>
    public sealed class TouchPinchInput
    {
        private const int NoFinger = -1;

        private int firstId = NoFinger;
        private int secondId = NoFinger;
        private Vector2 first;
        private Vector2 second;
        private float pendingSpread;

        public bool IsPinching => firstId != NoFinger && secondId != NoFinger;

        public void Press(int pointerId, Vector2 position)
        {
            if (firstId == NoFinger)
            {
                firstId = pointerId;
                first = position;
            }
            else if (secondId == NoFinger && pointerId != firstId)
            {
                secondId = pointerId;
                second = position;
            }
        }

        public void Move(int pointerId, Vector2 position)
        {
            float gapBefore = Vector2.Distance(first, second);
            if (pointerId == firstId)
            {
                first = position;
            }
            else if (pointerId == secondId)
            {
                second = position;
            }
            else
            {
                return;
            }

            if (IsPinching)
            {
                pendingSpread += Vector2.Distance(first, second) - gapBefore;
            }
        }

        public void Release(int pointerId)
        {
            if (pointerId == firstId)
            {
                firstId = secondId;
                first = second;
                secondId = NoFinger;
            }
            else if (pointerId == secondId)
            {
                secondId = NoFinger;
            }
        }

        /// <summary>지난번 읽은 뒤로 두 손가락 사이가 벌어진 양. 한 번 읽으면 비운다.</summary>
        public float ConsumeSpread()
        {
            float spread = pendingSpread;
            pendingSpread = 0f;
            return spread;
        }

        public void Reset()
        {
            firstId = NoFinger;
            secondId = NoFinger;
            pendingSpread = 0f;
        }
    }
}
