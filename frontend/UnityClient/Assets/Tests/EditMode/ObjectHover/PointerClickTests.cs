using NUnit.Framework;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class PointerClickTests
    {
        [Test]
        public void IsClick_ReleasedWhereItWasPressed_IsClick()
        {
            Assert.That(
                PointerClick.IsClick(new Vector2(400f, 300f), new Vector2(400f, 300f)),
                Is.True
            );
        }

        [Test]
        public void IsClick_SlightHandShake_IsStillClick()
        {
            Assert.That(
                PointerClick.IsClick(new Vector2(400f, 300f), new Vector2(405f, 304f)),
                Is.True
            );
        }

        [Test]
        public void IsClick_DraggedToTurnCamera_IsNotClick()
        {
            // 마우스 왼쪽 버튼을 누른 채 끌어 시점을 돌린 것은 사물 클릭이 아니다.
            Assert.That(
                PointerClick.IsClick(new Vector2(400f, 300f), new Vector2(460f, 300f)),
                Is.False
            );
        }
    }
}
