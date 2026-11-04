using NUnit.Framework;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 피드백 화면 아래 쪽수. 카드 번호와 카드 안 글 쪽수를 "1 / 1 · 1/2"처럼 따로 보이면 헷갈려서, 모든 쪽을 이어 센 하나의 번호로 보인다.
    /// </summary>
    public class FeedbackPageNumberTests
    {
        [Test]
        public void OneCardWithTwoTextPages_CountsTheTextPages()
        {
            Assert.That(FeedbackPageNumber.Format(new[] { 2 }, 0, 2), Is.EqualTo("2 / 2"));
        }

        [Test]
        public void SeveralCards_CountsEveryPageInOrder()
        {
            Assert.That(FeedbackPageNumber.Format(new[] { 1, 3, 1 }, 1, 2), Is.EqualTo("3 / 5"));
        }

        [Test]
        public void SinglePage_ShowsOneOfOne()
        {
            Assert.That(FeedbackPageNumber.Format(new[] { 1 }, 0, 1), Is.EqualTo("1 / 1"));
        }
    }
}
