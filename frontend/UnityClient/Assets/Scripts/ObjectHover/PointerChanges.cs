using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace A305.ObjectHover
{
    /// <summary>포인터 하나(마우스·펜 또는 손가락 하나)를 누르거나 뗀 일</summary>
    public readonly struct PointerChange
    {
        public PointerChange(int pointerId, bool isPress, Vector2 position)
        {
            PointerId = pointerId;
            IsPress = isPress;
            Position = position;
        }

        /// <summary>마우스·펜은 <see cref="PointerChanges.CursorId"/>, 손가락은 그 손가락의 번호(touchId)</summary>
        public int PointerId { get; }

        /// <summary>눌렀으면 true, 뗐으면 false</summary>
        public bool IsPress { get; }

        /// <summary>누르거나 뗀 화면 좌표</summary>
        public Vector2 Position { get; }
    }

    /// <summary>
    /// 이번 프레임에 누르거나 뗀 포인터를 모은다. 휴대폰에서는 처음 닿은 손가락만이 아니라 손가락마다 따로 알려 주어,
    /// 엄지로 조이스틱을 잡은 채 다른 손가락으로 탭한 것도 놓치지 않는다.
    /// 화면이 잠깐 멈춰 한 프레임 안에 누르고 뗐으면 누름 다음 뗌으로 알려 준다.
    /// 누른 직후를 알 수 있도록 매 프레임 읽어야 한다.
    /// </summary>
    public static class PointerChanges
    {
        /// <summary>마우스·펜처럼 손가락이 아닌 포인터의 번호. 손가락 번호(touchId)는 1부터다.</summary>
        public const int CursorId = 0;

        /// <summary>가장 최근에 쓴 포인터 장치에서 이번 프레임의 누름·뗌을 changes에 담는다.</summary>
        public static void Read(List<PointerChange> changes)
        {
            changes.Clear();
            Pointer device = Pointer.current;
            if (device is Touchscreen touchscreen)
            {
                foreach (TouchControl touch in touchscreen.touches)
                {
                    Add(
                        changes,
                        touch.touchId.ReadValue(),
                        touch.press,
                        touch.startPosition.ReadValue(),
                        touch.position.ReadValue()
                    );
                }
            }
            else if (device != null)
            {
                Vector2 position = device.position.ReadValue();
                Add(changes, CursorId, device.press, position, position);
            }
        }

        // 한 프레임 안에 누르고 뗐으면 둘 다 담는다. 손가락은 처음 닿은 자리를 누른 자리로 본다.
        private static void Add(
            List<PointerChange> changes,
            int pointerId,
            ButtonControl press,
            Vector2 pressedAt,
            Vector2 position
        )
        {
            if (press.wasPressedThisFrame)
            {
                changes.Add(new PointerChange(pointerId, true, pressedAt));
            }

            if (press.wasReleasedThisFrame)
            {
                changes.Add(new PointerChange(pointerId, false, position));
            }
        }
    }
}
