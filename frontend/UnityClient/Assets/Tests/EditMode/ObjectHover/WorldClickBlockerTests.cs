using A305.Player;
using NUnit.Framework;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    /// <summary>
    /// 누른 자리에 UI가 있어도 사물 클릭으로 볼지. 휴대폰·터치 노트북에서 화면을 덮는 터치 조작판(이동·시점 끌기 영역)은
    /// 끌기만 받으므로 사물 카드 고정을 막지 않는다.
    /// </summary>
    public class WorldClickBlockerTests
    {
        private GameObject touchArea;
        private GameObject button;

        [SetUp]
        public void CreateUi()
        {
            touchArea = new GameObject("LookArea", typeof(RectTransform));
            touchArea.AddComponent<TouchDragArea>();
            button = new GameObject("Button", typeof(RectTransform));
        }

        [TearDown]
        public void DestroyUi()
        {
            Object.DestroyImmediate(touchArea);
            Object.DestroyImmediate(button);
        }

        [Test]
        public void NothingUnderThePointer_DoesNotBlock()
        {
            Assert.That(WorldClickBlocker.IsBlockedBy(new GameObject[0]), Is.False);
        }

        [Test]
        public void OnlyTheTouchControlSurface_DoesNotBlock()
        {
            Assert.That(WorldClickBlocker.IsBlockedBy(new[] { touchArea }), Is.False);
        }

        [Test]
        public void OtherUi_Blocks()
        {
            Assert.That(WorldClickBlocker.IsBlockedBy(new[] { button }), Is.True);
            Assert.That(WorldClickBlocker.IsBlockedBy(new[] { button, touchArea }), Is.True);
        }
    }
}
