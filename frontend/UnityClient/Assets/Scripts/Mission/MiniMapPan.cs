using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 미니맵을 끌어 둘러보는 창 가운데. 끄는 동안 지도는 커서를 따라 움직이고 지도 가장자리에서 멈춘다.
    /// 손을 떼고 <see cref="ReturnDelay"/>초 동안은 둘러본 자리에 머물고, 그 뒤 부드럽게 내 위치로 돌아와 다시 따라간다.
    /// 좌표는 <see cref="TownMapArt"/> 그림 단위(아래로 갈수록 y가 커진다)다.
    /// </summary>
    public sealed class MiniMapPan
    {
        public const float ReturnDelay = 3f;
        private const float ReturnSeconds = .4f;

        private Vector2? looking;
        private bool holding;
        private float releasedAt;

        /// <param name="shown">지금 창에 보이는 가운데</param>
        /// <param name="cursorMove">커서가 움직인 거리(그림 단위)</param>
        /// <param name="window">창 크기(그림 단위)</param>
        public void Drag(Vector2 shown, Vector2 cursorMove, Vector2 window)
        {
            holding = true;
            looking = TownMapArt.Window(shown - cursorMove, window) + window / 2f;
        }

        public void Release(float now)
        {
            holding = false;
            releasedAt = now;
        }

        /// <summary>창 가운데. 둘러보는 중이 아니면 내 위치다.</summary>
        public Vector2 Centre(Vector2 player, float now)
        {
            if (!looking.HasValue)
                return player;
            float back = holding ? 0f : (now - releasedAt - ReturnDelay) / ReturnSeconds;
            if (back >= 1f)
            {
                looking = null;
                return player;
            }
            return Vector2.Lerp(looking.Value, player, Mathf.SmoothStep(0f, 1f, back));
        }
    }
}
