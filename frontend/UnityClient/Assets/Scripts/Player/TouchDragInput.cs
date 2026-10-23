using UnityEngine;

namespace A305.Player
{
    public sealed class TouchDragInput
    {
        private Vector2 origin;
        private Vector2 current;
        private Vector2 pendingDelta;
        private int owningPointerId;

        public bool IsHeld { get; private set; }

        public bool Begin(int pointerId, Vector2 position)
        {
            if (IsHeld)
            {
                return false;
            }

            IsHeld = true;
            owningPointerId = pointerId;
            origin = position;
            current = position;
            return true;
        }

        public void Drag(int pointerId, Vector2 position)
        {
            if (!IsHeld || owningPointerId != pointerId)
            {
                return;
            }

            pendingDelta += position - current;
            current = position;
        }

        public Vector2 GetDirection(float radius, float deadZone)
        {
            if (!IsHeld || radius <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 direction = (current - origin) / radius;
            float strength = Mathf.Min(direction.magnitude, 1f);
            if (strength <= deadZone)
            {
                return Vector2.zero;
            }

            return direction.normalized * ((strength - deadZone) / (1f - deadZone));
        }

        public void End(int pointerId)
        {
            if (IsHeld && owningPointerId == pointerId)
            {
                Reset();
            }
        }

        public Vector2 ConsumeDelta()
        {
            Vector2 delta = pendingDelta;
            pendingDelta = Vector2.zero;
            return delta;
        }

        public void Reset()
        {
            IsHeld = false;
            pendingDelta = Vector2.zero;
            origin = Vector2.zero;
            current = Vector2.zero;
        }
    }
}
