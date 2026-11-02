using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using A305.Player;
using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    public class MyPageFlowTests
    {
        private FakeMyPageApi api;
        private MyPageFlow flow;

        [SetUp]
        public void CreateFlow()
        {
            api = new FakeMyPageApi();
            flow = new MyPageFlow(api);
        }

        [TearDown]
        public void ForgetNickname() => PlayerNickname.Reset();

        // 폰 메뉴 날짜 줄이 바뀐 닉네임을 바로 보이도록 게임에도 알린다.
        [Test]
        public async Task ChangeNickname_SharesTheNewNicknameWithTheGame()
        {
            await flow.LoadAsync();
            Assert.That(PlayerNickname.Name, Is.EqualTo("Sun"));

            await flow.ChangeNicknameAsync("선종");

            Assert.That(PlayerNickname.Name, Is.EqualTo("선종"));
        }

        [Test]
        public async Task Load_ShowsSignedInUser()
        {
            await flow.LoadAsync();

            Assert.That(flow.User.Nickname, Is.EqualTo("Sun"));
            Assert.That(flow.User.AvatarType, Is.EqualTo(AvatarType.F));
            Assert.That(flow.User.CurrentLanguage, Is.EqualTo("en-US"));
            Assert.That(flow.Message, Is.Null);
        }

        [Test]
        public async Task Load_WhenRequestFails_TellsWhy()
        {
            api.BootstrapError = Failure("서버에 연결하지 못했어요.");

            await flow.LoadAsync();

            Assert.That(flow.User, Is.Null);
            Assert.That(flow.Message, Is.EqualTo("서버에 연결하지 못했어요."));
        }

        [Test]
        public async Task ChangeNickname_FollowingRule_SavesAndShowsNewNickname()
        {
            await flow.LoadAsync();

            bool changed = await flow.ChangeNicknameAsync("선종");

            Assert.That(changed, Is.True);
            Assert.That(api.Changes, Is.EqualTo(new[] { "선종" }));
            Assert.That(flow.User.Nickname, Is.EqualTo("선종"));
            Assert.That(flow.Message, Is.EqualTo("닉네임을 바꿨어요."));
        }

        [TestCase("S", "2자 이상 입력해 주세요.")]
        [TestCase("Sun!", "한글, 영문, 숫자만 쓸 수 있어요.")]
        public async Task ChangeNickname_BreakingRule_DoesNotSendAndTellsHowToFix(
            string nickname,
            string expected
        )
        {
            await flow.LoadAsync();

            bool changed = await flow.ChangeNicknameAsync(nickname);

            Assert.That(changed, Is.False);
            Assert.That(api.Changes, Is.Empty);
            Assert.That(flow.User.Nickname, Is.EqualTo("Sun"));
            Assert.That(flow.Message, Is.EqualTo(expected));
        }

        [Test]
        public async Task ChangeNickname_SameAsCurrent_DoesNotSend()
        {
            await flow.LoadAsync();

            bool changed = await flow.ChangeNicknameAsync("Sun");

            Assert.That(changed, Is.False);
            Assert.That(api.Changes, Is.Empty);
            Assert.That(flow.Message, Is.EqualTo("지금 쓰는 닉네임이에요."));
        }

        [Test]
        public async Task ChangeNickname_TakenByOthers_KeepsNicknameAndTellsWhy()
        {
            await flow.LoadAsync();
            api.ChangeError = new ApiException(
                "NICKNAME_TAKEN",
                409,
                "이미 사용 중인 닉네임입니다."
            );

            bool changed = await flow.ChangeNicknameAsync("Moon");

            Assert.That(changed, Is.False);
            Assert.That(flow.User.Nickname, Is.EqualTo("Sun"));
            Assert.That(flow.IsBusy, Is.False);
            Assert.That(flow.Message, Is.EqualTo("이미 사용 중인 닉네임입니다."));
        }

        [Test]
        public async Task ChangeNickname_WhileSaving_DoesNotSendAgain()
        {
            await flow.LoadAsync();
            api.ChangeGate = new TaskCompletionSource<bool>();

            Task<bool> first = flow.ChangeNicknameAsync("선종");
            bool wasBusy = flow.IsBusy;
            bool second = await flow.ChangeNicknameAsync("선종");
            api.ChangeGate.SetResult(true);
            await first;

            Assert.That(wasBusy, Is.True);
            Assert.That(second, Is.False);
            Assert.That(api.Changes.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ChangeNickname_BeforeProfileLoaded_DoesNotSend()
        {
            api.BootstrapError = Failure("서버에 연결하지 못했어요.");
            await flow.LoadAsync();

            bool changed = await flow.ChangeNicknameAsync("선종");

            Assert.That(changed, Is.False);
            Assert.That(api.Changes, Is.Empty);
        }

        [Test]
        public async Task Load_ShowsConsentsUserCanChange()
        {
            await flow.LoadAsync();

            Assert.That(
                flow.Consents.Select(consent => consent.ConsentType),
                Is.EqualTo(new[] { "VOICE", "TRAINING_TEXT" })
            );
            Assert.That(flow.IsVoiceAgreed, Is.False);
        }

        [Test]
        public async Task ChangeConsent_SavesAndShowsNewChoice()
        {
            await flow.LoadAsync();

            bool changed = await flow.ChangeConsentAsync("VOICE", true);

            Assert.That(changed, Is.True);
            Assert.That(api.ConsentChanges, Is.EqualTo(new[] { "VOICE:True" }));
            Assert.That(flow.IsVoiceAgreed, Is.True);
            Assert.That(flow.Message, Is.EqualTo("켰어요: 음성 처리 동의"));
        }

        [Test]
        public async Task ChangeConsent_WhenServerRejects_KeepsPreviousChoiceAndTellsWhy()
        {
            await flow.LoadAsync();
            api.ConsentChangeError = Failure("서버에 연결하지 못했어요.");

            bool changed = await flow.ChangeConsentAsync("VOICE", true);

            Assert.That(changed, Is.False);
            Assert.That(flow.IsVoiceAgreed, Is.False);
            Assert.That(flow.Message, Is.EqualTo("서버에 연결하지 못했어요."));
        }

        private static ApiException Failure(string message)
        {
            return new ApiException(ApiErrorCodes.NetworkError, 0, message);
        }

        private sealed class FakeMyPageApi : IMyPageApi
        {
            public ApiException BootstrapError { get; set; }

            public ApiException ChangeError { get; set; }

            /// <summary>정하면 닉네임 변경 응답이 이 작업이 끝날 때까지 오지 않는다.</summary>
            public TaskCompletionSource<bool> ChangeGate { get; set; }

            public List<string> Changes { get; } = new List<string>();

            public Task<BootstrapResponse> GetBootstrapAsync()
            {
                if (BootstrapError != null)
                {
                    throw BootstrapError;
                }

                return Task.FromResult(
                    new BootstrapResponse
                    {
                        AuthState = AuthState.SignedIn,
                        User = new OnboardingUser
                        {
                            UserId = 1,
                            Nickname = "Sun",
                            AvatarType = AvatarType.F,
                            CurrentLanguage = "en-US",
                            OnboardingStep = OnboardingStep.Done,
                        },
                    }
                );
            }

            public async Task<NicknameChange> ChangeNicknameAsync(string nickname)
            {
                Changes.Add(nickname);
                if (ChangeGate != null)
                {
                    await ChangeGate.Task;
                }

                if (ChangeError != null)
                {
                    throw ChangeError;
                }

                return new NicknameChange { Nickname = nickname };
            }

            public ApiException ConsentChangeError { get; set; }

            public List<string> ConsentChanges { get; } = new List<string>();

            public Task<List<ConsentStatus>> GetConsentsAsync()
            {
                return Task.FromResult(
                    new List<ConsentStatus>
                    {
                        Consent("VOICE", "음성 처리 동의", false),
                        Consent("TRAINING_TEXT", "AI 개선에 내 대화 기록 활용", false),
                    }
                );
            }

            public Task<ConsentStatus> ChangeConsentAsync(string consentType, bool agreed)
            {
                ConsentChanges.Add($"{consentType}:{agreed}");
                if (ConsentChangeError != null)
                {
                    throw ConsentChangeError;
                }

                return Task.FromResult(Consent(consentType, "음성 처리 동의", agreed));
            }

            private static ConsentStatus Consent(string consentType, string title, bool agreed)
            {
                return new ConsentStatus
                {
                    ConsentType = consentType,
                    Version = "2026-10",
                    Title = title,
                    Agreed = agreed,
                };
            }
        }
    }
}
