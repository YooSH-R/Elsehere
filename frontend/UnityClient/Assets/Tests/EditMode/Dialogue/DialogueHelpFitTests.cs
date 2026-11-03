using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    /// <summary>
    /// 오른쪽 위 단어·문장 카드는 대화창 이름 줄 위 남는 높이에 다 들어가도록 줄인다. 너무 작아 읽기 어렵지 않게 0.7배까지만 줄인다.
    /// </summary>
    public class DialogueHelpFitTests
    {
        [TestCase(600, 400, 1f)]
        [TestCase(400, 500, .8f)]
        [TestCase(200, 600, .7f)]
        public void Scale_FitsTheCardsIntoTheRoomDownToSeventyPercent(
            float room,
            float needed,
            float expected
        )
        {
            Assert.That(DialogueHelpFit.Scale(room, needed), Is.EqualTo(expected).Within(.001f));
        }
    }
}
