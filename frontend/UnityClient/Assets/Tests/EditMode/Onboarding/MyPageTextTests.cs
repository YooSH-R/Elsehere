using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    public class MyPageTextTests
    {
        [Test]
        public void Profile_ShowsNicknameCharacterAndLanguage()
        {
            var user = new OnboardingUser
            {
                Nickname = "Sun",
                AvatarType = AvatarType.F,
                CurrentLanguage = "en-US",
            };

            Assert.That(
                MyPageText.Profile(user),
                Is.EqualTo("닉네임 · Sun\n캐릭터 · 여성\n배우는 언어 · 영어")
            );
        }

        [Test]
        public void Profile_MaleCharacter_SaysMale()
        {
            var user = new OnboardingUser
            {
                Nickname = "Moon",
                AvatarType = AvatarType.M,
                CurrentLanguage = "en-US",
            };

            Assert.That(MyPageText.Profile(user), Does.Contain("캐릭터 · 남성"));
        }

        [Test]
        public void Profile_LanguageNotChosenYet_SaysSo()
        {
            var user = new OnboardingUser { Nickname = "Sun", CurrentLanguage = null };

            Assert.That(
                MyPageText.Profile(user),
                Does.Contain("배우는 언어 · 아직 고르지 않았어요")
            );
        }

        [Test]
        public void Profile_UnknownLanguage_ShowsLanguageCode()
        {
            var user = new OnboardingUser { Nickname = "Sun", CurrentLanguage = "ja-JP" };

            Assert.That(MyPageText.Profile(user), Does.Contain("배우는 언어 · ja-JP"));
        }
    }
}
