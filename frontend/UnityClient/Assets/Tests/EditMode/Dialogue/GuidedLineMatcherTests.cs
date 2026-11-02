using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    /// <summary>튜토리얼처럼 정해진 대사를 따라 말하는 대화에서 입력이 그 대사와 같은지.</summary>
    public class GuidedLineMatcherTests
    {
        [TestCase("Go straight ahead.", "Go straight ahead.")]
        [TestCase("go straight ahead", "Go straight ahead.")]
        [TestCase("  Go   straight ahead!  ", "Go straight ahead.")]
        [TestCase("No its just two minutes away", "No, it's just two minutes away.")]
        public void IsSameLine_IgnoresCaseSpacingAndPunctuation(string said, string line)
        {
            Assert.That(GuidedLineMatcher.IsSameLine(said, line), Is.True);
        }

        [TestCase("Go strait ahead.", "Go straight ahead.")]
        [TestCase("Turn left at the coner.", "Turn left at the corner.")]
        public void IsSameLine_AllowsASmallTypo(string said, string line)
        {
            Assert.That(GuidedLineMatcher.IsSameLine(said, line), Is.True);
        }

        [TestCase("Hello there", "Go straight ahead.")]
        [TestCase("Go", "Go straight ahead.")]
        [TestCase("Turn right at the corner.", "Turn left at the corner.")]
        [TestCase("", "Go straight ahead.")]
        [TestCase(null, "Go straight ahead.")]
        public void IsSameLine_RejectsADifferentSentence(string said, string line)
        {
            Assert.That(GuidedLineMatcher.IsSameLine(said, line), Is.False);
        }
    }
}
