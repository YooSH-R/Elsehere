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
            string initiatedBy,
            bool npcOffDuty,
            NpcTravelContext npcTravelContext = null
        )
        {
            return client.PostAsync<ConversationStart>(
                "/api/v1/conversations",
                new StartConversationBody
                {
                    RequestId = requestId,
                    NpcCode = npcCode,
                    InitiatedBy = initiatedBy,
                    NpcOffDuty = npcOffDuty,
                }
            );
        }

        public Task<TurnResult> SendTurnAsync(
            long conversationId,
            string requestId,
            string inputType,
            string text,
            string sttRawText,
            ITurnStream stream
        )
        {
            string path = $"/api/v1/conversations/{conversationId}/turns";
            var body = new TurnBody
            {
                RequestId = requestId,
                InputType = inputType,
                Text = text,
                SttRawText = sttRawText,
            };
            return stream == null
                ? client.PostAsync<TurnResult>(path, body)
                : SendTurnStreamingAsync(path, body, stream);
        }

        // CONV-03 스트리밍. 서버는 연결이 끊겨도 턴을 끝까지 저장하므로, 저장된 턴을 받지 못하고 끝나면 같은 요청 ID로 다시 묻는다.
        private async Task<TurnResult> SendTurnStreamingAsync(
            string path,
            TurnBody body,
            ITurnStream stream
        )
        {
            var events = new TurnEventReader(stream);
            try
            {
                await client.PostStreamAsync(path, body, events.Read);
            }
            catch (ApiException exception) when (exception.Code == ApiErrorCodes.NetworkError)
            {
                return await client.PostAsync<TurnResult>(path, body);
            }

            if (events.Error != null)
            {
                throw events.Error;
            }

            return events.Turn ?? await client.PostAsync<TurnResult>(path, body);
        }

        public Task<EndResult> EndAsync(long conversationId, string requestId, bool skipFeedback)
        {
            return feedbackClient.PostAsync<EndResult>(
                $"/api/v1/conversations/{conversationId}/end",
                new EndBody { RequestId = requestId, SkipFeedback = skipFeedback }
            );
        }

        // 피드백을 새로 만들면 종료 때처럼 오래 걸릴 수 있어 대기 시간이 긴 클라이언트를 쓴다.
        public Task<FeedbackData> RegenerateFeedbackAsync(long conversationId, string requestId)
        {
            return feedbackClient.PostAsync<FeedbackData>(
                $"/api/v1/conversations/{conversationId}/feedback",
                new RequestIdBody { RequestId = requestId }
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

        public Task<Transcription> TranscribeTutorialAsync(string requestId, byte[] wav)
        {
            var fields = new Dictionary<string, string>
            {
                { "requestId", requestId },
                { "purpose", "TUTORIAL" },
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

        public Task<MeaningData> GetMeaningAsync(long turnId, string requestId)
        {
            return client.PostAsync<MeaningData>(
                $"/api/v1/turns/{turnId}/meaning",
                new RequestIdBody { RequestId = requestId }
            );
        }

        public Task<HintData> GetHintAsync(long turnId, string requestId)
        {
            return client.PostAsync<HintData>(
                $"/api/v1/turns/{turnId}/hint",
                new RequestIdBody { RequestId = requestId }
            );
        }

        public Task<SuggestWordsData> GetSuggestWordsAsync(long turnId, string requestId)
        {
            return client.PostAsync<SuggestWordsData>(
                $"/api/v1/turns/{turnId}/suggest-words",
                new RequestIdBody { RequestId = requestId }
            );
        }
    }
}
