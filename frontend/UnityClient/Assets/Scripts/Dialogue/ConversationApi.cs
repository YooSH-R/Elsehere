using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Dialogue
{
    /// <summary>서버 대화 API의 경로와 본문. 경로는 API 명세(docs/api/mvp-api.md 4.6·4.7)를 따른다.</summary>
    public sealed class ConversationApi : IConversationApi
    {
        private readonly ApiClient client;
        private readonly ApiClient feedbackClient;

        /// <param name="client">대화 시작·발화·음성 요청용</param>
        /// <param name="feedbackClient">종료 피드백처럼 오래 걸리는 요청용(대기 시간이 긴 클라이언트)</param>
        public ConversationApi(ApiClient client, ApiClient feedbackClient)
        {
            this.client = client;
            this.feedbackClient = feedbackClient;
        }

        public Task<ConversationStart> StartAsync(
            string requestId,
            string npcCode,
            string initiatedBy
        )
        {
            return client.PostAsync<ConversationStart>(
                "/api/v1/conversations",
                new StartConversationBody
                {
                    RequestId = requestId,
                    NpcCode = npcCode,
                    InitiatedBy = initiatedBy,
                }
            );
        }

        public Task<TurnResult> SendTurnAsync(
            long conversationId,
            string requestId,
            string inputType,
            string text,
            string sttRawText
        )
        {
            return client.PostAsync<TurnResult>(
                $"/api/v1/conversations/{conversationId}/turns",
                new TurnBody
                {
                    RequestId = requestId,
                    InputType = inputType,
                    Text = text,
                    SttRawText = sttRawText,
                }
            );
        }

        public Task<EndResult> EndAsync(long conversationId, string requestId, bool skipFeedback)
        {
            return feedbackClient.PostAsync<EndResult>(
                $"/api/v1/conversations/{conversationId}/end",
                new EndBody { RequestId = requestId, SkipFeedback = skipFeedback }
            );
        }

        public Task<DetailedFeedback> GetDetailedFeedbackAsync(
            long conversationId,
            string requestId
        )
        {
            return client.PostAsync<DetailedFeedback>(
                $"/api/v1/conversations/{conversationId}/detailed-feedback",
                new RequestIdBody { RequestId = requestId }
            );
        }

        public Task<ActiveConversation> GetActiveAsync()
        {
            return client.GetAsync<ActiveConversation>("/api/v1/conversations/active");
        }

        public Task<Transcription> TranscribeAsync(
            long conversationId,
            string requestId,
            byte[] wav
        )
        {
            var fields = new Dictionary<string, string>
            {
                { "requestId", requestId },
                { "purpose", "TURN" },
                { "conversationId", conversationId.ToString(CultureInfo.InvariantCulture) },
            };
            return client.PostMultipartAsync<Transcription>(
                "/api/v1/speech/transcriptions",
                fields,
                new ApiFile("audio", "speech.wav", "audio/wav", wav)
            );
        }

        public Task<SpeechData> RegenerateSpeechAsync(long turnId, string requestId)
        {
            return client.PostAsync<SpeechData>(
                $"/api/v1/turns/{turnId}/speech",
                new RequestIdBody { RequestId = requestId }
            );
        }
    }
}
