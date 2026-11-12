using A305.World;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public sealed class HudTopBarLayoutTests
    {
        [Test]
        public void SurveyInsertsOneSlotWithoutMovingThePhone()
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
            // 스피커는 설정 창으로 옮겨 휴대폰이 화면 오른쪽 여백에 바로 붙는다.
            Assert.That(HudTopBarLayout.PhoneRight, Is.EqualTo(HudTopBarLayout.Margin));
        }
    }
}
