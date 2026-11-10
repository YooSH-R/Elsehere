using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
        public async Task BootstrapRetainsTheAccountCharacterForEnteringTheWorld()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");
            api.Bootstrap.User.AvatarType = AvatarType.M;

            await flow.StartAsync();

            Assert.That(flow.SelectedAvatar, Is.EqualTo(AvatarType.M));
        }

        // 폰 메뉴 날짜 줄에 닉네임을 보이도록 접속한 계정의 닉네임을 기억한다.
        [Test]
        public async Task BootstrapRetainsTheNicknameForThePhoneMenu()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");
            api.Bootstrap.User.Nickname = "다빈";

            await flow.StartAsync();

            Assert.That(flow.Nickname, Is.EqualTo("다빈"));
        }

        [Test]
        public async Task BootstrapRetainsTheAccountUiThemeForEnteringTheWorld()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");
            api.Bootstrap.User.UiTheme = "CLOUD";

            await flow.StartAsync();

            Assert.That(flow.UiTheme, Is.EqualTo("CLOUD"));
        }

        [Test]
        public async Task BootstrapTellsWhetherTheUserAgreedToVoiceInput()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");
            api.Bootstrap.User.VoiceConsent = true;

            await flow.StartAsync();

            Assert.That(flow.IsVoiceAgreed, Is.True);
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
        public async Task ChooseGuest_ShowsCharacterChoiceWithoutCallingServer()
        {
            await StartAtLogin();

            flow.ChooseGuest();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.GuestCharacter));
            Assert.That(flow.GuestAvatar, Is.Null);
            Assert.That(api.GuestAvatarSent, Is.Null);
        }

        [Test]
        public async Task GuestLogin_SendsChosenCharacterAndShowsLevelTestIntro()
        {
            await StartAtLogin();
            flow.ChooseGuest();
            flow.SelectGuestAvatar(AvatarType.M);
            api.Bootstrap = SignedIn(OnboardingStep.Tutorial, currentLanguage: "en-US");
            api.Bootstrap.User.AvatarType = AvatarType.M;
            api.Bootstrap.User.Guest = true;

            await flow.GuestLoginAsync();

            Assert.That(api.GuestAvatarSent, Is.EqualTo(AvatarType.M));
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelTest));
            Assert.That(flow.SelectedAvatar, Is.EqualTo(AvatarType.M));
        }

        [Test]
        public async Task GuestLogin_SendsVoiceConsentOnlyWhenChecked()
        {
            await StartAtLogin();
            flow.ChooseGuest();
            flow.SelectGuestAvatar(AvatarType.F);
            api.Bootstrap = SignedIn(OnboardingStep.Tutorial, currentLanguage: "en-US");

            flow.SetGuestVoiceConsent(true);
            await flow.GuestLoginAsync();

            Assert.That(api.GuestVoiceConsentSent, Is.True);
        }

        [Test]
        public async Task ChooseGuestAgain_StartsWithVoiceConsentUnchecked()
        {
            await StartAtLogin();
            flow.ChooseGuest();
            flow.SetGuestVoiceConsent(true);

            flow.BackToLogin();
            flow.ChooseGuest();

            Assert.That(flow.GuestVoiceConsent, Is.False);
        }

        [Test]
        public async Task LoadGuestGuide_KeepsConversationLimitForCharacterChoice()
        {
            await StartAtLogin();
            flow.ChooseGuest();
            api.GuestGuide = new GuestGuideResponse { ConversationLimit = 3 };

            await flow.LoadGuestGuideAsync();

            Assert.That(flow.GuestConversationLimit, Is.EqualTo(3));
        }

        [Test]
        public async Task LoadGuestGuideFailed_StaysAtCharacterChoiceWithoutLimitOrMessage()
        {
            // 횟수를 읽지 못해도 캐릭터를 골라 시작할 수 있어야 하므로 실패 안내를 띄우지 않는다.
            await StartAtLogin();
            flow.ChooseGuest();
            api.GuestGuideError = new ApiException(
                "INTERNAL_ERROR",
                500,
                "잠시 후 다시 시도해 주세요."
            );

            await flow.LoadGuestGuideAsync();

            Assert.That(flow.GuestConversationLimit, Is.Null);
            Assert.That(flow.ErrorMessage, Is.Null);
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.GuestCharacter));
        }

        [Test]
        public async Task GuestLoginWithoutCharacter_StaysWithoutCallingServer()
        {
            await StartAtLogin();
            flow.ChooseGuest();

            await flow.GuestLoginAsync();

            Assert.That(api.GuestAvatarSent, Is.Null);
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.GuestCharacter));
        }

        [Test]
        public async Task GuestLoginFailed_StaysAtCharacterChoiceWithMessage()
        {
            await StartAtLogin();
            flow.ChooseGuest();
            flow.SelectGuestAvatar(AvatarType.F);
            api.GuestLoginError = new ApiException(
                "INTERNAL_ERROR",
                500,
                "잠시 후 다시 시도해 주세요."
            );

            await flow.GuestLoginAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.GuestCharacter));
            Assert.That(flow.GuestAvatar, Is.EqualTo(AvatarType.F));
            Assert.That(flow.ErrorMessage, Is.EqualTo("잠시 후 다시 시도해 주세요."));
        }

        [Test]
        public async Task BackFromGuestCharacter_ShowsLoginWithoutPreviousMessage()
        {
            await StartAtLogin();
            flow.ChooseGuest();
            flow.SelectGuestAvatar(AvatarType.F);
            api.GuestLoginError = new ApiException(
                "INTERNAL_ERROR",
                500,
                "잠시 후 다시 시도해 주세요."
            );
            await flow.GuestLoginAsync();

            flow.BackToLogin();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Login));
            Assert.That(flow.ErrorMessage, Is.Null);
        }

        // S15P31A305-596: 가입은 약관 동의 단계부터 시작한다.
        [Test]
        public async Task StartWhileSignupRequired_ShowsTermsFirst()
        {
            api.Bootstrap = new BootstrapResponse { AuthState = AuthState.SignupRequired };

            await flow.StartAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Terms));
            Assert.That(flow.SignupForm.Terms, Is.EqualTo(api.SignupForm.Consents));
        }

        [Test]
        public async Task ContinueWithoutRequiredTerms_StaysOnTerms()
        {
            api.Bootstrap = new BootstrapResponse { AuthState = AuthState.SignupRequired };
            await flow.StartAsync();

            flow.ContinueToProfile();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Terms));
        }

        [Test]
        public async Task ContinueWithRequiredTerms_ShowsNicknameAndCharacter()
        {
            api.Bootstrap = new BootstrapResponse { AuthState = AuthState.SignupRequired };
            await flow.StartAsync();
            flow.SignupForm.SetAgreed("TERMS", true);

            flow.ContinueToProfile();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Signup));
        }

        [Test]
        public async Task BackFromNicknameAndCharacter_ShowsTermsKeepingTheAgreements()
        {
            await StartAtSignup();

            flow.BackToTerms();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Terms));
            Assert.That(flow.SignupForm.IsAgreed("TERMS"), Is.True);
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
        public async Task GuestComingBack_ChoosesBetweenSignupAndContinuingFirst()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");
            api.Bootstrap.User.Guest = true;
            api.Bootstrap.LastSceneCode = "CAFE";

            await flow.StartAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.GuestReturn));
        }

        [Test]
        public async Task ContinueAsGuest_EntersThePlaceTheGuestLeft()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");
            api.Bootstrap.User.Guest = true;
            api.Bootstrap.LastSceneCode = "CAFE";
            await flow.StartAsync();

            flow.ContinueToGame();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelTest));
            await flow.SkipLevelTestAsync();
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.World));
            Assert.That(flow.LastSceneName, Is.EqualTo("CafeScene"));
        }

        [Test]
        public async Task GuestComingBackDuringTutorial_ContinuesToTutorial()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Tutorial, currentLanguage: "en-US");
            api.Bootstrap.User.Guest = true;
            await flow.StartAsync();

            flow.ContinueToGame();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelTest));
            await flow.SkipLevelTestAsync();
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Tutorial));
        }

        [Test]
        public async Task GuestJustStarted_ShowsLevelTestWithoutCallingItsApi()
        {
            flow.ChooseGuest();
            flow.SelectGuestAvatar(AvatarType.F);
            api.Bootstrap = SignedIn(OnboardingStep.Tutorial, currentLanguage: "en-US");
            api.Bootstrap.User.Guest = true;

            await flow.GuestLoginAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelTest));
            Assert.That(flow.LevelTest, Is.Null);
            Assert.That(api.LevelTestStartedFor, Is.Null);
        }

        [Test]
        public async Task GuestStartingLevelTest_ShowsLoginPromptWithoutCreatingATest()
        {
            flow.ChooseGuest();
            flow.SelectGuestAvatar(AvatarType.F);
            api.Bootstrap = SignedIn(OnboardingStep.Tutorial, currentLanguage: "en-US");
            api.Bootstrap.User.Guest = true;
            await flow.GuestLoginAsync();

            await flow.StartLevelTestAsync();

            Assert.That(flow.GuestLevelTestLoginRequired, Is.True);
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelTest));
            Assert.That(flow.LevelTest, Is.Null);
            Assert.That(api.LevelTestStartedFor, Is.Null);
            flow.DismissGuestLevelTestLogin();
            Assert.That(flow.GuestLevelTestLoginRequired, Is.False);
        }

        [Test]
        public async Task GuestSkippingLevelTest_GoesToTutorialWithoutWritingASkipRecord()
        {
            flow.ChooseGuest();
            flow.SelectGuestAvatar(AvatarType.M);
            api.Bootstrap = SignedIn(OnboardingStep.Tutorial, currentLanguage: "en-US");
            api.Bootstrap.User.Guest = true;
            api.Bootstrap.User.AvatarType = AvatarType.M;
            await flow.GuestLoginAsync();
            await flow.StartLevelTestAsync();

            await flow.SkipLevelTestAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Tutorial));
            Assert.That(flow.GuestLevelTestLoginRequired, Is.False);
            Assert.That(api.LevelTestSkippedFor, Is.Null);
            Assert.That(api.LevelTestStartedFor, Is.Null);
            Assert.That(flow.SelectedAvatar, Is.EqualTo(AvatarType.M));
        }

        [Test]
        public async Task SignedInToExistingAccount_IsToldBeforeEnteringTheGame()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");

            await flow.StartAsync(signedInToExistingAccount: true);

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.ExistingAccount));
            flow.ContinueToGame();
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.World));
        }

        [Test]
        public async Task GuestSignup_StartsWithTheGuestCharacter()
        {
            api.SignupForm.Guest = new ConvertingGuest
            {
                AvatarType = AvatarType.M,
                VoiceConsent = true,
            };

            await StartAtSignup();

            Assert.That(flow.SignupForm.IsGuestConversion, Is.True);
            Assert.That(flow.SignupForm.Avatar, Is.EqualTo(AvatarType.M));
        }

        [Test]
        public async Task GuestSignup_EntersTheGameWhereTheGuestLeft()
        {
            api.SignupForm.Guest = new ConvertingGuest { AvatarType = AvatarType.M };
            await StartAtSignup();
            // 가입하면 게스트가 회원으로 바뀌어 게스트 때 초기 설정 단계와 장소가 그대로 남는다.
            api.SignedUpStep = OnboardingStep.Done;
            api.Bootstrap = SignedIn(OnboardingStep.Done, currentLanguage: "en-US");
            api.Bootstrap.LastSceneCode = "CAFE";

            await flow.SubmitSignupAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.World));
            Assert.That(flow.LastSceneName, Is.EqualTo("CafeScene"));
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
        public async Task StartLevelTest_ShowsTheFirstItemOfTheStartedTest()
        {
            await StartAtLevelTest();

            await flow.StartLevelTestAsync();

            Assert.That(api.LevelTestStartedFor, Is.EqualTo("en-US"));
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelTest));
            Assert.That(flow.LevelTest.TestId, Is.EqualTo(9));
            Assert.That(flow.LevelTest.CurrentItem.ItemCode, Is.EqualTo("EN_LT_L1"));
        }

        [Test]
        public async Task SubmitLevelTestAnswer_SavesTheChoiceAndShowsTheNextItem()
        {
            await StartAtLevelTest();
            await flow.StartLevelTestAsync();
            flow.LevelTest.Choose(1);

            await flow.SubmitLevelTestAnswerAsync();

            Assert.That(api.Answers, Is.EqualTo(new[] { "9/EN_LT_L1/나" }));
            Assert.That(flow.LevelTest.CurrentItem.ItemCode, Is.EqualTo("EN_LT_S1"));
            Assert.That(api.CompleteCount, Is.EqualTo(0));
        }

        [Test]
        public async Task SubmitLastLevelTestAnswer_CompletesAndShowsTheResult()
        {
            await StartAtLastLevelTestItem();
            flow.LevelTest.ChangeAnswer("I left my bag here.");

            await flow.SubmitLevelTestAnswerAsync();

            Assert.That(api.Answers[1], Is.EqualTo("9/EN_LT_S1/I left my bag here."));
            Assert.That(api.CompleteCount, Is.EqualTo(1));
            Assert.That(flow.LevelTest.Result.ResultLevel, Is.EqualTo(2));
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelTest));
        }

        // 채점(ONB-05)이 실패해도 마지막 문항에 남아 [다음]으로 다시 채점할 수 있다. 같은 문항 답은 서버가 덮어쓴다.
        [Test]
        public async Task CompleteLevelTestFails_StaysOnTheLastItemAndRetryCompletes()
        {
            await StartAtLastLevelTestItem();
            flow.LevelTest.ChangeAnswer("I left my bag here.");
            api.CompleteError = new ApiException(
                "AI_UNAVAILABLE",
                503,
                "잠시 후 다시 시도해 주세요."
            );

            await flow.SubmitLevelTestAnswerAsync();

            Assert.That(flow.ErrorMessage, Is.EqualTo("잠시 후 다시 시도해 주세요."));
            Assert.That(flow.LevelTest.CurrentItem.ItemCode, Is.EqualTo("EN_LT_S1"));
            Assert.That(flow.LevelTest.Result, Is.Null);

            api.CompleteError = null;
            await flow.SubmitLevelTestAnswerAsync();

            Assert.That(flow.ErrorMessage, Is.Null);
            Assert.That(flow.LevelTest.Result, Is.Not.Null);
        }

        [Test]
        public async Task QuitLevelTest_QuitsAndShowsLevelSettingWithTheProfileReadAgain()
        {
            await StartAtLevelTest();
            await flow.StartLevelTestAsync();
            int profileReads = api.ProfileReads;

            await flow.QuitLevelTestAsync();

            Assert.That(api.QuitTestId, Is.EqualTo(9));
            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelSetting));
            Assert.That(api.ProfileReads, Is.EqualTo(profileReads + 1));
            Assert.That(flow.LanguageProfile.LevelTest.Status, Is.EqualTo(LevelTestStatus.Quit));
        }

        // 결과 화면의 [다음]은 학습 설정을 다시 읽어 화면 05에 추천 난이도 배지가 붙게 한다.
        [Test]
        public async Task ContinueAfterLevelTest_ShowsLevelSettingWithTheRecommendation()
        {
            await StartAtLastLevelTestItem();
            flow.LevelTest.ChangeAnswer("I left my bag here.");
            await flow.SubmitLevelTestAnswerAsync();

            await flow.ContinueAfterLevelTestAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.LevelSetting));
            Assert.That(flow.LanguageProfile.RecommendedLevel, Is.EqualTo(2));
        }

        [Test]
        public async Task TranscribeLevelTestAnswer_ReplacesTheAnswerWithWhatWasHeard()
        {
            await StartAtLastLevelTestItem();
            flow.LevelTest.ChangeAnswer("typed before");
            byte[] wav = { 1, 2, 3 };

            await flow.TranscribeLevelTestAnswerAsync(wav);

            Assert.That(api.TranscribedWav, Is.SameAs(wav));
            Assert.That(flow.LevelTest.SpokenAnswer, Is.EqualTo("I think I left my bag here."));
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
        public async Task StartFailedToConnect_ShowsRetryScreenWithReason()
        {
            api.BootstrapError = new ApiException(
                ApiErrorCodes.NetworkError,
                0,
                "서버에 연결하지 못했습니다."
            );

            await flow.StartAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.ConnectionFailed));
            Assert.That(flow.ErrorMessage, Is.EqualTo("서버에 연결하지 못했습니다."));
        }

        [Test]
        public async Task SignupTermsFailedToLoad_ShowsRetryScreenInsteadOfLogin()
        {
            api.Bootstrap = new BootstrapResponse { AuthState = AuthState.SignupRequired };
            api.SignupFormError = new ApiException(
                "INTERNAL_ERROR",
                500,
                "잠시 후 다시 시도해 주세요."
            );

            await flow.StartAsync();

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.ConnectionFailed));
        }

        [Test]
        public async Task UnexpectedFailure_KeepsScreenAndShowsGeneralMessage()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Language, currentLanguage: null);
            await flow.StartAsync();
            api.ChooseLanguageError = new InvalidOperationException("응답 처리 오류");
            LogAssert.Expect(LogType.Exception, new Regex("응답 처리 오류"));

            await flow.ChooseLanguageAsync("en-US");

            Assert.That(flow.Screen, Is.EqualTo(EntryScreen.Language));
            Assert.That(flow.ErrorMessage, Is.EqualTo("문제가 생겼어요. 다시 시도해 주세요."));
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

        [Test]
        public async Task SignupRetainsTheCharacterConfirmedByTheServer()
        {
            await StartAtSignup();
            flow.SignupForm.SelectAvatar(AvatarType.M);
            await flow.SubmitSignupAsync();
            Assert.That(flow.SelectedAvatar, Is.EqualTo(AvatarType.M));
        }

        [Test]
        public async Task ExpiredSessionClearsThePreviousAccountsCharacter()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Language, null);
            api.Bootstrap.User.AvatarType = AvatarType.M;
            await flow.StartAsync();
            api.ChooseLanguageError = new ApiException(
                "UNAUTHENTICATED",
                401,
                "로그인이 필요합니다."
            );
            await flow.ChooseLanguageAsync("en-US");
            Assert.That(flow.SelectedAvatar, Is.Null);
        }

        private async Task StartAtLogin()
        {
            api.BootstrapError = new ApiException("UNAUTHENTICATED", 401, "로그인이 필요합니다.");
            await flow.StartAsync();
            api.BootstrapError = null;
        }

        // 약관 단계에서 필수 약관에 동의하고 가입 정보(닉네임·캐릭터) 단계로 넘어간다(S15P31A305-596).
        private async Task StartAtSignup()
        {
            api.Bootstrap = new BootstrapResponse { AuthState = AuthState.SignupRequired };
            await flow.StartAsync();
            flow.SignupForm.SetAgreed("TERMS", true);
            flow.ContinueToProfile();
        }

        private async Task StartAtLevelTest()
        {
            api.Bootstrap = SignedIn(OnboardingStep.Level, currentLanguage: "en-US");
            await flow.StartAsync();
        }

        // 듣기 문항에 답하고 마지막(말하기) 문항까지 온다.
        private async Task StartAtLastLevelTestItem()
        {
            await StartAtLevelTest();
            await flow.StartLevelTestAsync();
            flow.LevelTest.Choose(0);
            await flow.SubmitLevelTestAnswerAsync();
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
            public ApiException GuestLoginError { get; set; }
            public GuestGuideResponse GuestGuide { get; set; } =
                new GuestGuideResponse { ConversationLimit = 3 };
            public ApiException GuestGuideError { get; set; }
            public ApiException SignupError { get; set; }
            public Exception SignupFormError { get; set; }
            public Exception ChooseLanguageError { get; set; }

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
            public AvatarType? GuestAvatarSent { get; private set; }
            public bool? GuestVoiceConsentSent { get; private set; }
            public string NicknameCheckedFor { get; private set; }
            public SignupRequest SignupSent { get; private set; }

            /// <summary>가입 응답의 초기 설정 단계. 새 회원은 언어 선택부터, 회원이 된 게스트는 게스트 때 단계 그대로다.</summary>
            public OnboardingStep SignedUpStep { get; set; } = OnboardingStep.Language;
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

            public Task<GuestGuideResponse> GetGuestGuideAsync()
            {
                return GuestGuideError != null
                    ? Task.FromException<GuestGuideResponse>(GuestGuideError)
                    : Task.FromResult(GuestGuide);
            }

            public Task GuestLoginAsync(AvatarType avatarType, bool voiceConsent)
            {
                GuestAvatarSent = avatarType;
                GuestVoiceConsentSent = voiceConsent;
                return GuestLoginError != null
                    ? Task.FromException(GuestLoginError)
                    : Task.CompletedTask;
            }

            public Task<SignupFormResponse> GetSignupFormAsync()
            {
                return SignupFormError != null
                    ? Task.FromException<SignupFormResponse>(SignupFormError)
                    : Task.FromResult(SignupForm);
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
                        User = new OnboardingUser
                        {
                            OnboardingStep = SignedUpStep,
                            AvatarType = request.AvatarType,
                        },
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
                ProfileReads++;
                return Task.FromResult(Profile);
            }

            public int ProfileReads { get; private set; }
            public string LevelTestStartedFor { get; private set; }

            /// <summary>보낸 답. "테스트 ID/문항/답" 형식이다.</summary>
            public List<string> Answers { get; } = new List<string>();
            public int CompleteCount { get; private set; }
            public ApiException CompleteError { get; set; }
            public long? QuitTestId { get; private set; }
            public byte[] TranscribedWav { get; private set; }

            public Task<LevelTestStartResponse> StartLevelTestAsync(string languageCode)
            {
                LevelTestStartedFor = languageCode;
                return Task.FromResult(
                    new LevelTestStartResponse
                    {
                        TestId = 9,
                        Status = LevelTestStatus.InProgress,
                        TotalItems = 2,
                        Items = new List<LevelTestItem>
                        {
                            new LevelTestItem
                            {
                                ItemCode = "EN_LT_L1",
                                Type = LevelTestItemType.Listen,
                                Choices = new List<string> { "가", "나", "다", "라" },
                                Cues = new List<string>(),
                            },
                            new LevelTestItem
                            {
                                ItemCode = "EN_LT_S1",
                                Type = LevelTestItemType.Speak,
                                Choices = new List<string>(),
                                Cues = new List<string> { "파란 숄더백" },
                            },
                        },
                    }
                );
            }

            public Task AnswerLevelTestAsync(long testId, string itemCode, string answer)
            {
                Answers.Add($"{testId}/{itemCode}/{answer}");
                return Task.CompletedTask;
            }

            public Task<LevelTestResultResponse> CompleteLevelTestAsync(long testId)
            {
                if (CompleteError != null)
                {
                    return Task.FromException<LevelTestResultResponse>(CompleteError);
                }

                CompleteCount++;
                Profile.RecommendedLevel = 2;
                Profile.LevelTest = new LevelTestSummary
                {
                    TestId = testId,
                    Status = LevelTestStatus.Done,
                };
                return Task.FromResult(
                    new LevelTestResultResponse
                    {
                        TestId = testId,
                        Status = LevelTestStatus.Done,
                        ResultLevel = 2,
                        ListeningScore = 1,
                        SpeakingPassed = 1,
                    }
                );
            }

            public Task<LevelTestResultResponse> QuitLevelTestAsync(long testId)
            {
                QuitTestId = testId;
                Profile.LevelTest = new LevelTestSummary
                {
                    TestId = testId,
                    Status = LevelTestStatus.Quit,
                };
                return Task.FromResult(
                    new LevelTestResultResponse { TestId = testId, Status = LevelTestStatus.Quit }
                );
            }

            public Task<SpeechTranscription> TranscribeLevelTestAsync(string requestId, byte[] wav)
            {
                TranscribedWav = wav;
                return Task.FromResult(
                    new SpeechTranscription
                    {
                        SttRawText = "I think I left my bag here.",
                        DurationMs = 2300,
                    }
                );
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
