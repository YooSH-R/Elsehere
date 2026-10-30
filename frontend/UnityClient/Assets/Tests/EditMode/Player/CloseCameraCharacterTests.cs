using NUnit.Framework;

namespace A305.Player.Tests
{
    /// <summary>
    /// 좁은 실내에서 벽에 밀린 3인칭 카메라가 캐릭터 머리 속을 비추지 않도록, 너무 가까워지면 캐릭터를 숨긴다.
    /// 기준 근처에서 깜빡이지 않게 숨기는 거리와 다시 보이는 거리를 나눈다.
    /// </summary>
    public class CloseCameraCharacterTests
    {
        private CloseCameraCharacter rule;

        [SetUp]
        public void Create()
        {
            rule = new CloseCameraCharacter();
        }

        [Test]
        public void CameraAtNormalDistance_ShowsTheCharacter()
        {
            Assert.That(rule.IsHidden(3.8f), Is.False);
        }

        [Test]
        public void CameraPushedIntoTheCharacter_HidesIt()
        {
            Assert.That(rule.IsHidden(.9f), Is.True);
        }

        [Test]
        public void CameraLeavingSlightly_StaysHiddenUntilClearlyAway()
        {
            rule.IsHidden(.9f);

            Assert.That(rule.IsHidden(CloseCameraCharacter.HideBelowMeters + .05f), Is.True);
            Assert.That(rule.IsHidden(CloseCameraCharacter.ShowAboveMeters + .05f), Is.False);
        }

        [Test]
        public void CameraApproachingSlightly_StaysVisibleUntilReallyClose()
        {
            rule.IsHidden(3.8f);

            Assert.That(rule.IsHidden(CloseCameraCharacter.ShowAboveMeters - .05f), Is.False);
            Assert.That(rule.IsHidden(CloseCameraCharacter.HideBelowMeters - .05f), Is.True);
        }
    }
}
