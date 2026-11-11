using NUnit.Framework;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 왼쪽 위 퀘스트 카드는 평소 알약으로 접혀 있다. 알약을 눌러 펼치면 접기 버튼을 누를 때까지 그대로 두고,
    /// 퀘스트를 시작했을 때만 5초 펼쳤다가 저절로 접는다.
    /// </summary>
    public sealed class QuestCardFoldTests
    {
        [Test]
        public void StartingAQuest_ExpandsForFiveSecondsThenFolds()
        {
            var fold = new QuestCardFold();

            fold.Announce();
            Assert.That(fold.Tick(4.9f), Is.False);
            Assert.That(fold.Expanded, Is.True, "5초 동안 펼친다");
            Assert.That(fold.AutoFoldLeft, Is.EqualTo(.1f / 5f).Within(.001f));

            Assert.That(fold.Tick(.2f), Is.True, "저절로 접혔다고 알린다");
            Assert.That(fold.Expanded, Is.False);
            Assert.That(fold.AutoFoldLeft, Is.Zero);
        }

        [Test]
        public void ExpandedByTheUser_StaysOpenUntilFolded()
        {
            var fold = new QuestCardFold();

            fold.Expand();
            Assert.That(fold.Tick(60), Is.False);
            Assert.That(fold.Expanded, Is.True);
            Assert.That(fold.AutoFoldLeft, Is.Zero, "저절로 접는 막대를 보이지 않는다");

            fold.Fold();
            Assert.That(fold.Expanded, Is.False);
        }
    }
}
