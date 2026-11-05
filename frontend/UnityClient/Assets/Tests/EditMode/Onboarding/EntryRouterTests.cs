using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    public class EntryRouterTests
    {
        [Test]
        public void SignupRequired_GoesToTermsFirst()
        {
            var boot = new BootstrapResponse { AuthState = AuthState.SignupRequired };

            // S15P31A305-596: 가입은 약관 동의부터 받는다.
            Assert.That(EntryRouter.Route(boot, null), Is.EqualTo(EntryScreen.Terms));
        }

        [TestCase(OnboardingStep.Language, EntryScreen.Language)]
        [TestCase(OnboardingStep.Tutorial, EntryScreen.Tutorial)]
        [TestCase(OnboardingStep.Done, EntryScreen.World)]
        public void SignedIn_GoesToScreenOfOnboardingStep(OnboardingStep step, EntryScreen expected)
        {
            Assert.That(EntryRouter.Route(SignedIn(step), null), Is.EqualTo(expected));
        }

        [Test]
        public void LevelStepWithoutLevelTest_GoesToLevelTest()
        {
            Assert.That(
                EntryRouter.Route(SignedIn(OnboardingStep.Level), null),
                Is.EqualTo(EntryScreen.LevelTest)
            );
        }

        [Test]
        public void LevelStepWithTestInProgress_GoesBackToLevelTest()
        {
            // 중간에 나갔다 오면 이어서 풀지 않고 화면 04에서 다시 시작한다.
            Assert.That(
                EntryRouter.Route(SignedIn(OnboardingStep.Level), LevelTestStatus.InProgress),
                Is.EqualTo(EntryScreen.LevelTest)
            );
        }

        [TestCase(LevelTestStatus.Done)]
        [TestCase(LevelTestStatus.Quit)]
        [TestCase(LevelTestStatus.Skipped)]
        public void LevelStepWithFinishedTest_GoesToLevelSetting(LevelTestStatus status)
        {
            Assert.That(
                EntryRouter.Route(SignedIn(OnboardingStep.Level), status),
                Is.EqualTo(EntryScreen.LevelSetting)
            );
        }

        private static BootstrapResponse SignedIn(OnboardingStep step)
        {
            return new BootstrapResponse
            {
                AuthState = AuthState.SignedIn,
                User = new OnboardingUser { OnboardingStep = step, CurrentLanguage = "en-US" },
            };
        }
    }
}
