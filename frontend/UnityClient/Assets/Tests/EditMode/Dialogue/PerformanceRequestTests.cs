using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public sealed class PerformanceRequestTests
    {
        [TestCase("랩 해줘")]
        [TestCase("다시 보여줘")]
        [TestCase("한 번 더 들려주세요")]
        [TestCase("Can you rap?")]
        [TestCase("Could you do that again, please?")]
        [TestCase("One more time, please!")]
        [TestCase("Please show me your rap.")]
        public void ExplicitRequestsAreAccepted(string request) =>
            Assert.That(PerformanceRequest.Matches(request), Is.True);

        [TestCase("Don't rap")]
        [TestCase("랩 하지 마")]
        [TestCase("I'm happy again")]
        [TestCase("I like rap music")]
        [TestCase("What is a wrapper?")]
        [TestCase("How are you?")]
        [TestCase("")]
        public void OtherConversationIsPreserved(string request) =>
            Assert.That(PerformanceRequest.Matches(request), Is.False);
    }
}
