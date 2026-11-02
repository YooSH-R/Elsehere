using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class DialoguePanelLayoutTests
    {
        [Test]
        public void WidePanel_KeepsItsHeightForAShortLine()
        {
            Assert.That(DialoguePanelLayout.Height(true, 40), Is.EqualTo(390));
        }

        [Test]
        public void WidePanel_GrowsToShowEveryLineOfALongerLine()
        {
            Assert.That(DialoguePanelLayout.Height(true, 120), Is.EqualTo(460));
        }

        // 긴 대사와 뜻을 한 번에 보일 만큼 키우되, 그보다 길면 대사 칸 안에서 스크롤한다.
        [Test]
        public void WidePanel_StopsGrowingAtItsMaximumHeight()
        {
            Assert.That(DialoguePanelLayout.Height(true, 400), Is.EqualTo(560));
        }

        [Test]
        public void NarrowPanel_AlreadyHasRoomForSeveralLines()
        {
            Assert.That(DialoguePanelLayout.Height(false, 120), Is.EqualTo(480));
        }
    }
}
