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
using UnityEngine.UI;
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
            // 비 오는 저녁을 읽으면 대화 잠금이 걸린다. 장면을 내려도 남으므로 지워 다음 NPC 테스트를 막지 않게 한다.
            ShowcaseTalkLock.Clear();
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

        // 시연을 그만두면 서버에 그만두기를 보내고 일반 진행으로 돌아간다. 그만둔 것을 완료로 처리하지 않는다.
        [UnityTest]
        public IEnumerator CancellingTheShowcaseReturnsToNormalProgressWithoutCompletingIt()
        {
            SelectAndStart(1);
            yield return null;
            view.OpenQuestBoard();
            var board = view.GetComponentInChildren<QuestBoardView>(true);
            board.ShowMap(1);
            board.RequestCancel();
            yield return null;

            Assert.That(transport.Cancelled, Is.EqualTo(FirstDayMissionState.ShowcaseQuestId));
            Assert.That(NpcPopulation.UsesShowcasePopulation, Is.False);
            AssertNoCompletionOrSignup();
            view.GetComponentInChildren<QuestBoardView>(true).Close();
            view.ExpandQuestCard();
            Assert.That(ShowsGoal("호텔 체크인하기"), Is.True);
        }

        // 처음 들어와 저절로 시작한 호텔 퀘스트를 [퀘스트 그만두기] → [그만두기]로 그만둔다. 완료 팡파레 없이 시작 전으로 돌아가
        // 도장 지도에는 다시 [퀘스트 시작]이 보인다.
        [UnityTest]
        public IEnumerator HotelQuestStartedOnFirstVisitCanBeCancelledWithTheButtons()
        {
            view.OpenQuestBoard();
            var board = view.GetComponentInChildren<QuestBoardView>(true);
            yield return null;
            board.ShowMap(0);
            Button cancel = MapButton(board, "Cancel");
            Assert.That(
                cancel.gameObject.activeInHierarchy,
                Is.True,
                "하는 중인 호텔 퀘스트에 [퀘스트 그만두기]가 보여야 합니다."
            );

            cancel.onClick.Invoke();
            ConfirmButton(board).onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(transport.Cancelled, Is.EqualTo("CHECK_IN"));
            AssertNoCompletionOrSignup();
            Assert.That(MapButton(board, "Start").gameObject.activeInHierarchy, Is.True);
            Assert.That(cancel.gameObject.activeInHierarchy, Is.False);
        }

        // 호텔 퀘스트를 하는 중에 시연 카드의 [퀘스트 시작]을 누르면 호텔 퀘스트가 처음으로 돌아간다고 먼저 묻고,
        // [그만두고 시작]을 누르면 시연을 시작한다. 호텔 퀘스트를 완료로 처리하지 않는다.
        [UnityTest]
        public IEnumerator StartingTheShowcaseWithTheButtonsDuringTheHotelQuestAsksAndDoesNotCompleteIt()
        {
            view.OpenQuestBoard();
            var board = view.GetComponentInChildren<QuestBoardView>(true);
            yield return null;
            board.ShowMap(1);

            MapButton(board, "Start").onClick.Invoke();
            Assert.That(transport.ShowcaseStarts, Is.Zero, "확인하기 전에는 시작하지 않습니다.");
            Assert.That(
                board
                    .GetComponentsInChildren<TMP_Text>()
                    .Any(text => text.text.Contains("호텔 체크인과 잃어버린 가방")),
                Is.True,
                "처음으로 돌아갈 호텔 퀘스트를 알려야 합니다."
            );
            ConfirmButton(board).onClick.Invoke();
            yield return null;

            Assert.That(transport.ShowcaseStarts, Is.EqualTo(1));
            Assert.That(ShowsGoal("호텔에서 저녁 나들이 준비"), Is.True);
            AssertNoCompletionOrSignup();
        }

        // 도장 지도 머리줄의 버튼([퀘스트 시작]·[퀘스트 그만두기])
        private static Button MapButton(QuestBoardView board, string name) =>
            board.transform.Find("Sheet/Map/" + name).GetComponent<Button>();

        // 확인 창의 확인 버튼([그만두기]·[그만두고 시작])
        private static Button ConfirmButton(QuestBoardView board) =>
            board.transform.Find("QuestConfirm/Card/Confirm").GetComponent<Button>();

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

            // 처음 들어온 사용자처럼 튜토리얼 뒤 호텔 퀘스트가 시작된 상태로 둔다. 그만두면 시작 전이 된다.
            private bool hotelStarted = true;
            public int ShowcaseStarts { get; private set; }
            public int ShowcaseStops { get; private set; }
            public bool FailShowcaseStart { get; set; }
            public string Cancelled { get; private set; }

            public void FinishShowcase() => showcaseStarted = false;

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                if (request.Url.EndsWith("/cancel"))
                {
                    Cancelled = request.Url.Split('/').Reverse().Skip(1).First();
                    if (Cancelled == "CHECK_IN")
                        hotelStarted = false;
                    else
                        showcaseStarted = false;
                    return Response(Board());
                }
                if (request.Url.EndsWith("/missions/first-day/start"))
                    hotelStarted = true;
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
                    return Response(Board());
                return Task.FromResult(ApiTransportResponse.NetworkFailed());
            }

            private QuestBoardState Board() =>
                new QuestBoardState
                {
                    Balance = 100,
                    Quests =
                    {
                        new QuestEntry
                        {
                            Id = "CHECK_IN",
                            Title = "호텔 체크인과 잃어버린 가방",
                            Status = hotelStarted ? QuestEntry.InProgress : QuestEntry.Available,
                            Reward = 10,
                        },
                        new QuestEntry
                        {
                            Id = FirstDayMissionState.ShowcaseQuestId,
                            Title = "비 오는 저녁, 햄버거 한 끼",
                            Status = showcaseStarted ? QuestEntry.InProgress : QuestEntry.Available,
                        },
                    },
                };

            private FirstDayMissionState CurrentState() =>
                new FirstDayMissionState
                {
                    Quest = showcaseStarted ? FirstDayMissionState.ShowcaseQuestId : "CHECK_IN",
                    Started = showcaseStarted || hotelStarted,
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
