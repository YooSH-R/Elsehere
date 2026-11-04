using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class FeedbackRegeneratorTests
    {
        private const long ConversationId = 58;

        private FakeFeedbackApi api;
        private FeedbackRegenerator regenerator;

        [SetUp]
        public void CreateRegenerator()
        {
            api = new FakeFeedbackApi();
            regenerator = new FeedbackRegenerator(api);
        }

        [Test]
        public async Task Regenerate_WhenFeedbackIsMade_ReturnsItWithDetailedFeedback()
        {
            api.Regenerated = new FeedbackData { Status = "DONE", Summary = "주문에 성공했어요" };
            api.Detailed = new DetailedFeedback
            {
                Items = new List<DetailedFeedbackItem>
                {
                    new DetailedFeedbackItem { UserText = "I want latte." },
                },
            };

            ServerFeedback feedback = await regenerator.RegenerateAsync(FailedFeedback());

            Assert.That(api.RegeneratedFor, Is.EqualTo(new[] { ConversationId }));
            Assert.That(feedback.Summary.Status, Is.EqualTo("DONE"));
            Assert.That(feedback.Details, Has.Count.EqualTo(1));
            Assert.That(feedback.FailureMessage, Is.Null);
            Assert.That(feedback.ConversationId, Is.EqualTo(ConversationId));
        }

        [Test]
        public async Task Regenerate_WhenFeedbackFailsAgain_DoesNotAskForDetailedFeedback()
        {
            api.Regenerated = new FeedbackData { Status = "FAILED" };

            ServerFeedback feedback = await regenerator.RegenerateAsync(FailedFeedback());

            Assert.That(feedback.Summary.Status, Is.EqualTo("FAILED"));
            Assert.That(api.DetailedRequested, Is.False);
        }

        [Test]
        public async Task Regenerate_WhenRequestFails_KeepsFailedFeedbackWithReason()
        {
            // 다시 만들지 못해도 [다시 만들기]를 또 누를 수 있게 실패한 피드백과 대화 ID를 그대로 둔다.
            api.RegenerateError = new ApiException(
                ApiErrorCodes.NetworkError,
                0,
                "서버에 연결하지 못했습니다."
            );

            ServerFeedback feedback = await regenerator.RegenerateAsync(FailedFeedback());

            Assert.That(feedback.Summary.Status, Is.EqualTo("FAILED"));
            Assert.That(feedback.FailureMessage, Is.EqualTo("서버에 연결하지 못했습니다."));
            Assert.That(feedback.ConversationId, Is.EqualTo(ConversationId));
        }

        private static ServerFeedback FailedFeedback()
        {
            return new ServerFeedback(
                new FeedbackData { Status = "FAILED", ErrorCode = "AI_TIMEOUT" },
                null,
                null,
                ConversationId
            );
        }

        /// <summary>피드백 다시 만들기(FB-02)와 상세 평가(FB-03)만 쓰는 테스트용 API.</summary>
        private sealed class FakeFeedbackApi : IConversationApi
        {
            public FeedbackData Regenerated { get; set; }
            public ApiException RegenerateError { get; set; }
            public DetailedFeedback Detailed { get; set; } = new DetailedFeedback();
            public List<long> RegeneratedFor { get; } = new List<long>();
            public bool DetailedRequested { get; private set; }

            public Task<FeedbackData> RegenerateFeedbackAsync(long conversationId, string requestId)
            {
                RegeneratedFor.Add(conversationId);
                return RegenerateError != null
                    ? Task.FromException<FeedbackData>(RegenerateError)
                    : Task.FromResult(Regenerated);
            }

            public Task<DetailedFeedback> GetDetailedFeedbackAsync(
                long conversationId,
                string requestId
            )
            {
                DetailedRequested = true;
                return Task.FromResult(Detailed);
            }

            public Task<ConversationStart> StartAsync(
                string requestId,
                string npcCode,
                string initiatedBy,
                bool npcOffDuty
            )
            {
                throw new NotSupportedException();
            }

            public Task<TurnResult> SendTurnAsync(
                long conversationId,
                string requestId,
                string inputType,
                string text,
                string sttRawText
            )
            {
                throw new NotSupportedException();
            }

            public Task<EndResult> EndAsync(
                long conversationId,
                string requestId,
                bool skipFeedback
            )
            {
                throw new NotSupportedException();
            }

            public Task<ActiveConversation> GetActiveAsync()
            {
                throw new NotSupportedException();
            }

            public Task<Transcription> TranscribeAsync(
                long conversationId,
                string requestId,
                byte[] wav
            )
            {
                throw new NotSupportedException();
            }

            public Task<Transcription> TranscribeTutorialAsync(string requestId, byte[] wav)
            {
                throw new NotSupportedException();
            }

            public Task<SpeechData> RegenerateSpeechAsync(long turnId, string requestId)
            {
                throw new NotSupportedException();
            }

            public Task<MeaningData> GetMeaningAsync(long turnId, string requestId)
            {
                throw new NotSupportedException();
            }

            public Task<HintData> GetHintAsync(long turnId, string requestId)
            {
                throw new NotSupportedException();
            }

            public Task<SuggestWordsData> GetSuggestWordsAsync(long turnId, string requestId)
            {
                throw new NotSupportedException();
            }
        }
    }
}
