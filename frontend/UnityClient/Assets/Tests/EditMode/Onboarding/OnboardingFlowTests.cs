using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    public class OnboardingFlowTests
    {
        private FakeOnboardingApi api;
        private OnboardingFlow flow;

        [SetUp]
        public void CreateFlow()
        {
            api = new FakeOnboardingApi();
            flow = new OnboardingFlow(api);
        }

        [Test]
        public async Task StartWithoutLogin_ShowsLogin()
        {
            api.BootstrapError = new ApiException("UNAUTHENTICATED", 401, "로그인이 필요합니다.");

            await flow.StartAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Login));
            Assert.That(flow.ErrorMessage, Is.Null);
        }

        [Test]
        public async Task StartWhileSignupRequired_ShowsSignupWithTerms()
        {
            api.Bootstrap = new BootstrapResponse { AuthState = AuthState.SignupRequired };

            await flow.StartAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Signup));
            Assert.That(flow.SignupForm.Terms, Is.EqualTo(api.SignupForm.Consents));
        }

        [Test]
        public async Task StartAtLanguageStep_ShowsLanguagesToChoose()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Language, currentLanguage: null);

            await flow.StartAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Language));
            Assert.That(flow.Languages, Is.EqualTo(api.Languages));
        }

        [Test]
        public async Task StartAtLevelStepAfterSkippingTest_ShowsLevelSettingOfCurrentLanguage()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Level, currentLanguage: "en-US");
            api.Profile.LevelTest = new LevelTestSummary
            {
                TestId = 10,
                Status = LevelTestStatus.Skipped,
            };

            await flow.StartAsync();

            Assert.That(api.ProfileRequestedFor, Is.EqualTo("en-US"));
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelSetting));
            Assert.That(flow.LanguageProfile, Is.SameAs(api.Profile));
        }

        [Test]
        public async Task StartWhenDoneWithLastScene_OpensUnitySceneOfThatPlace()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");
            api.Bootstrap.LastSceneCode = "CAFE";

            await flow.StartAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.World));
            Assert.That(flow.LastSceneName, Is.EqualTo("CafeScene"));
        }

        [Test]
        public async Task StartWhenDoneWithoutLastScene_OpensDefaultWorldWithoutAskingPlaces()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");

            await flow.StartAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.World));
            Assert.That(flow.LastSceneName, Is.Null);
            Assert.That(api.ScenesRequested, Is.False);
        }

        // 닫힌 장소(HOTEL)이거나 목록에 없는 장소(AIRPORT)면 마을에서 시작한다.
        [TestCase("HOTEL")]
        [TestCase("AIRPORT")]
        public async Task StartWhenLastSceneIsClosedOrUnknown_OpensDefaultWorld(
            string lastSceneCode
        )
        {
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");
            api.Bootstrap.LastSceneCode = lastSceneCode;

            await flow.StartAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.World));
            Assert.That(flow.LastSceneName, Is.Null);
        }

        [Test]
        public async Task CheckNickname_RecordsAvailabilityOfTypedNickname()
        {
            await StartAtSignup();
            flow.SignupForm.ChangeNickname("Sun");

            await flow.CheckNicknameAsync();

            Assert.That(api.NicknameCheckedFor, Is.EqualTo("Sun"));
            Assert.That(flow.SignupForm.NicknameStatus, Is.EqualTo(NicknameStatus.Available));
        }

        [Test]
        public async Task SubmitSignup_MovesToLanguageStep()
        {
            await StartAtSignup();

            await flow.SubmitSignupAsync();

            Assert.That(api.SignupSent, Is.Not.Null);
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Language));
            Assert.That(flow.Languages, Is.EqualTo(api.Languages));
        }

        [Test]
        public async Task SubmitSignupWithTakenNickname_StaysWithTakenStatusAndMessage()
        {
            // 확인한 뒤 제출하기 전에 다른 사용자가 같은 닉네임으로 먼저 가입한 경우다.
            await StartAtSignup();
            flow.SignupForm.ChangeNickname("Sun");
            api.SignupError = new ApiException(
                "NICKNAME_TAKEN",
                409,
                "이미 사용 중인 닉네임입니다."
            );

            await flow.SubmitSignupAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Signup));
            Assert.That(flow.SignupForm.NicknameStatus, Is.EqualTo(NicknameStatus.Taken));
            Assert.That(flow.ErrorMessage, Is.EqualTo("이미 사용 중인 닉네임입니다."));
        }

        [Test]
        public async Task ChooseLanguage_MovesToLevelTest()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Language, currentLanguage: null);
            await flow.StartAsync();

            await flow.ChooseLanguageAsync("en-US");

            Assert.That(api.LanguageChosen, Is.EqualTo("en-US"));
            Assert.That(flow.LanguageCode, Is.EqualTo("en-US"));
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelTest));
        }

        [Test]
        public async Task SkipLevelTest_MovesToLevelSettingWithProfile()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Level, currentLanguage: "en-US");
            await flow.StartAsync();

            await flow.SkipLevelTestAsync();

            Assert.That(api.LevelTestSkippedFor, Is.EqualTo("en-US"));
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelSetting));
            Assert.That(flow.LanguageProfile, Is.SameAs(api.Profile));
        }

        [Test]
        public async Task SelectLevel_MovesToTutorial()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Level, currentLanguage: "en-US");
            api.Profile.LevelTest = new LevelTestSummary { Status = LevelTestStatus.Skipped };
            await flow.StartAsync();

            await flow.SelectLevelAsync(1);

            Assert.That(api.LevelSelected, Is.EqualTo(1));
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Tutorial));
        }

        [Test]
        public async Task FailureOtherThanLogin_KeepsScreenAndShowsMessage()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Language, currentLanguage: null);
            await flow.StartAsync();
            api.ChooseLanguageError = new ApiException(
                ApiErrorCodes.NetworkError,
                0,
                "서버에 연결하지 못했습니다."
            );

            await flow.ChooseLanguageAsync("en-US");

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Language));
            Assert.That(flow.ErrorMessage, Is.EqualTo("서버에 연결하지 못했습니다."));
        }

        [Test]
        public async Task SessionExpiredDuringFlow_GoesBackToLogin()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Language, currentLanguage: null);
            await flow.StartAsync();
            api.ChooseLanguageError = new ApiException(
                "UNAUTHENTICATED",
                401,
                "로그인이 필요합니다."
            );

            await flow.ChooseLanguageAsync("en-US");

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Login));
        }

        private async Task StartAtSignup()
        {
            api.Bootstrap = new BootstrapResponse { AuthState = AuthState.SignupRequired };
            await flow.StartAsync();
        }

        private static BootstrapResponse SignedIn(OnboardingStep step, string currentLanguage)
        {
            return new BootstrapResponse
            {
                AuthState = AuthState.SignedIn,
                User = new OnboardingUser
                {
                    OnboardingStep = step,
                    CurrentLanguage = currentLanguage,
                },
            };
        }

        private sealed class FakeOnboardingApi : IOnboardingApi
        {
            public BootstrapResponse Bootstrap { get; set; }
            public ApiException BootstrapError { get; set; }
            public ApiException SignupError { get; set; }
            public ApiException ChooseLanguageError { get; set; }

            public SignupFormResponse SignupForm { get; } =
                new SignupFormResponse
                {
                    Email = "sun@example.com",
                    Consents = new List<ConsentTerm>
                    {
                        new ConsentTerm
                        {
                            ConsentType = "TERMS",
                            Version = "2026-10",
                            Required = true,
                        },
                    },
                };

            public List<LanguageOption> Languages { get; } =
                new List<LanguageOption>
                {
                    new LanguageOption
                    {
                        LanguageCode = "en-US",
                        Name = "English (US)",
                        Available = true,
                    },
                };

            public LanguageProfileResponse Profile { get; } =
                new LanguageProfileResponse
                {
                    LanguageCode = "en-US",
                    Levels = new List<ConversationLevel>
                    {
                        new ConversationLevel
                        {
                            Level = 1,
                            Name = "초급",
                            Available = true,
                        },
                    },
                };

            public List<SceneSummary> Scenes { get; } =
                new List<SceneSummary>
                {
                    new SceneSummary
                    {
                        SceneCode = "TOWN",
                        UnityKey = "TownScene",
                        IsOpen = true,
                    },
                    new SceneSummary
                    {
                        SceneCode = "CAFE",
                        UnityKey = "CafeScene",
                        IsOpen = true,
                    },
                    new SceneSummary
                    {
                        SceneCode = "HOTEL",
                        UnityKey = "HotelScene",
                        IsOpen = false,
                    },
                };

            public bool ScenesRequested { get; private set; }
            public string NicknameCheckedFor { get; private set; }
            public SignupRequest SignupSent { get; private set; }
            public string LanguageChosen { get; private set; }
            public string ProfileRequestedFor { get; private set; }
            public string LevelTestSkippedFor { get; private set; }
            public int? LevelSelected { get; private set; }

            public string GoogleLoginUrl => "/api/v1/auth/login/google";

            public Task<BootstrapResponse> GetBootstrapAsync()
            {
                return BootstrapError != null
                    ? Task.FromException<BootstrapResponse>(BootstrapError)
                    : Task.FromResult(Bootstrap);
            }

            public Task DevLoginAsync(long userId)
            {
                return Task.CompletedTask;
            }

            public Task<SignupFormResponse> GetSignupFormAsync()
            {
                return Task.FromResult(SignupForm);
            }

            public Task<NicknameAvailability> CheckNicknameAsync(string nickname)
            {
                NicknameCheckedFor = nickname;
                return Task.FromResult(
                    new NicknameAvailability { Nickname = nickname, Available = true }
                );
            }

            public Task<SignupResponse> SignUpAsync(SignupRequest request)
            {
                SignupSent = request;
                if (SignupError != null)
                {
                    return Task.FromException<SignupResponse>(SignupError);
                }

                return Task.FromResult(
                    new SignupResponse
                    {
                        User = new OnboardingUser { OnboardingStep = OnboardingStep.Language },
                    }
                );
            }

            public Task<List<LanguageOption>> GetLanguagesAsync()
            {
                return Task.FromResult(Languages);
            }

            public Task<LanguageSelectionResponse> ChooseLanguageAsync(string languageCode)
            {
                if (ChooseLanguageError != null)
                {
                    return Task.FromException<LanguageSelectionResponse>(ChooseLanguageError);
                }

                LanguageChosen = languageCode;
                return Task.FromResult(
                    new LanguageSelectionResponse
                    {
                        LanguageCode = languageCode,
                        OnboardingStep = OnboardingStep.Level,
                    }
                );
            }

            public Task<LanguageProfileResponse> GetLanguageProfileAsync(string languageCode)
            {
                ProfileRequestedFor = languageCode;
                return Task.FromResult(Profile);
            }

            public Task<LevelTestResultResponse> SkipLevelTestAsync(string languageCode)
            {
                LevelTestSkippedFor = languageCode;
                Profile.LevelTest = new LevelTestSummary { Status = LevelTestStatus.Skipped };
                return Task.FromResult(
                    new LevelTestResultResponse { Status = LevelTestStatus.Skipped }
                );
            }

            public Task<List<SceneSummary>> GetScenesAsync()
            {
                ScenesRequested = true;
                return Task.FromResult(Scenes);
            }

            public Task<LevelSelectionResponse> SelectLevelAsync(string languageCode, int level)
            {
                LevelSelected = level;
                return Task.FromResult(
                    new LevelSelectionResponse
                    {
                        LanguageCode = languageCode,
                        SelectedLevel = level,
                        OnboardingStep = OnboardingStep.Tutorial,
                    }
                );
            }
        }
    }
}
