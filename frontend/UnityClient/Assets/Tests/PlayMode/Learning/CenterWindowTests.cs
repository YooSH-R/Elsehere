using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Learning.PlayModeTests
{
    /// <summary>
    /// 단어장·대화 노트를 열고 닫을 때 학습 창 상태와 게임 시계 정지가 함께 바뀌는지 확인한다.
    /// </summary>
    public class CenterWindowTests
    {
        [UnityTest]
        public IEnumerator StartingAgainWhileCancelledOpeningIsPending_WaitsForItsCleanup()
        {
            yield return LoadTown();
            Object.Destroy(Single<LearningController>());
            yield return null;
            var transport = new DelayedOpeningTransport();
            var client = new ApiClient("", transport);
            DialogueController dialogue = Single<DialogueController>();
            typeof(DialogueController)
                .GetField("conversationApi", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(dialogue, new ConversationApi(client, client));

            Assert.That(dialogue.TryStart("CLOCK_TEST", dialogue.transform), Is.True);
            dialogue.End();
            Assert.That(dialogue.TryStart("CLOCK_TEST", dialogue.transform), Is.True);
            Assert.That(transport.StartCalls, Is.EqualTo(1));

            transport.Opening.SetResult(
                ApiTransportResponse.Received(200, "{\"conversationId\":42}")
            );
            for (int frame = 0; frame < 10 && transport.StartCalls < 2; frame++)
                yield return null;
            yield return null;
            Assert.That(transport.EndCalls, Is.EqualTo(1));
            Assert.That(transport.StartCalls, Is.EqualTo(2));
            Assert.That(dialogue.IsTalking, Is.True);
            dialogue.End();
            Assert.That(transport.EndCalls, Is.EqualTo(2));
            Assert.That(dialogue.IsTalking, Is.False);
        }

        [UnityTest]
        public IEnumerator ClosingConversationThenFeedback_ResumesTheClockUntilTheNextConversation()
        {
            yield return LoadTown();
            DialogueController dialogue = Single<DialogueController>();
            typeof(DialogueController)
                .GetField("conversationApi", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(dialogue, null);
            typeof(DialogueController)
                .GetField("serverLink", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(dialogue, null);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(dialogue.TryStart("CLOCK_TEST", dialogue.transform), Is.True);
                double stopped = GameClock.Shared.Minutes;
                GameClock.Shared.Flow(1);
                Assert.That(GameClock.Shared.Minutes, Is.EqualTo(stopped));

                DialoguePanelView panel = Single<DialoguePanelView>();
                panel.SetInputText("Hello.");
                panel
                    .GetComponentsInChildren<Button>()
                    .Single(button => button.name == "SendButton")
                    .onClick.Invoke();
                dialogue.End();
                Assert.That(GameClock.Shared.IsTalking, Is.False);
                Assert.That(Single<FeedbackView>().IsVisible, Is.True);
                GameClock.Shared.Flow(1);
                Assert.That(
                    GameClock.Shared.Minutes,
                    Is.EqualTo(stopped),
                    "피드백 중에는 정지한다."
                );

                Single<FeedbackView>()
                    .GetComponentsInChildren<Button>()
                    .First(button => button.isActiveAndEnabled && button.name.Contains("Close"))
                    .onClick.Invoke();
                yield return null;
                Assert.That(Single<LearningController>().IsShowingScreen, Is.False);
                GameClock.Shared.Flow(1);
                Assert.That(GameClock.Shared.Minutes, Is.GreaterThan(stopped));
            }
        }

        [UnityTest]
        public IEnumerator CancelledServerOpening_ResumesTheClockAfterTheLateConversationIsClosed()
        {
            yield return LoadTown();
            Object.Destroy(Single<LearningController>());
            yield return null;
            var transport = new DelayedOpeningTransport();
            var client = new ApiClient("", transport);
            DialogueController dialogue = Single<DialogueController>();
            typeof(DialogueController)
                .GetField("conversationApi", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(dialogue, new ConversationApi(client, client));

            Assert.That(dialogue.TryStart("CLOCK_TEST", dialogue.transform), Is.True);
            Assert.That(GameClock.Shared.IsTalking, Is.True);
            dialogue.End();
            Assert.That(dialogue.IsTalking, Is.False);

            // 서버가 아직 준비 중인 대화를 활성 상태로 보고 정지 응답을 준다.
            GameClock.Shared.TickAsync(
                new FixedGameClockApi { Paused = true },
                Time.realtimeSinceStartupAsDouble + 60
            );
            double stopped = GameClock.Shared.Minutes;
            GameClock.Shared.Flow(1);
            Assert.That(GameClock.Shared.Minutes, Is.EqualTo(stopped));

            transport.Opening.SetResult(
                ApiTransportResponse.Received(200, "{\"conversationId\":42}")
            );
            for (int frame = 0; frame < 10 && transport.EndCalls == 0; frame++)
                yield return null;
            yield return null;

            Assert.That(transport.EndCalls, Is.EqualTo(1));
            Assert.That(dialogue.IsTalking, Is.False);
            Assert.That(GameClock.Shared.IsTalking, Is.False);
            GameClock.Shared.Flow(1);
            Assert.That(GameClock.Shared.Minutes, Is.GreaterThan(stopped));
        }

        // 학습 창을 여는 흐름만 확인하도록 튜토리얼을 마친 사용자로 시작한다.
        [SetUp]
        public void StartAfterTheTutorial()
        {
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
            // 서버가 없는 시험에서 마을의 게임 시계 신호가 응답을 기다리는 채로 다음 시험에 남지 않게,
            // 시각을 미리 맞춰 이 시험 동안(신호 간격 30초 안)에는 신호를 보낼 때가 오지 않게 한다.
            GameClock.Shared.TickAsync(new FixedGameClockApi(), Time.realtimeSinceStartupAsDouble);
        }

        // 열어 둔 창이 다음 시험의 마을 HUD를 숨기지 않게 창을 다루는 학습 처리를 지운다. 지울 때 가운데 창이 닫혔다고 알린다.
        [UnityTearDown]
        public IEnumerator CloseWindowsAndClearTutorial()
        {
            foreach (
                LearningController learning in Object.FindObjectsByType<LearningController>(
                    FindObjectsSortMode.None
                )
            )
            {
                Object.Destroy(learning);
            }

            yield return null;
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator VocabularyAndHistory_PauseTheClockWhileOpen()
        {
            yield return LoadTown();
            foreach (string app in new[] { "VocabularyButton", "HistoryButton" })
            {
                yield return Open(app);
                Assert.That(GameClock.Shared.IsLearningWindowOpen, Is.True);
                double stopped = GameClock.Shared.Minutes;
                GameClock.Shared.Flow(60);
                Assert.That(GameClock.Shared.Minutes, Is.EqualTo(stopped));
                LearningController learning = Single<LearningController>();
                RectTransform window =
                    app == "VocabularyButton"
                        ? Single<VocabularyView>().PanelRect
                        : Single<LearningHudView>().HistoryView.PanelRect;
                window
                    .GetComponentsInChildren<Button>(true)
                    .First(button => button.name == "CloseButton")
                    .onClick.Invoke();
                yield return null;
                Assert.That(learning.IsShowingScreen, Is.False);
                Assert.That(GameClock.Shared.IsLearningWindowOpen, Is.False);
            }
        }

        private static IEnumerator LoadTown()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            yield return null;
            // 창 경계 신호는 즉시 보내므로 실제 서버 대신 이 시험이 정한 시계만 사용한다.
            foreach (
                GameClockDriver driver in Object.FindObjectsByType<GameClockDriver>(
                    FindObjectsSortMode.None
                )
            )
                driver.enabled = false;
        }

        // 폰 메뉴의 앱 버튼. 폰을 접어 둔 채로 눌러도 같은 창을 연다.
        private static IEnumerator Open(string appButton)
        {
            Single<LearningHudView>()
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == appButton)
                .onClick.Invoke();
            yield return null;
            yield return null;
        }

        private static T Single<T>()
            where T : Object
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(found, Has.Length.EqualTo(1), $"{typeof(T).Name} 개수");
            return found[0];
        }

        // 1일차 오전 8시로 바로 답하는 게임 시계 신호
        private sealed class FixedGameClockApi : IGameClockApi
        {
            public bool Paused { get; set; }

            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            )
            {
                return Task.FromResult(
                    new GameClockTime
                    {
                        Day = 1,
                        MinuteOfDay = 8 * 60,
                        Paused = Paused,
                    }
                );
            }
        }

        private sealed class DelayedOpeningTransport : IApiTransport
        {
            public TaskCompletionSource<ApiTransportResponse> Opening { get; } =
                new TaskCompletionSource<ApiTransportResponse>();
            public int EndCalls { get; private set; }
            public int StartCalls { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                if (request.Url == "/api/v1/conversations")
                {
                    StartCalls++;
                    return StartCalls == 1
                        ? Opening.Task
                        : Task.FromResult(
                            ApiTransportResponse.Received(
                                200,
                                "{\"conversationId\":43,\"openingTurn\":{\"turnId\":1,\"text\":\"Hello.\",\"suggestWords\":[\"hello\"]}}"
                            )
                        );
                }
                if (
                    request.Url == "/api/v1/conversations/42/end"
                    || request.Url == "/api/v1/conversations/43/end"
                )
                {
                    EndCalls++;
                    return Task.FromResult(
                        ApiTransportResponse.Received(200, "{\"status\":\"ENDED_BY_USER\"}")
                    );
                }
                Assert.Fail("예상하지 않은 대화 요청: " + request.Url);
                return Task.FromResult(ApiTransportResponse.NetworkFailed());
            }
        }
    }
}
