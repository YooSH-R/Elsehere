using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Tutorial.Tests
{
    public class TutorialFinishRecorderTests
    {
        private FakeTutorialApi api;
        private TutorialProgress progress;
        private TutorialFinishRecorder recorder;

        [SetUp]
        public void CreateRecorder()
        {
            api = new FakeTutorialApi();
            progress = new TutorialProgress("TOWN_PASSERBY");
            recorder = new TutorialFinishRecorder(api);
        }

        [Test]
        public async Task SaveAsync_Skipped_CallsSkipOnce()
        {
            progress.Skip();

            TutorialFinishSaveStatus status = await recorder.SaveAsync(progress);

            Assert.That(status, Is.EqualTo(TutorialFinishSaveStatus.Saved));
            Assert.That(api.Calls, Is.EqualTo(new[] { "skip" }));
            Assert.That(progress.NeedsFinishSave, Is.False);
        }

        [Test]
        public async Task SaveAsync_Completed_CallsCompleteOnce()
        {
            CompleteTutorial();

            TutorialFinishSaveStatus status = await recorder.SaveAsync(progress);

            Assert.That(status, Is.EqualTo(TutorialFinishSaveStatus.Saved));
            Assert.That(api.Calls, Is.EqualTo(new[] { "complete" }));
        }

        [Test]
        public async Task SaveAsync_NotFinished_DoesNotCallServer()
        {
            TutorialFinishSaveStatus status = await recorder.SaveAsync(progress);

            Assert.That(status, Is.EqualTo(TutorialFinishSaveStatus.NotNeeded));
            Assert.That(api.Calls, Is.Empty);
        }

        [Test]
        public async Task SaveAsync_FinishedOnServer_DoesNotCallServer()
        {
            progress.MarkFinishedOnServer();

            TutorialFinishSaveStatus status = await recorder.SaveAsync(progress);

            Assert.That(status, Is.EqualTo(TutorialFinishSaveStatus.NotNeeded));
            Assert.That(api.Calls, Is.Empty);
        }

        [Test]
        public async Task SaveAsync_AlreadySaved_DoesNotCallAgain()
        {
            progress.Skip();
            await recorder.SaveAsync(progress);

            TutorialFinishSaveStatus status = await recorder.SaveAsync(progress);

            Assert.That(status, Is.EqualTo(TutorialFinishSaveStatus.NotNeeded));
            Assert.That(api.Calls, Is.EqualTo(new[] { "skip" }));
        }

        [Test]
        public async Task SaveAsync_NetworkFails_KeepsFinishToRetry()
        {
            progress.Skip();
            api.Failure = new ApiException(ApiErrorCodes.NetworkError, 0, "연결 실패");

            TutorialFinishSaveStatus status = await recorder.SaveAsync(progress);

            Assert.That(status, Is.EqualTo(TutorialFinishSaveStatus.Failed));
            Assert.That(recorder.LastError.Code, Is.EqualTo(ApiErrorCodes.NetworkError));
            Assert.That(progress.NeedsFinishSave, Is.True);
        }

        [Test]
        public async Task SaveAsync_RetryAfterFailure_SavesFinish()
        {
            progress.Skip();
            api.Failure = new ApiException(ApiErrorCodes.NetworkError, 0, "연결 실패");
            await recorder.SaveAsync(progress);
            api.Failure = null;

            TutorialFinishSaveStatus status = await recorder.SaveAsync(progress);

            Assert.That(status, Is.EqualTo(TutorialFinishSaveStatus.Saved));
            Assert.That(progress.NeedsFinishSave, Is.False);
        }

        [Test]
        public async Task SaveAsync_ServerRejects_DoesNotRetry()
        {
            progress.Skip();
            api.Failure = new ApiException("TUTORIAL_NOT_IN_PROGRESS", 409, "튜토리얼 단계가 아님");

            TutorialFinishSaveStatus status = await recorder.SaveAsync(progress);

            Assert.That(status, Is.EqualTo(TutorialFinishSaveStatus.Rejected));
            Assert.That(recorder.LastError.Code, Is.EqualTo("TUTORIAL_NOT_IN_PROGRESS"));
            Assert.That(progress.NeedsFinishSave, Is.False);
        }

        [TestCase("complete")]
        [TestCase("skip")]
        public async Task TutorialApi_PostsToTutorialPath(string action)
        {
            var transport = new RecordingTransport();
            var tutorialApi = new TutorialApi(new ApiClient("", transport));

            await (action == "complete" ? tutorialApi.CompleteAsync() : tutorialApi.SkipAsync());

            Assert.That(transport.Request.Method, Is.EqualTo("POST"));
            Assert.That(transport.Request.Url, Is.EqualTo("/api/v1/me/tutorial/" + action));
            Assert.That(transport.Request.Headers.ContainsKey(ApiClient.CsrfHeaderName), Is.True);
        }

        // Ben 길 안내를 마치고 지도에서 오늘의 목표를 열면 튜토리얼이 끝난다.
        private void CompleteTutorial()
        {
            progress.Continue();
            progress.ReportWalked();
            progress.Continue();
            progress.ReportLooked();
            progress.Continue();
            progress.ReportWordSaved();
            progress.ReportVocabularyOpened();
            progress.LeaveHome();
            progress.StartDialogue("TOWN_PASSERBY");
            progress.EndDialogue("TOWN_PASSERBY", true);
            progress.CloseFeedback("TOWN_PASSERBY");
            progress.ReportJournalOpened();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed), "준비: 튜토리얼 완료");
        }

        private sealed class FakeTutorialApi : ITutorialApi
        {
            public List<string> Calls { get; } = new List<string>();

            public ApiException Failure { get; set; }

            public Task CompleteAsync() => Record("complete");

            public Task SkipAsync() => Record("skip");

            private Task Record(string call)
            {
                if (Failure != null)
                {
                    return Task.FromException(Failure);
                }

                Calls.Add(call);
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingTransport : IApiTransport
        {
            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Request = request;
                return Task.FromResult(
                    ApiTransportResponse.Received(200, "{\"onboardingStep\":\"DONE\"}")
                );
            }
        }
    }
}
