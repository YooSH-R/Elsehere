using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Learning;
using A305.Player;
using A305.Tutorial;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.Npc.PlayModeTests
{
    /// <summary>실제 대화창의 나중에 확인 버튼과 이동 잠금·장면 전환을 제어 가능한 서버 응답으로 검증한다.</summary>
    public sealed class FeedbackLaterTests
    {
        private PendingApi api;
        private DialogueController dialogue;
        private DialoguePanelView panel;
        private GameObject lookTarget;
        private DialogueRecord backgroundResult;

        [TearDown]
        public void TearDown()
        {
            DialogueController.BackgroundFeedbackCompleted -= Receive;
            api?.Finish();
            if (dialogue != null && dialogue.IsTalking)
                dialogue.End();
            Object.Destroy(lookTarget);
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator LaterButton_ReleasesMovementAndOldCompletionDoesNotCloseNewConversation()
        {
            yield return BeginPendingFeedback();
            ClickLater();
            Assert.That(dialogue.IsTalking, Is.False);
            Assert.That(Single<PlayerMover>().IsMovementLocked, Is.False);
            Assert.That(panel.transform.Find("FeedbackWait").gameObject.activeSelf, Is.False);
            Assert.That(backgroundResult, Is.Null);
            Assert.That(api.EndRequests, Is.EqualTo(1));

            Assert.That(dialogue.TryStart("TOWN_PASSERBY", lookTarget.transform), Is.True);
            yield return null;
            api.Finish();
            yield return WaitForBackground();

            Assert.That(
                dialogue.IsTalking,
                Is.True,
                "이전 피드백 완료가 새 대화를 닫으면 안 됩니다."
            );
            Assert.That(backgroundResult.ServerFeedback.Summary.Status, Is.EqualTo("DONE"));
            Assert.That(api.EndRequests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LaterButton_CompletionSurvivesChangingScenesWithoutOpeningAModal()
        {
            yield return BeginPendingFeedback();
            ClickLater();
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;

            api.Finish();
            yield return WaitForBackground();

            Assert.That(backgroundResult.ServerFeedback.ConversationId, Is.EqualTo(58));
            Assert.That(Single<LearningController>().IsShowingScreen, Is.False);
            Assert.That(Single<PlayerMover>().IsMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator ReviewConversation_HidesLaterButtonAndKeepsWaitingForTheResult()
        {
            yield return BeginPendingFeedback(reviewCardId: 7);
            Assert.That(
                panel.transform.Find("FeedbackWait/Card/FeedbackLaterButton").gameObject.activeSelf,
                Is.False,
                "복습 대화는 피드백을 만들지 않으므로 나중에 확인할 것이 없습니다."
            );

            dialogue.CheckFeedbackLater();

            Assert.That(dialogue.IsTalking, Is.True);
            Assert.That(Single<PlayerMover>().IsMovementLocked, Is.True);
        }

        private IEnumerator BeginPendingFeedback(long? reviewCardId = null)
        {
            backgroundResult = null;
            TutorialState.MarkFinishedOnServer();
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            dialogue = Single<DialogueController>();
            panel = Single<DialoguePanelView>();
            api = new PendingApi();
            SetField(dialogue, "conversationApi", api);
            SetField(dialogue, "aiStatusApi", null);
            SetField(dialogue, "followUpApi", null);
            lookTarget = new GameObject("FeedbackTestLookTarget");
            lookTarget.transform.position =
                Single<PlayerMover>().transform.position + Vector3.forward * 2;
            DialogueController.BackgroundFeedbackCompleted += Receive;
            Assert.That(
                dialogue.TryStart(
                    "TOWN_PASSERBY",
                    lookTarget.transform,
                    reviewCardId: reviewCardId,
                    reviewStart: reviewCardId == null ? null : api.StartReviewAsync
                ),
                Is.True
            );
            yield return null;
            var fields = new SerializedFields(panel);
            fields.Get<TMP_InputField>("inputField").text = "Hello, how are you?";
            fields.Get<Button>("sendButton").onClick.Invoke();
            yield return null;
            dialogue.End();
            yield return null;
            Assert.That(panel.transform.Find("FeedbackWait").gameObject.activeSelf, Is.True);
        }

        private void ClickLater() =>
            panel
                .transform.Find("FeedbackWait/Card/FeedbackLaterButton")
                .GetComponent<Button>()
                .onClick.Invoke();

        private void Receive(DialogueRecord result) => backgroundResult = result;

        private IEnumerator WaitForBackground()
        {
            float until = Time.realtimeSinceStartup + 10;
            while (backgroundResult == null && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.That(backgroundResult, Is.Not.Null);
        }

        private static T Single<T>()
            where T : Object =>
            Object
                .FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single();

        private static void SetField(object target, string name, object value) =>
            target
                .GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private sealed class SerializedFields
        {
            private readonly object target;

            public SerializedFields(object target) => this.target = target;

            public T Get<T>(string name) =>
                (T)
                    target
                        .GetType()
                        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(target);
        }

        private sealed class PendingApi : IConversationApi
        {
            private readonly TaskCompletionSource<EndResult> end =
                new TaskCompletionSource<EndResult>();
            private long nextId = 58;
            public int EndRequests { get; private set; }

            public void Finish() =>
                end.TrySetResult(
                    new EndResult
                    {
                        Status = "ENDED_BY_USER",
                        Feedback = new FeedbackData
                        {
                            Status = "DONE",
                            Summary = "대화를 잘 마쳤어요.",
                        },
                        DetailedFeedback =
                            new System.Collections.Generic.List<DetailedFeedbackItem>(),
                    }
                );

            public Task<ConversationStart> StartReviewAsync(string requestId) =>
                StartAsync(requestId, "TOWN_PASSERBY", "USER", false);

            public Task<ConversationStart> StartAsync(
                string requestId,
                string npcCode,
                string initiatedBy,
                bool npcOffDuty
            ) =>
                Task.FromResult(
                    new ConversationStart
                    {
                        ConversationId = nextId++,
                        Npc = new NpcSummary { NpcCode = npcCode, Name = "Ben" },
                        OpeningTurn = new NpcTurnData { TurnId = 1, Text = "Hello!" },
                    }
                );

            public Task<TurnResult> SendTurnAsync(
                long id,
                string requestId,
                string inputType,
                string text,
                string raw
            ) =>
                Task.FromResult(
                    new TurnResult
                    {
                        UserTurn = new UserTurnData { Text = text },
                        NpcTurn = new NpcTurnData { TurnId = 3, Text = "I'm good, thanks!" },
                    }
                );

            public Task<TurnResult> SendTurnAsync(
                long id,
                string requestId,
                string inputType,
                string text,
                string raw,
                ITurnStream stream
            ) => SendTurnAsync(id, requestId, inputType, text, raw);

            public Task<EndResult> EndAsync(long id, string requestId, bool skip)
            {
                EndRequests++;
                return end.Task;
            }

            public Task<FeedbackData> RegenerateFeedbackAsync(long id, string requestId) =>
                Task.FromResult<FeedbackData>(null);

            public Task<DetailedFeedback> GetDetailedFeedbackAsync(long id, string requestId) =>
                Task.FromResult<DetailedFeedback>(null);

            public Task<ActiveConversation> GetActiveAsync() =>
                Task.FromResult<ActiveConversation>(null);

            public Task<Transcription> TranscribeAsync(long id, string requestId, byte[] wav) =>
                Task.FromResult<Transcription>(null);

            public Task<Transcription> TranscribeTutorialAsync(string requestId, byte[] wav) =>
                Task.FromResult<Transcription>(null);

            public Task<SpeechData> RegenerateSpeechAsync(long id, string requestId) =>
                Task.FromResult<SpeechData>(null);

            public Task<MeaningData> GetMeaningAsync(long id, string requestId) =>
                Task.FromResult<MeaningData>(null);

            public Task<HintData> GetHintAsync(long id, string requestId) =>
                Task.FromResult<HintData>(null);

            public Task<SuggestWordsData> GetSuggestWordsAsync(long id, string requestId) =>
                Task.FromResult<SuggestWordsData>(null);
        }
    }
}
