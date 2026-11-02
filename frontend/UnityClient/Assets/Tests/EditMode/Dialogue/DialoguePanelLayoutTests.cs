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

        // 세로 화면은 창 높이가 정해져 있어, 누른 단어 줄을 열면 그만큼 창을 키워 최근 대사가 밀려나지 않게 한다.
        [Test]
        public void NarrowPanel_WithWordRow_GrowsByTheRow()
        {
            Assert.That(
                DialoguePanelLayout.Height(false, 120, showsWordRow: true),
                Is.EqualTo(534)
            );
        }

        // 넓은 화면도 단어 줄만큼 키우되, NPC 얼굴을 가리지 않도록 최대 높이는 넘지 않는다.
        [Test]
        public void WidePanel_WithWordRow_GrowsUpToItsMaximumHeight()
        {
            Assert.That(DialoguePanelLayout.Height(true, 40, showsWordRow: true), Is.EqualTo(434));
            Assert.That(DialoguePanelLayout.Height(true, 400, showsWordRow: true), Is.EqualTo(560));
        }
    }
}
