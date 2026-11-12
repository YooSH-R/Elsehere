using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>
    /// 포인터(마우스·펜 또는 손가락 하나)마다 누른 자리를 따로 기억해 클릭·탭인지 정한다.
    /// 클릭을 막는 UI 위에서 누른 것은 기억하지 않고, 누른 자리에서 거의 움직이지 않고 뗀 것만 클릭으로 본다(<see cref="PointerClick"/>).
    /// 장치 입력은 <see cref="PointerChanges"/>로 읽는다.
    /// </summary>
    public sealed class PointerClicks
    {
        private readonly Func<Vector2, bool> isBlockedAt;
        private readonly Dictionary<int, Vector2> pressedAt = new Dictionary<int, Vector2>();

        /// <param name="isBlockedAt">화면 좌표에 클릭을 막는 UI가 있는지</param>
        public PointerClicks(Func<Vector2, bool> isBlockedAt)
        {
            this.isBlockedAt = isBlockedAt;
        }

        /// <summary>포인터를 눌렀다.</summary>
        /// <returns>클릭이 될 수 있는 누름으로 기억했으면 true</returns>
        public bool Press(int pointerId, Vector2 position)
        {
            if (isBlockedAt(position))
            {
                pressedAt.Remove(pointerId);
                return false;
            }

            pressedAt[pointerId] = position;
            return true;
        }

        /// <summary>포인터를 뗐다.</summary>
        /// <returns>클릭이면 true</returns>
        public bool Release(int pointerId, Vector2 position)
        {
            return pressedAt.Remove(pointerId, out Vector2 pressed)
                && PointerClick.IsClick(pressed, position);
        }

        /// <summary>기억한 누름을 모두 잊는다.</summary>
        public void Clear()
        {
            pressedAt.Clear();
        }
    }
}
