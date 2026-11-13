using NUnit.Framework;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    /// <summary>포인터마다 누른 자리를 따로 기억해 클릭·탭인지 정하는지 확인한다.</summary>
    public class PointerClicksTests
    {
        private const int Thumb = 1;
        private const int Finger = 2;
        private static readonly Vector2 Joystick = new Vector2(150f, 150f);
        private static readonly Vector2 Npc = new Vector2(900f, 500f);
        private static readonly Vector2 MenuButton = new Vector2(60f, 1000f);

        [Test]
        public void ReleasedWhereItWasPressed_IsClick()
        {
            var clicks = new PointerClicks(_ => false);

            clicks.Press(PointerChanges.CursorId, Npc);

            Assert.That(
                clicks.Release(PointerChanges.CursorId, Npc + new Vector2(3f, 2f)),
                Is.True
            );
        }

        [Test]
        public void SecondFingerTapped_WhileThumbHoldsJoystick_IsClick()
        {
            // 휴대폰에서 엄지로 조이스틱을 잡고 걸으면서 다른 손가락으로 NPC를 탭했다.
            var clicks = new PointerClicks(_ => false);
            clicks.Press(Thumb, Joystick);
            clicks.Press(Finger, Npc);

            Assert.That(clicks.Release(Finger, Npc), Is.True);
            // 조이스틱을 끈 엄지는 클릭이 아니다.
            Assert.That(clicks.Release(Thumb, Joystick + new Vector2(60f, 0f)), Is.False);
        }

        [Test]
        public void DraggedToTurnCamera_IsNotClick()
        {
            var clicks = new PointerClicks(_ => false);

            clicks.Press(Finger, Npc);

            Assert.That(clicks.Release(Finger, Npc + new Vector2(60f, 0f)), Is.False);
        }

        [Test]
        public void PressedOnBlockingUi_IsNotClick()
        {
            var clicks = new PointerClicks(position => position == MenuButton);

            Assert.That(clicks.Press(Finger, MenuButton), Is.False);
            Assert.That(clicks.Release(Finger, MenuButton), Is.False);
        }

        [Test]
        public void ReleasedWithoutPress_IsNotClick()
        {
            var clicks = new PointerClicks(_ => false);

            Assert.That(clicks.Release(Finger, Npc), Is.False);
        }

        [Test]
        public void Cleared_ForgetsPresses()
        {
            // 화면이 꺼졌다 켜지는 사이에 누른 것은 다시 켠 뒤 뗀 것과 이어지지 않는다.
            var clicks = new PointerClicks(_ => false);
            clicks.Press(Finger, Npc);

            clicks.Clear();

            Assert.That(clicks.Release(Finger, Npc), Is.False);
        }
    }
}
