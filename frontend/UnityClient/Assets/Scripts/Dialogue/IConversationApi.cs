using System.Threading.Tasks;

namespace A305.Dialogue
{
    /// <summary>
    /// 서버 대화 API. 실패하면 <see cref="A305.Api.ApiException"/>을 던진다. 테스트에서는 가짜 구현으로 바꾼다.
    /// </summary>
    public interface IConversationApi
    {
        /// <summary>CONV-01 대화 시작.</summary>
        Task<ConversationStart> StartAsync(string requestId, string npcCode, string initiatedBy);

        /// <summary>CONV-03 발화 전송.</summary>
        Task<TurnResult> SendTurnAsync(
            long conversationId,
            string requestId,
            string inputType,
            string text,
            string sttRawText
        );

        /// <summary>CONV-08 대화 종료와 짧은 피드백.</summary>
        Task<EndResult> EndAsync(long conversationId, string requestId, bool skipFeedback);

        /// <summary>FB-02 만들지 못한 짧은 피드백을 다시 만든다. 이미 만든 피드백이면 저장된 것을 돌려준다.</summary>
        Task<FeedbackData> RegenerateFeedbackAsync(long conversationId, string requestId);

        /// <summary>FB-03 상세 평가. 종료 때 만든 평가를 돌려준다.</summary>
        Task<DetailedFeedback> GetDetailedFeedbackAsync(long conversationId, string requestId);

        /// <summary>CONV-11 진행 중인 대화. 없으면 null이다.</summary>
        Task<ActiveConversation> GetActiveAsync();

        /// <summary>CONV-02 녹음 전사(16kHz 모노 WAV).</summary>
        Task<Transcription> TranscribeAsync(long conversationId, string requestId, byte[] wav);

        /// <summary>CONV-07 NPC 음성 다시 만들기.</summary>
        Task<SpeechData> RegenerateSpeechAsync(long turnId, string requestId);

        /// <summary>CONV-04 NPC 대사 뜻과 단어 풀이. 서버가 한 번 만든 뜻은 다시 만들지 않는다.</summary>
        Task<MeaningData> GetMeaningAsync(long turnId, string requestId);

        /// <summary>CONV-05 NPC 대사에 대답할 추천 문장. 서버가 한 번 만든 문장은 다시 만들지 않는다.</summary>
        Task<HintData> GetHintAsync(long turnId, string requestId);
    }
}
