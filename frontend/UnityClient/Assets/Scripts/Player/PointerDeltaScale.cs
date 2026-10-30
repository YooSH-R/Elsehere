using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 브라우저는 마우스 이동을 화면 배율(devicePixelRatio)을 곱한 픽셀로 넘긴다.
    /// 화면 배율과 관계없이 같은 손 움직임에 같은 각도로 돌도록 배율로 나눈다.
    /// </summary>
    public static class PointerDeltaScale
    {
        public static Vector2 ToLogicalPixels(Vector2 delta, float pixelRatio)
        {
            return pixelRatio > 0f ? delta / pixelRatio : delta;
        }
    }
}
