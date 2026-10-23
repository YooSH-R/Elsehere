using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    public class TouchDragInputTests
    {
        [TestCase(100f, 5f, 0f)]
        [TestCase(100f, 10f, 0f)]
        [TestCase(100f, 55f, 0.5f)]
        [TestCase(0f, 50f, 0f)]
        public void GetDirection_RespectsDeadZoneAndUninitializedRadius(
            float radius,
            float displacement,
            float expected
        )
        {
            var input = new TouchDragInput();
            input.Begin(1, Vector2.zero);
            input.Drag(1, Vector2.up * displacement);

            Assert.That(input.GetDirection(radius, 0.1f).y, Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReleaseOrCancel_DiscardsHeldMovementAndPendingLook(bool cancelled)
        {
            var input = new TouchDragInput();
            input.Begin(1, Vector2.zero);
            input.Drag(1, new Vector2(50f, 30f));

            if (cancelled)
            {
                input.Reset();
            }
            else
            {
                input.End(1);
            }

            Assert.That(input.IsHeld, Is.False);
            Assert.That(input.GetDirection(100f, 0f), Is.EqualTo(Vector2.zero));
            Assert.That(input.ConsumeDelta(), Is.EqualTo(Vector2.zero));
            Assert.That(input.Begin(2, new Vector2(200f, 100f)), Is.True);
            Assert.That(input.ConsumeDelta(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ConsumeDelta_LookFingerMovesIndependently_AndDeltaIsConsumedOnce()
        {
            var movement = new TouchDragInput();
            var look = new TouchDragInput();
            movement.Begin(1, Vector2.zero);
            movement.Drag(1, Vector2.up * 50f);
            look.Begin(2, new Vector2(100f, 100f));
            look.Drag(2, new Vector2(110f, 105f));
            look.Drag(2, new Vector2(125f, 115f));

            Assert.That(look.ConsumeDelta(), Is.EqualTo(new Vector2(25f, 15f)));
            Assert.That(look.ConsumeDelta(), Is.EqualTo(Vector2.zero));
            Assert.That(movement.GetDirection(100f, 0f), Is.EqualTo(Vector2.up * 0.5f));
        }

        [Test]
        public void Drag_OnlyOwningFingerControlsMovement_AndReleaseStopsIt()
        {
            var input = new TouchDragInput();
            input.Begin(1, Vector2.zero);
            Assert.That(input.Begin(2, Vector2.zero), Is.False);

            input.Drag(2, Vector2.up * 100f);
            Assert.That(input.GetDirection(100f, 0f), Is.EqualTo(Vector2.zero));

            input.Drag(1, Vector2.right * 100f);
            input.End(2);
            Assert.That(input.GetDirection(100f, 0f), Is.EqualTo(Vector2.right));

            input.End(1);
            Assert.That(input.GetDirection(100f, 0f), Is.EqualTo(Vector2.zero));
            Assert.That(input.Begin(2, Vector2.zero), Is.True);
        }

        [Test]
        public void GetDirection_DragBeyondRadius_ClampsDiagonalMovement()
        {
            var input = new TouchDragInput();
            input.Begin(1, Vector2.zero);
            input.Drag(1, new Vector2(200f, 200f));

            Vector2 direction = input.GetDirection(100f, 0.1f);

            Assert.That(direction.x, Is.EqualTo(0.7071f).Within(0.0001f));
            Assert.That(direction.y, Is.EqualTo(0.7071f).Within(0.0001f));
        }
    }
}
