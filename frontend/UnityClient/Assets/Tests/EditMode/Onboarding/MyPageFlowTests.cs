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

        [Test]
        public async Task CanWithdraw_SignedInMember_IsTrue()
        {
            await flow.LoadAsync();

            Assert.That(flow.CanWithdraw, Is.True);
        }

        [Test]
        public async Task CanWithdraw_Guest_IsFalse()
        {
            api.Guest = true;

            await flow.LoadAsync();

            Assert.That(flow.CanWithdraw, Is.False);
        }

        // 게스트인지 알기 전에는 보이지 않아, 게스트에게 잠깐이라도 [회원 탈퇴]가 보이지 않게 한다.
        [Test]
        public async Task CanWithdraw_BeforeProfileLoaded_IsFalse()
        {
            Assert.That(flow.CanWithdraw, Is.False);

            api.BootstrapError = Failure("서버에 연결하지 못했어요.");
            await flow.LoadAsync();

            Assert.That(flow.CanWithdraw, Is.False);
        }

        [Test]
        public async Task Load_Member_CanChangeNickname()
        {
            Assert.That(flow.CanChangeNickname, Is.False);

            await flow.LoadAsync();

            Assert.That(flow.CanChangeNickname, Is.True);
        }

        // 게스트 닉네임은 서버가 정하므로 바꾸지 못하게 하고, 열자마자 이유를 알린다.
        [Test]
        public async Task Load_Guest_CannotChangeNicknameAndTellsWhy()
        {
            api.Guest = true;

            await flow.LoadAsync();

            Assert.That(flow.CanChangeNickname, Is.False);
            Assert.That(flow.Message, Is.EqualTo("게스트는 닉네임을 바꿀 수 없어요."));
        }

        [Test]
        public async Task ChangeNickname_Guest_DoesNotSend()
        {
            api.Guest = true;
            await flow.LoadAsync();

            bool changed = await flow.ChangeNicknameAsync("선종");

            Assert.That(changed, Is.False);
            Assert.That(api.Changes, Is.Empty);
            Assert.That(flow.Message, Is.EqualTo("게스트는 닉네임을 바꿀 수 없어요."));
        }

        // 플레이 중에도 대화 난이도를 바꿀 수 있게 지금 단계와 고를 수 있는 단계를 보인다(S15P31A305-799).
        [Test]
        public async Task Load_ShowsConversationLevelsAndCurrentLevel()
        {
            await flow.LoadAsync();

            Assert.That(
                flow.Levels.Select(level => level.Name),
                Is.EqualTo(new[] { "초급", "중급", "고급" })
            );
            Assert.That(flow.SelectedLevel, Is.EqualTo(1));
            Assert.That(api.ProfileRequests, Is.EqualTo(new[] { "en-US" }));
        }

        [Test]
        public async Task ChangeLevel_ToAnotherLevel_SavesAndTellsItAppliesFromNextConversation()
        {
            await flow.LoadAsync();

            bool changed = await flow.ChangeLevelAsync(2);

            Assert.That(changed, Is.True);
            Assert.That(api.LevelChanges, Is.EqualTo(new[] { "en-US:2" }));
            Assert.That(flow.SelectedLevel, Is.EqualTo(2));
            Assert.That(flow.Message, Is.EqualTo("중급으로 바꿨어요. 다음 대화부터 적용돼요."));
        }

        [Test]
        public async Task ChangeLevel_SameAsCurrent_DoesNotSend()
        {
            await flow.LoadAsync();

            bool changed = await flow.ChangeLevelAsync(1);

            Assert.That(changed, Is.False);
            Assert.That(api.LevelChanges, Is.Empty);
            Assert.That(flow.Message, Is.EqualTo("지금 쓰는 난이도예요."));
        }

        // 고급은 "추후 제공"으로 잠겨 있어 눌러도 보내지 않는다.
        [Test]
        public async Task ChangeLevel_NotYetAvailable_DoesNotSend()
        {
            await flow.LoadAsync();

            bool changed = await flow.ChangeLevelAsync(3);

            Assert.That(changed, Is.False);
            Assert.That(api.LevelChanges, Is.Empty);
            Assert.That(flow.SelectedLevel, Is.EqualTo(1));
        }

        [Test]
        public async Task ChangeLevel_WhenServerRejects_KeepsPreviousLevelAndTellsWhy()
        {
            await flow.LoadAsync();
            api.LevelChangeError = Failure("서버에 연결하지 못했어요.");

            bool changed = await flow.ChangeLevelAsync(2);

            Assert.That(changed, Is.False);
            Assert.That(flow.SelectedLevel, Is.EqualTo(1));
            Assert.That(flow.IsBusy, Is.False);
            Assert.That(flow.Message, Is.EqualTo("서버에 연결하지 못했어요."));
        }

        [Test]
        public async Task ChangeLevel_BeforeProfileLoaded_DoesNotSend()
        {
            api.BootstrapError = Failure("서버에 연결하지 못했어요.");
            await flow.LoadAsync();

            bool changed = await flow.ChangeLevelAsync(2);

            Assert.That(changed, Is.False);
            Assert.That(flow.Levels, Is.Empty);
            Assert.That(api.LevelChanges, Is.Empty);
        }

        private static ApiException Failure(string message)
        {
            return new ApiException(ApiErrorCodes.NetworkError, 0, message);
        }

        private sealed class FakeMyPageApi : IMyPageApi
        {
            public ApiException BootstrapError { get; set; }

            /// <summary>정하면 게스트로 시작한 사용자를 돌려준다.</summary>
            public bool Guest { get; set; }

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
                            Guest = Guest,
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

            public List<string> ProfileRequests { get; } = new List<string>();

            public ApiException LevelChangeError { get; set; }

            public List<string> LevelChanges { get; } = new List<string>();

            public Task<LanguageProfileResponse> GetLanguageProfileAsync(string languageCode)
            {
                ProfileRequests.Add(languageCode);
                return Task.FromResult(
                    new LanguageProfileResponse
                    {
                        LanguageCode = languageCode,
                        SelectedLevel = 1,
                        Levels = new List<ConversationLevel>
                        {
                            Level(1, "초급", true),
                            Level(2, "중급", true),
                            Level(3, "고급", false),
                        },
                    }
                );
            }

            public Task<LevelSelectionResponse> SelectLevelAsync(string languageCode, int level)
            {
                LevelChanges.Add($"{languageCode}:{level}");
                if (LevelChangeError != null)
                {
                    throw LevelChangeError;
                }

                return Task.FromResult(
                    new LevelSelectionResponse
                    {
                        LanguageCode = languageCode,
                        SelectedLevel = level,
                        OnboardingStep = OnboardingStep.Done,
                    }
                );
            }

            private static ConversationLevel Level(int level, string name, bool available)
            {
                return new ConversationLevel
                {
                    Level = level,
                    Name = name,
                    Available = available,
                };
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
