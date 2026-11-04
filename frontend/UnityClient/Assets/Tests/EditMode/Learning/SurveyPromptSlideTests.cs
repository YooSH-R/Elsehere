using NUnit.Framework;

namespace A305.Learning.Tests
{
    /// <summary>설문 카드는 화면 위 바깥에서 시작해 정한 시간 동안 내려와 제자리에 멈춘다. 끝으로 갈수록 천천히 내려온다.</summary>
    public class SurveyPromptSlideTests
    {
        private const float CardHeight = 100f;

        [Test]
        public void Start_BeginsAboveItsPlaceByItsHeight()
        {
            var slide = new SurveyPromptSlide(CardHeight);

            slide.Start();

            Assert.That(slide.Offset, Is.EqualTo(CardHeight));
            Assert.That(slide.IsDone, Is.False);
        }

        [Test]
        public void Advance_ReachesItsPlaceAfterTheDuration()
        {
            var slide = new SurveyPromptSlide(CardHeight);
            slide.Start();

            slide.Advance(SurveyPromptSlide.Duration);

            Assert.That(slide.Offset, Is.EqualTo(0f));
            Assert.That(slide.IsDone, Is.True);
        }

        [Test]
        public void Advance_MovesMoreThanHalfwayInHalfTheTime()
        {
            var slide = new SurveyPromptSlide(CardHeight);
            slide.Start();

            slide.Advance(SurveyPromptSlide.Duration / 2);

            Assert.That(slide.Offset, Is.LessThan(CardHeight / 2));
            Assert.That(slide.Offset, Is.GreaterThan(0f));
        }

        [Test]
        public void Advance_PastTheDuration_StaysAtItsPlace()
        {
            var slide = new SurveyPromptSlide(CardHeight);
            slide.Start();

            slide.Advance(SurveyPromptSlide.Duration * 3);

            Assert.That(slide.Offset, Is.EqualTo(0f));
        }
    }
}
