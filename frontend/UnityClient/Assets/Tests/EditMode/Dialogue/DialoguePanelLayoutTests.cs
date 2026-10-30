using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class DialoguePanelLayoutTests
    {
        [Test]
        public void WidePanel_KeepsItsHeightForAShortLine()
        {
            Assert.That(DialoguePanelLayout.Height(true, 40), Is.EqualTo(360));
        }

        [Test]
        public void WidePanel_GrowsToShowEveryLineOfALongerLine()
        {
            Assert.That(DialoguePanelLayout.Height(true, 120), Is.EqualTo(406));
        }

        // 그보다 긴 대사와 뜻 풀이는 대사 칸 안에서 스크롤한다.
        [Test]
        public void WidePanel_StopsGrowingAtTheNarrowPanelHeight()
        {
            Assert.That(DialoguePanelLayout.Height(true, 400), Is.EqualTo(440));
        }

        [Test]
        public void NarrowPanel_AlreadyHasRoomForSeveralLines()
        {
            Assert.That(DialoguePanelLayout.Height(false, 120), Is.EqualTo(440));
        }
    }
}
