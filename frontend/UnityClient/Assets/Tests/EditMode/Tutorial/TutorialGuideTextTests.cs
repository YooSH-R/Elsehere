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
        public void TaskLine_ShowsDonePartsInGreen()
        {
            // 한 줄 안내의 " · "로 나뉜 할 일마다, 해낸 것은 초록색으로 바꿔 무엇이 남았는지 보이게 한다.
            string line = TutorialGuideText.TaskLine(
                "{W} 걷기 · {Shift} 달리기",
                new[] { true, false }
            );

            Assert.That(line, Does.StartWith("<color=#3E9B5A>[W] 걷기</color>"));
            Assert.That(line, Does.EndWith(" · " + TutorialGuideText.KeyChips("{Shift} 달리기")));
        }

        [Test]
        public void PartCount_CountsTheDotSeparatedTasks()
        {
            Assert.That(
                TutorialGuideText.PartCount("{W} 걷기 · {Shift} 달리기 · {V} 시점"),
                Is.EqualTo(3)
            );
            Assert.That(TutorialGuideText.PartCount("문으로 나가기"), Is.EqualTo(1));
        }

        [Test]
        public void ProgressDots_FillUpToTheCurrentStep()
        {
            Assert.That(TutorialGuideText.ProgressDots(2, 5), Is.EqualTo("● ● ○ ○ ○"));
            Assert.That(TutorialGuideText.ProgressDots(0, 3), Is.EqualTo("○ ○ ○"));
        }
    }
}
