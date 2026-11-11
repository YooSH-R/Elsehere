using A305.World;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public sealed class HudTopBarLayoutTests
    {
        [Test]
        public void SurveyInsertsOneSlotWithoutMovingPhoneOrSpeaker()
        {
            float before = HudTopBarLayout.StatusRight(false);
            float after = HudTopBarLayout.StatusRight(true);
            Assert.That(
                before,
                Is.EqualTo(
                    HudTopBarLayout.PhoneRight + HudTopBarLayout.PhoneWidth + HudTopBarLayout.Gap
                )
            );
            Assert.That(
                after - before,
                Is.EqualTo(HudTopBarLayout.SurveyWidth + HudTopBarLayout.Gap).Within(.001f)
            );
            Assert.That(
                HudTopBarLayout.PhoneRight,
                Is.GreaterThan(HudTopBarLayout.Margin + HudTopBarLayout.SpeakerWidth)
            );
        }
    }
}
