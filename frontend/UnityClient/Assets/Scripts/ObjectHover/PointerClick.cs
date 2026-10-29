using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>
    /// 누른 자리와 뗀 자리로 사물 클릭인지 정한다. 마우스 왼쪽 버튼을 누른 채 끌면 시점을 돌리므로,
    /// 거의 움직이지 않고 뗀 것만 클릭으로 본다.
    /// </summary>
    public static class PointerClick
    {
        // 손떨림은 클릭으로 보고, 이보다 많이 끌면 시점 회전으로 본다(화면 픽셀).
        private const float MaxMovePixels = 8f;

        public static bool IsClick(Vector2 pressedAt, Vector2 releasedAt)
        {
            return (releasedAt - pressedAt).sqrMagnitude <= MaxMovePixels * MaxMovePixels;
        }
    }
}
