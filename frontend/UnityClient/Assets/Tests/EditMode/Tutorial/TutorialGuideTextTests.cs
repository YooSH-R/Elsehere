using NUnit.Framework;

namespace A305.Tutorial.Tests
{
    /// <summary>안내 문구의 {키} 표시를 키 모양 칩으로 바꾸고, 진행 점을 그리는지.</summary>
    public class TutorialGuideTextTests
    {
        [Test]
        public void KeyChips_WrapEachKeyInAChip()
        {
            string text = TutorialGuideText.KeyChips("{W}{A} 걷기");

            Assert.That(text, Does.Not.Contain("{"));
            Assert.That(text, Does.Contain("[W]"));
            Assert.That(text, Does.Contain("[A]"));
            Assert.That(text, Does.Contain("<color="));
            Assert.That(text, Does.EndWith(" 걷기"));
        }

        [Test]
        public void KeyChips_LeaveTextWithoutKeysAsIs()
        {
            Assert.That(TutorialGuideText.KeyChips("문으로 나가기"), Is.EqualTo("문으로 나가기"));
        }

        [Test]
        public void VisibleLength_CountsOnlyWhatTheReaderSees()
        {
            Assert.That(TutorialGuideText.VisibleLength("{Shift} 달리기"), Is.EqualTo(11));
        }

        [Test]
        public void ProgressDots_FillUpToTheCurrentStep()
        {
            Assert.That(TutorialGuideText.ProgressDots(2, 5), Is.EqualTo("● ● ○ ○ ○"));
            Assert.That(TutorialGuideText.ProgressDots(0, 3), Is.EqualTo("○ ○ ○"));
        }
    }
}
