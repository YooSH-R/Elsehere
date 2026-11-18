#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using A305.Npc;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Mission.PlayModeTests
{
    public class ShowcaseQuestSwitchTests
    {
        private Func<int, ApiClient> previousFactory;
        private bool wasGuest;
        private bool surveyWasPaused;
        private bool wasShowcasePopulation;
        private string previousMissionNpc;
        private QuestTransport transport;
        private FirstDayMissionView view;
        private int signupRequests;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousFactory = ApiSettings.EditorClientFactory;
            wasGuest = PlayerAccount.IsGuest;
            surveyWasPaused = SurveyPrompt.AutomaticInvitationsPaused;
            wasShowcasePopulation = NpcPopulation.UsesShowcasePopulation;
            previousMissionNpc = NpcDuty.MissionNpcCode;
            transport = new QuestTransport();
            signupRequests = 0;
            ApiSettings.EditorClientFactory = _ => new ApiClient("", transport);
            GuestSignupPrompt.Requested += CountSignup;
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;
            PlayerAccount.SetGuest(true);
            view = Object.FindFirstObjectByType<FirstDayMissionView>();
            view.SetAvailable(true);
            Assert.That(NpcDuty.MissionNpcCode, Is.EqualTo("HOTEL_STAFF"));
            Assert.That(view.IsQuestCompleteOpen, Is.False);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene scene = view.gameObject.scene;
            SceneManager.SetActiveScene(SceneManager.CreateScene("QuestSwitchCleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
            GuestSignupPrompt.Requested -= CountSignup;
            ApiSettings.EditorClientFactory = previousFactory;
            PlayerAccount.SetGuest(wasGuest);
            SurveyPrompt.AutomaticInvitationsPaused = surveyWasPaused;
            NpcPopulation.UsesShowcasePopulation = wasShowcasePopulation;
            NpcDuty.MissionNpcCode = previousMissionNpc;
            NpcOutings.Shared.SetClosedToVisitors(FirstDayMissionState.ShowcaseRestaurant, false);
        }

        private void CountSignup() => signupRequests++;

        [UnityTest]
        public IEnumerator GuestStartsShowcaseWithoutCompletingHotelQuestOrAskingToSignUp()
        {
            SelectAndStart(1);
            yield return null;

            Assert.That(transport.ShowcaseStarts, Is.EqualTo(1));
            Assert.That(ShowsGoal("호텔에서 저녁 나들이 준비"), Is.True);
            Assert.That(
                view.IsQuestBoardOpen,
                Is.False,
                "시작한 퀘스트를 바로 진행하도록 목록을 닫습니다."
            );
            AssertNoCompletionOrSignup();

            Object.FindFirstObjectByType<FirstDayMissionController>().RequestRefresh();
            yield return null;
            AssertNoCompletionOrSignup();
        }

        [UnityTest]
        public IEnumerator ReturningToHotelQuestDoesNotCompleteTheShowcase()
        {
            SelectAndStart(1);
            yield return null;
            SelectAndStart(0);
            yield return null;

            Assert.That(transport.ShowcaseStops, Is.EqualTo(1));
            Assert.That(ShowsGoal("호텔 체크인하기"), Is.True);
            Assert.That(view.IsQuestBoardOpen, Is.False);
            AssertNoCompletionOrSignup();
        }

        [UnityTest]
        public IEnumerator FailedShowcaseStartKeepsHotelQuestAndTheSelectionOpen()
        {
            transport.FailShowcaseStart = true;
            SelectAndStart(1);
            yield return null;

            Assert.That(transport.ShowcaseStarts, Is.EqualTo(1));
            Assert.That(NpcPopulation.UsesShowcasePopulation, Is.False);
            Assert.That(view.IsQuestBoardOpen, Is.True);
            AssertNoCompletionOrSignup();
            view.GetComponentInChildren<QuestBoardView>(true).Close();
            view.ExpandQuestCard();
            Assert.That(ShowsGoal("호텔 체크인하기"), Is.True);
        }

        [UnityTest]
        public IEnumerator FinishingShowcaseStillShowsItsCompletionWhenRegularQuestReturns()
        {
            SelectAndStart(1);
            yield return null;
            transport.FinishShowcase();
            Object.FindFirstObjectByType<FirstDayMissionController>().RequestRefresh();
            yield return null;

            Assert.That(view.IsQuestCompleteOpen, Is.True);
            Assert.That(ShowsGoal("비 오는 저녁, 햄버거 한 끼"), Is.True);
            view.ShowNextQuest();
            AssertNoCompletionOrSignup();
        }

        private void SelectAndStart(int index)
        {
            view.OpenQuestBoard();
            var board = view.GetComponentInChildren<QuestBoardView>(true);
            board.ShowMap(index);
            board.RequestStart();
        }

        private bool ShowsGoal(string title) =>
            view.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains(title));

        private void AssertNoCompletionOrSignup()
        {
            Assert.That(
                view.IsQuestCompleteOpen,
                Is.False,
                "선택만 바꾼 퀘스트를 완료로 처리하지 않습니다."
            );
            Assert.That(signupRequests, Is.Zero);
            Assert.That(Object.FindFirstObjectByType<GuestSignupDialogView>().IsVisible, Is.False);
        }

        private sealed class QuestTransport : IApiTransport
        {
            private bool showcaseStarted;
            public int ShowcaseStarts { get; private set; }
            public int ShowcaseStops { get; private set; }
            public bool FailShowcaseStart { get; set; }

            public void FinishShowcase() => showcaseStarted = false;

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                if (request.Url.EndsWith("/missions/showcase/start"))
                {
                    ShowcaseStarts++;
                    if (FailShowcaseStart)
                        return Task.FromResult(ApiTransportResponse.NetworkFailed());
                    showcaseStarted = true;
                    return Response(CurrentState());
                }
                if (request.Url.EndsWith("/missions/showcase/stop"))
                {
                    ShowcaseStops++;
                    showcaseStarted = false;
                    return Response(CurrentState());
                }
                if (
                    request.Url.EndsWith("/missions/first-day/visit")
                    || request.Url.EndsWith("/missions/first-day/start")
                )
                    return Response(CurrentState());
                if (request.Url.EndsWith("/missions/quests"))
                    return Response(
                        new QuestBoardState
                        {
                            Balance = 100,
                            Quests =
                            {
                                new QuestEntry
                                {
                                    Id = "CHECK_IN",
                                    Title = "호텔 체크인과 잃어버린 가방",
                                    Status = QuestEntry.InProgress,
                                    Reward = 10,
                                },
                                new QuestEntry
                                {
                                    Id = FirstDayMissionState.ShowcaseQuestId,
                                    Title = "비 오는 저녁, 햄버거 한 끼",
                                    Status = showcaseStarted
                                        ? QuestEntry.InProgress
                                        : QuestEntry.Available,
                                },
                            },
                        }
                    );
                return Task.FromResult(ApiTransportResponse.NetworkFailed());
            }

            private FirstDayMissionState CurrentState() =>
                new FirstDayMissionState
                {
                    Quest = showcaseStarted ? FirstDayMissionState.ShowcaseQuestId : "CHECK_IN",
                    Started = true,
                    Balance = 100,
                    Scene = "HOTEL",
                    Situation = "",
                    Direction = "",
                    Steps =
                    {
                        new FirstDayMissionStep
                        {
                            Title = showcaseStarted
                                ? "호텔에서 저녁 나들이 준비"
                                : "호텔 체크인하기",
                            Scene = "HOTEL",
                            NpcCode = "HOTEL_STAFF",
                            Status = "CURRENT",
                        },
                    },
                };

            private static Task<ApiTransportResponse> Response(object body) =>
                Task.FromResult(ApiTransportResponse.Received(200, ApiJson.Serialize(body)));
        }
    }
}
#endif
