using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ControlsGuideTextTests
    {
        [TestCase("WASD")]
        [TestCase("Shift")]
        [TestCase("Space")]
        [TestCase("마우스 왼쪽")]
        [TestCase("휠")]
        [TestCase("V")]
        [TestCase("사물 클릭")]
        [TestCase("NPC 클릭")]
        public void Body_ListsKeyboardAndMouseControlsOnComputers(string control)
        {
            Assert.That(ControlsGuideText.Body(isTouchDevice: false), Does.Contain(control));
        }

        [TestCase("조이스틱")]
        [TestCase("오른쪽 끌기")]
        [TestCase("1인칭 버튼")]
        [TestCase("사물 탭")]
        [TestCase("NPC 탭")]
        public void Body_ListsTouchControlsOnPhonesAndTablets(string control)
        {
            Assert.That(ControlsGuideText.Body(isTouchDevice: true), Does.Contain(control));
        }

        [TestCase("WASD")]
        [TestCase("Shift")]
        [TestCase("마우스")]
        public void Body_LeavesOutKeyboardControlsOnTouchDevices(string control)
        {
            Assert.That(ControlsGuideText.Body(isTouchDevice: true), Does.Not.Contain(control));
        }

        [Test]
        public void ReopenHint_NamesTheMenuItem()
        {
            Assert.That(
                ControlsGuideText.ReopenHint,
                Does.Contain("[" + ControlsGuideText.MenuLabel + "]")
            );
        }
    }
}
