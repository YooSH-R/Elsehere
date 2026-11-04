using NUnit.Framework;

namespace A305.World.Tests
{
    public class OpeningHoursTextTests
    {
        [TestCase("카페", "07:00–21:00", "카페는 지금 영업시간이 아니에요 · 07:00–21:00")]
        [TestCase(
            "드럭스토어",
            "09:00–22:00",
            "드럭스토어는 지금 영업시간이 아니에요 · 09:00–22:00"
        )]
        public void ClosedNotice_AfterNameWithoutFinalConsonant_UsesNeun(
            string placeName,
            string hours,
            string expected
        )
        {
            Assert.That(OpeningHoursText.ClosedNotice(placeName, hours), Is.EqualTo(expected));
        }

        [TestCase("우체국", "09:00–17:00", "우체국은 지금 영업시간이 아니에요 · 09:00–17:00")]
        [TestCase("타운 은행", "09:00–16:00", "타운 은행은 지금 영업시간이 아니에요 · 09:00–16:00")]
        public void ClosedNotice_AfterNameWithFinalConsonant_UsesEun(
            string placeName,
            string hours,
            string expected
        )
        {
            Assert.That(OpeningHoursText.ClosedNotice(placeName, hours), Is.EqualTo(expected));
        }

        [Test]
        public void ClosingNotice_ShowsMinutesLeft()
        {
            Assert.That(
                OpeningHoursText.ClosingNotice(30),
                Is.EqualTo("영업이 끝났어요. 30분 후 마을로 이동합니다")
            );
        }

        [Test]
        public void ClosingNotice_WithNoMinutesLeft_SaysItIsMovingNow()
        {
            Assert.That(
                OpeningHoursText.ClosingNotice(0),
                Is.EqualTo("영업이 끝났어요. 지금 마을로 이동합니다")
            );
        }
    }
}
