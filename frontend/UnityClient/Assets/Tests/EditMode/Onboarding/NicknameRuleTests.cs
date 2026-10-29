using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    // 서버의 닉네임 규칙(AUTH-04)과 같은 기준으로 입력 중에 바로 안내한다. 최종 판단은 서버가 한다.
    public class NicknameRuleTests
    {
        [TestCase("Sun")]
        [TestCase("선종")]
        [TestCase("Sun선종12")]
        [TestCase("ab")]
        [TestCase("가나다라마바사아")]
        [TestCase("abcdefghijklmnop")]
        [TestCase("가나다라마바사ab")]
        public void NicknameFollowingRule_HasNoProblem(string nickname)
        {
            Assert.That(NicknameRule.Check(nickname), Is.EqualTo(NicknameProblem.None));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("a")]
        [TestCase("선")]
        public void NicknameShorterThanTwoCharacters_IsTooShort(string nickname)
        {
            Assert.That(NicknameRule.Check(nickname), Is.EqualTo(NicknameProblem.TooShort));
        }

        [TestCase("가나다라마바사아자")]
        [TestCase("abcdefghijklmnopq")]
        [TestCase("가나다라마바사abc")]
        public void NicknameWiderThanSixteen_IsTooLong(string nickname)
        {
            Assert.That(NicknameRule.Check(nickname), Is.EqualTo(NicknameProblem.TooLong));
        }

        [TestCase("Sun Lee")]
        [TestCase("Sun!")]
        [TestCase("ㅋㅋ")]
        [TestCase("Ｓｕｎ")]
        [TestCase("\U0001F600\U0001F600")]
        public void NicknameWithDisallowedCharacter_HasInvalidCharacter(string nickname)
        {
            Assert.That(NicknameRule.Check(nickname), Is.EqualTo(NicknameProblem.InvalidCharacter));
        }

        [TestCase(NicknameProblem.TooShort, "2자 이상 입력해 주세요.")]
        [TestCase(NicknameProblem.TooLong, "한글은 8자, 영문·숫자는 16자까지 쓸 수 있어요.")]
        [TestCase(NicknameProblem.InvalidCharacter, "한글, 영문, 숫자만 쓸 수 있어요.")]
        public void Describe_Problem_TellsHowToFix(NicknameProblem problem, string expected)
        {
            Assert.That(NicknameRule.Describe(problem), Is.EqualTo(expected));
        }

        [Test]
        public void Describe_NoProblem_SaysNothing()
        {
            Assert.That(NicknameRule.Describe(NicknameProblem.None), Is.Null);
        }
    }
}
